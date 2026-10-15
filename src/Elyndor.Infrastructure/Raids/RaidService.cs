using Elyndor.Core.Characters;
using Elyndor.Core.Raids;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Raids;

public static class RaidErrorCodes
{
    public const string InvalidRequest = "raid_request_invalid";
    public const string CharacterNotFound = "raid_character_not_found";
    public const string AlreadyInGroupContext = "raid_already_in_group_context";
    public const string IdempotencyConflict = "raid_idempotency_conflict";
    public const string RaidNotFound = "raid_not_found";
    public const string NotLeader = "raid_not_leader";
    public const string NotLeaderOrAssistant = "raid_not_leader_or_assistant";
    public const string InviteNotFound = "raid_invite_not_found";
    public const string InviteExpired = "raid_invite_expired";
    public const string RaidFull = "raid_full";
    public const string ReadyCheckNotFound = "raid_ready_check_not_found";
    public const string ReadyCheckExpired = "raid_ready_check_expired";
    public const string InvalidState = "raid_invalid_state";
}

public sealed record RaidMemberView(
    Guid CharacterId,
    string Name,
    int Level,
    string ClassId,
    RaidMemberRole Role,
    DateTimeOffset JoinedAtUtc,
    RaidReadyState ReadyState = RaidReadyState.NoResponse);

