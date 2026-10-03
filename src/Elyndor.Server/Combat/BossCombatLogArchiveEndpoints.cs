using System.Collections.Concurrent;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Contribution;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Progression;
using Elyndor.Server.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Elyndor.Server.Combat;

public static class BossCombatLogArchiveEndpoints
{
    public static IEndpointRouteBuilder MapBossCombatLogArchiveEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/combat")
            .RequireAuthorization()
            .WithTags("Combat");

        group.MapPost("/boss-log/telegram-v2", SendAsync);
        return endpoints;
    }

    private static async Task<IResult> SendAsync(
        BossCombatLogRequest request,
        ClaimsPrincipal user,
        CombatSessionRegistry registry,
        GameDbContext dbContext,
        ITelegramMessageSender messageSender,
        ILoggerFactory loggerFactory,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        if (request.SessionId == Guid.Empty)
        {
            return Results.BadRequest(
                new BossCombatLogResponse(false, "combat_log_session_invalid"));
        }

        CombatSessionSnapshot? snapshot = null;
        IReadOnlyList<CombatEvent>? events = null;
        GameContentSnapshot? contentSnapshot = null;
        CombatRewardApplicationResult? reward = null;

        CombatOperationResult current = registry.Resume(accountId);
        if (current.Succeeded
            && current.Snapshot is not null
            && current.Snapshot.SessionId == request.SessionId
            && current.Snapshot.Status != CombatSessionStatus.Active)
        {
            snapshot = current.Snapshot;
            contentSnapshot = current.ContentSnapshot;
            reward = current.Reward;

            CombatOperationResult historyRead = await registry.ExecuteAsync(
                accountId,
                (session, pinnedContent, _) =>
                {
                    if (session.SessionId == request.SessionId)
                    {
                        events = session.GetEventsAfter(0);
                        contentSnapshot ??= pinnedContent;
                    }

                    return new CombatCommandResult(
                        session.SessionId == request.SessionId,
                        session.SessionId == request.SessionId
                            ? null
                            : CombatErrorCodes.NotFound,
                        session.Snapshot(),
                        []);
                },
                cancellationToken);

            if (!historyRead.Succeeded)
            {
                snapshot = null;
                events = null;
                contentSnapshot = null;
            }
        }

        BossCombatLogResponse response = await BossCombatLogArchive.SendAsync(
            accountId,
            request.SessionId,
            snapshot,
            events,
            contentSnapshot,
            reward,
            dbContext,
            messageSender,
            loggerFactory.CreateLogger("Elyndor.BossCombatLog"),
            timeProvider.GetUtcNow(),
            cancellationToken);

        return Results.Ok(response);
    }

    private static bool TryGetAccountId(ClaimsPrincipal user, out Guid accountId) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId)
        && accountId != Guid.Empty;
}

internal static class BossCombatLogArchive
{
    private const int MaxEvents = 1500;
    private const int MaxSessions = 256;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    private static readonly ConcurrentDictionary<ArchiveKey, ArchiveEntry> Entries = [];

