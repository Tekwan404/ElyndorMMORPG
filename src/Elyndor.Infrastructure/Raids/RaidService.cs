using Elyndor.Core.Characters;
using Elyndor.Core.Parties;
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
    public const string NotLeaderOrAssistant = "raid_not_leader_or_assistant";
    public const string InviteNotFound = "raid_invite_not_found";
    public const string InviteExpired = "raid_invite_expired";
    public const string RaidFull = "raid_full";
    public const string InvalidState = "raid_invalid_state";
}

public sealed record RaidMemberView(
    Guid CharacterId,
    string Name,
    int Level,
    string ClassId,
    RaidMemberRole Role,
    DateTimeOffset JoinedAtUtc);

public sealed record RaidSnapshot(
    Guid RaidId,
    Guid LeaderCharacterId,
    int MaximumMembers,
    long Version,
    IReadOnlyList<RaidMemberView> Members);

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

        Character? character = await GetCharacterAsync(accountId, cancellationToken);
        if (character is null)
            return RaidOperationResult.Failure(RaidErrorCodes.CharacterNotFound);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquireCharacterLockAsync(character.Id, cancellationToken);

        RaidGroup? replay = await dbContext.RaidGroups
            .SingleOrDefaultAsync(raid => raid.CreationRequestId == requestId, cancellationToken);
        if (replay is not null)
        {
            if (replay.LeaderCharacterId != character.Id)
            {
                await transaction.CommitAsync(cancellationToken);
                return RaidOperationResult.Failure(RaidErrorCodes.IdempotencyConflict);
            }

            RaidSnapshot snapshot = await BuildSnapshotAsync(replay.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new RaidOperationResult(true, null, snapshot);
        }

        bool inParty = await dbContext.PartyMembers
            .AnyAsync(member => member.CharacterId == character.Id, cancellationToken);
        bool inRaid = await dbContext.RaidMembers
            .AnyAsync(member => member.CharacterId == character.Id, cancellationToken);
        if (inParty || inRaid)
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
        return new RaidOperationResult(true, null, await BuildSnapshotAsync(raid.Id, cancellationToken));
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

        Character? inviter = await GetCharacterAsync(accountId, cancellationToken);
        Character? target = await dbContext.Characters
            .SingleOrDefaultAsync(character => character.Id == targetCharacterId, cancellationToken);
        if (inviter is null || target is null)
            return RaidOperationResult.Failure(RaidErrorCodes.CharacterNotFound);
        if (inviter.Id == target.Id)
            return RaidOperationResult.Failure(RaidErrorCodes.InvalidRequest);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquireCharacterLockAsync(inviter.Id, cancellationToken);
        await AcquireCharacterLockAsync(target.Id, cancellationToken);

        RaidInvite? replay = await dbContext.RaidInvites
            .SingleOrDefaultAsync(invite => invite.Id == inviteId, cancellationToken);
        if (replay is not null)
        {
            if (replay.InviterCharacterId != inviter.Id || replay.TargetCharacterId != target.Id)
            {
                await transaction.CommitAsync(cancellationToken);
                return RaidOperationResult.Failure(RaidErrorCodes.IdempotencyConflict);
            }

            await transaction.CommitAsync(cancellationToken);
            return new RaidOperationResult(true, null, null, ToView(replay));
        }

        RaidGroup? raid = await GetActiveRaidForCharacterAsync(inviter.Id, cancellationToken);
        if (raid is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return RaidOperationResult.Failure(RaidErrorCodes.RaidNotFound);
        }
        await AcquireRaidLockAsync(raid.Id, cancellationToken);
        raid = await GetActiveRaidForCharacterAsync(inviter.Id, cancellationToken);
        if (raid is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return RaidOperationResult.Failure(RaidErrorCodes.RaidNotFound);
        }

        RaidMember? inviterMember = raid.Members.SingleOrDefault(member => member.CharacterId == inviter.Id);
        if (inviterMember is null || inviterMember.Role is RaidMemberRole.Member)
        {
            await transaction.CommitAsync(cancellationToken);
            return RaidOperationResult.Failure(RaidErrorCodes.NotLeaderOrAssistant);
        }
        if (raid.Members.Count >= raid.MaximumMembers)
        {
            await transaction.CommitAsync(cancellationToken);
            return RaidOperationResult.Failure(RaidErrorCodes.RaidFull);
        }
        if (await IsInGroupContextAsync(target.Id, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return RaidOperationResult.Failure(RaidErrorCodes.AlreadyInGroupContext);
        }

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
        return new RaidOperationResult(true, null, null, ToView(invite));
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
        Character? target = await GetCharacterAsync(accountId, cancellationToken);
        if (target is null)
            return RaidOperationResult.Failure(RaidErrorCodes.CharacterNotFound);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquireCharacterLockAsync(target.Id, cancellationToken);
        RaidInvite? invite = await dbContext.RaidInvites
            .SingleOrDefaultAsync(candidate => candidate.Id == inviteId, cancellationToken);
        if (invite is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return RaidOperationResult.Failure(RaidErrorCodes.InviteNotFound);
        }
        await AcquireRaidLockAsync(invite.RaidId, cancellationToken);
        invite = await dbContext.RaidInvites
            .SingleOrDefaultAsync(candidate => candidate.Id == inviteId, cancellationToken);
        RaidGroup? raid = await dbContext.RaidGroups
            .Include(candidate => candidate.Members)
            .SingleOrDefaultAsync(candidate => candidate.Id == invite!.RaidId, cancellationToken);
        if (invite is null || raid is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return RaidOperationResult.Failure(RaidErrorCodes.InviteNotFound);
        }
        if (invite.TargetCharacterId != target.Id || invite.Status != RaidInviteStatus.Pending)
        {
            await transaction.CommitAsync(cancellationToken);
            return RaidOperationResult.Failure(RaidErrorCodes.InvalidState);
        }
        if (timeProvider.GetUtcNow() >= invite.ExpiresAtUtc)
        {
            await transaction.CommitAsync(cancellationToken);
            return RaidOperationResult.Failure(RaidErrorCodes.InviteExpired);
        }
        if (await IsInGroupContextAsync(target.Id, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return RaidOperationResult.Failure(RaidErrorCodes.AlreadyInGroupContext);
        }

        invite.Accept(target.Id, timeProvider.GetUtcNow());
        raid.AddMember(target.Id, timeProvider.GetUtcNow());
        dbContext.RaidMembers.Add(raid.Members.Single(member => member.CharacterId == target.Id));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RaidOperationResult(true, null, await BuildSnapshotAsync(raid.Id, cancellationToken));
    }

    public Task<RaidOperationResult> LeaveAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => LeaveCoreAsync(accountId, cancellationToken));

    private async Task<RaidOperationResult> LeaveCoreAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        Character? character = await GetCharacterAsync(accountId, cancellationToken);
        if (character is null)
            return RaidOperationResult.Failure(RaidErrorCodes.CharacterNotFound);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquireCharacterLockAsync(character.Id, cancellationToken);
        RaidGroup? raid = await GetActiveRaidForCharacterAsync(character.Id, cancellationToken);
        if (raid is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return RaidOperationResult.Failure(RaidErrorCodes.RaidNotFound);
        }
        await AcquireRaidLockAsync(raid.Id, cancellationToken);
        raid = await GetActiveRaidForCharacterAsync(character.Id, cancellationToken);
        if (raid is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return RaidOperationResult.Failure(RaidErrorCodes.RaidNotFound);
        }

        RaidMember removed = raid.Leave(character.Id, timeProvider.GetUtcNow());
        dbContext.RaidMembers.Remove(removed);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RaidOperationResult(true, null, await BuildSnapshotAsync(raid.Id, cancellationToken));
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
        Character? leader = await GetCharacterAsync(accountId, cancellationToken);
        if (leader is null)
            return RaidOperationResult.Failure(RaidErrorCodes.CharacterNotFound);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        RaidGroup? raid = await GetActiveRaidForCharacterAsync(leader.Id, cancellationToken);
        if (raid is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return RaidOperationResult.Failure(RaidErrorCodes.RaidNotFound);
        }
        await AcquireRaidLockAsync(raid.Id, cancellationToken);
        raid = await GetActiveRaidForCharacterAsync(leader.Id, cancellationToken);
        if (raid is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return RaidOperationResult.Failure(RaidErrorCodes.RaidNotFound);
        }
        if (raid.LeaderCharacterId != leader.Id)
        {
            await transaction.CommitAsync(cancellationToken);
            return RaidOperationResult.Failure(RaidErrorCodes.NotLeaderOrAssistant);
        }

        try
        {
            raid.PromoteAssistant(leader.Id, targetCharacterId);
        }
        catch (InvalidOperationException)
        {
            await transaction.CommitAsync(cancellationToken);
            return RaidOperationResult.Failure(RaidErrorCodes.InvalidState);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RaidOperationResult(true, null, await BuildSnapshotAsync(raid.Id, cancellationToken));
    }

    private async Task<Character?> GetCharacterAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        await dbContext.Characters.SingleOrDefaultAsync(
            character => character.AccountId == accountId,
            cancellationToken);

    private async Task<bool> IsInGroupContextAsync(Guid characterId, CancellationToken cancellationToken) =>
        await dbContext.PartyMembers.AnyAsync(member => member.CharacterId == characterId, cancellationToken)
        || await dbContext.RaidMembers.AnyAsync(member => member.CharacterId == characterId, cancellationToken);

    private async Task<RaidGroup?> GetActiveRaidForCharacterAsync(
        Guid characterId,
        CancellationToken cancellationToken) =>
        await dbContext.RaidGroups
            .Include(raid => raid.Members)
            .SingleOrDefaultAsync(
                raid => raid.State == RaidState.Active
                    && raid.Members.Any(member => member.CharacterId == characterId),
                cancellationToken);

    private async Task<RaidSnapshot> BuildSnapshotAsync(
        Guid raidId,
        CancellationToken cancellationToken)
    {
        RaidGroup raid = await dbContext.RaidGroups
            .SingleAsync(candidate => candidate.Id == raidId, cancellationToken);
        RaidMember[] members = await dbContext.RaidMembers
            .Where(member => member.RaidId == raidId)
            .OrderBy(member => member.JoinedAtUtc)
            .ToArrayAsync(cancellationToken);
        Guid[] characterIds = members.Select(member => member.CharacterId).ToArray();
        Dictionary<Guid, Character> characters = await dbContext.Characters
            .Where(character => characterIds.Contains(character.Id))
            .ToDictionaryAsync(character => character.Id, cancellationToken);

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
                    member.JoinedAtUtc);
            }).ToArray());
    }

    private async Task AcquireCharacterLockAsync(
        Guid characterId,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
            return;

        string lockKey = $"raid-membership:{characterId:N}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))",
            cancellationToken);
    }

    private async Task AcquireRaidLockAsync(Guid raidId, CancellationToken cancellationToken)
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
}
