using Elyndor.Core.Items;

namespace Elyndor.Core.Combat;

public enum CombatLootRollState
{
    Open,
    Resolved
}

public sealed class CombatLootRoll
{
    private CombatLootRoll()
    {
        ItemDefinitionId = null!;
        EligibleCharacterIdsJson = null!;
        ChoicesJson = "{}";
        RollsJson = "{}";
    }

    public CombatLootRoll(
        Guid lootRollId,
        Guid dungeonRunId,
        Guid combatSessionId,
        string itemDefinitionId,
        ItemRarity rarity,
        int quantity,
        Guid itemInstanceSeed,
        DateTimeOffset endsAtUtc,
        string eligibleCharacterIdsJson,
        string sourceQualityProfileId = "NORMAL",
        string? generatedItemJson = null)
    {
        if (lootRollId == Guid.Empty || dungeonRunId == Guid.Empty || combatSessionId == Guid.Empty)
            throw new ArgumentException("Loot roll identifiers cannot be empty.");
        ArgumentException.ThrowIfNullOrWhiteSpace(itemDefinitionId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        if (itemInstanceSeed == Guid.Empty)
            throw new ArgumentException("Item instance seed cannot be empty.", nameof(itemInstanceSeed));
        if (endsAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Loot roll timestamps must be UTC.", nameof(endsAtUtc));
        ArgumentException.ThrowIfNullOrWhiteSpace(eligibleCharacterIdsJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceQualityProfileId);

        LootRollId = lootRollId;
        DungeonRunId = dungeonRunId;
        CombatSessionId = combatSessionId;
        ItemDefinitionId = itemDefinitionId;
        Rarity = rarity;
        Quantity = quantity;
        ItemInstanceSeed = itemInstanceSeed;
        EndsAtUtc = endsAtUtc;
        EligibleCharacterIdsJson = eligibleCharacterIdsJson;
        SourceQualityProfileId = sourceQualityProfileId;
        GeneratedItemJson = generatedItemJson;
        ChoicesJson = "{}";
        RollsJson = "{}";
        State = CombatLootRollState.Open;
    }

    public Guid LootRollId { get; private set; }
    public Guid DungeonRunId { get; private set; }
    public Guid CombatSessionId { get; private set; }
    public string ItemDefinitionId { get; private set; }
    public ItemRarity Rarity { get; private set; }
    public int Quantity { get; private set; }
    public Guid ItemInstanceSeed { get; private set; }
    public string EligibleCharacterIdsJson { get; private set; }
    public string SourceQualityProfileId { get; private set; } = "NORMAL";
    public string? GeneratedItemJson { get; private set; }
    public string ChoicesJson { get; private set; }
    public string RollsJson { get; private set; }
    public DateTimeOffset EndsAtUtc { get; private set; }
    public CombatLootRollState State { get; private set; }
    public Guid? WinnerCharacterId { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public void SetGeneratedItemJson(string generatedItemJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(generatedItemJson);
        if (GeneratedItemJson is null)
            GeneratedItemJson = generatedItemJson;
    }

    public void Resolve(
        Guid? winnerCharacterId,
        string choicesJson,
        string rollsJson,
        DateTimeOffset resolvedAtUtc)
    {
        if (State != CombatLootRollState.Open)
            return;
        ArgumentException.ThrowIfNullOrWhiteSpace(choicesJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(rollsJson);
        if (resolvedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Loot roll timestamps must be UTC.", nameof(resolvedAtUtc));

        State = CombatLootRollState.Resolved;
        WinnerCharacterId = winnerCharacterId;
        ChoicesJson = choicesJson;
        RollsJson = rollsJson;
        ResolvedAtUtc = resolvedAtUtc;
    }

    public void RecordChoice(string choicesJson)
    {
        if (State != CombatLootRollState.Open)
            return;
        ArgumentException.ThrowIfNullOrWhiteSpace(choicesJson);
        ChoicesJson = choicesJson;
    }
}