    private static readonly Action<ILogger, Guid, Exception?> DocumentSenderUnavailable =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(2301, nameof(DocumentSenderUnavailable)),
            "Boss combat log sender does not support documents for session {SessionId}.");

    private static readonly Action<ILogger, Guid, Guid, Exception?> DeliveryFailed =
        LoggerMessage.Define<Guid, Guid>(
            LogLevel.Error,
            new EventId(2302, nameof(DeliveryFailed)),
            "Failed to send boss combat log {SessionId} for account {AccountId}; "
            + "the archived log is retained for retry.");

    private static readonly Action<ILogger, Guid, Guid, int, int, Exception?> DeliverySucceeded =
        LoggerMessage.Define<Guid, Guid, int, int>(
            LogLevel.Information,
            new EventId(2303, nameof(DeliverySucceeded)),
            "Sent boss combat log {SessionId} for account {AccountId} "
            + "with {EventCount} events ({DroppedEventCount} dropped from archive).");

    public static void Capture(
        Guid accountId,
        CombatOperationResult update,
        DateTimeOffset capturedAtUtc)
    {
        CombatSessionSnapshot? snapshot = update.Snapshot;
        if (accountId == Guid.Empty
            || snapshot is null
            || snapshot.SessionId == Guid.Empty
            || !BossCombatLogPolicy.TryResolve(snapshot, out _))
        {
            return;
        }

        ArchiveKey key = new(accountId, snapshot.SessionId);
        ArchiveEntry entry = Entries.GetOrAdd(
            key,
            _ => new ArchiveEntry(
                snapshot,
                update.ContentSnapshot,
                update.Reward,
                capturedAtUtc));

        Merge(
            entry,
            snapshot,
            update.Events,
            update.ContentSnapshot,
            update.Reward,
            capturedAtUtc);
        Purge(capturedAtUtc);
    }

    public static async Task<BossCombatLogResponse> SendAsync(
        Guid accountId,
        Guid sessionId,
        CombatSessionSnapshot? authoritativeSnapshot,
        IReadOnlyList<CombatEvent>? authoritativeEvents,
        GameContentSnapshot? authoritativeContent,
        CombatRewardApplicationResult? authoritativeReward,
        GameDbContext dbContext,
        ITelegramMessageSender messageSender,
        ILogger logger,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        Purge(nowUtc);

        ArchiveKey key = new(accountId, sessionId);
        ArchiveEntry? entry = null;

        if (authoritativeSnapshot is not null
            && authoritativeSnapshot.SessionId == sessionId)
        {
            entry = Entries.GetOrAdd(
                key,
                _ => new ArchiveEntry(
                    authoritativeSnapshot,
                    authoritativeContent,
                    authoritativeReward,
                    nowUtc));
            Merge(
                entry,
                authoritativeSnapshot,
                authoritativeEvents ?? [],
                authoritativeContent,
                authoritativeReward,
                nowUtc);
        }
        else
        {
            Entries.TryGetValue(key, out entry);
        }

        if (entry is null)
            return new BossCombatLogResponse(false, "combat_log_session_not_found");

        await entry.SendGate.WaitAsync(cancellationToken);
        try
        {
            CombatSessionSnapshot snapshot;
            CombatEvent[] events;
            HashSet<long> filteredSequences;
            int droppedEvents;
            bool alreadySent;
            CombatRewardApplicationResult? reward;

            lock (entry.Gate)
            {
                snapshot = entry.Snapshot;
                events = entry.Events.Values.ToArray();
                filteredSequences = new HashSet<long>(entry.FilteredSequences);
                droppedEvents = entry.DroppedEvents;
                alreadySent = entry.Sent;
                reward = entry.Reward;
                entry.UpdatedAtUtc = nowUtc;
            }

            if (alreadySent)
                return new BossCombatLogResponse(true, null);

            if (snapshot.Status == CombatSessionStatus.Active)
                return new BossCombatLogResponse(false, "combat_log_combat_active");

            if (!BossCombatLogPolicy.TryResolve(
                    snapshot,
                    out BossCombatLogTarget target))
            {
                return new BossCombatLogResponse(
                    false,
                    BossCombatLogPolicy.IneligibleErrorCode);
            }

            if (events.Length == 0)
                return new BossCombatLogResponse(false, "combat_log_empty");

            long? telegramUserId = await dbContext.Accounts
                .AsNoTracking()
                .Where(account => account.Id == accountId)
                .Select(account => (long?)account.TelegramUserId)
                .SingleOrDefaultAsync(cancellationToken);
            if (telegramUserId is null)
            {
                return new BossCombatLogResponse(
                    false,
                    "combat_log_account_not_found");
            }

            if (messageSender is not ITelegramDocumentSender documentSender)
            {
                DocumentSenderUnavailable(logger, sessionId, null);
                return new BossCombatLogResponse(
                    false,
                    "combat_log_sender_unavailable");
            }

            CombatEvent[] ordered = events
                .OrderBy(combatEvent => combatEvent.Sequence)
                .ToArray();
            string log = BuildLog(
                snapshot,
                ordered,
                filteredSequences,
                droppedEvents,
                reward,
                target);
            string fileName =
                $"elyndor-boss-{FileSegment(target.DefinitionId)}-{sessionId:N}.txt";
            string caption =
                $"⚔️ Elyndor · {target.DisplayName} · {ordered.Length} событий";

            try
            {
                await documentSender.SendDocumentAsync(
                    telegramUserId.Value,
                    fileName,
                    log,
                    caption,
                    cancellationToken);
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException
                || !cancellationToken.IsCancellationRequested)
            {
                DeliveryFailed(logger, sessionId, accountId, exception);
                return new BossCombatLogResponse(
                    false,
                    "combat_log_telegram_failed");
            }

            lock (entry.Gate)
            {
                entry.Sent = true;
                entry.UpdatedAtUtc = nowUtc;
            }

            DeliverySucceeded(
                logger,
                sessionId,
                accountId,
                ordered.Length,
                droppedEvents,
                null);

            return new BossCombatLogResponse(true, null);
        }
        finally
        {
            entry.SendGate.Release();
        }
    }

    private static void Merge(
        ArchiveEntry entry,
        CombatSessionSnapshot snapshot,
        IReadOnlyList<CombatEvent> events,
        GameContentSnapshot? contentSnapshot,
        CombatRewardApplicationResult? reward,
        DateTimeOffset capturedAtUtc)
    {
        lock (entry.Gate)
        {
            entry.Snapshot = snapshot;
            if (contentSnapshot is not null)
                entry.ContentSnapshot = contentSnapshot;
            if (reward is not null)
                entry.Reward = reward;

            foreach (CombatEvent combatEvent in events)
            {
                if (ShouldArchiveEvent(combatEvent))
                {
                    entry.Events[combatEvent.Sequence] = combatEvent;
                    entry.FilteredSequences.Remove(combatEvent.Sequence);
                }
                else
                {
                    entry.Events.Remove(combatEvent.Sequence);
                    entry.FilteredSequences.Add(combatEvent.Sequence);
                }
            }

            while (entry.Events.Count > MaxEvents)
            {
                long firstSequence = entry.Events.Keys.First();
                entry.Events.Remove(firstSequence);
                entry.DroppedEvents++;
            }

            entry.UpdatedAtUtc = capturedAtUtc;
        }
    }

    private static void Purge(DateTimeOffset nowUtc)
    {
        foreach (KeyValuePair<ArchiveKey, ArchiveEntry> pair in Entries)
        {
            DateTimeOffset updatedAtUtc;
            lock (pair.Value.Gate)
                updatedAtUtc = pair.Value.UpdatedAtUtc;

            if (nowUtc - updatedAtUtc > Lifetime)
                Entries.TryRemove(pair.Key, out _);
        }

        int overflow = Entries.Count - MaxSessions;
        if (overflow <= 0)
            return;

        ArchiveKey[] oldest = Entries
            .Select(pair =>
            {
                DateTimeOffset updatedAtUtc;
                lock (pair.Value.Gate)
                    updatedAtUtc = pair.Value.UpdatedAtUtc;
                return (pair.Key, UpdatedAtUtc: updatedAtUtc);
            })
            .OrderBy(item => item.UpdatedAtUtc)
            .Take(overflow)
            .Select(item => item.Key)
            .ToArray();

        foreach (ArchiveKey key in oldest)
            Entries.TryRemove(key, out _);
    }

    private static string BuildLog(
        CombatSessionSnapshot snapshot,
        CombatEvent[] events,
        IReadOnlySet<long> filteredSequences,
        int droppedEvents,
        CombatRewardApplicationResult? reward,
        BossCombatLogTarget target)
    {
        Dictionary<Guid, string> actorNames = new();
        foreach (CombatActorSnapshot actor in snapshot.Players ?? [snapshot.Player])
            actorNames[actor.ActorId] = actor.Name;
        foreach (CombatActorSnapshot actor in snapshot.Enemies ?? [snapshot.Enemy])
            actorNames[actor.ActorId] = actor.Name;
        if (snapshot.Companion is not null)
            actorNames[snapshot.Companion.ActorId] = snapshot.Companion.Name;

        string result = snapshot.Status switch
        {
            CombatSessionStatus.Victory => "ПОБЕДА",
            CombatSessionStatus.Defeat => "ПОРАЖЕНИЕ",
            CombatSessionStatus.Cancelled => "ОТМЕНЁН",
            _ => snapshot.Status.ToString().ToUpperInvariant()
        };

        long[] missingSequences = FindMissingSequences(events, filteredSequences);

        StringBuilder builder = new();
        builder.AppendLine(target.IsTrainingDummy
            ? "⚔️ ELYNDOR · COMBAT REPORT"
            : "⚔️ ELYNDOR · BOSS COMBAT REPORT");
        builder.Append("Цель: ").AppendLine(target.DisplayName);
        builder.Append("Результат: ").AppendLine(result);
        builder.Append("Игрок: ").AppendLine(snapshot.Player.Name);
        builder.Append("Correlation/Session: ").AppendLine(snapshot.SessionId.ToString("D"));

        WriteSummary(builder, snapshot, events, reward, target);
        WriteKeyEvents(builder, snapshot, events, actorNames);

        builder.AppendLine();
        builder.AppendLine("── DIAGNOSTICS ──");
        builder.Append("Generated: ")
            .AppendLine(snapshot.Sequence.ToString(CultureInfo.InvariantCulture));
        builder.Append("Archived: ")
            .AppendLine(events.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append("Filtered noise: ")
            .AppendLine(filteredSequences.Count.ToString(CultureInfo.InvariantCulture));
        builder.Append("Dropped by archive limit: ")
            .AppendLine(droppedEvents.ToString(CultureInfo.InvariantCulture));
        builder.Append("Sequence gaps: ")
            .AppendLine(FormatMissingSequences(missingSequences));
        builder.Append("Full session statistics: ")
            .AppendLine(snapshot.Statistics is null ? "no (legacy fallback)" : "yes");
        builder.Append("Контент: ").Append(snapshot.ContentVersion)
            .Append(" · баланс: ").AppendLine(snapshot.BalanceVersion);

        if (events.Length > 0)
        {
            builder.Append("Archived sequence: #")
                .Append(events[0].Sequence.ToString(CultureInfo.InvariantCulture))
                .Append("–#")
                .AppendLine(events[^1].Sequence.ToString(CultureInfo.InvariantCulture));
        }

        builder.AppendLine();
        builder.AppendLine("── RAW EVENTS ──");
        foreach (CombatEvent combatEvent in events)
            WriteEvent(builder, combatEvent, actorNames);

        return builder.ToString();
    }

    private static void WriteSummary(
        StringBuilder builder,
        CombatSessionSnapshot snapshot,
        CombatEvent[] events,
        CombatRewardApplicationResult? reward,
        BossCombatLogTarget target)
    {
        Guid playerActorId = snapshot.Player.ActorId;
        HashSet<Guid> enemyActorIds = (snapshot.Enemies ?? [snapshot.Enemy])
            .Select(enemy => enemy.ActorId)
            .ToHashSet();
        CombatSessionStatisticsSnapshot? statistics = snapshot.Statistics;

        DateTimeOffset? startedAt = statistics?.StartedAtUtc
            ?? events.FirstOrDefault(combatEvent =>
                combatEvent.Type == CombatEventType.CombatStarted)?.OccurredAtUtc
            ?? events.FirstOrDefault()?.OccurredAtUtc;
        DateTimeOffset endedAt = snapshot.ServerTimeUtc;
        double seconds = startedAt is null
            ? 0
            : Math.Max(0, (endedAt - startedAt.Value).TotalSeconds);

        decimal damageDealt = statistics?.DamageDealt
            ?? events
                .Where(combatEvent =>
                    combatEvent.Type == CombatEventType.DamageDealt
                    && combatEvent.SourceActorId == playerActorId
                    && combatEvent.TargetActorId is Guid targetActorId
                    && enemyActorIds.Contains(targetActorId))
                .Sum(combatEvent => Math.Max(0, combatEvent.Amount));
        decimal damageReceived = statistics?.DamageTaken
            ?? events
                .Where(combatEvent =>
                    combatEvent.Type == CombatEventType.DamageDealt
                    && combatEvent.TargetActorId == playerActorId)
                .Sum(combatEvent => Math.Max(0, combatEvent.Amount));
        decimal healingDone = statistics?.HealingDone
            ?? events
                .Where(combatEvent =>
                    combatEvent.Type == CombatEventType.HealingApplied
                    && combatEvent.SourceActorId == playerActorId)
                .Sum(combatEvent => Math.Max(0, combatEvent.Amount));
        int criticalHits = statistics?.CriticalHits
            ?? events.Count(combatEvent =>
                combatEvent.Type == CombatEventType.DamageDealt
                && combatEvent.SourceActorId == playerActorId
                && combatEvent.IsCritical);
        decimal armorMitigated = statistics?.ArmorMitigated
            ?? events
                .Where(combatEvent =>
                    combatEvent.Type == CombatEventType.DamageDealt
                    && combatEvent.TargetActorId == playerActorId
                    && combatEvent.RawDamage > 0
                    && combatEvent.DamageAfterMitigation >= 0)
                .Sum(combatEvent => Math.Max(
                    0,
                    combatEvent.RawDamage - combatEvent.DamageAfterMitigation));
        decimal blocked = statistics?.Blocked
            ?? events
                .Where(combatEvent =>
                    combatEvent.Type == CombatEventType.DamageBlocked
                    && combatEvent.TargetActorId == playerActorId)
                .Sum(combatEvent => Math.Max(0, combatEvent.Amount));
        decimal absorbed = statistics?.ShieldAbsorbed
            ?? events
                .Where(combatEvent =>
                    combatEvent.Type == CombatEventType.ShieldAbsorbed
                    && combatEvent.TargetActorId == playerActorId)
                .Sum(combatEvent => Math.Max(0, combatEvent.Amount));
        int dodges = statistics?.Dodges
            ?? events.Count(combatEvent =>
                combatEvent.Type == CombatEventType.Dodge
                && combatEvent.TargetActorId == playerActorId);
        int playerDeaths = statistics?.Deaths
            ?? events.Count(combatEvent =>
                combatEvent.Type == CombatEventType.ActorDied
                && combatEvent.ActorId == playerActorId);
        decimal dps = seconds > 0 ? damageDealt / (decimal)seconds : 0;

        builder.AppendLine();
        builder.AppendLine("── ИТОГИ ──");
        if (seconds > 0)
        {
            builder.Append("Длительность: ")
                .Append(seconds.ToString("0.0", CultureInfo.InvariantCulture))
                .AppendLine(" сек.");
        }
        builder.Append("Урон: ").Append(FormatNumber(damageDealt))
            .Append(" · DPS: ").AppendLine(FormatNumber(dps));
        builder.Append("Получено урона: ").AppendLine(FormatNumber(damageReceived));
        builder.Append("Лечение: ").AppendLine(FormatNumber(healingDone));
        builder.Append("Критов: ")
            .Append(criticalHits.ToString(CultureInfo.InvariantCulture))
            .Append(" · уклонений: ")
            .Append(dodges.ToString(CultureInfo.InvariantCulture))
            .Append(" · смертей: ")
            .AppendLine(playerDeaths.ToString(CultureInfo.InvariantCulture));
        builder.Append("Снято бронёй: ").Append(FormatNumber(armorMitigated))
            .Append(" · заблокировано: ").Append(FormatNumber(blocked))
            .Append(" · поглощено щитами: ").AppendLine(FormatNumber(absorbed));
        if (statistics is not null)
        {
            builder.Append("Ресурс: +").Append(FormatNumber(statistics.ResourceGained))
                .Append(" / -").AppendLine(FormatNumber(statistics.ResourceSpent));
        }
        builder.Append("Финал HP/ресурс: ")
            .Append(FormatNumber(snapshot.Player.Hp))
            .Append('/')
            .Append(FormatNumber(snapshot.Player.MaxHp))
            .Append(" · ")
            .Append(FormatNumber(snapshot.Player.Resource))
            .Append('/')
            .AppendLine(FormatNumber(snapshot.Player.MaxResource));

        WriteDamageSources(builder, statistics, damageDealt);
        WriteAbilityBreakdown(builder, snapshot, events, damageDealt);
        WritePartyBreakdown(builder, snapshot);

        if (snapshot.PlayerContribution is not null)
        {
            builder.AppendLine();
            builder.AppendLine("── CONTRIBUTION ──");
            builder.Append("damage=").Append(FormatNumber(snapshot.PlayerContribution.DamageDealt))
                .Append(" · healing=").Append(FormatNumber(snapshot.PlayerContribution.EffectiveHealing))
                .Append(" · actions=")
                .Append(snapshot.PlayerContribution.QualifyingActions
                    .ToString(CultureInfo.InvariantCulture))
                .Append(" · eligible=")
                .AppendLine((snapshot.PlayerContributionEligible ?? false)
                    ? "yes"
                    : "no");
        }

        builder.AppendLine();
        builder.AppendLine("── НАГРАДА ──");
        if (reward is not null)
        {
            builder.Append('+')
                .Append(reward.XpEarned.ToString(CultureInfo.InvariantCulture))
                .Append(" XP · +")
                .Append(reward.GoldEarned.ToString(CultureInfo.InvariantCulture))
                .Append(" gold");
            if (reward.Items.Count > 0)
            {
                builder.Append(" · ")
                    .Append(string.Join(
                        ", ",
                        reward.Items.Select(item =>
                            $"{item.Name}×{item.Quantity}")));
            }
            builder.AppendLine();
        }
        else if (target.DefinitionId.StartsWith("WORLD_BOSS_", StringComparison.Ordinal))
        {
            builder.AppendLine("Рассчитывается глобальным World Boss settlement.");
        }
        else
        {
            builder.AppendLine("—");
        }
    }

    private static void WriteDamageSources(
        StringBuilder builder,
        CombatSessionStatisticsSnapshot? statistics,
        decimal totalDamage)
    {
        CombatDamageSourceStatisticsSnapshot? sources = statistics?.DamageSources;
        if (sources is null)
            return;

        builder.AppendLine();
        builder.AppendLine("── ИСТОЧНИКИ УРОНА ──");
        WriteDamageSource(builder, "Автоатака", sources.AutoAttack, totalDamage);
        WriteDamageSource(builder, "Способности/proc", sources.DirectOrProc, totalDamage);
        WriteDamageSource(builder, "DoT", sources.Periodic, totalDamage);
        WriteDamageSource(builder, "Отражение", sources.Reflected, totalDamage);
        WriteDamageSource(builder, "Компаньон", sources.Companion, totalDamage);
        WriteDamageSource(builder, "Прочее", sources.Other, totalDamage);
    }

    private static void WriteDamageSource(
        StringBuilder builder,
        string name,
        decimal damage,
        decimal totalDamage)
    {
        if (damage <= 0)
            return;

        decimal percent = totalDamage > 0
            ? damage / totalDamage * 100m
            : 0;
        builder.Append(name)
            .Append(": ")
            .Append(FormatNumber(damage))
            .Append(" · ")
            .Append(percent.ToString("0.0", CultureInfo.InvariantCulture))
            .AppendLine("%");
    }

    private static void WriteAbilityBreakdown(
        StringBuilder builder,
        CombatSessionSnapshot snapshot,
        IReadOnlyList<CombatEvent> events,
        decimal totalDamage)
    {
        builder.AppendLine();
        builder.AppendLine("── СПОСОБНОСТИ / EFFECTS ──");

        IReadOnlyDictionary<string, CombatAbilityStatisticsSnapshot>? abilities =
            snapshot.Statistics?.Abilities;
        if (abilities is null || abilities.Count == 0)
        {
            string fallback = FormatAbilitySummary(snapshot, events);
            builder.AppendLine(string.IsNullOrWhiteSpace(fallback) ? "—" : fallback);
            return;
        }

        CombatAbilityStatisticsSnapshot[] ordered = abilities.Values
            .Where(item =>
                item.Uses > 0
                || item.Applications > 0
                || item.PeriodicTicks > 0
                || item.Damage > 0)
            .OrderByDescending(item => item.Damage)
            .ThenByDescending(item => item.Uses)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Take(16)
            .ToArray();

        if (ordered.Length == 0)
        {
            builder.AppendLine("—");
            return;
        }

        foreach (CombatAbilityStatisticsSnapshot ability in ordered)
        {
            decimal percent = totalDamage > 0
                ? ability.Damage / totalDamage * 100m
                : 0;
            builder.Append(ability.Id);

            if (ability.Uses > 0)
                builder.Append(" · uses=").Append(ability.Uses);
            if (ability.Applications > 0)
                builder.Append(" · applications=").Append(ability.Applications);
            if (ability.PeriodicTicks > 0)
                builder.Append(" · ticks=").Append(ability.PeriodicTicks);
            if (ability.Hits > 0)
                builder.Append(" · hits=").Append(ability.Hits);
            if (ability.CriticalHits > 0)
                builder.Append(" · crits=").Append(ability.CriticalHits);
            if (ability.Damage > 0)
            {
                builder.Append(" · dmg=").Append(FormatNumber(ability.Damage))
                    .Append(" (")
                    .Append(percent.ToString("0.0", CultureInfo.InvariantCulture))
                    .Append("%)")
                    .Append(" · max=").Append(FormatNumber(ability.MaxHit));
            }

            builder.AppendLine();
        }
    }

    private static void WritePartyBreakdown(
        StringBuilder builder,
        CombatSessionSnapshot snapshot)
    {
        if (snapshot.ParticipantContributions is not { Count: > 1 } contributions)
            return;

        Dictionary<Guid, string> names = (snapshot.Players ?? [snapshot.Player])
            .ToDictionary(player => player.ActorId, player => player.Name);

        builder.AppendLine();
        builder.AppendLine("── ГРУППА ──");
        foreach (ContributionEligibilityResult contribution in contributions
                     .OrderByDescending(item => item.Snapshot.DamageDealt)
                     .ThenBy(item => item.Snapshot.CharacterId))
        {
            string name = names.GetValueOrDefault(
                contribution.Snapshot.CharacterId,
                contribution.Snapshot.CharacterId.ToString("N")[..8]);
            builder.Append(name)
                .Append(" · damage=")
                .Append(FormatNumber(contribution.Snapshot.DamageDealt))
                .Append(" · healing=")
                .Append(FormatNumber(contribution.Snapshot.EffectiveHealing))
                .Append(" · actions=")
                .Append(contribution.Snapshot.QualifyingActions
                    .ToString(CultureInfo.InvariantCulture))
                .Append(" · ")
                .AppendLine(contribution.IsEligible ? "eligible" : contribution.Reason);
        }
    }

    private static void WriteKeyEvents(
        StringBuilder builder,
        CombatSessionSnapshot snapshot,
        IReadOnlyList<CombatEvent> events,
        IReadOnlyDictionary<Guid, string> actorNames)
    {
        CombatEvent[] keyEvents = events
            .Where(IsKeyEvent)
            .Take(24)
            .ToArray();
        if (keyEvents.Length == 0)
            return;

        DateTimeOffset origin = snapshot.Statistics?.StartedAtUtc
            ?? events.FirstOrDefault()?.OccurredAtUtc
            ?? snapshot.ServerTimeUtc;

        builder.AppendLine();
        builder.AppendLine("── KEY EVENTS ──");
        foreach (CombatEvent combatEvent in keyEvents)
        {
            builder.Append(FormatElapsed(origin, combatEvent.OccurredAtUtc))
                .Append(" · ")
                .Append(combatEvent.Type);

            Guid sourceId = combatEvent.SourceActorId ?? combatEvent.ActorId;
            builder.Append(" · ").Append(ResolveActorName(sourceId, actorNames));
            if (combatEvent.TargetActorId is Guid targetActorId)
                builder.Append(" → ").Append(ResolveActorName(targetActorId, actorNames));
            if (!string.IsNullOrWhiteSpace(combatEvent.DefinitionId))
                builder.Append(" · ").Append(Sanitize(combatEvent.DefinitionId, 80));
            builder.AppendLine();
        }
    }

    private static bool IsKeyEvent(CombatEvent combatEvent) =>
        combatEvent.Type is
            CombatEventType.ActorDied
            or CombatEventType.AbilityInterrupted
            or CombatEventType.EffectImmune
            or CombatEventType.ActorSummoned
            or CombatEventType.CombatEnded
        || (!string.IsNullOrWhiteSpace(combatEvent.DefinitionId)
            && combatEvent.DefinitionId.Contains(
                "PHASE",
                StringComparison.OrdinalIgnoreCase));

    private static string FormatElapsed(
        DateTimeOffset origin,
        DateTimeOffset occurredAtUtc)
    {
        TimeSpan elapsed = occurredAtUtc - origin;
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        return elapsed.TotalHours >= 1
            ? elapsed.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture)
            : elapsed.ToString(@"mm\:ss\.fff", CultureInfo.InvariantCulture);
    }

    private static decimal DamageAmount(CombatEvent combatEvent) =>
        combatEvent.AmountBeforeShields > 0
            ? combatEvent.AmountBeforeShields
            : Math.Max(0, combatEvent.Amount);

    internal static bool ShouldArchiveEvent(CombatEvent combatEvent) =>
        !(string.Equals(
                combatEvent.DefinitionId,
                "COMBAT_REGEN",
                StringComparison.Ordinal)
            && combatEvent.Amount == 0);

    internal static string FormatAbilitySummary(
        CombatSessionSnapshot snapshot,
        IReadOnlyList<CombatEvent> events)
    {
        IEnumerable<KeyValuePair<string, int>> counts =
            snapshot.Statistics?.AbilityUses
            ?? events
                .Where(combatEvent =>
                    combatEvent.Type == CombatEventType.AbilityUsed
                    && combatEvent.SourceActorId == snapshot.Player.ActorId
                    && !string.IsNullOrWhiteSpace(combatEvent.DefinitionId))
                .GroupBy(
                    combatEvent => combatEvent.DefinitionId!,
                    StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count(),
                    StringComparer.Ordinal);

        return string.Join(
            ", ",
            counts
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Take(8)
                .Select(pair => $"{pair.Key}×{pair.Value}"));
    }

    private static void WriteEvent(
        StringBuilder builder,
        CombatEvent combatEvent,
        Dictionary<Guid, string> actorNames)
    {
        Guid sourceActorId = combatEvent.SourceActorId ?? combatEvent.ActorId;
        string source = ResolveActorName(sourceActorId, actorNames);
        string target = combatEvent.TargetActorId is Guid targetActorId
            ? ResolveActorName(targetActorId, actorNames)
            : "—";

        builder.Append('#')
            .Append(combatEvent.Sequence.ToString(CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(FormatTime(combatEvent.OccurredAtUtc))
            .Append(" · ")
            .Append(combatEvent.Type)
            .Append(" · ")
            .Append(source);

        if (combatEvent.TargetActorId is not null)
            builder.Append(" → ").Append(target);
        if (!string.IsNullOrWhiteSpace(combatEvent.DefinitionId))
            builder.Append(" · ").Append(Sanitize(combatEvent.DefinitionId, 80));

        if (combatEvent.Amount != 0
            || combatEvent.AmountBeforeShields != 0
            || combatEvent.RawDamage != 0
            || combatEvent.DamageAfterMitigation != 0
            || combatEvent.DamageBeforeBlock != 0)
        {
            builder.Append(" · amount=").Append(FormatNumber(combatEvent.Amount));

            if (combatEvent.AmountBeforeShields != 0)
            {
                builder.Append(" · beforeShields=")
                    .Append(FormatNumber(combatEvent.AmountBeforeShields));
            }

            if (combatEvent.RawDamage != 0)
                builder.Append(" · raw=").Append(FormatNumber(combatEvent.RawDamage));
            if (combatEvent.DamageAfterMitigation != 0)
            {
                builder.Append(" · afterArmor=")
                    .Append(FormatNumber(combatEvent.DamageAfterMitigation));
            }

            if (combatEvent.DamageBeforeBlock != 0)
            {
                builder.Append(" · beforeBlock=")
                    .Append(FormatNumber(combatEvent.DamageBeforeBlock));
            }
        }

        if (combatEvent.WeaponHand is not null)
            builder.Append(" · ").Append(combatEvent.WeaponHand);
        if (!string.IsNullOrWhiteSpace(combatEvent.WeaponDefinitionId))
        {
            builder.Append(" · weapon=")
                .Append(Sanitize(combatEvent.WeaponDefinitionId, 80));
        }

        builder.AppendLine();
    }

    private static long[] FindMissingSequences(
        CombatEvent[] events,
        IReadOnlySet<long> filteredSequences)
    {
        if (events.Length == 0)
            return [];

        List<long> missing = [];
        long previous = 0;
        foreach (CombatEvent combatEvent in events)
        {
            for (long sequence = previous + 1;
                 sequence < combatEvent.Sequence;
                 sequence++)
            {
                if (filteredSequences.Contains(sequence))
                    continue;

                missing.Add(sequence);
                if (missing.Count >= 100)
                    return missing.ToArray();
            }

            previous = Math.Max(previous, combatEvent.Sequence);
        }

        return missing.ToArray();
    }

    private static string FormatMissingSequences(long[] missing)
    {
        if (missing.Length == 0)
            return "нет";

        string values = string.Join(", ", missing.Take(20));
        return missing.Length <= 20
            ? values
            : $"{values}, … (не менее {missing.Length})";
    }

    private static string ResolveActorName(
        Guid actorId,
        Dictionary<Guid, string> names) =>
        names.TryGetValue(actorId, out string? name)
            ? name
            : actorId.ToString("N")[..8];

    private static string FormatTime(DateTimeOffset value) =>
        value.ToUniversalTime()
            .ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    private static string FormatNumber(decimal value) =>
        decimal.Round(value, 2)
            .ToString("0.##", CultureInfo.InvariantCulture);

    private static string FileSegment(string value)
    {
        string sanitized = new(
            value
                .ToLowerInvariant()
                .Select(character =>
                    char.IsLetterOrDigit(character) ? character : '-')
                .ToArray());
        return sanitized.Trim('-');
    }

    private static string Sanitize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "—";

        string sanitized = value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        return sanitized.Length <= maxLength
            ? sanitized
            : sanitized[..maxLength];
    }

    private readonly record struct ArchiveKey(Guid AccountId, Guid SessionId);

    private sealed class ArchiveEntry(
        CombatSessionSnapshot snapshot,
        GameContentSnapshot? contentSnapshot,
        CombatRewardApplicationResult? reward,
        DateTimeOffset updatedAtUtc)
    {
        public object Gate { get; } = new();
        public SemaphoreSlim SendGate { get; } = new(1, 1);
        public SortedDictionary<long, CombatEvent> Events { get; } = [];
        public HashSet<long> FilteredSequences { get; } = [];
        public CombatSessionSnapshot Snapshot { get; set; } = snapshot;
        public GameContentSnapshot? ContentSnapshot { get; set; } = contentSnapshot;
        public CombatRewardApplicationResult? Reward { get; set; } = reward;
        public DateTimeOffset UpdatedAtUtc { get; set; } = updatedAtUtc;
        public int DroppedEvents { get; set; }
        public bool Sent { get; set; }
    }
}
