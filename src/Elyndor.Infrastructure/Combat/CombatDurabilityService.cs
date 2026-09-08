using System.Text.Json;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Contribution;
using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Items;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace Elyndor.Infrastructure.Combat;

public sealed record CombatDurabilityBeginResult(bool Succeeded, bool Created);

public sealed class CombatDurabilityService(
    GameDbContext dbContext,
    ILogger<CombatDurabilityService> logger,
    IServiceScopeFactory? scopeFactory = null)
{
    private static readonly JsonSerializerOptions TerminalSnapshotJsonOptions = new()
    {
        Converters = { new ReadOnlyStringSetJsonConverter() }
    };

    private static readonly Action<ILogger, Guid, Guid, bool, Exception?>
        InterruptedCombatRecovered =
            LoggerMessage.Define<Guid, Guid, bool>(
                LogLevel.Warning,
                new EventId(2101, nameof(InterruptedCombatRecovered)),
                "Recovered interrupted combat session {SessionId} for character "
                + "{CharacterId}; rewardCommitted={RewardCommitted}.");
    private static readonly Action<ILogger, Guid, Exception?>
        TerminalCombatRecoveryFailed =
            LoggerMessage.Define<Guid>(
                LogLevel.Error,
                new EventId(2102, nameof(TerminalCombatRecoveryFailed)),
                "Failed to finalize durable terminal combat session {SessionId}; "
                + "the recovery journal will remain for retry.");

    public async Task<bool> BeginAsync(
        Guid characterId,
        CombatSessionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        CombatDurabilityBeginResult result = await BeginParticipantAsync(
            characterId,
            snapshot,
            cancellationToken);
        return result.Succeeded;
    }

    public Task<CombatDurabilityBeginResult> BeginParticipantAsync(
        Guid characterId,
        CombatSessionSnapshot snapshot,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => BeginParticipantCoreAsync(characterId, snapshot, cancellationToken));

    private async Task<CombatDurabilityBeginResult> BeginParticipantCoreAsync(
        Guid characterId,
        CombatSessionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (characterId == Guid.Empty)
            throw new ArgumentException("Character id cannot be empty.", nameof(characterId));

        await using IDbContextTransaction? transaction =
            await BeginAdvisoryLockAsync($"combat-character:{characterId:N}", cancellationToken);

        ActiveCombatSession? existing = await dbContext.ActiveCombatSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                state => state.CharacterId == characterId,
                cancellationToken);
        if (existing is not null)
        {
            await CommitAsync(transaction, cancellationToken);
            return new(
                existing.SessionId == snapshot.SessionId,
                false);
        }

        dbContext.ActiveCombatSessions.Add(new ActiveCombatSession(
            snapshot.SessionId,
            characterId,
            snapshot.ServerTimeUtc,
            snapshot.ContentVersion,
            snapshot.BalanceVersion));
        await dbContext.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        return new(true, true);
    }

    public Task RemoveParticipantAsync(
        Guid sessionId,
        Guid characterId,
        CancellationToken cancellationToken) =>
        dbContext.ActiveCombatSessions
            .Where(state => state.SessionId == sessionId
                && state.CharacterId == characterId)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task CompleteAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await dbContext.ActiveCombatSessions
            .Where(state => state.SessionId == sessionId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task CompleteParticipantAsync(
        Guid sessionId,
        Guid characterId,
        CancellationToken cancellationToken)
    {
        await dbContext.ActiveCombatSessions
            .Where(state => state.SessionId == sessionId
                && state.CharacterId == characterId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task RecordTerminalSnapshotAsync(
        Guid sessionId,
        CombatSessionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (snapshot.Status == CombatSessionStatus.Active)
            throw new ArgumentException(
                "Only terminal combat snapshots can be persisted.",
                nameof(snapshot));

        string snapshotJson = JsonSerializer.Serialize(
            snapshot,
            TerminalSnapshotJsonOptions);
        await dbContext.ActiveCombatSessions
            .Where(state => state.SessionId == sessionId
                && state.TerminalSnapshotJson == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    state => state.TerminalSnapshotJson,
                    snapshotJson),
                cancellationToken);
    }

    public async Task<string?> ReserveConsumableAsync(
        Guid accountId,
        Guid sessionId,
        string commandId,
        string itemDefinitionId,
        GameContentSnapshot contentSnapshot,
        DateTimeOffset usedAtUtc,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(commandId)
            || commandId.Length > 128)
        {
            return InventoryErrorCodes.Conflict;
        }

        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using IDbContextTransaction transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);

            Character? character = await dbContext.Characters
                .FromSqlInterpolated(
                    $"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (character is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return InventoryErrorCodes.CharacterNotFound;
            }

            ActiveCombatSession? active = await dbContext.ActiveCombatSessions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    state => state.SessionId == sessionId
                        && state.CharacterId == character.Id,
                    cancellationToken);
            if (active is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return InventoryErrorCodes.Conflict;
            }

            CombatConsumableUse? existing = await dbContext.CombatConsumableUses
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    use => use.SessionId == sessionId
                        && use.CommandId == commandId,
                    cancellationToken);
            if (existing is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return string.Equals(
                    existing.ItemDefinitionId,
                    itemDefinitionId,
                    StringComparison.Ordinal)
                        ? null
                        : InventoryErrorCodes.MutationConflict;
            }

            if (!contentSnapshot.Indexes.ItemsById.TryGetValue(
                    itemDefinitionId,
                    out ItemDefinition? definition)
                || definition.Type != ItemType.Consumable
                || !definition.Stackable
                || definition.MaxStack < 2)
            {
                await transaction.RollbackAsync(cancellationToken);
                return InventoryErrorCodes.NotConsumable;
            }

            CharacterItem? item = await dbContext.CharacterItems
                .Where(candidate =>
                    candidate.CharacterId == character.Id
                    && candidate.ItemDefinitionId == itemDefinitionId
                    && candidate.Quantity > 0)
                .OrderBy(candidate => candidate.AcquiredAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            if (item is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return InventoryErrorCodes.ItemNotFound;
            }

            item.RemoveQuantity(1);
            if (item.Quantity == 0)
                dbContext.CharacterItems.Remove(item);

            dbContext.CombatConsumableUses.Add(new CombatConsumableUse(
                sessionId,
                commandId,
                character.Id,
                definition.Id,
                item.DefinitionVersion,
                definition.MaxStack,
                usedAtUtc));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        });
    }

    public async Task RollbackConsumableAsync(
        Guid sessionId,
        string commandId,
        CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using IDbContextTransaction transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);
            CombatConsumableUse? use = await dbContext.CombatConsumableUses
                .SingleOrDefaultAsync(
                    candidate => candidate.SessionId == sessionId
                        && candidate.CommandId == commandId,
                    cancellationToken);
            if (use is not null)
            {
                await RefundUseAsync(use, cancellationToken);
                dbContext.CombatConsumableUses.Remove(use);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        });
    }

    public async Task<int> RecoverInterruptedAsync(
        CancellationToken cancellationToken)
    {
        ActiveCombatSession[] sessions = await dbContext.ActiveCombatSessions
            .AsNoTracking()
            .OrderBy(state => state.StartedAtUtc)
            .ToArrayAsync(cancellationToken);
        if (sessions.Length == 0)
            return 0;

        IServiceScope? recoveryScope = scopeFactory?.CreateScope();
        try
        {
            ICombatSessionFinalizer? finalizer = recoveryScope?.ServiceProvider
                .GetService<ICombatSessionFinalizer>();

            foreach (IGrouping<Guid, ActiveCombatSession> sessionGroup in sessions.GroupBy(
                         session => session.SessionId))
            {
                ActiveCombatSession[] participantStates = sessionGroup.ToArray();
                ActiveCombatSession session = participantStates[0];
                bool rewardCommitted = await dbContext.CombatRewardGrants
                    .AsNoTracking()
                    .AnyAsync(
                        grant => grant.CombatSessionId == session.SessionId,
                        cancellationToken);

                bool finalizedTerminalCombat = false;
                bool terminalRecoveryAttempted = false;
                string? terminalSnapshotJson = participantStates
                    .Select(state => state.TerminalSnapshotJson)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                if (finalizer is not null
                    && !string.IsNullOrWhiteSpace(terminalSnapshotJson))
                {
                    try
                    {
                        CombatSessionSnapshot? terminalSnapshot =
                            JsonSerializer.Deserialize<CombatSessionSnapshot>(
                                terminalSnapshotJson,
                                TerminalSnapshotJsonOptions);
                        if (terminalSnapshot is not null
                            && terminalSnapshot.Status != CombatSessionStatus.Active)
                        {
                            terminalRecoveryAttempted = true;
                            foreach (ActiveCombatSession participant in participantStates)
                            {
                                CombatSessionSnapshot participantSnapshot =
                                    ProjectSnapshotForCharacter(
                                        terminalSnapshot,
                                        participant.CharacterId);
                                await finalizer.FinalizeAsync(
                                    participant.CharacterId,
                                    participantSnapshot,
                                    cancellationToken);
                            }

                            finalizedTerminalCombat = !await dbContext.ActiveCombatSessions
                                .AsNoTracking()
                                .AnyAsync(
                                    state => state.SessionId == session.SessionId,
                                    cancellationToken);
                        }
                    }
                    catch (Exception exception)
                    {
                        TerminalCombatRecoveryFailed(
                            logger,
                            session.SessionId,
                            exception);
                    }
                }

                if (!finalizedTerminalCombat && !terminalRecoveryAttempted)
                {
                    if (!rewardCommitted)
                    {
                        CombatConsumableUse[] uses = await dbContext.CombatConsumableUses
                            .Where(use => use.SessionId == session.SessionId)
                            .OrderBy(use => use.UsedAtUtc)
                            .ToArrayAsync(cancellationToken);
                        foreach (CombatConsumableUse use in uses)
                            await RefundUseAsync(use, cancellationToken);
                    }

                    DungeonEncounter? interruptedEncounter = await dbContext.DungeonEncounters
                        .SingleOrDefaultAsync(
                            encounter => encounter.CombatSessionId == session.SessionId,
                            cancellationToken);
                    interruptedEncounter?.MarkWiped();

                    dbContext.ActiveCombatSessions.RemoveRange(participantStates);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                if (!terminalRecoveryAttempted || finalizedTerminalCombat)
                {
                    InterruptedCombatRecovered(
                        logger,
                        session.SessionId,
                        session.CharacterId,
                        rewardCommitted || finalizedTerminalCombat,
                        null);
                }
            }
        }
        finally
        {
            recoveryScope?.Dispose();
        }

        return sessions.Length;
    }

    private static CombatSessionSnapshot ProjectSnapshotForCharacter(
        CombatSessionSnapshot snapshot,
        Guid characterId)
    {
        CombatParticipantSnapshot? participant = snapshot.ParticipantRoster?
            .FirstOrDefault(candidate => candidate.CharacterId == characterId);
        CombatActorSnapshot? player = participant is null
            ? snapshot.Players?.FirstOrDefault(candidate => candidate.ActorId == characterId)
            : snapshot.Players?.FirstOrDefault(candidate =>
                candidate.ActorId == participant.ActorId);
        player ??= snapshot.Player;

        ContributionEligibilityResult? contribution = snapshot.ParticipantContributions?
            .FirstOrDefault(candidate => candidate.Snapshot.CharacterId == characterId);
        return snapshot with
        {
            Player = player,
            PlayerContribution = contribution?.Snapshot
                ?? (snapshot.PlayerContribution?.CharacterId == characterId
                    ? snapshot.PlayerContribution
                    : null),
            PlayerContributionEligible = contribution?.IsEligible
                ?? (snapshot.PlayerContribution?.CharacterId == characterId
                    ? snapshot.PlayerContributionEligible
                    : null)
        };
    }

    private sealed class ReadOnlyStringSetJsonConverter : JsonConverter<IReadOnlySet<string>>
    {
        public override IReadOnlySet<string> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            string[] values = JsonSerializer.Deserialize<string[]>(ref reader, options) ?? [];
            return new HashSet<string>(values, StringComparer.Ordinal);
        }

        public override void Write(
            Utf8JsonWriter writer,
            IReadOnlySet<string> value,
            JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.ToArray(), options);
    }

    private async Task RefundUseAsync(
        CombatConsumableUse use,
        CancellationToken cancellationToken)
    {
        CharacterItem? stack = await dbContext.CharacterItems
            .Where(item =>
                item.CharacterId == use.CharacterId
                && item.ItemDefinitionId == use.ItemDefinitionId
                && item.DefinitionVersion == use.DefinitionVersion
                && item.Quantity < use.MaxStack)
            .OrderBy(item => item.AcquiredAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (stack is not null)
        {
            stack.AddQuantity(1, use.MaxStack);
            return;
        }

        dbContext.CharacterItems.Add(new CharacterItem(
            Guid.NewGuid(),
            use.CharacterId,
            use.ItemDefinitionId,
            1,
            use.UsedAtUtc,
            use.DefinitionVersion));
    }

    private async Task<IDbContextTransaction?> BeginAdvisoryLockAsync(
        string lockKey,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
            return null;

        IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))",
            cancellationToken);
        return transaction;
    }

    private static async Task CommitAsync(
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
    }
}
