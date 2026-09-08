using Elyndor.Core.Characters;
using Elyndor.Core.Parties;
using Elyndor.Core.Social;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Parties;

public static class PartyErrorCodes
{
    public const string InvalidRequest = "party_request_invalid";
    public const string CharacterNotFound = "party_character_not_found";
    public const string PartyNotFound = "party_not_found";
    public const string NotLeader = "party_not_leader";
    public const string AlreadyInParty = "party_already_in_party";
    public const string PartyFull = "party_full";
    public const string InviteNotFound = "party_invite_not_found";
    public const string InviteExpired = "party_invite_expired";
    public const string NotFriends = "party_target_not_friend";
    public const string IdempotencyConflict = "party_idempotency_conflict";
    public const string InvalidState = "party_invalid_state";
}

public sealed record PartyMemberView(
    Guid CharacterId,
    string Name,
    int Level,
    string ClassId,
    bool IsLeader,
    DateTimeOffset JoinedAtUtc);

public sealed record PartyCombatMember(Guid AccountId, Guid CharacterId, bool IsLeader);

public sealed record PartySnapshot(
    Guid PartyId,
    Guid LeaderCharacterId,
    long Version,
    IReadOnlyList<PartyMemberView> Members);

public sealed record PartyInviteView(
    Guid Id,
    Guid PartyId,
    Guid InviterCharacterId,
    Guid TargetCharacterId,
    PartyInviteMode Mode,
    PartyInviteStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record PartyOperationResult(
    bool IsSuccess,
    string? ErrorCode,
    PartySnapshot? Snapshot = null,
    PartyInviteView? Invite = null)
{
    public static PartyOperationResult Failure(string code) => new(false, code);
}

public sealed class PartyService(
    GameDbContext dbContext,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromMinutes(5);

    public Task<PartyOperationResult> CreateAsync(
        Guid accountId,
        Guid requestId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => CreateCoreAsync(accountId, requestId, cancellationToken));

    private async Task<PartyOperationResult> CreateCoreAsync(
        Guid accountId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty || requestId == Guid.Empty)
            return PartyOperationResult.Failure(PartyErrorCodes.InvalidRequest);

        Character? character = await GetCharacterAsync(accountId, cancellationToken);
        if (character is null)
            return PartyOperationResult.Failure(PartyErrorCodes.CharacterNotFound);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquireCharacterLockAsync(character.Id, cancellationToken);
        Party? replay = await dbContext.Parties
            .SingleOrDefaultAsync(party => party.CreationRequestId == requestId, cancellationToken);
        if (replay is not null)
        {
            bool matches = replay.LeaderCharacterId == character.Id;
            if (!matches)
            {
                await transaction.CommitAsync(cancellationToken);
                return PartyOperationResult.Failure(PartyErrorCodes.IdempotencyConflict);
            }

            Party? replayParty = await GetPartyAsync(replay.Id, cancellationToken);
            PartySnapshot snapshot = replayParty is null
                ? throw new InvalidOperationException("Replayed party was not found.")
                : await BuildSnapshotAsync(replayParty, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new PartyOperationResult(true, null, snapshot);
        }

        if (await dbContext.PartyMembers.AnyAsync(
                member => member.CharacterId == character.Id,
                cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return PartyOperationResult.Failure(PartyErrorCodes.AlreadyInParty);
        }

        Party party = Party.Create(
            Guid.CreateVersion7(),
            requestId,
            character.Id,
            timeProvider.GetUtcNow());
        dbContext.Parties.Add(party);
        dbContext.PartyMembers.AddRange(party.Members);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await BuildSnapshotResultAsync(party, cancellationToken);
    }

    public Task<PartySnapshot?> GetAsync(Guid accountId, CancellationToken cancellationToken) =>
        GetForCharacterAsync(accountId, cancellationToken);

    public async Task<IReadOnlyList<PartyCombatMember>> GetCombatMembersAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        Character? character = await GetCharacterAsync(accountId, cancellationToken);
        if (character is null)
            return [];

        Party? party = await GetPartyForCharacterAsync(character.Id, cancellationToken);
        if (party is null || party.State != PartyState.Active)
            return [new PartyCombatMember(accountId, character.Id, true)];

        Guid[] characterIds = party.Members
            .Select(member => member.CharacterId)
            .ToArray();
        Dictionary<Guid, Guid> accountIds = await dbContext.Characters
            .Where(candidate => characterIds.Contains(candidate.Id))
            .ToDictionaryAsync(candidate => candidate.Id, candidate => candidate.AccountId, cancellationToken);

        return party.Members
            .OrderBy(member => member.JoinedAtUtc)
            .Where(member => accountIds.ContainsKey(member.CharacterId))
            .Select(member => new PartyCombatMember(
                accountIds[member.CharacterId],
                member.CharacterId,
                member.CharacterId == party.LeaderCharacterId))
            .ToArray();
    }

    public async Task<IReadOnlyList<PartyInviteView>> GetInvitesAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        Character? character = await GetCharacterAsync(accountId, cancellationToken);
        if (character is null)
            return [];

        DateTimeOffset now = timeProvider.GetUtcNow();
        PartyInvite[] invites = await dbContext.PartyInvites
            .Where(invite => invite.TargetCharacterId == character.Id
                && invite.Status == PartyInviteStatus.Pending
                && invite.ExpiresAtUtc > now)
            .OrderBy(invite => invite.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);
        return invites.Select(ToView).ToArray();
    }

    public async Task<PartyOperationResult> InviteAsync(
        Guid accountId,
        Guid inviteId,
        Guid targetCharacterId,
        PartyInviteMode mode,
        CancellationToken cancellationToken) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => InviteCoreAsync(
                accountId,
                inviteId,
                targetCharacterId,
                mode,
                cancellationToken));

    private async Task<PartyOperationResult> InviteCoreAsync(
        Guid accountId,
        Guid inviteId,
        Guid targetCharacterId,
        PartyInviteMode mode,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        Character? inviter = await GetCharacterAsync(accountId, cancellationToken);
        if (inviter is null)
            return PartyOperationResult.Failure(PartyErrorCodes.CharacterNotFound);

        Party? party = await GetPartyForCharacterAsync(inviter.Id, cancellationToken);
        if (party is null)
            return PartyOperationResult.Failure(PartyErrorCodes.PartyNotFound);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquirePartyLockAsync(party.Id, cancellationToken);
        dbContext.ChangeTracker.Clear();
        inviter = await GetCharacterAsync(accountId, cancellationToken);
        party = inviter is null
            ? null
            : await GetPartyForCharacterAsync(inviter.Id, cancellationToken);
        if (inviter is null)
            return PartyOperationResult.Failure(PartyErrorCodes.CharacterNotFound);
        if (party is null)
            return PartyOperationResult.Failure(PartyErrorCodes.PartyNotFound);
        if (party.LeaderCharacterId != inviter.Id)
            return PartyOperationResult.Failure(PartyErrorCodes.NotLeader);
        if (targetCharacterId == inviter.Id)
            return PartyOperationResult.Failure(PartyErrorCodes.InvalidRequest);
        if (party.Members.Count >= Party.MaxPartySize)
            return PartyOperationResult.Failure(PartyErrorCodes.PartyFull);
        if (await dbContext.PartyMembers.AnyAsync(
                member => member.CharacterId == targetCharacterId,
                cancellationToken))
            return PartyOperationResult.Failure(PartyErrorCodes.AlreadyInParty);
        if (!await dbContext.Characters.AnyAsync(
                character => character.Id == targetCharacterId,
                cancellationToken))
            return PartyOperationResult.Failure(PartyErrorCodes.CharacterNotFound);

        if (mode == PartyInviteMode.Friend
            && !await dbContext.Friendships.AnyAsync(
                friendship => friendship.PairKey == Friendship.GetPairKey(inviter.Id, targetCharacterId),
                cancellationToken))
            return PartyOperationResult.Failure(PartyErrorCodes.NotFriends);

        PartyInvite? replay = await dbContext.PartyInvites
            .SingleOrDefaultAsync(invite => invite.Id == inviteId, cancellationToken);
        if (replay is not null)
        {
            bool matches = replay.PartyId == party.Id
                && replay.InviterCharacterId == inviter.Id
                && replay.TargetCharacterId == targetCharacterId;
            await transaction.CommitAsync(cancellationToken);
            return matches
                ? new PartyOperationResult(true, null, Invite: ToView(replay))
                : PartyOperationResult.Failure(PartyErrorCodes.IdempotencyConflict);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        PartyInvite invite = PartyInvite.Create(
            inviteId,
            party.Id,
            inviter.Id,
            targetCharacterId,
            mode,
            now,
            now.Add(InviteLifetime));
        dbContext.PartyInvites.Add(invite);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PartyOperationResult(true, null, Invite: ToView(invite));
    }

    public Task<PartyOperationResult> AcceptInviteAsync(
        Guid accountId,
        Guid inviteId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => AcceptInviteCoreAsync(accountId, inviteId, cancellationToken));

    private async Task<PartyOperationResult> AcceptInviteCoreAsync(
        Guid accountId,
        Guid inviteId,
        CancellationToken cancellationToken)
    {
        Character? target = await GetCharacterAsync(accountId, cancellationToken);
        if (target is null)
            return PartyOperationResult.Failure(PartyErrorCodes.CharacterNotFound);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        PartyInvite? pendingInvite = await dbContext.PartyInvites
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == inviteId, cancellationToken);
        if (pendingInvite is null || pendingInvite.TargetCharacterId != target.Id)
            return PartyOperationResult.Failure(PartyErrorCodes.InviteNotFound);

        // A character can receive invites from multiple parties. Serialize all accepts
        // for that character before locking the selected party so the unique membership
        // constraint becomes a normal AlreadyInParty result instead of a race exception.
        await AcquireCharacterLockAsync(target.Id, cancellationToken);
        await AcquirePartyLockAsync(pendingInvite.PartyId, cancellationToken);
        PartyInvite? invite = await dbContext.PartyInvites
            .SingleOrDefaultAsync(candidate => candidate.Id == inviteId, cancellationToken);
        if (invite is null || invite.TargetCharacterId != target.Id)
            return PartyOperationResult.Failure(PartyErrorCodes.InviteNotFound);

        DateTimeOffset now = timeProvider.GetUtcNow();
        invite.Expire(now);
        if (invite.Status == PartyInviteStatus.Expired)
            return PartyOperationResult.Failure(PartyErrorCodes.InviteExpired);
        if (invite.Status != PartyInviteStatus.Pending)
            return PartyOperationResult.Failure(PartyErrorCodes.InvalidState);
        if (await dbContext.PartyMembers.AnyAsync(
                member => member.CharacterId == target.Id,
                cancellationToken))
            return PartyOperationResult.Failure(PartyErrorCodes.AlreadyInParty);

        Party? party = await GetPartyAsync(invite.PartyId, cancellationToken);
        if (party is null || party.State != PartyState.Active)
            return PartyOperationResult.Failure(PartyErrorCodes.PartyNotFound);
        if (party.Members.Count >= Party.MaxPartySize)
            return PartyOperationResult.Failure(PartyErrorCodes.PartyFull);

        invite.Accept(target.Id, now);
        party.AddMember(target.Id, now);
        dbContext.PartyMembers.Add(party.Members.Single(member => member.CharacterId == target.Id));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await BuildSnapshotResultAsync(party, cancellationToken);
    }

    public Task<PartyOperationResult> DeclineInviteAsync(
        Guid accountId,
        Guid inviteId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => DeclineInviteCoreAsync(accountId, inviteId, cancellationToken));

    private async Task<PartyOperationResult> DeclineInviteCoreAsync(
        Guid accountId,
        Guid inviteId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        Character? target = await GetCharacterAsync(accountId, cancellationToken);
        if (target is null)
            return PartyOperationResult.Failure(PartyErrorCodes.CharacterNotFound);
        PartyInvite? probe = await dbContext.PartyInvites
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == inviteId, cancellationToken);
        if (probe is null || probe.TargetCharacterId != target.Id)
            return PartyOperationResult.Failure(PartyErrorCodes.InviteNotFound);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquirePartyLockAsync(probe.PartyId, cancellationToken);
        dbContext.ChangeTracker.Clear();
        target = await GetCharacterAsync(accountId, cancellationToken);
        PartyInvite? invite = await dbContext.PartyInvites
            .SingleOrDefaultAsync(candidate => candidate.Id == inviteId, cancellationToken);
        if (target is null)
            return PartyOperationResult.Failure(PartyErrorCodes.CharacterNotFound);
        if (invite is null || invite.TargetCharacterId != target.Id)
            return PartyOperationResult.Failure(PartyErrorCodes.InviteNotFound);
        try
        {
            invite.Decline(target.Id, timeProvider.GetUtcNow());
        }
        catch (InvalidOperationException)
        {
            return PartyOperationResult.Failure(PartyErrorCodes.InvalidState);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PartyOperationResult(true, null, Invite: ToView(invite));
    }

    public Task<PartyOperationResult> LeaveAsync(Guid accountId, CancellationToken cancellationToken) =>
        MutateMembershipAsync(accountId, PartyMutation.Leave, Guid.Empty, cancellationToken);

    public Task<PartyOperationResult> KickAsync(
        Guid accountId,
        Guid targetCharacterId,
        CancellationToken cancellationToken) =>
        MutateMembershipAsync(accountId, PartyMutation.Kick, targetCharacterId, cancellationToken);

    public Task<PartyOperationResult> TransferLeadershipAsync(
        Guid accountId,
        Guid targetCharacterId,
        CancellationToken cancellationToken) =>
        MutateMembershipAsync(accountId, PartyMutation.Transfer, targetCharacterId, cancellationToken);

    public Task<PartyOperationResult> DisbandAsync(Guid accountId, CancellationToken cancellationToken) =>
        MutateMembershipAsync(accountId, PartyMutation.Disband, Guid.Empty, cancellationToken);

    private async Task<PartyOperationResult> MutateMembershipAsync(
        Guid accountId,
        PartyMutation mutation,
        Guid targetCharacterId,
        CancellationToken cancellationToken) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => MutateMembershipCoreAsync(
                accountId,
                mutation,
                targetCharacterId,
                cancellationToken));

    private async Task<PartyOperationResult> MutateMembershipCoreAsync(
        Guid accountId,
        PartyMutation mutation,
        Guid targetCharacterId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        Character? actor = await GetCharacterAsync(accountId, cancellationToken);
        if (actor is null)
            return PartyOperationResult.Failure(PartyErrorCodes.CharacterNotFound);
        Party? party = await GetPartyForCharacterAsync(actor.Id, cancellationToken);
        if (party is null)
            return PartyOperationResult.Failure(PartyErrorCodes.PartyNotFound);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquirePartyLockAsync(party.Id, cancellationToken);
        dbContext.ChangeTracker.Clear();
        actor = await GetCharacterAsync(accountId, cancellationToken);
        party = actor is null
            ? null
            : await GetPartyForCharacterAsync(actor.Id, cancellationToken);
        if (actor is null)
            return PartyOperationResult.Failure(PartyErrorCodes.CharacterNotFound);
        if (party is null)
            return PartyOperationResult.Failure(PartyErrorCodes.PartyNotFound);

        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid[] membersBeforeMutation = party.Members
            .Select(member => member.CharacterId)
            .ToArray();
        try
        {
            switch (mutation)
            {
                case PartyMutation.Leave:
                    party.Leave(actor.Id, now);
                    break;
                case PartyMutation.Kick:
                    party.Kick(actor.Id, targetCharacterId, now);
                    break;
                case PartyMutation.Transfer:
                    party.TransferLeadership(actor.Id, targetCharacterId);
                    break;
                case PartyMutation.Disband:
                    party.Disband(actor.Id);
                    break;
            }
        }
        catch (UnauthorizedAccessException)
        {
            return PartyOperationResult.Failure(PartyErrorCodes.NotLeader);
        }
        catch (InvalidOperationException)
        {
            return PartyOperationResult.Failure(PartyErrorCodes.InvalidState);
        }

        if (mutation is PartyMutation.Leave or PartyMutation.Kick)
        {
            Guid removed = mutation == PartyMutation.Leave ? actor.Id : targetCharacterId;
            PartyMember? member = await dbContext.PartyMembers
                .SingleOrDefaultAsync(candidate => candidate.PartyId == party.Id
                    && candidate.CharacterId == removed, cancellationToken);
            if (member is not null)
                dbContext.PartyMembers.Remove(member);
        }
        else if (mutation == PartyMutation.Disband)
        {
            PartyMember[] members = await dbContext.PartyMembers
                .Where(member => member.PartyId == party.Id
                    && membersBeforeMutation.Contains(member.CharacterId))
                .ToArrayAsync(cancellationToken);
            dbContext.PartyMembers.RemoveRange(members);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await BuildSnapshotResultAsync(party, cancellationToken);
    }

    private async Task<PartySnapshot?> GetForCharacterAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        Character? character = await GetCharacterAsync(accountId, cancellationToken);
        if (character is null)
            return null;
        Party? party = await GetPartyForCharacterAsync(character.Id, cancellationToken);
        return party is null ? null : await BuildSnapshotAsync(party, cancellationToken);
    }

    private async Task<Party?> GetPartyForCharacterAsync(
        Guid characterId,
        CancellationToken cancellationToken)
    {
        Guid? partyId = await dbContext.PartyMembers
            .Where(member => member.CharacterId == characterId)
            .Select(member => (Guid?)member.PartyId)
            .SingleOrDefaultAsync(cancellationToken);
        return partyId.HasValue ? await GetPartyAsync(partyId.Value, cancellationToken) : null;
    }

    private async Task<Party?> GetPartyAsync(Guid partyId, CancellationToken cancellationToken)
    {
        Party? party = await dbContext.Parties
            .SingleOrDefaultAsync(candidate => candidate.Id == partyId, cancellationToken);
        if (party is not null)
        {
            await dbContext.Entry(party)
                .Collection(candidate => candidate.Members)
                .LoadAsync(cancellationToken);
        }
        return party;
    }

    private async Task<PartyOperationResult> BuildSnapshotResultAsync(
        Party party,
        CancellationToken cancellationToken) =>
        new(true, null, await BuildSnapshotAsync(party, cancellationToken));

    private async Task<PartySnapshot> BuildSnapshotAsync(
        Party party,
        CancellationToken cancellationToken)
    {
        Guid[] ids = party.Members.Select(member => member.CharacterId).ToArray();
        Dictionary<Guid, (string Name, int Level, string ClassId)> profiles = await dbContext.Characters
            .Where(character => ids.Contains(character.Id))
            .ToDictionaryAsync(
                character => character.Id,
                character => (character.Name, character.Level, character.ClassId),
                cancellationToken);
        return new PartySnapshot(
            party.Id,
            party.LeaderCharacterId,
            party.Version,
            party.Members
                .OrderBy(member => member.JoinedAtUtc)
                .Select(member =>
                {
                    (string name, int level, string classId) = profiles[member.CharacterId];
                    return new PartyMemberView(
                        member.CharacterId,
                        name,
                        level,
                        classId,
                        member.CharacterId == party.LeaderCharacterId,
                        member.JoinedAtUtc);
                })
                .ToArray());
    }

    private async Task<Character?> GetCharacterAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        await dbContext.Characters.SingleOrDefaultAsync(
            character => character.AccountId == accountId,
            cancellationToken);

    private async Task AcquirePartyLockAsync(
        Guid partyId,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
            return;

        string lockKey = $"party-membership:{partyId:N}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))",
            cancellationToken);
    }

    private async Task AcquireCharacterLockAsync(
        Guid characterId,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
            return;

        string lockKey = $"party-creation:{characterId:N}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))",
            cancellationToken);
    }

    private static PartyInviteView ToView(PartyInvite invite) =>
        new(
            invite.Id,
            invite.PartyId,
            invite.InviterCharacterId,
            invite.TargetCharacterId,
            invite.Mode,
            invite.Status,
            invite.CreatedAtUtc,
            invite.ExpiresAtUtc);

    private enum PartyMutation
    {
        Leave,
        Kick,
        Transfer,
        Disband
    }
}
