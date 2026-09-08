using Elyndor.Core.Content;

namespace Elyndor.Core.Dungeons;

public sealed record DungeonDefinition(
    string Id,
    string DisplayName,
    string Description,
    int MinimumLevel,
    int MaximumLevel,
    string EntryLocationId,
    IReadOnlyList<DungeonEncounterDefinition> Encounters,
    int MinimumPartySize = 1,
    int MaximumPartySize = 5);

public sealed record DungeonEncounterDefinition(
    string Id,
    string MonsterId,
    string CheckpointId,
    bool IsBoss = false);

public enum DungeonRunState
{
    Active,
    Completed,
    Abandoned
}

public enum DungeonEncounterState
{
    Pending,
    Active,
    Wiped,
    Completed
}

public enum DungeonRunMemberState
{
    Active,
    Left
}

public sealed class DungeonRun
{
    private DungeonRun()
    {
        DungeonId = null!;
    }

    private DungeonRun(
        Guid id,
        Guid creationRequestId,
        Guid partyId,
        string dungeonId,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || creationRequestId == Guid.Empty || partyId == Guid.Empty)
            throw new ArgumentException("Dungeon run identifiers cannot be empty.");
        ArgumentException.ThrowIfNullOrWhiteSpace(dungeonId);
        EnsureUtc(createdAtUtc);

        Id = id;
        CreationRequestId = creationRequestId;
        PartyId = partyId;
        DungeonId = dungeonId;
        CreatedAtUtc = createdAtUtc;
        State = DungeonRunState.Active;
        CurrentEncounterIndex = 0;
    }

    public Guid Id { get; private set; }
    public Guid CreationRequestId { get; private set; }
    public Guid PartyId { get; private set; }
    public string DungeonId { get; private set; }
    public DungeonRunState State { get; private set; }
    public int CurrentEncounterIndex { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public List<DungeonRunMember> Members { get; private set; } = [];
    public List<DungeonEncounter> Encounters { get; private set; } = [];

    public static DungeonRun Create(
        Guid id,
        Guid creationRequestId,
        Guid partyId,
        string dungeonId,
        DateTimeOffset createdAtUtc) =>
        new(id, creationRequestId, partyId, dungeonId, createdAtUtc);

    public void AddMember(Guid characterId, DateTimeOffset joinedAtUtc)
    {
        EnsureActive();
        if (characterId == Guid.Empty)
            throw new ArgumentException("Character identifier cannot be empty.", nameof(characterId));
        DungeonRunMember? existing = Members.SingleOrDefault(member => member.CharacterId == characterId);
        if (existing is not null)
        {
            existing.Rejoin(joinedAtUtc);
            return;
        }
        Members.Add(DungeonRunMember.Create(Id, characterId, joinedAtUtc));
    }

    public void AdvanceEncounter(DateTimeOffset completedAtUtc)
    {
        EnsureActive();
        EnsureUtc(completedAtUtc);
        CurrentEncounterIndex++;
    }

    public void Complete(DateTimeOffset completedAtUtc)
    {
        EnsureActive();
        EnsureUtc(completedAtUtc);
        State = DungeonRunState.Completed;
        CompletedAtUtc = completedAtUtc;
    }

    public void Abandon()
    {
        EnsureActive();
        State = DungeonRunState.Abandoned;
    }

    private void EnsureActive()
    {
        if (State != DungeonRunState.Active)
            throw new InvalidOperationException("Dungeon run is not active.");
    }

    private static void EnsureUtc(DateTimeOffset timestamp)
    {
        if (timestamp.Offset != TimeSpan.Zero)
            throw new ArgumentException("Dungeon timestamps must be UTC.", nameof(timestamp));
    }
}

public sealed class DungeonRunMember
{
    private DungeonRunMember()
    {
    }

