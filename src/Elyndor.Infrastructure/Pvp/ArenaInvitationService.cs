using System.Data;
using Elyndor.Core.Characters;
using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elyndor.Infrastructure.Pvp;

public sealed record ArenaInvitationView
{
    public Guid Id { get; init; }
    public Guid InviterCharacterId { get; init; }
    public required string InviterName { get; init; }
    public Guid TargetCharacterId { get; init; }
    public required string TargetName { get; init; }
    public ArenaInvitationStatus Status { get; init; }
    public DateTimeOffset ExpiresAtUtc { get; init; }
    public Guid? MatchId { get; init; }
    public bool Incoming { get; init; }
}
public sealed record ArenaInvitationResult(bool Succeeded, string? ErrorCode,
    ArenaInvitationView? Invitation = null, bool Created = false);

public sealed partial class ArenaInvitationService(GameDbContext db, TimeProvider time,
    CharacterOperationGuard guard, ArenaEligibilityService eligibility, ArenaPresenceTracker presence,
    ArenaMatchRuntime runtime, ArenaSettlementService settlement, IArenaUpdatePublisher publisher,
    IOptions<ArenaOptions> options, ILogger<ArenaInvitationService> logger)
{
    public async Task<IReadOnlyList<ArenaInvitationView>> ListAsync(Guid accountId, CancellationToken ct)
    {
        Guid characterId = await CharacterIdAsync(accountId, ct);
        DateTimeOffset now = time.GetUtcNow();
        return await Views(characterId).Where(x => x.ExpiresAtUtc > now && x.Status == ArenaInvitationStatus.Pending)
            .OrderByDescending(x => x.ExpiresAtUtc).Take(20).ToArrayAsync(ct);
    }

    public async Task<ArenaInvitationResult> InviteAsync(Guid accountId, Guid requestId, string? targetName,
        CancellationToken ct)
    {
        if (!options.Value.Enabled) return Failure("arena_disabled");
        string name = targetName?.Trim().ToUpperInvariant() ?? "";
        if (requestId == Guid.Empty || name.Length is < 1 or > 32) return Failure("arena_invite_invalid");
        using IDisposable lease = await guard.AcquireManyAsync([accountId], ct);
        Guid replayCharacterId = await CharacterIdAsync(accountId, ct);
        ArenaInvitationView? previous = await Views(replayCharacterId).SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (previous is not null)
            return previous.InviterCharacterId == replayCharacterId && string.Equals(previous.TargetName, name, StringComparison.OrdinalIgnoreCase)
                ? new ArenaInvitationResult(true, null, Normalize(previous)) : Failure("arena_invite_invalid");
        ArenaEligibilityResult admission = await eligibility.CheckAsync(accountId, ct);
        if (!admission.Eligible) return Failure(admission.ErrorCode ?? "arena_unavailable");
        Guid inviterId = admission.Entrant!.Fighter.CharacterId;
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await db.Characters.FromSqlInterpolated(
                $"SELECT * FROM game.characters WHERE \"Id\" = {inviterId} FOR UPDATE").SingleAsync(ct);
            ArenaInvitation? replay = await db.ArenaInvitations.SingleOrDefaultAsync(x => x.Id == requestId, ct);
            if (replay is not null)
            {
                if (replay.InviterCharacterId != inviterId) return Failure("arena_invite_invalid");
                ArenaInvitationView? view = await Views(inviterId).SingleOrDefaultAsync(x => x.Id == requestId, ct);
                if (!string.Equals(view?.TargetName, name, StringComparison.OrdinalIgnoreCase)) return Failure("arena_invite_invalid");
                return new ArenaInvitationResult(true, null, Normalize(view!));
            }
            Character? target = await db.Characters.AsNoTracking().SingleOrDefaultAsync(x => x.NormalizedName == name, ct);
            if (target is null) return Failure("arena_invite_player_not_found");
            if (target.Id == inviterId) return Failure("arena_invite_self");
            DateTimeOffset now = time.GetUtcNow();
            // One pending outgoing challenge and a cooldown bound bot notification spam.
            if (await db.ArenaInvitations.AnyAsync(x => x.InviterCharacterId == inviterId
                && (x.CreatedAtUtc > now.AddSeconds(-30)
                    || (x.Status == ArenaInvitationStatus.Pending && x.ExpiresAtUtc > now)), ct))
                return Failure("arena_invite_pending");
            if (await db.ArenaQueueEntries.AnyAsync(x => x.CharacterId == inviterId, ct))
                return Failure("arena_invite_queued");
            var invite = new ArenaInvitation(requestId, inviterId, target.Id, now);
            db.ArenaInvitations.Add(invite);
            await db.SaveChangesAsync(ct);
            ArenaInvitationView committedView = await Views(inviterId).SingleAsync(x => x.Id == requestId, ct);
            await transaction.CommitAsync(CancellationToken.None);
            return new ArenaInvitationResult(true, null, committedView, true);
        });
    }

    public async Task<ArenaInvitationResult> RespondAsync(Guid accountId, Guid inviteId, string? action,
        CancellationToken ct)
    {
        if (!options.Value.Enabled) return Failure("arena_disabled");
        if (action is not ("accept" or "decline" or "cancel")) return Failure("arena_invite_invalid");
        ArenaInvitation? found = await db.ArenaInvitations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == inviteId, ct);
        if (found is null) return Failure("arena_invite_not_found");
        Guid characterId = await CharacterIdAsync(accountId, ct);
        if ((action == "cancel" ? found.InviterCharacterId : found.TargetCharacterId) != characterId)
            return Failure("arena_invite_not_found");
        Guid[] characterIds = [found.InviterCharacterId, found.TargetCharacterId];
        Dictionary<Guid, Guid> accounts = await db.Characters.AsNoTracking().Where(x => characterIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.AccountId, ct);
        if (accounts.Count != 2) return Failure("arena_invite_not_found");
        using IDisposable lease = await guard.AcquireManyAsync(accounts.Values, ct);

        // Replay must not re-register an old match, even after finalization or restart.
        ArenaInvitationResult? replay = await ReplayAsync(inviteId, characterId, action, ct);
        if (replay is not null) return replay;
        ArenaEligibilityResult? first = null, second = null;
        if (action == "accept")
        {
            if (!accounts.Values.All(presence.IsConnected)) return Failure("arena_invite_player_offline");
            first = await eligibility.CheckAsync(accounts[found.InviterCharacterId], ct);
            second = await eligibility.CheckAsync(accounts[found.TargetCharacterId], ct);
            if (!first.Eligible || !second.Eligible)
                return Failure(first.ErrorCode ?? second.ErrorCode ?? "arena_unavailable");
        }

        Guid candidateMatchId = Guid.NewGuid();
        ArenaInvitationResult result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await db.Characters.FromSqlInterpolated(
                $"SELECT * FROM game.characters WHERE \"Id\" = ANY({characterIds}) ORDER BY \"Id\" FOR UPDATE").ToArrayAsync(ct);
            ArenaInvitation invite = await db.ArenaInvitations.FromSqlInterpolated(
                $"SELECT * FROM game.arena_invitations WHERE \"Id\" = {inviteId} FOR UPDATE").SingleAsync(ct);
            ArenaInvitationResult? repeated = await ReplayAsync(inviteId, characterId, action, ct);
            if (repeated is not null)
                return repeated.Invitation?.MatchId == candidateMatchId
                    ? repeated with { Created = true } : repeated;
            if (invite.StatusAt(time.GetUtcNow()) != ArenaInvitationStatus.Pending) return Failure("arena_invite_expired");
            if (action == "accept")
            {
                if (await db.ArenaQueueEntries.AnyAsync(x => characterIds.Contains(x.CharacterId), ct))
                    return Failure("arena_invite_queued");
                if (await db.ArenaMatches.AnyAsync(x => x.Outcome == ArenaMatchOutcome.Active
                    && (characterIds.Contains(x.CharacterAId) || characterIds.Contains(x.CharacterBId)), ct))
                    return Failure("arena_match_active");
                var match = new ArenaMatch(candidateMatchId, invite.InviterCharacterId, invite.TargetCharacterId,
                    time.GetUtcNow(), mode: ArenaQueueMode.Unranked);
                db.ArenaMatches.Add(match);
                invite.Accept(match.Id, time.GetUtcNow());
            }
            else invite.Close(action == "cancel" ? ArenaInvitationStatus.Cancelled : ArenaInvitationStatus.Declined,
                time.GetUtcNow());
            await db.SaveChangesAsync(ct);
            ArenaInvitationView committedView = await Views(characterId).SingleAsync(x => x.Id == inviteId, ct);
            await transaction.CommitAsync(CancellationToken.None);
            return new ArenaInvitationResult(true, null, committedView, Created: true);
        });
        if (result.Succeeded && result.Created && action == "accept" && result.Invitation?.MatchId is { } matchId)
        {
            try
            {
                // Never tie a committed start to the browser's request cancellation.
                runtime.Register(matchId, first!.Entrant!, second!.Entrant!);
            }
            catch (Exception exception)
            {
                LogStartFailed(logger, matchId, exception);
                await settlement.CompleteAndSettleAsync(matchId, ArenaMatchOutcome.Cancelled, false, CancellationToken.None);
                return Failure("arena_start_failed");
            }
            foreach (Guid participant in accounts.Values)
            {
                try { await publisher.PublishMatchFoundAsync(participant, matchId, CancellationToken.None); }
                catch (Exception exception) { LogPublishFailed(logger, matchId, exception); }
            }
        }
        return result;
    }

    private async Task<ArenaInvitationResult?> ReplayAsync(Guid id, Guid characterId, string action, CancellationToken ct)
    {
        ArenaInvitationView view = Normalize(await Views(characterId).SingleAsync(x => x.Id == id, ct));
        if (view.Status == ArenaInvitationStatus.Pending) return null;
        bool same = action switch
        {
            "accept" => view.Status == ArenaInvitationStatus.Accepted,
            "decline" => view.Status == ArenaInvitationStatus.Declined,
            "cancel" => view.Status == ArenaInvitationStatus.Cancelled,
            _ => false
        };
        return same ? new ArenaInvitationResult(true, null, view) : Failure("arena_invite_expired");
    }

    private IQueryable<ArenaInvitationView> Views(Guid characterId) =>
        from invite in db.ArenaInvitations.AsNoTracking()
        join inviter in db.Characters on invite.InviterCharacterId equals inviter.Id
        join target in db.Characters on invite.TargetCharacterId equals target.Id
        where invite.InviterCharacterId == characterId || invite.TargetCharacterId == characterId
        select new ArenaInvitationView
        {
            Id = invite.Id, InviterCharacterId = inviter.Id, InviterName = inviter.Name,
            TargetCharacterId = target.Id, TargetName = target.Name, Status = invite.Status,
            ExpiresAtUtc = invite.ExpiresAtUtc, MatchId = invite.MatchId,
            Incoming = invite.TargetCharacterId == characterId
        };

    private ArenaInvitationView Normalize(ArenaInvitationView view) =>
        view.Status == ArenaInvitationStatus.Pending && view.ExpiresAtUtc <= time.GetUtcNow()
            ? view with { Status = ArenaInvitationStatus.Expired } : view;
    private Task<Guid> CharacterIdAsync(Guid accountId, CancellationToken ct) => db.Characters.AsNoTracking()
        .Where(x => x.AccountId == accountId).Select(x => x.Id).SingleOrDefaultAsync(ct);
    private static ArenaInvitationResult Failure(string error) => new(false, error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Invited arena match {MatchId} failed to start after commit.")]
    private static partial void LogStartFailed(ILogger logger, Guid matchId, Exception exception);
    [LoggerMessage(Level = LogLevel.Warning, Message = "Invited arena match {MatchId} notification failed.")]
    private static partial void LogPublishFailed(ILogger logger, Guid matchId, Exception exception);
}
