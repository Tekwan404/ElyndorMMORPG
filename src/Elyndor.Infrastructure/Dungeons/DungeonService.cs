using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Dungeons;

public static class DungeonErrorCodes
{
    public const string CharacterNotFound = "dungeon_character_not_found";
    public const string DungeonNotFound = "dungeon_not_found";
    public const string PartyRequired = "dungeon_party_required";
    public const string NotLeader = "dungeon_not_leader";
    public const string InvalidLocation = "dungeon_invalid_location";
    public const string LevelRequired = "dungeon_level_required";
    public const string RunNotFound = "dungeon_run_not_found";
    public const string RunAlreadyActive = "dungeon_run_already_active";
    public const string EncounterActive = "dungeon_encounter_active";
    public const string EncounterNotReady = "dungeon_encounter_not_ready";
    public const string MemberNotInParty = "dungeon_member_not_in_party";
    public const string MemberNotInRun = "dungeon_member_not_in_run";
    public const string MemberCannotEnter = "dungeon_member_cannot_enter";
    public const string TeleportIdempotencyConflict = "dungeon_teleport_idempotency_conflict";
    public const string TravelInProgress = "dungeon_travel_in_progress";
}

public sealed record DungeonOperationResult(
    bool Succeeded,
    string? ErrorCode,
    DungeonRunView? Run = null)
{
    public static DungeonOperationResult Failure(string code) => new(false, code);
}

public sealed record DungeonRunMemberView(
    Guid CharacterId,
    DungeonRunMemberState State,
    DateTimeOffset JoinedAtUtc);

public sealed record DungeonEncounterView(
    Guid EncounterId,
    int EncounterIndex,
    string MonsterId,
    DungeonEncounterState State,
    int WipeCount,
    IReadOnlyList<Guid> CharacterIds);

public sealed record DungeonRunView(
    Guid RunId,
    string DungeonId,
    string DisplayName,
    string Description,
    DungeonRunState State,
    int CurrentEncounterIndex,
    string CurrentCheckpointId,
    int EncounterCount,
    Guid PartyId,
    IReadOnlyList<DungeonRunMemberView> Members,
    IReadOnlyList<DungeonEncounterView> Encounters);

public sealed record DungeonTeleportResult(
    bool Succeeded,
    string? ErrorCode,
    string? DungeonId = null,
    string? LocationId = null,
    long? LocationVersion = null)
{
    public static DungeonTeleportResult Failure(string code) => new(false, code);

    public static DungeonTeleportResult Success(
        string dungeonId,
        string locationId,
        long locationVersion) =>
        new(true, null, dungeonId, locationId, locationVersion);
}

public sealed record DungeonPreparation(
    Guid RunId,
    Guid EncounterId,
    string MonsterId,
    string LocationId,
    IReadOnlyList<PartyCombatMember> Participants);

