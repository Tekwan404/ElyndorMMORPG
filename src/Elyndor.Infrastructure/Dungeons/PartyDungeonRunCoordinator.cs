using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Dungeons;

public sealed record PartyDungeonStartResult(
    bool Succeeded,
    string? ErrorCode,
    Guid? RunId = null)
{
    public static PartyDungeonStartResult Success(Guid runId) => new(true, null, runId);
    public static PartyDungeonStartResult Failure(string code) => new(false, code);
}

/// <summary>
/// Starts a dungeon for an existing party as one authoritative operation.
/// Old dungeon memberships for party members are retired before the new run is created,
/// and every member is moved to the new dungeon entry in the same database transaction.
/// </summary>
public sealed class PartyDungeonRunCoordinator(
    GameDbContext dbContext,
    PartyService partyService,
    IContentSnapshotProvider contentProvider,
    ICombatActivityReader combatActivity,
    TimeProvider timeProvider)
{
    public Task<PartyDungeonStartResult> StartAsync(
        Guid accountId,
        string dungeonId,
        Guid requestId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => StartCoreAsync(accountId, dungeonId, requestId, cancellationToken));

    private async Task<PartyDungeonStartResult> StartCoreAsync(
        Guid accountId,
        string dungeonId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        if (accountId == Guid.Empty || requestId == Guid.Empty || string.IsNullOrWhiteSpace(dungeonId))
            return PartyDungeonStartResult.Failure(DungeonErrorCodes.MemberCannotEnter);

        if (!contentProvider.GetCurrent().Indexes.DungeonsById.TryGetValue(
                dungeonId,
                out DungeonDefinition? definition))
        {
            return PartyDungeonStartResult.Failure(DungeonErrorCodes.DungeonNotFound);
        }

        PartySnapshot? initialParty = await partyService.GetAsync(accountId, cancellationToken);
        if (initialParty is null)
            return PartyDungeonStartResult.Failure(DungeonErrorCodes.PartyRequired);

        Guid? callerCharacterId = await dbContext.Characters
            .AsNoTracking()
            .Where(character => character.AccountId == accountId)
            .Select(character => (Guid?)character.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!callerCharacterId.HasValue)
            return PartyDungeonStartResult.Failure(DungeonErrorCodes.CharacterNotFound);
        if (initialParty.LeaderCharacterId != callerCharacterId.Value)
            return PartyDungeonStartResult.Failure(DungeonErrorCodes.NotLeader);

        if (initialParty.Members.Count < definition.MinimumPartySize
            || initialParty.Members.Count > Math.Min(
                definition.MaximumPartySize,
                CombatParticipantRoster.DefaultMaximumParticipants))
        {
            return PartyDungeonStartResult.Failure(DungeonErrorCodes.PartyRequired);
        }

        Guid partyId = initialParty.PartyId;
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (dbContext.Database.IsNpgsql())
        {
            string lockKey = $"party-membership:{partyId:N}";
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))",
                cancellationToken);
        }

        dbContext.ChangeTracker.Clear();
        PartySnapshot? party = await partyService.GetAsync(accountId, cancellationToken);
        if (party is null || party.PartyId != partyId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return PartyDungeonStartResult.Failure(DungeonErrorCodes.PartyRequired);
        }
        if (party.LeaderCharacterId != callerCharacterId.Value)
        {
            await transaction.RollbackAsync(cancellationToken);
            return PartyDungeonStartResult.Failure(DungeonErrorCodes.NotLeader);
        }
        if (party.Members.Count < definition.MinimumPartySize
            || party.Members.Count > Math.Min(
                definition.MaximumPartySize,
                CombatParticipantRoster.DefaultMaximumParticipants))
        {
            await transaction.RollbackAsync(cancellationToken);
            return PartyDungeonStartResult.Failure(DungeonErrorCodes.PartyRequired);
        }

        DungeonRun? replay = await dbContext.DungeonRuns
            .Include(run => run.Members)
            .Include(run => run.Encounters)
                .ThenInclude(encounter => encounter.Members)
            .SingleOrDefaultAsync(run => run.CreationRequestId == requestId, cancellationToken);
        if (replay is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return PartyDungeonStartResult.Success(replay.Id);
        }

        Guid[] memberIds = party.Members
            .Select(member => member.CharacterId)
            .Distinct()
            .ToArray();

        var characters = await dbContext.Characters
            .Where(character => memberIds.Contains(character.Id))
            .Select(character => new
            {
                character.Id,
                character.AccountId,
                character.Level
            })
            .ToArrayAsync(cancellationToken);
        if (characters.Length != memberIds.Length)
        {
            await transaction.RollbackAsync(cancellationToken);
            return PartyDungeonStartResult.Failure(DungeonErrorCodes.CharacterNotFound);
        }
        if (characters.Any(character => character.Level < definition.MinimumLevel))
        {
            await transaction.RollbackAsync(cancellationToken);
            return PartyDungeonStartResult.Failure(DungeonErrorCodes.LevelRequired);
        }
        if (characters.Any(character => combatActivity.HasActiveCombat(character.AccountId)))
        {
            await transaction.RollbackAsync(cancellationToken);
            return PartyDungeonStartResult.Failure(DungeonErrorCodes.EncounterActive);
        }

        int healthyMembers = await dbContext.CharacterVitals
            .AsNoTracking()
            .CountAsync(vitals => memberIds.Contains(vitals.CharacterId) && vitals.CurrentHp > 0, cancellationToken);
        if (healthyMembers != memberIds.Length)
        {
            await transaction.RollbackAsync(cancellationToken);
            return PartyDungeonStartResult.Failure(DungeonErrorCodes.MemberCannotEnter);
        }

        bool hasTravel = await dbContext.CharacterTravelStates
            .AsNoTracking()
            .AnyAsync(state => memberIds.Contains(state.CharacterId), cancellationToken);
        if (hasTravel)
        {
            await transaction.RollbackAsync(cancellationToken);
            return PartyDungeonStartResult.Failure(DungeonErrorCodes.TravelInProgress);
        }

        DungeonRun? currentPartyRun = await dbContext.DungeonRuns
            .Include(run => run.Members)
            .Include(run => run.Encounters)
                .ThenInclude(encounter => encounter.Members)
            .Where(run => run.PartyId == partyId && run.State == DungeonRunState.Active)
            .OrderByDescending(run => run.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (currentPartyRun is not null
            && string.Equals(currentPartyRun.DungeonId, definition.Id, StringComparison.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken);
            return PartyDungeonStartResult.Success(currentPartyRun.Id);
        }

        DungeonRun[] previousRuns = await dbContext.DungeonRuns
            .Include(run => run.Members)
            .Include(run => run.Encounters)
                .ThenInclude(encounter => encounter.Members)
            .Where(run => run.State != DungeonRunState.Abandoned
                && run.Members.Any(member => memberIds.Contains(member.CharacterId)
                    && member.State == DungeonRunMemberState.Active))
            .OrderBy(run => run.Id)
            .ToArrayAsync(cancellationToken);

        if (previousRuns.Any(run => run.Encounters.Any(
                encounter => encounter.State == DungeonEncounterState.Active)))
        {
            await transaction.RollbackAsync(cancellationToken);
            return PartyDungeonStartResult.Failure(DungeonErrorCodes.EncounterActive);
        }

        foreach (DungeonRun previous in previousRuns)
        {
            foreach (DungeonRunMember member in previous.Members.Where(member =>
                         memberIds.Contains(member.CharacterId)
                         && member.State == DungeonRunMemberState.Active))
            {
                member.MarkLeft();
            }

            if (previous.State == DungeonRunState.Active
                && previous.Members.All(member => member.State == DungeonRunMemberState.Left))
            {
                previous.Abandon();
            }
        }

        CharacterLocation[] locations = await dbContext.CharacterLocations
            .Where(location => memberIds.Contains(location.CharacterId))
            .ToArrayAsync(cancellationToken);
        if (locations.Length != memberIds.Length)
        {
            await transaction.RollbackAsync(cancellationToken);
            return PartyDungeonStartResult.Failure(DungeonErrorCodes.InvalidLocation);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        foreach (CharacterLocation location in locations)
        {
            if (!string.Equals(location.LocationId, definition.EntryLocationId, StringComparison.Ordinal))
                location.Relocate(definition.EntryLocationId, now);
        }

        DungeonRun run = DungeonRun.Create(
            Guid.CreateVersion7(),
            requestId,
            partyId,
            definition.Id,
            now);
        foreach (Guid memberId in memberIds)
            run.AddMember(memberId, now);

        DungeonEncounterDefinition first = definition.Encounters[0];
        run.Encounters.Add(DungeonEncounter.Create(
            Guid.CreateVersion7(),
            run.Id,
            0,
            first.MonsterId,
            now));
        dbContext.DungeonRuns.Add(run);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return PartyDungeonStartResult.Success(run.Id);
    }
}
