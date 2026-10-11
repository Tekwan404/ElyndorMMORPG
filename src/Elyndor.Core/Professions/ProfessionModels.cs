namespace Elyndor.Core.Professions;

public static class ProfessionIds
{
    public const string Skinning = "SKINNING";
    public const string Leatherworking = "LEATHERWORKING";
    public const string Mining = "MINING";
    public const string Blacksmithing = "BLACKSMITHING";
    public const string Enchanting = "ENCHANTING";
}

public enum ProfessionCategory
{
    Gathering,
    Production
}

public sealed record ProfessionDefinition(
    string Id,
    string Name,
    ProfessionCategory Category,
    int MaxSkill = 300,
    string? RequiredLocationId = null);

public sealed record SkinningSourceDefinition(
    string Id,
    string MonsterId,
    int RequiredSkill,
    int SkillUpUntil,
    string ItemId,
    int MinQuantity,
    int MaxQuantity);

public sealed record ProfessionRecipeIngredient(
    string ItemId,
    int Quantity);

public sealed record ProfessionGatheringNodeDefinition(
    string Id, string Name, string LocationId, int RequiredSkill, int SkillUpUntil,
    string ItemId, int Quantity, int CooldownSeconds);

public sealed record ProfessionEnchantmentDefinition(
    string Id, string Name, string ScrollItemId, string StatId, decimal Value,
    IReadOnlyList<Elyndor.Core.Items.EquipmentSlot> Slots);

public sealed record ProfessionDisenchantingDefinition(
    Elyndor.Core.Items.ItemRarity Rarity, string ItemId, int Quantity);

public sealed record ProfessionRecipeDefinition(
    string Id,
    string ProfessionId,
    string Name,
    int RequiredSkill,
    int SkillUpUntil,
    string OutputItemId,
    int OutputQuantity,
    IReadOnlyList<ProfessionRecipeIngredient> Ingredients,
    string? RequiredLocationId = null);

public sealed class CharacterProfession
{
    private CharacterProfession()
    {
        ProfessionId = null!;
    }

    public CharacterProfession(
        Guid characterId,
        string professionId,
        DateTimeOffset learnedAtUtc)
    {
        if (characterId == Guid.Empty)
            throw new ArgumentException("Character identifier cannot be empty.", nameof(characterId));
        ArgumentException.ThrowIfNullOrWhiteSpace(professionId);
        if (learnedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Profession timestamps must be UTC.", nameof(learnedAtUtc));

        CharacterId = characterId;
        ProfessionId = professionId;
        Skill = 1;
        LearnedAtUtc = learnedAtUtc;
        UpdatedAtUtc = learnedAtUtc;
    }

    public Guid CharacterId { get; private set; }
    public string ProfessionId { get; private set; }
    public int Skill { get; private set; }
    public DateTimeOffset LearnedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? NextGatherAtUtc { get; private set; }

    public void CompleteGathering(DateTimeOffset now, int cooldownSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(cooldownSeconds, 1);
        if (now.Offset != TimeSpan.Zero) throw new ArgumentException("Gathering timestamps must be UTC.");
        if (NextGatherAtUtc > now) throw new InvalidOperationException("Gathering is on cooldown.");
        NextGatherAtUtc = now.AddSeconds(cooldownSeconds);
        UpdatedAtUtc = now;
    }

    public bool TryIncreaseSkill(int maximumSkill, DateTimeOffset now)
    {
        if (Skill >= maximumSkill)
            return false;
        Skill++;
        UpdatedAtUtc = now;
        return true;
    }
}

public sealed class SkinnableCorpse
{
    private SkinnableCorpse()
    {
        MonsterDefinitionId = null!;
    }

    public SkinnableCorpse(
        Guid characterId,
        Guid combatSessionId,
        Guid enemyActorId,
        string monsterDefinitionId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (characterId == Guid.Empty || combatSessionId == Guid.Empty || enemyActorId == Guid.Empty)
            throw new ArgumentException("Skinning corpse identifiers cannot be empty.");
        ArgumentException.ThrowIfNullOrWhiteSpace(monsterDefinitionId);
        if (createdAtUtc.Offset != TimeSpan.Zero || expiresAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Skinning corpse timestamps must be UTC.");
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAtUtc, createdAtUtc);

        CharacterId = characterId;
        CombatSessionId = combatSessionId;
        EnemyActorId = enemyActorId;
        MonsterDefinitionId = monsterDefinitionId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid CharacterId { get; private set; }
    public Guid CombatSessionId { get; private set; }
    public Guid EnemyActorId { get; private set; }
    public string MonsterDefinitionId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? SkinnedAtUtc { get; private set; }
    public Guid? SkinningMutationId { get; private set; }
    public string? YieldItemId { get; private set; }
    public int? YieldQuantity { get; private set; }
    public bool SkillIncreased { get; private set; }

    public void MarkSkinned(
        Guid mutationId,
        string itemId,
        int quantity,
        bool skillIncreased,
        DateTimeOffset now)
    {
        if (SkinnedAtUtc.HasValue)
        {
            if (SkinningMutationId == mutationId)
                return;
            throw new InvalidOperationException("Corpse has already been skinned.");
        }
        if (mutationId == Guid.Empty)
            throw new ArgumentException("Mutation identifier cannot be empty.", nameof(mutationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);

        SkinningMutationId = mutationId;
        YieldItemId = itemId;
        YieldQuantity = quantity;
        SkillIncreased = skillIncreased;
        SkinnedAtUtc = now;
    }
}