public sealed record RaidReadyCheckView(
    Guid Id,
    Guid StartedByCharacterId,
    RaidReadyCheckState State,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record RaidSnapshot(
    Guid RaidId,
    Guid LeaderCharacterId,
    int MaximumMembers,
    long Version,
    IReadOnlyList<RaidMemberView> Members,
    RaidState State = RaidState.Active,
    RaidReadyCheckView? ReadyCheck = null);

public sealed record RaidInviteView(
    Guid Id,
    Guid RaidId,
    Guid InviterCharacterId,
    Guid TargetCharacterId,
    RaidInviteStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record RaidOperationResult(
    bool IsSuccess,
    string? ErrorCode,
    RaidSnapshot? Snapshot = null,
    RaidInviteView? Invite = null)
{
    public static RaidOperationResult Failure(string code) => new(false, code);
}

public sealed class RaidService(
    GameDbContext dbContext,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ReadyCheckLifetime = TimeSpan.FromSeconds(60);

    public Task<RaidOperationResult> CreateAsync(
        Guid accountId,
        Guid requestId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => CreateCoreAsync(accountId, requestId, cancellationToken));

    private async Task<RaidOperationResult> CreateCoreAsync(
        Guid accountId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty || requestId == Guid.Empty)
            return RaidOperationResult.Failure(RaidErrorCodes.InvalidRequest);

        dbContext.ChangeTracker.Clear();
        Character? character = await GetCharacterAsync(accountId, cancellationToken);
        if (character is null)
            return RaidOperationResult.Failure(RaidErrorCodes.CharacterNotFound);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquireCharacterLockAsync(character.Id, cancellationToken);
        dbContext.ChangeTracker.Clear();
        character = await GetCharacterAsync(accountId, cancellationToken);
        if (character is null)
            return RaidOperationResult.Failure(RaidErrorCodes.CharacterNotFound);

        RaidGroup? replay = await dbContext.RaidGroups
            .SingleOrDefaultAsync(raid => raid.CreationRequestId == requestId, cancellationToken);
        if (replay is not null)
        {
            if (replay.LeaderCharacterId != character.Id)
            {
                await transaction.CommitAsync(cancellationToken);
                return RaidOperationResult.Failure(RaidErrorCodes.IdempotencyConflict);
            }

            await transaction.CommitAsync(cancellationToken);
            return new RaidOperationResult(
                true,
                null,
                await BuildSnapshotAsync(replay.Id, cancellationToken));
        }

        if (await IsInGroupContextAsync(character.Id, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return RaidOperationResult.Failure(RaidErrorCodes.AlreadyInGroupContext);
        }

        RaidGroup raid = RaidGroup.Create(
            Guid.CreateVersion7(),
            requestId,
            character.Id,
            RaidGroup.DefaultMaximumMembers,
            timeProvider.GetUtcNow());
        dbContext.RaidGroups.Add(raid);
        dbContext.RaidMembers.AddRange(raid.Members);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RaidOperationResult(
            true,
            null,
            await BuildSnapshotAsync(raid.Id, cancellationToken));
    }

    public async Task<RaidSnapshot?> GetAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        Character? character = await GetCharacterAsync(accountId, cancellationToken);
        if (character is null)
            return null;

        RaidGroup? raid = await GetActiveRaidForCharacterAsync(character.Id, cancellationToken);
        return raid is null ? null : await BuildSnapshotAsync(raid.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<RaidInviteView>> GetInvitesAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        Character? character = await GetCharacterAsync(accountId, cancellationToken);
        if (character is null)
            return [];

        DateTimeOffset now = timeProvider.GetUtcNow();
        RaidInvite[] invites = await dbContext.RaidInvites
            .AsNoTracking()
            .Where(invite => invite.TargetCharacterId == character.Id
                && invite.Status == RaidInviteStatus.Pending
                && invite.ExpiresAtUtc > now)
            .OrderBy(invite => invite.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);
        return invites.Select(ToView).ToArray();
    }

    public Task<RaidOperationResult> InviteAsync(
        Guid accountId,
        Guid inviteId,
        Guid targetCharacterId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => InviteCoreAsync(accountId, inviteId, targetCharacterId, cancellationToken));

    private async Task<RaidOperationResult> InviteCoreAsync(
        Guid accountId,
        Guid inviteId,
        Guid targetCharacterId,
        CancellationToken cancellationToken)
    {
        if (inviteId == Guid.Empty || targetCharacterId == Guid.Empty)
            return RaidOperationResult.Failure(RaidErrorCodes.InvalidRequest);

        dbContext.ChangeTracker.Clear();
        Character? inviter = await GetCharacterAsync(accountId, cancellationToken);
        Character? target = await dbContext.Characters
            .SingleOrDefaultAsync(character => character.Id == targetCharacterId, cancellationToken);
        if (inviter is null || target is null)
            return RaidOperationResult.Failure(RaidErrorCodes.CharacterNotFound);
        if (inviter.Id == target.Id)
            return RaidOperationResult.Failure(RaidErrorCodes.InvalidRequest);

        RaidGroup? raid = await GetActiveRaidForCharacterAsync(inviter.Id, cancellationToken);
        if (raid is null)
            return RaidOperationResult.Failure(RaidErrorCodes.RaidNotFound);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquireRaidLockAsync(raid.Id, cancellationToken);
        dbContext.ChangeTracker.Clear();
        inviter = await GetCharacterAsync(accountId, cancellationToken);
        target = await dbContext.Characters
            .SingleOrDefaultAsync(character => character.Id == targetCharacterId, cancellationToken);
        raid = inviter is null
            ? null
            : await GetActiveRaidForCharacterAsync(inviter.Id, cancellationToken);
        if (inviter is null || target is null)
            return RaidOperationResult.Failure(RaidErrorCodes.CharacterNotFound);
        if (raid is null)
            return RaidOperationResult.Failure(RaidErrorCodes.RaidNotFound);

        RaidInvite? replay = await dbContext.RaidInvites
            .SingleOrDefaultAsync(invite => invite.Id == inviteId, cancellationToken);
        if (replay is not null)
        {
            bool matches = replay.RaidId == raid.Id
                && replay.InviterCharacterId == inviter.Id
                && replay.TargetCharacterId == target.Id;
            await transaction.CommitAsync(cancellationToken);
            return matches
                ? new RaidOperationResult(true, null, Invite: ToView(replay))
                : RaidOperationResult.Failure(RaidErrorCodes.IdempotencyConflict);
        }

        RaidMember? inviterMember = raid.Members.SingleOrDefault(member => member.CharacterId == inviter.Id);
        if (inviterMember is null || inviterMember.Role is RaidMemberRole.Member)
            return RaidOperationResult.Failure(RaidErrorCodes.NotLeaderOrAssistant);
        if (raid.Members.Count >= raid.MaximumMembers)
            return RaidOperationResult.Failure(RaidErrorCodes.RaidFull);
        if (await IsInGroupContextAsync(target.Id, cancellationToken))
            return RaidOperationResult.Failure(RaidErrorCodes.AlreadyInGroupContext);

        DateTimeOffset now = timeProvider.GetUtcNow();
        RaidInvite invite = RaidInvite.Create(
            inviteId,
            raid.Id,
            inviter.Id,
            target.Id,
            now,
            now.Add(InviteLifetime));
        dbContext.RaidInvites.Add(invite);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RaidOperationResult(true, null, Invite: ToView(invite));
    }

    public Task<RaidOperationResult> AcceptInviteAsync(
        Guid accountId,
        Guid inviteId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => AcceptInviteCoreAsync(accountId, inviteId, cancellationToken));

    private async Task<RaidOperationResult> AcceptInviteCoreAsync(
        Guid accountId,
        Guid inviteId,
        CancellationToken cancellationToken)
    {
        if (inviteId == Guid.Empty)
            return RaidOperationResult.Failure(RaidErrorCodes.InvalidRequest);

        dbContext.ChangeTracker.Clear();
        Character? target = await GetCharacterAsync(accountId, cancellationToken);
        if (target is null)
            return RaidOperationResult.Failure(RaidErrorCodes.CharacterNotFound);

        RaidInvite? probe = await dbContext.RaidInvites
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == inviteId, cancellationToken);
        if (probe is null || probe.TargetCharacterId != target.Id)
            return RaidOperationResult.Failure(RaidErrorCodes.InviteNotFound);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquireCharacterLockAsync(target.Id, cancellationToken);
        await AcquireRaidLockAsync(probe.RaidId, cancellationToken);
        dbContext.ChangeTracker.Clear();
        target = await GetCharacterAsync(accountId, cancellationToken);
        RaidInvite? invite = await dbContext.RaidInvites
            .SingleOrDefaultAsync(candidate => candidate.Id == inviteId, cancellationToken);
        RaidGroup? raid = invite is null
            ? null
            : await GetRaidAsync(invite.RaidId, cancellationToken);
        if (target is null)
            return RaidOperationResult.Failure(RaidErrorCodes.CharacterNotFound);
        if (invite is null || invite.TargetCharacterId != target.Id || raid is null)
            return RaidOperationResult.Failure(RaidErrorCodes.InviteNotFound);

        if (invite.Status == RaidInviteStatus.Accepted)
        {
            if (raid.Members.Any(member => member.CharacterId == target.Id))
            {
                await transaction.CommitAsync(cancellationToken);
                return new RaidOperationResult(
                    true,
                    null,
                    await BuildSnapshotAsync(raid.Id, cancellationToken));
            }
            return RaidOperationResult.Failure(RaidErrorCodes.InvalidState);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        invite.Expire(now);
        if (invite.Status == RaidInviteStatus.Expired)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return RaidOperationResult.Failure(RaidErrorCodes.InviteExpired);
        }
        if (invite.Status != RaidInviteStatus.Pending || raid.State != RaidState.Active)
            return RaidOperationResult.Failure(RaidErrorCodes.InvalidState);
        if (await IsInGroupContextAsync(target.Id, cancellationToken))
            return RaidOperationResult.Failure(RaidErrorCodes.AlreadyInGroupContext);
        if (raid.Members.Count >= raid.MaximumMembers)
            return RaidOperationResult.Failure(RaidErrorCodes.RaidFull);

        invite.Accept(target.Id, now);
        raid.AddMember(target.Id, now);
        dbContext.RaidMembers.Add(raid.Members.Single(member => member.CharacterId == target.Id));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RaidOperationResult(
            true,
            null,
            await BuildSnapshotAsync(raid.Id, cancellationToken));
    }

    public Task<RaidOperationResult> DeclineInviteAsync(
        Guid accountId,
        Guid inviteId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => DeclineInviteCoreAsync(accountId, inviteId, cancellationToken));

    private async Task<RaidOperationResult> DeclineInviteCoreAsync(
        Guid accountId,
        Guid inviteId,
        CancellationToken cancellationToken)
    {
        if (inviteId == Guid.Empty)
            return RaidOperationResult.Failure(RaidErrorCodes.InvalidRequest);

        dbContext.ChangeTracker.Clear();
        Character? target = await GetCharacterAsync(accountId, cancellationToken);
        if (target is null)
            return RaidOperationResult.Failure(RaidErrorCodes.CharacterNotFound);
        RaidInvite? probe = await dbContext.RaidInvites
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == inviteId, cancellationToken);
        if (probe is null || probe.TargetCharacterId != target.Id)
            return RaidOperationResult.Failure(RaidErrorCodes.InviteNotFound);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquireRaidLockAsync(probe.RaidId, cancellationToken);
        dbContext.ChangeTracker.Clear();
        target = await GetCharacterAsync(accountId, cancellationToken);
        RaidInvite? invite = await dbContext.RaidInvites
            .SingleOrDefaultAsync(candidate => candidate.Id == inviteId, cancellationToken);
        if (target is null)
            return RaidOperationResult.Failure(RaidErrorCodes.CharacterNotFound);
        if (invite is null || invite.TargetCharacterId != target.Id)
            return RaidOperationResult.Failure(RaidErrorCodes.InviteNotFound);
        if (invite.Status == RaidInviteStatus.Declined)
        {
            await transaction.CommitAsync(cancellationToken);
            return new RaidOperationResult(true, null, Invite: ToView(invite));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        invite.Expire(now);
        if (invite.Status == RaidInviteStatus.Expired)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return RaidOperationResult.Failure(RaidErrorCodes.InviteExpired);
        }
        if (invite.Status != RaidInviteStatus.Pending)
            return RaidOperationResult.Failure(RaidErrorCodes.InvalidState);

        invite.Decline(target.Id, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RaidOperationResult(true, null, Invite: ToView(invite));
    }

    public Task<RaidOperationResult> LeaveAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        MutateMembershipAsync(accountId, RaidMutation.Leave, Guid.Empty, cancellationToken);

    public Task<RaidOperationResult> KickAsync(
        Guid accountId,
        Guid targetCharacterId,
        CancellationToken cancellationToken) =>
        targetCharacterId == Guid.Empty
            ? Task.FromResult(RaidOperationResult.Failure(RaidErrorCodes.InvalidRequest))
            : MutateMembershipAsync(accountId, RaidMutation.Kick, targetCharacterId, cancellationToken);

    public Task<RaidOperationResult> TransferLeadershipAsync(
        Guid accountId,
        Guid targetCharacterId,
        CancellationToken cancellationToken) =>
        targetCharacterId == Guid.Empty
            ? Task.FromResult(RaidOperationResult.Failure(RaidErrorCodes.InvalidRequest))
            : MutateMembershipAsync(accountId, RaidMutation.Transfer, targetCharacterId, cancellationToken);

    public Task<RaidOperationResult> DisbandAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        MutateMembershipAsync(accountId, RaidMutation.Disband, Guid.Empty, cancellationToken);

    private Task<RaidOperationResult> MutateMembershipAsync(
        Guid accountId,
        RaidMutation mutation,
        Guid targetCharacterId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => MutateMembershipCoreAsync(
                accountId,
                mutation,
                targetCharacterId,
                cancellationToken));

    private async Task<RaidOperationResult> MutateMembershipCoreAsync(
        Guid accountId,
        RaidMutation mutation,
        Guid targetCharacterId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        Character? actor = await GetCharacterAsync(accountId, cancellationToken);
        if (actor is null)
            return RaidOperationResult.Failure(RaidErrorCodes.CharacterNotFound);
        RaidGroup? raid = await GetActiveRaidForCharacterAsync(actor.Id, cancellationToken);
        if (raid is null)
            return RaidOperationResult.Failure(RaidErrorCodes.RaidNotFound);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (mutation == RaidMutation.Leave)
            await AcquireCharacterLockAsync(actor.Id, cancellationToken);
        else if (mutation == RaidMutation.Kick)
            await AcquireCharacterLockAsync(targetCharacterId, cancellationToken);
        await AcquireRaidLockAsync(raid.Id, cancellationToken);
        dbContext.ChangeTracker.Clear();
        actor = await GetCharacterAsync(accountId, cancellationToken);
        raid = actor is null
            ? null
            : await GetActiveRaidForCharacterAsync(actor.Id, cancellationToken);
        if (actor is null)
            return RaidOperationResult.Failure(RaidErrorCodes.CharacterNotFound);
        if (raid is null)
            return RaidOperationResult.Failure(RaidErrorCodes.RaidNotFound);

        DateTimeOffset now = timeProvider.GetUtcNow();
        RaidMember[] membersBeforeMutation = raid.Members.ToArray();
        RaidMember? removedMember = null;
        try
        {
            switch (mutation)
            {
                case RaidMutation.Leave:
                    removedMember = raid.Leave(actor.Id, now);
                    break;
                case RaidMutation.Kick:
                    removedMember = raid.Kick(actor.Id, targetCharacterId, now);
                    break;
                case RaidMutation.Transfer:
                    raid.TransferLeadership(actor.Id, targetCharacterId);
                    break;
                case RaidMutation.Disband:
                    raid.Disband(actor.Id);
                    break;
            }
        }
        catch (UnauthorizedAccessException)
        {
            return RaidOperationResult.Failure(RaidErrorCodes.NotLeader);
        }
        catch (InvalidOperationException)
        {
            return RaidOperationResult.Failure(RaidErrorCodes.InvalidState);
        }

        if (removedMember is not null)
        {
            dbContext.RaidMembers.Remove(removedMember);
        }
        else if (mutation == RaidMutation.Disband)
        {
            dbContext.RaidMembers.RemoveRange(membersBeforeMutation);
            RaidReadyCheck[] openChecks = await dbContext.RaidReadyChecks
                .Where(check => check.RaidId == raid.Id && check.State == RaidReadyCheckState.Open)
                .ToArrayAsync(cancellationToken);
            foreach (RaidReadyCheck check in openChecks)
                check.Cancel(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RaidOperationResult(
            true,
            null,
            await BuildSnapshotAsync(raid.Id, cancellationToken));
    }

    public Task<RaidOperationResult> PromoteAssistantAsync(
        Guid accountId,
        Guid targetCharacterId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => PromoteAssistantCoreAsync(accountId, targetCharacterId, cancellationToken));

    private async Task<RaidOperationResult> PromoteAssistantCoreAsync(
        Guid accountId,
        Guid targetCharacterId,
        CancellationToken cancellationToken)
    {
        if (targetCharacterId == Guid.Empty)
            return RaidOperationResult.Failure(RaidErrorCodes.InvalidRequest);

        dbContext.ChangeTracker.Clear();
        Character? leader = await GetCharacterAsync(accountId, cancellationToken);
        if (leader is null)
            return RaidOperationResult.Failure(RaidErrorCodes.CharacterNotFound);
        RaidGroup? raid = await GetActiveRaidForCharacterAsync(leader.Id, cancellationToken);
        if (raid is null)
            return RaidOperationResult.Failure(RaidErrorCodes.RaidNotFound);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquireRaidLockAsync(raid.Id, cancellationToken);
        dbContext.ChangeTracker.Clear();
        leader = await GetCharacterAsync(accountId, cancellationToken);
        raid = leader is null
            ? null
            : await GetActiveRaidForCharacterAsync(leader.Id, cancellationToken);
        if (leader is null)
            return RaidOperationResult.Failure(RaidErrorCodes.CharacterNotFound);
        if (raid is null)
            return RaidOperationResult.Failure(RaidErrorCodes.RaidNotFound);

        try
        {
            raid.PromoteAssistant(leader.Id, targetCharacterId);
        }
        catch (UnauthorizedAccessException)
        {
            return RaidOperationResult.Failure(RaidErrorCodes.NotLeader);
        }
        catch (InvalidOperationException)
        {
            return RaidOperationResult.Failure(RaidErrorCodes.InvalidState);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RaidOperationResult(
            true,
            null,
            await BuildSnapshotAsync(raid.Id, cancellationToken));
    }

    public Task<RaidOperationResult> BeginReadyCheckAsync(
        Guid accountId,
        Guid readyCheckId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => BeginReadyCheckCoreAsync(accountId, readyCheckId, cancellationToken));

    private async Task<RaidOperationResult> BeginReadyCheckCoreAsync(
        Guid accountId,
        Guid readyCheckId,
        CancellationToken cancellationToken)
    {
        if (readyCheckId == Guid.Empty)
            return RaidOperationResult.Failure(RaidErrorCodes.InvalidRequest);

        dbContext.ChangeTracker.Clear();
        Character? actor = await GetCharacterAsync(accountId, cancellationToken);
        if (actor is null)
            return RaidOperationResult.Failure(RaidErrorCodes.CharacterNotFound);
        RaidGroup? raid = await GetActiveRaidForCharacterAsync(actor.Id, cancellationToken);
        if (raid is null)
            return RaidOperationResult.Failure(RaidErrorCodes.RaidNotFound);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquireRaidLockAsync(raid.Id, cancellationToken);
        dbContext.ChangeTracker.Clear();
        actor = await GetCharacterAsync(accountId, cancellationToken);
        raid = actor is null
            ? null
            : await GetActiveRaidForCharacterAsync(actor.Id, cancellationToken);
        if (actor is null)
            return RaidOperationResult.Failure(RaidErrorCodes.CharacterNotFound);
        if (raid is null)
            return RaidOperationResult.Failure(RaidErrorCodes.RaidNotFound);

        RaidReadyCheck? replay = await dbContext.RaidReadyChecks
            .SingleOrDefaultAsync(check => check.Id == readyCheckId, cancellationToken);
        if (replay is not null)
        {
            bool matches = replay.RaidId == raid.Id && replay.StartedByCharacterId == actor.Id;
            await transaction.CommitAsync(cancellationToken);
            return matches
                ? new RaidOperationResult(
                    true,
                    null,
                    await BuildSnapshotAsync(raid.Id, cancellationToken))
                : RaidOperationResult.Failure(RaidErrorCodes.IdempotencyConflict);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        RaidReadyCheck[] openChecks = await dbContext.RaidReadyChecks
            .Where(check => check.RaidId == raid.Id && check.State == RaidReadyCheckState.Open)
            .ToArrayAsync(cancellationToken);
        if (openChecks.Any(check => check.ExpiresAtUtc > now))
            return RaidOperationResult.Failure(RaidErrorCodes.InvalidState);
        foreach (RaidReadyCheck check in openChecks)
            check.Expire(now);

        try
        {
            raid.BeginReadyCheck(actor.Id);
        }
        catch (UnauthorizedAccessException)
        {
            return RaidOperationResult.Failure(RaidErrorCodes.NotLeaderOrAssistant);
        }
        catch (InvalidOperationException)
        {
            return RaidOperationResult.Failure(RaidErrorCodes.InvalidState);
        }

        RaidReadyCheck readyCheck = RaidReadyCheck.Create(
            readyCheckId,
            raid.Id,
            actor.Id,
            now,
            now.Add(ReadyCheckLifetime));
        dbContext.RaidReadyChecks.Add(readyCheck);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RaidOperationResult(
            true,
            null,
            await BuildSnapshotAsync(raid.Id, cancellationToken));
    }

    public Task<RaidOperationResult> SetReadyStateAsync(
        Guid accountId,
        Guid readyCheckId,
        RaidReadyState readyState,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => SetReadyStateCoreAsync(
                accountId,
                readyCheckId,
                readyState,
                cancellationToken));

    private async Task<RaidOperationResult> SetReadyStateCoreAsync(
        Guid accountId,
        Guid readyCheckId,
        RaidReadyState readyState,
        CancellationToken cancellationToken)
    {
        if (readyCheckId == Guid.Empty || readyState == RaidReadyState.NoResponse)
            return RaidOperationResult.Failure(RaidErrorCodes.InvalidRequest);

        dbContext.ChangeTracker.Clear();
        Character? actor = await GetCharacterAsync(accountId, cancellationToken);
        if (actor is null)
            return RaidOperationResult.Failure(RaidErrorCodes.CharacterNotFound);
        RaidReadyCheck? probe = await dbContext.RaidReadyChecks
            .AsNoTracking()
            .SingleOrDefaultAsync(check => check.Id == readyCheckId, cancellationToken);
        if (probe is null)
            return RaidOperationResult.Failure(RaidErrorCodes.ReadyCheckNotFound);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquireRaidLockAsync(probe.RaidId, cancellationToken);
        dbContext.ChangeTracker.Clear();
        actor = await GetCharacterAsync(accountId, cancellationToken);
        RaidGroup? raid = await GetRaidAsync(probe.RaidId, cancellationToken);
        RaidReadyCheck? check = await dbContext.RaidReadyChecks
            .SingleOrDefaultAsync(candidate => candidate.Id == readyCheckId, cancellationToken);
        if (actor is null)
            return RaidOperationResult.Failure(RaidErrorCodes.CharacterNotFound);
        if (raid is null || raid.State != RaidState.Active)
            return RaidOperationResult.Failure(RaidErrorCodes.RaidNotFound);
        if (check is null)
            return RaidOperationResult.Failure(RaidErrorCodes.ReadyCheckNotFound);

        RaidMember? member = raid.Members.SingleOrDefault(candidate => candidate.CharacterId == actor.Id);
        if (member is null || member.JoinedAtUtc > check.StartedAtUtc)
            return RaidOperationResult.Failure(RaidErrorCodes.InvalidState);

        if (check.State == RaidReadyCheckState.Completed && member.ReadyState == readyState)
        {
            await transaction.CommitAsync(cancellationToken);
            return new RaidOperationResult(
                true,
                null,
                await BuildSnapshotAsync(raid.Id, cancellationToken));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        check.Expire(now);
        if (check.State == RaidReadyCheckState.Expired)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return RaidOperationResult.Failure(RaidErrorCodes.ReadyCheckExpired);
        }
        if (check.State != RaidReadyCheckState.Open)
            return RaidOperationResult.Failure(RaidErrorCodes.InvalidState);

        if (member.ReadyState != readyState)
            raid.SetReadyState(actor.Id, readyState);

        RaidMember[] capturedMembers = raid.Members
            .Where(candidate => candidate.JoinedAtUtc <= check.StartedAtUtc)
            .ToArray();
        if (capturedMembers.Length > 0
            && capturedMembers.All(candidate => candidate.ReadyState != RaidReadyState.NoResponse))
        {
            check.Complete(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RaidOperationResult(
            true,
            null,
            await BuildSnapshotAsync(raid.Id, cancellationToken));
    }

    private async Task<Character?> GetCharacterAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        await dbContext.Characters.SingleOrDefaultAsync(
            character => character.AccountId == accountId,
            cancellationToken);

    private async Task<bool> IsInGroupContextAsync(
        Guid characterId,
        CancellationToken cancellationToken) =>
        await dbContext.PartyMembers.AnyAsync(member => member.CharacterId == characterId, cancellationToken)
        || await dbContext.RaidMembers.AnyAsync(member => member.CharacterId == characterId, cancellationToken);

    private async Task<RaidGroup?> GetActiveRaidForCharacterAsync(
        Guid characterId,
        CancellationToken cancellationToken)
    {
        Guid? raidId = await dbContext.RaidMembers
            .Where(member => member.CharacterId == characterId)
            .Select(member => (Guid?)member.RaidId)
            .SingleOrDefaultAsync(cancellationToken);
        return raidId.HasValue ? await GetRaidAsync(raidId.Value, cancellationToken) : null;
    }

    private async Task<RaidGroup?> GetRaidAsync(
        Guid raidId,
        CancellationToken cancellationToken)
    {
        RaidGroup? raid = await dbContext.RaidGroups
            .SingleOrDefaultAsync(candidate => candidate.Id == raidId, cancellationToken);
        if (raid is not null)
        {
            await dbContext.Entry(raid)
                .Collection(candidate => candidate.Members)
                .LoadAsync(cancellationToken);
        }
        return raid;
    }

    private async Task<RaidSnapshot> BuildSnapshotAsync(
        Guid raidId,
        CancellationToken cancellationToken)
    {
        RaidGroup raid = await dbContext.RaidGroups
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == raidId, cancellationToken);
        RaidMember[] members = await dbContext.RaidMembers
            .AsNoTracking()
            .Where(member => member.RaidId == raidId)
            .OrderBy(member => member.JoinedAtUtc)
            .ThenBy(member => member.CharacterId)
            .ToArrayAsync(cancellationToken);
        Guid[] characterIds = members.Select(member => member.CharacterId).ToArray();
        Dictionary<Guid, Character> characters = await dbContext.Characters
            .AsNoTracking()
            .Where(character => characterIds.Contains(character.Id))
            .ToDictionaryAsync(character => character.Id, cancellationToken);
        RaidReadyCheck? latestReadyCheck = await dbContext.RaidReadyChecks
            .AsNoTracking()
            .Where(check => check.RaidId == raidId)
            .OrderByDescending(check => check.StartedAtUtc)
            .ThenByDescending(check => check.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return new RaidSnapshot(
            raid.Id,
            raid.LeaderCharacterId,
            raid.MaximumMembers,
            raid.Version,
            members.Select(member =>
            {
                Character character = characters[member.CharacterId];
                return new RaidMemberView(
                    character.Id,
                    character.Name,
                    character.Level,
                    character.ClassId,
                    member.Role,
                    member.JoinedAtUtc,
                    member.ReadyState);
            }).ToArray(),
            raid.State,
            latestReadyCheck is null ? null : ToView(latestReadyCheck, timeProvider.GetUtcNow()));
    }

    private async Task AcquireCharacterLockAsync(
        Guid characterId,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
            return;

        string lockKey = $"group-membership:{characterId:N}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))",
            cancellationToken);
    }

    private async Task AcquireRaidLockAsync(
        Guid raidId,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
            return;

        string lockKey = $"raid-membership:{raidId:N}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))",
            cancellationToken);
    }

    private static RaidInviteView ToView(RaidInvite invite) =>
        new(
            invite.Id,
            invite.RaidId,
            invite.InviterCharacterId,
            invite.TargetCharacterId,
            invite.Status,
            invite.CreatedAtUtc,
            invite.ExpiresAtUtc);

    private static RaidReadyCheckView ToView(
        RaidReadyCheck check,
        DateTimeOffset now)
    {
        RaidReadyCheckState state = check.State == RaidReadyCheckState.Open && now >= check.ExpiresAtUtc
            ? RaidReadyCheckState.Expired
            : check.State;
        return new RaidReadyCheckView(
            check.Id,
            check.StartedByCharacterId,
            state,
            check.StartedAtUtc,
            check.ExpiresAtUtc,
            check.CompletedAtUtc);
    }

    private enum RaidMutation
    {
        Leave,
        Kick,
        Transfer,
        Disband
    }
}