    private DungeonRunMember(Guid runId, Guid characterId, DateTimeOffset joinedAtUtc)
    {
        RunId = runId;
        CharacterId = characterId;
        JoinedAtUtc = joinedAtUtc;
        State = DungeonRunMemberState.Active;
    }

    public Guid RunId { get; private set; }
    public Guid CharacterId { get; private set; }
    public DateTimeOffset JoinedAtUtc { get; private set; }
    public DungeonRunMemberState State { get; private set; }

    public static DungeonRunMember Create(Guid runId, Guid characterId, DateTimeOffset joinedAtUtc) =>
        new(runId, characterId, joinedAtUtc);

    public void MarkLeft()
    {
        State = DungeonRunMemberState.Left;
    }

    public void Rejoin(DateTimeOffset joinedAtUtc)
    {
        if (joinedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Dungeon timestamps must be UTC.", nameof(joinedAtUtc));
        State = DungeonRunMemberState.Active;
    }
}

public sealed class DungeonEncounter
{
    private DungeonEncounter()
    {
        MonsterId = null!;
    }

    private DungeonEncounter(
        Guid id,
        Guid runId,
        int encounterIndex,
        string monsterId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        RunId = runId;
        EncounterIndex = encounterIndex;
        MonsterId = monsterId;
        CreatedAtUtc = createdAtUtc;
        State = DungeonEncounterState.Pending;
    }

    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public int EncounterIndex { get; private set; }
    public string MonsterId { get; private set; }
    public DungeonEncounterState State { get; private set; }
    public Guid? CombatSessionId { get; private set; }
    public int WipeCount { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public List<DungeonEncounterMember> Members { get; private set; } = [];
    public DungeonRun? Run { get; private set; }

    public static DungeonEncounter Create(
        Guid id,
        Guid runId,
        int encounterIndex,
        string monsterId,
        DateTimeOffset createdAtUtc) =>
        new(id, runId, encounterIndex, monsterId, createdAtUtc);

    public void Activate(Guid combatSessionId)
    {
        if (State != DungeonEncounterState.Pending)
            throw new InvalidOperationException("Only a pending dungeon encounter can start.");
        if (combatSessionId == Guid.Empty)
            throw new ArgumentException("Combat session identifier cannot be empty.", nameof(combatSessionId));
        State = DungeonEncounterState.Active;
        CombatSessionId = combatSessionId;
    }

    public void MarkWiped()
    {
        if (State != DungeonEncounterState.Active)
            return;
        State = DungeonEncounterState.Wiped;
        WipeCount++;
    }

    public void ResetForRetry()
    {
        if (State != DungeonEncounterState.Wiped)
            throw new InvalidOperationException("Only a wiped dungeon encounter can be retried.");
        State = DungeonEncounterState.Pending;
        CombatSessionId = null;
        Members.Clear();
    }

    public void MarkCompleted(DateTimeOffset completedAtUtc)
    {
        if (State == DungeonEncounterState.Completed)
            return;
        if (State != DungeonEncounterState.Active)
            throw new InvalidOperationException("Only an active dungeon encounter can complete.");
        if (completedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Dungeon timestamps must be UTC.", nameof(completedAtUtc));
        State = DungeonEncounterState.Completed;
        CompletedAtUtc = completedAtUtc;
    }
}

public sealed class DungeonEncounterMember
{
    private DungeonEncounterMember()
    {
    }

    private DungeonEncounterMember(
        Guid encounterId,
        Guid characterId,
        DateTimeOffset joinedAtUtc)
    {
        EncounterId = encounterId;
        CharacterId = characterId;
        JoinedAtUtc = joinedAtUtc;
    }

    public Guid EncounterId { get; private set; }
    public Guid CharacterId { get; private set; }
    public DateTimeOffset JoinedAtUtc { get; private set; }

    public static DungeonEncounterMember Create(
        Guid encounterId,
        Guid characterId,
        DateTimeOffset joinedAtUtc) =>
        new(encounterId, characterId, joinedAtUtc);
}
