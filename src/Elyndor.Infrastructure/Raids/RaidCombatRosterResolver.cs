using Elyndor.Core.Afk;
using Elyndor.Core.Content;
using Elyndor.Core.Raids;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.World;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Raids;

public static class RaidCombatRosterErrorCodes
{
    public const string RaidNotFound = "raid_combat_raid_not_found";
    public const string NotLeader = "raid_combat_not_leader";
    public const string LeaderUnavailable = "raid_combat_leader_unavailable";
    public const string CapacityExceeded = "raid_combat_capacity_exceeded";
}

public sealed record RaidCombatMember(
    Guid AccountId,
    Guid CharacterId,
    bool IsLeader);

public sealed record RaidCombatRosterResult(
    bool Succeeded,
    string? ErrorCode,
    Guid RaidId,
    IReadOnlyList<RaidCombatMember> Members)
{
    public static RaidCombatRosterResult Failure(Guid raidId, string errorCode) =>
        new(false, errorCode, raidId, []);
}

public sealed class RaidCombatRosterResolver(
    GameDbContext dbContext,
    BootstrapService bootstrapService,
    IContentSnapshotProvider contentProvider,
    ICombatActivityReader combatActivity)
{
    public async Task<RaidCombatRosterResult> ResolveAsync(
        Guid raidId,
        Guid leaderCharacterId,
        string locationId,
        int encounterMaximum,
        CancellationToken cancellationToken)
    {
        if (raidId == Guid.Empty
            || leaderCharacterId == Guid.Empty
            || string.IsNullOrWhiteSpace(locationId))
        {
            return RaidCombatRosterResult.Failure(
                raidId,
                RaidCombatRosterErrorCodes.RaidNotFound);
        }
        if (encounterMaximum <= 0
            || encounterMaximum > RaidGroup.DefaultMaximumMembers)
        {
            throw new ArgumentOutOfRangeException(nameof(encounterMaximum));
        }

        RaidGroup? raid = await dbContext.RaidGroups
            .AsNoTracking()
            .Include(candidate => candidate.Members)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == raidId && candidate.State == RaidState.Active,
                cancellationToken);
        if (raid is null)
        {
            return RaidCombatRosterResult.Failure(
                raidId,
                RaidCombatRosterErrorCodes.RaidNotFound);
        }
        if (raid.LeaderCharacterId != leaderCharacterId)
        {
            return RaidCombatRosterResult.Failure(
                raidId,
                RaidCombatRosterErrorCodes.NotLeader);
        }

        Guid[] memberCharacterIds = raid.Members
            .Where(member => member.State == RaidMemberState.Active)
            .Select(member => member.CharacterId)
            .Distinct()
            .ToArray();
        Dictionary<Guid, Guid> accountIdsByCharacter = await dbContext.Characters
            .AsNoTracking()
            .Where(character => memberCharacterIds.Contains(character.Id))
            .ToDictionaryAsync(
                character => character.Id,
                character => character.AccountId,
                cancellationToken);

        List<RaidCombatMember> eligible = [];
        foreach (RaidMember member in raid.Members
                     .Where(member => member.State == RaidMemberState.Active)
                     .OrderBy(member => member.CharacterId == leaderCharacterId ? 0 : 1)
                     .ThenBy(member => member.JoinedAtUtc)
                     .ThenBy(member => member.CharacterId))
        {
            if (!accountIdsByCharacter.TryGetValue(member.CharacterId, out Guid accountId))
                continue;
            if (combatActivity.HasActiveCombat(accountId))
                continue;

            BootstrapSnapshot bootstrap = await bootstrapService.GetAsync(
                accountId,
                contentProvider.GetCurrent(),
                cancellationToken,
                checkpoint: true);
            if (bootstrap.Character is null
                || bootstrap.Character.Id != member.CharacterId
                || bootstrap.Character.Vitals.CurrentHp <= 0
                || bootstrap.AfkFarm?.Status == AfkFarmStatus.Active
                || bootstrap.World is null
                || bootstrap.World.Travel is not null
                || !string.Equals(
                    bootstrap.World.CurrentLocation.Id,
                    locationId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            eligible.Add(new RaidCombatMember(
                accountId,
                member.CharacterId,
                member.CharacterId == leaderCharacterId));
        }

        if (eligible.SingleOrDefault(member => member.IsLeader) is null)
        {
            return RaidCombatRosterResult.Failure(
                raidId,
                RaidCombatRosterErrorCodes.LeaderUnavailable);
        }
        if (eligible.Count > encounterMaximum)
        {
            return RaidCombatRosterResult.Failure(
                raidId,
                RaidCombatRosterErrorCodes.CapacityExceeded);
        }

        return new RaidCombatRosterResult(
            true,
            null,
            raidId,
            eligible);
    }
}