public sealed class DungeonService(
    GameDbContext dbContext,
    PartyService partyService,
    IContentSnapshotProvider contentProvider,
    TimeProvider timeProvider)
{
    public IReadOnlyList<DungeonDefinition> GetDefinitions() =>
        contentProvider.GetCurrent().Package.Dungeons ?? [];

    public DungeonDefinition? GetDefinition(string dungeonId) =>
        contentProvider.GetCurrent().Indexes.DungeonsById.GetValueOrDefault(dungeonId);

    public Task<DungeonTeleportResult> TeleportToEntryAsync(
        Guid accountId,
        string dungeonId,
        Guid requestId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => TeleportToEntryCoreAsync(accountId, dungeonId, requestId, cancellationToken));

    private async Task<DungeonTeleportResult> TeleportToEntryCoreAsync(
        Guid accountId,
        string dungeonId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        if (accountId == Guid.Empty || requestId == Guid.Empty || string.IsNullOrWhiteSpace(dungeonId))
            return DungeonTeleportResult.Failure(DungeonErrorCodes.MemberCannotEnter);

        DungeonDefinition? definition = GetDefinition(dungeonId);
        if (definition is null)
            return DungeonTeleportResult.Failure(DungeonErrorCodes.DungeonNotFound);

        await using IDbContextTransaction? transaction =
            await BeginAdvisoryLockAsync($"dungeon-teleport:{accountId:N}", cancellationToken);
        Character? character = await dbContext.Characters
            .FromSqlInterpolated(
                $"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (character is null)
            return DungeonTeleportResult.Failure(DungeonErrorCodes.CharacterNotFound);
        if (character.Level < definition.MinimumLevel || character.Level > definition.MaximumLevel)
            return DungeonTeleportResult.Failure(DungeonErrorCodes.LevelRequired);

        DateTimeOffset now = timeProvider.GetUtcNow();
        await TravelPersistence.CompleteDueAsync(dbContext, character.Id, now, cancellationToken);

        TravelOperation? replay = await dbContext.TravelOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                operation => operation.CharacterId == character.Id
                    && operation.RequestId == requestId,
                cancellationToken);
        if (replay is not null)
        {
            await CommitAsync(transaction, cancellationToken);
            return string.Equals(replay.TargetLocationId, definition.EntryLocationId, StringComparison.Ordinal)
                ? DungeonTeleportResult.Success(definition.Id, replay.ResultLocationId, replay.ResultVersion)
                : DungeonTeleportResult.Failure(DungeonErrorCodes.TeleportIdempotencyConflict);
        }

        CharacterTravelState? activeTravel = await dbContext.CharacterTravelStates
            .AsNoTracking()
            .SingleOrDefaultAsync(state => state.CharacterId == character.Id, cancellationToken);
        if (activeTravel is not null)
            return DungeonTeleportResult.Failure(DungeonErrorCodes.TravelInProgress);

        CharacterLocation location = await dbContext.CharacterLocations
            .SingleAsync(candidate => candidate.CharacterId == character.Id, cancellationToken);
        if (!string.Equals(location.LocationId, definition.EntryLocationId, StringComparison.Ordinal))
            location.Relocate(definition.EntryLocationId, now);

        dbContext.TravelOperations.Add(new TravelOperation(
            character.Id,
            requestId,
            definition.EntryLocationId,
            location.LocationId,
            location.Version,
            now));
        await dbContext.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        return DungeonTeleportResult.Success(definition.Id, location.LocationId, location.Version);
    }

    public Task<DungeonOperationResult> CreateAsync(
        Guid accountId,
        string dungeonId,
        Guid creationRequestId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => CreateCoreAsync(accountId, dungeonId, creationRequestId, cancellationToken));

    private async Task<DungeonOperationResult> CreateCoreAsync(
        Guid accountId,
        string dungeonId,
        Guid creationRequestId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        if (creationRequestId == Guid.Empty)
            return DungeonOperationResult.Failure(DungeonErrorCodes.MemberCannotEnter);
        DungeonDefinition? definition = GetDefinition(dungeonId);
        if (definition is null)
            return DungeonOperationResult.Failure(DungeonErrorCodes.DungeonNotFound);

        PartySnapshot? party = await partyService.GetAsync(accountId, cancellationToken);
        if (party is null)
            return DungeonOperationResult.Failure(DungeonErrorCodes.PartyRequired);

        Character? character = await GetCharacterAsync(accountId, cancellationToken);
        if (character is null)
            return DungeonOperationResult.Failure(DungeonErrorCodes.CharacterNotFound);
        if (party.LeaderCharacterId != character.Id)
            return DungeonOperationResult.Failure(DungeonErrorCodes.NotLeader);
        if (party.Members.Count < definition.MinimumPartySize
            || party.Members.Count > Math.Min(definition.MaximumPartySize, CombatParticipantRoster.DefaultMaximumParticipants))
            return DungeonOperationResult.Failure(DungeonErrorCodes.PartyRequired);

        string? validationError = await ValidateMembersAsync(
            party,
            definition,
            cancellationToken);
        if (validationError is not null)
            return DungeonOperationResult.Failure(validationError);

        Guid lockedPartyId = party.PartyId;
        await using IDbContextTransaction? transaction =
            await BeginAdvisoryLockAsync($"party-membership:{lockedPartyId:N}", cancellationToken);
        dbContext.ChangeTracker.Clear();
        party = await partyService.GetAsync(accountId, cancellationToken);
        if (party is null || party.PartyId != lockedPartyId)
            return DungeonOperationResult.Failure(DungeonErrorCodes.PartyRequired);
        if (party.LeaderCharacterId != character.Id)
            return DungeonOperationResult.Failure(DungeonErrorCodes.NotLeader);
        validationError = await ValidateMembersAsync(party, definition, cancellationToken);
        if (validationError is not null)
            return DungeonOperationResult.Failure(validationError);
        DungeonRun? existingByRequest = await LoadRunByRequestAsync(creationRequestId, cancellationToken);
        if (existingByRequest is not null)
        {
            await CommitAsync(transaction, cancellationToken);
            return new DungeonOperationResult(true, null, ToView(existingByRequest, definition));
        }

        DungeonRun? existing = await LoadActiveRunAsync(party.PartyId, cancellationToken);
        if (existing is not null)
        {
            await CommitAsync(transaction, cancellationToken);
            return new DungeonOperationResult(true, null, ToView(existing, definition));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        DungeonRun run = DungeonRun.Create(
            Guid.CreateVersion7(),
            creationRequestId,
            party.PartyId,
            definition.Id,
            now);
        foreach (PartyMemberView member in party.Members)
            run.AddMember(member.CharacterId, now);

        DungeonEncounterDefinition first = definition.Encounters[0];
        run.Encounters.Add(DungeonEncounter.Create(
            Guid.CreateVersion7(),
            run.Id,
            0,
            first.MonsterId,
            now));
        dbContext.DungeonRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        return new DungeonOperationResult(true, null, ToView(run, definition));
    }

    public async Task<DungeonRunView?> GetCurrentAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        Character? character = await GetCharacterAsync(accountId, cancellationToken);
        if (character is null)
            return null;
        PartySnapshot? party = await partyService.GetAsync(accountId, cancellationToken);
        if (party is null)
            return null;
        DungeonRun? run = await dbContext.DungeonRuns
            .Include(candidate => candidate.Members)
            .Include(candidate => candidate.Encounters)
                .ThenInclude(encounter => encounter.Members)
            .Where(candidate => candidate.PartyId == party.PartyId
                && candidate.State == DungeonRunState.Active)
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (run is null || !contentProvider.GetCurrent().Indexes.DungeonsById
                .TryGetValue(run.DungeonId, out DungeonDefinition? definition))
            return null;
        return ToView(run, definition);
    }

    public Task<DungeonOperationResult> EnterAsync(
        Guid accountId,
        Guid runId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => EnterCoreAsync(accountId, runId, cancellationToken));

    private async Task<DungeonOperationResult> EnterCoreAsync(
        Guid accountId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction? transaction =
            await BeginAdvisoryLockAsync($"dungeon-run:{runId:N}", cancellationToken);
        DungeonDefinition? definition;
        DungeonRun? run = await LoadRunAsync(runId, cancellationToken);
        if (run is null)
            return DungeonOperationResult.Failure(DungeonErrorCodes.RunNotFound);
        if (!contentProvider.GetCurrent().Indexes.DungeonsById
                .TryGetValue(run.DungeonId, out definition))
            return DungeonOperationResult.Failure(DungeonErrorCodes.DungeonNotFound);
        if (run.State != DungeonRunState.Active)
            return DungeonOperationResult.Failure(DungeonErrorCodes.EncounterNotReady);

        PartySnapshot? party = await partyService.GetAsync(accountId, cancellationToken);
        Character? character = await GetCharacterAsync(accountId, cancellationToken);
        if (party is null || character is null || party.PartyId != run.PartyId)
            return DungeonOperationResult.Failure(DungeonErrorCodes.MemberNotInParty);
        if (!party.Members.Any(member => member.CharacterId == character.Id))
            return DungeonOperationResult.Failure(DungeonErrorCodes.MemberNotInParty);

        string? validationError = await ValidateMemberAsync(character, definition, cancellationToken);
        if (validationError is not null)
            return DungeonOperationResult.Failure(validationError);
        run.AddMember(character.Id, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        return new DungeonOperationResult(true, null, ToView(run, definition));
    }

    public Task<(DungeonPreparation? Preparation, string? ErrorCode)> PrepareEncounterAsync(
        Guid accountId,
        Guid runId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => PrepareEncounterCoreAsync(accountId, runId, cancellationToken));

    public Task<DungeonOperationResult> RestartEncounterAsync(
        Guid accountId,
        Guid runId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => RestartEncounterCoreAsync(accountId, runId, cancellationToken));

    private async Task<DungeonOperationResult> RestartEncounterCoreAsync(
        Guid accountId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction? transaction =
            await BeginAdvisoryLockAsync($"dungeon-run:{runId:N}", cancellationToken);
        DungeonRun? run = await LoadRunAsync(runId, cancellationToken);
        if (run is null)
            return DungeonOperationResult.Failure(DungeonErrorCodes.RunNotFound);
        if (!contentProvider.GetCurrent().Indexes.DungeonsById
                .TryGetValue(run.DungeonId, out DungeonDefinition? definition))
            return DungeonOperationResult.Failure(DungeonErrorCodes.DungeonNotFound);

        Character? leader = await GetCharacterAsync(accountId, cancellationToken);
        PartySnapshot? party = await partyService.GetAsync(accountId, cancellationToken);
        if (leader is null || party is null || party.PartyId != run.PartyId)
            return DungeonOperationResult.Failure(DungeonErrorCodes.MemberNotInParty);
        if (party.LeaderCharacterId != leader.Id)
            return DungeonOperationResult.Failure(DungeonErrorCodes.NotLeader);
        if (run.State != DungeonRunState.Active)
            return DungeonOperationResult.Failure(DungeonErrorCodes.EncounterNotReady);
        if (!run.Members.Any(member => member.CharacterId == leader.Id
                && member.State == DungeonRunMemberState.Active))
            return DungeonOperationResult.Failure(DungeonErrorCodes.MemberNotInRun);
        if (run.Encounters.Any(encounter => encounter.State == DungeonEncounterState.Active))
            return DungeonOperationResult.Failure(DungeonErrorCodes.EncounterActive);

        DungeonEncounter? encounter = run.Encounters.SingleOrDefault(candidate =>
            candidate.EncounterIndex == run.CurrentEncounterIndex);
        if (encounter is null || encounter.State != DungeonEncounterState.Wiped)
            return DungeonOperationResult.Failure(DungeonErrorCodes.EncounterNotReady);

        encounter.ResetForRetry();
        await dbContext.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        return new DungeonOperationResult(true, null, ToView(run, definition));
    }

    public Task<DungeonOperationResult> ExitAsync(
        Guid accountId,
        Guid runId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => ExitCoreAsync(accountId, runId, cancellationToken));

    private async Task<DungeonOperationResult> ExitCoreAsync(
        Guid accountId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction? transaction =
            await BeginAdvisoryLockAsync($"dungeon-run:{runId:N}", cancellationToken);
        DungeonRun? run = await LoadRunAsync(runId, cancellationToken);
        if (run is null)
            return DungeonOperationResult.Failure(DungeonErrorCodes.RunNotFound);
        if (!contentProvider.GetCurrent().Indexes.DungeonsById
                .TryGetValue(run.DungeonId, out DungeonDefinition? definition))
            return DungeonOperationResult.Failure(DungeonErrorCodes.DungeonNotFound);
        if (run.State != DungeonRunState.Active)
            return DungeonOperationResult.Failure(DungeonErrorCodes.EncounterNotReady);
        if (run.Encounters.Any(encounter => encounter.State == DungeonEncounterState.Active))
            return DungeonOperationResult.Failure(DungeonErrorCodes.EncounterActive);

        Character? character = await GetCharacterAsync(accountId, cancellationToken);
        if (character is null)
            return DungeonOperationResult.Failure(DungeonErrorCodes.CharacterNotFound);
        DungeonRunMember? member = run.Members.SingleOrDefault(candidate =>
            candidate.CharacterId == character.Id);
        if (member is null)
            return DungeonOperationResult.Failure(DungeonErrorCodes.MemberNotInRun);

        member.MarkLeft();
        if (run.Members.All(candidate => candidate.State == DungeonRunMemberState.Left))
            run.Abandon();
        await dbContext.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        return new DungeonOperationResult(true, null, ToView(run, definition));
    }

    private async Task<(DungeonPreparation? Preparation, string? ErrorCode)> PrepareEncounterCoreAsync(
        Guid accountId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction? transaction =
            await BeginAdvisoryLockAsync($"dungeon-run:{runId:N}", cancellationToken);
        DungeonRun? run = await LoadRunAsync(runId, cancellationToken);
        if (run is null)
            return (null, DungeonErrorCodes.RunNotFound);
        if (!contentProvider.GetCurrent().Indexes.DungeonsById
                .TryGetValue(run.DungeonId, out DungeonDefinition? definition))
            return (null, DungeonErrorCodes.DungeonNotFound);

        Character? leader = await GetCharacterAsync(accountId, cancellationToken);
        PartySnapshot? party = await partyService.GetAsync(accountId, cancellationToken);
        if (leader is null || party is null || party.PartyId != run.PartyId)
            return (null, DungeonErrorCodes.MemberNotInParty);
        if (party.LeaderCharacterId != leader.Id)
            return (null, DungeonErrorCodes.NotLeader);
        if (run.State != DungeonRunState.Active)
            return (null, DungeonErrorCodes.EncounterNotReady);
        if (!run.Members.Any(member => member.CharacterId == leader.Id
                && member.State == DungeonRunMemberState.Active))
            return (null, DungeonErrorCodes.MemberNotInRun);
        if (run.Encounters.Any(encounter => encounter.State == DungeonEncounterState.Active))
            return (null, DungeonErrorCodes.EncounterActive);

        DungeonEncounter? encounter = run.Encounters
            .SingleOrDefault(candidate => candidate.EncounterIndex == run.CurrentEncounterIndex
                && candidate.State is DungeonEncounterState.Pending or DungeonEncounterState.Wiped);
        if (encounter?.State == DungeonEncounterState.Wiped)
            encounter.ResetForRetry();
        if (encounter is null)
        {
            DungeonEncounterDefinition encounterDefinition = definition.Encounters[run.CurrentEncounterIndex];
            encounter = DungeonEncounter.Create(
                Guid.CreateVersion7(),
                run.Id,
                run.CurrentEncounterIndex,
                encounterDefinition.MonsterId,
                timeProvider.GetUtcNow());
            run.Encounters.Add(encounter);
        }

        HashSet<Guid> currentPartyMemberIds = party.Members
            .Select(member => member.CharacterId)
            .ToHashSet();
        foreach (DungeonRunMember member in run.Members
                     .Where(member => member.State == DungeonRunMemberState.Active
                         && !currentPartyMemberIds.Contains(member.CharacterId)))
        {
            member.MarkLeft();
        }
        Guid[] runMemberIds = run.Members
            .Where(member => member.State == DungeonRunMemberState.Active
                && currentPartyMemberIds.Contains(member.CharacterId))
            .Select(member => member.CharacterId)
            .Distinct()
            .ToArray();
        if (runMemberIds.Length == 0)
            return (null, DungeonErrorCodes.MemberNotInParty);

        DateTimeOffset now = timeProvider.GetUtcNow();
        Dictionary<Guid, Character> characters = await dbContext.Characters
            .Where(candidate => runMemberIds.Contains(candidate.Id))
            .ToDictionaryAsync(candidate => candidate.Id, cancellationToken);
        List<PartyCombatMember> participantList = [];
        foreach (Guid characterId in runMemberIds)
        {
            Character member = characters.GetValueOrDefault(characterId)!;
            string? error = await ValidateMemberAsync(member, definition, cancellationToken);
            if (error is not null)
                continue;
            if (!encounter.Members.Any(snapshot => snapshot.CharacterId == characterId))
                encounter.Members.Add(DungeonEncounterMember.Create(encounter.Id, characterId, now));
            participantList.Add(new PartyCombatMember(member.AccountId, member.Id, member.Id == leader.Id));
        }
        PartyCombatMember[] participants = participantList.ToArray();
        if (participants.Length == 0)
            return (null, DungeonErrorCodes.MemberCannotEnter);

        await dbContext.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        return (new DungeonPreparation(
            run.Id,
            encounter.Id,
            encounter.MonsterId,
            definition.EntryLocationId,
            participants), null);
    }

    public Task<bool> BindCombatAsync(
        Guid runId,
        Guid encounterId,
        Guid combatSessionId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => BindCombatCoreAsync(runId, encounterId, combatSessionId, cancellationToken));

    private async Task<bool> BindCombatCoreAsync(
        Guid runId,
        Guid encounterId,
        Guid combatSessionId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction? transaction =
            await BeginAdvisoryLockAsync($"dungeon-run:{runId:N}", cancellationToken);
        DungeonEncounter? encounter = await dbContext.DungeonEncounters
            .SingleOrDefaultAsync(candidate => candidate.Id == encounterId
                && candidate.RunId == runId && candidate.Run!.State == DungeonRunState.Active, cancellationToken);
        if (encounter is null || encounter.State != DungeonEncounterState.Pending)
            return false;
        encounter.Activate(combatSessionId);
        await dbContext.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        return true;
    }

    public Task HandleCombatFinishedAsync(
        CombatSessionSnapshot snapshot,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => HandleCombatFinishedCoreAsync(snapshot, cancellationToken));

    private async Task HandleCombatFinishedCoreAsync(
        CombatSessionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction? transaction =
            await BeginAdvisoryLockAsync($"dungeon-session:{snapshot.SessionId:N}", cancellationToken);
        DungeonEncounter? encounter = await dbContext.DungeonEncounters
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.CombatSessionId == snapshot.SessionId, cancellationToken);
        if (encounter is null || encounter.State != DungeonEncounterState.Active)
            return;

        if (dbContext.Database.IsNpgsql())
        {
            string runLock = $"dungeon-run:{encounter.RunId:N}";
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({runLock}))", cancellationToken);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (snapshot.Status == CombatSessionStatus.Victory)
        {
            int completed = await dbContext.DungeonEncounters
                .Where(candidate => candidate.Id == encounter.Id
                    && candidate.State == DungeonEncounterState.Active
                    && candidate.CombatSessionId == snapshot.SessionId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(candidate => candidate.State, DungeonEncounterState.Completed)
                    .SetProperty(candidate => candidate.CompletedAtUtc, now), cancellationToken);
            if (completed == 0)
                return;

            DungeonRun run = await dbContext.DungeonRuns
                .SingleAsync(candidate => candidate.Id == encounter.RunId, cancellationToken);
            if (run.State != DungeonRunState.Active)
            {
                await CommitAsync(transaction, cancellationToken);
                return;
            }
            DungeonDefinition definition = contentProvider.GetCurrent().Indexes.DungeonsById[run.DungeonId];
            run.AdvanceEncounter(now);
            if (run.CurrentEncounterIndex >= definition.Encounters.Count)
            {
                run.Complete(now);
            }
            else
            {
                DungeonEncounterDefinition next = definition.Encounters[run.CurrentEncounterIndex];
                dbContext.DungeonEncounters.Add(DungeonEncounter.Create(
                    Guid.CreateVersion7(),
                    run.Id,
                    run.CurrentEncounterIndex,
                    next.MonsterId,
                    now));
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            await dbContext.DungeonEncounters
                .Where(candidate => candidate.Id == encounter.Id
                    && candidate.State == DungeonEncounterState.Active
                    && candidate.CombatSessionId == snapshot.SessionId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(candidate => candidate.State, DungeonEncounterState.Wiped)
                    .SetProperty(candidate => candidate.WipeCount, candidate => candidate.WipeCount + 1), cancellationToken);
        }
        await CommitAsync(transaction, cancellationToken);
    }

    private async Task<string?> ValidateMembersAsync(
        PartySnapshot party,
        DungeonDefinition definition,
        CancellationToken cancellationToken)
    {
        foreach (PartyMemberView member in party.Members)
        {
            Character? character = await dbContext.Characters
                .SingleOrDefaultAsync(candidate => candidate.Id == member.CharacterId, cancellationToken);
            if (character is null)
                return DungeonErrorCodes.CharacterNotFound;
            string? error = await ValidateMemberAsync(character, definition, cancellationToken);
            if (error is not null)
                return error;
        }
        return null;
    }

    private async Task<string?> ValidateMemberAsync(
        Character character,
        DungeonDefinition definition,
        CancellationToken cancellationToken)
    {
        CharacterLocation? location = await dbContext.CharacterLocations
            .SingleOrDefaultAsync(candidate => candidate.CharacterId == character.Id, cancellationToken);
        if (location is null || !string.Equals(location.LocationId, definition.EntryLocationId, StringComparison.Ordinal))
            return DungeonErrorCodes.InvalidLocation;
        if (character.Level < definition.MinimumLevel || character.Level > definition.MaximumLevel)
            return DungeonErrorCodes.LevelRequired;
        CharacterVitals? vitals = await dbContext.CharacterVitals
            .SingleOrDefaultAsync(candidate => candidate.CharacterId == character.Id, cancellationToken);
        if (vitals is null || vitals.CurrentHp <= 0)
            return DungeonErrorCodes.MemberCannotEnter;
        if (await dbContext.CharacterTravelStates.AnyAsync(
                travel => travel.CharacterId == character.Id,
                cancellationToken))
            return DungeonErrorCodes.InvalidLocation;
        return null;
    }

    private async Task<DungeonRun?> LoadRunAsync(Guid runId, CancellationToken cancellationToken) =>
        await dbContext.DungeonRuns
            .Include(run => run.Members)
            .Include(run => run.Encounters)
                .ThenInclude(encounter => encounter.Members)
            .SingleOrDefaultAsync(run => run.Id == runId, cancellationToken);

    private async Task<DungeonRun?> LoadActiveRunAsync(Guid partyId, CancellationToken cancellationToken) =>
        await dbContext.DungeonRuns
            .Include(run => run.Members)
            .Include(run => run.Encounters)
                .ThenInclude(encounter => encounter.Members)
            .SingleOrDefaultAsync(run => run.PartyId == partyId && run.State == DungeonRunState.Active, cancellationToken);

    private async Task<DungeonRun?> LoadRunByRequestAsync(
        Guid creationRequestId,
        CancellationToken cancellationToken) =>
        await dbContext.DungeonRuns
            .Include(run => run.Members)
            .Include(run => run.Encounters)
                .ThenInclude(encounter => encounter.Members)
            .SingleOrDefaultAsync(run => run.CreationRequestId == creationRequestId, cancellationToken);

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

    private async Task<Character?> GetCharacterAsync(Guid accountId, CancellationToken cancellationToken) =>
        await dbContext.Characters.SingleOrDefaultAsync(
            character => character.AccountId == accountId,
            cancellationToken);

    private static DungeonRunView ToView(DungeonRun run, DungeonDefinition definition) =>
        new(
            run.Id,
            run.DungeonId,
            definition.DisplayName,
            definition.Description,
            run.State,
            run.CurrentEncounterIndex,
            ResolveCurrentCheckpointId(run, definition),
            definition.Encounters.Count,
            run.PartyId,
            run.Members
                .OrderBy(member => member.JoinedAtUtc)
                .Select(member => new DungeonRunMemberView(
                    member.CharacterId,
                    member.State,
                    member.JoinedAtUtc))
                .ToArray(),
            run.Encounters
                .OrderBy(encounter => encounter.EncounterIndex)
                .Select(encounter => new DungeonEncounterView(
                    encounter.Id,
                    encounter.EncounterIndex,
                    encounter.MonsterId,
                    encounter.State,
                    encounter.WipeCount,
                    encounter.Members.Select(member => member.CharacterId).ToArray()))
                .ToArray());

    private static string ResolveCurrentCheckpointId(
        DungeonRun run,
        DungeonDefinition definition)
    {
        if (definition.Encounters.Count == 0)
            return string.Empty;

        int checkpointIndex = Math.Min(
            Math.Max(run.CurrentEncounterIndex, 0),
            definition.Encounters.Count - 1);
        return definition.Encounters[checkpointIndex].CheckpointId;
    }
}
