using Elyndor.Core.Dungeons;
using Elyndor.Core.Characters;
using Elyndor.Infrastructure.Parties;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Dungeons;

public sealed partial class DungeonService
{
    public Task<DungeonOperationResult> SelectFloorAsync(Guid accountId, Guid runId, int floorNumber,
        CancellationToken cancellationToken) => dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => SelectFloorCoreAsync(accountId, runId, floorNumber, cancellationToken));

    private async Task<DungeonOperationResult> SelectFloorCoreAsync(Guid accountId, Guid runId, int floorNumber,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await BeginAdvisoryLockAsync($"dungeon-run:{runId:N}", cancellationToken);
        DungeonRun? run = await LoadRunAsync(runId, cancellationToken);
        if (run is null) return DungeonOperationResult.Failure(DungeonErrorCodes.RunNotFound);
        Character? actor = await GetCharacterAsync(accountId, cancellationToken);
        if (actor is null) return DungeonOperationResult.Failure(DungeonErrorCodes.CharacterNotFound);
        if (!run.Members.Any(member => member.CharacterId == actor.Id && member.State == DungeonRunMemberState.Active))
            return DungeonOperationResult.Failure(DungeonErrorCodes.MemberNotInRun);
        if (!IsSoloRun(run, actor.Id))
        {
            PartySnapshot? party = await partyService.GetAsync(accountId, cancellationToken);
            if (party is null || party.PartyId != run.PartyId)
                return DungeonOperationResult.Failure(DungeonErrorCodes.MemberNotInParty);
            if (party.LeaderCharacterId != actor.Id)
                return DungeonOperationResult.Failure(DungeonErrorCodes.NotLeader);
        }
        if (run.State != DungeonRunState.Active)
            return DungeonOperationResult.Failure(DungeonErrorCodes.EncounterNotReady);
        Guid[] members = run.Members.Where(member => member.State == DungeonRunMemberState.Active)
            .Select(member => member.CharacterId).ToArray();
        if (run.Encounters.Any(encounter => encounter.State == DungeonEncounterState.Active)
            || await dbContext.ActiveCombatSessions.AnyAsync(state => members.Contains(state.CharacterId), cancellationToken))
            return DungeonOperationResult.Failure(DungeonErrorCodes.EncounterActive);
        if (registry is not null)
        {
            Guid[] accountIds = await dbContext.Characters.AsNoTracking()
                .Where(character => members.Contains(character.Id)).Select(character => character.AccountId)
                .ToArrayAsync(cancellationToken);
            if (accountIds.Any(registry.HasActiveCombat))
                return DungeonOperationResult.Failure(DungeonErrorCodes.EncounterActive);
        }
        if (!contentProvider.GetCurrent().Indexes.DungeonsById.TryGetValue(run.DungeonId, out DungeonDefinition? definition))
            return DungeonOperationResult.Failure(DungeonErrorCodes.DungeonNotFound);
        string? validation = await ValidateMemberAsync(actor, definition, cancellationToken);
        if (validation is not null) return DungeonOperationResult.Failure(validation);
        DungeonFloorRange? floor = DungeonFloorLayout.Build(definition).SingleOrDefault(candidate => candidate.Number == floorNumber);
        if (floor is null) return DungeonOperationResult.Failure(DungeonErrorCodes.FloorNotFound);
        HashSet<int> completed = CompletedIndexes(run);
        if (!DungeonFloorLayout.IsUnlocked(definition, floor, completed))
            return DungeonOperationResult.Failure(DungeonErrorCodes.FloorLocked);
        int? next = DungeonFloorLayout.NextEncounterIndex(floor, completed);
        run.SelectEncounter(next ?? floor.LastEncounterIndex);
        if (next.HasValue) await EnsurePendingEncounterAsync(run, definition, next.Value, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        return new(true, null, ToView(run, definition));
    }

    private async Task EnsurePendingEncounterAsync(DungeonRun run, DungeonDefinition definition, int index,
        CancellationToken cancellationToken)
    {
        if (await dbContext.DungeonEncounters.AnyAsync(encounter => encounter.RunId == run.Id
                && encounter.EncounterIndex == index, cancellationToken)) return;
        DungeonEncounter encounter = DungeonEncounter.Create(Guid.CreateVersion7(), run.Id, index,
            definition.Encounters[index].MonsterId, timeProvider.GetUtcNow());
        run.Encounters.Add(encounter);
        dbContext.DungeonEncounters.Add(encounter);
    }

    private static HashSet<int> CompletedIndexes(DungeonRun run) => run.Encounters
        .Where(encounter => encounter.State == DungeonEncounterState.Completed)
        .Select(encounter => encounter.EncounterIndex).ToHashSet();

    private static DungeonFloorStateView[] FloorStates(DungeonRun run, DungeonDefinition definition)
    {
        HashSet<int> completed = CompletedIndexes(run);
        return DungeonFloorLayout.Build(definition).Select(floor => new DungeonFloorStateView(floor.Number,
            DungeonFloorLayout.IsUnlocked(definition, floor, completed),
            DungeonFloorLayout.NextEncounterIndex(floor, completed) is null,
            DungeonFloorLayout.NextEncounterIndex(floor, completed))).ToArray();
    }
}
