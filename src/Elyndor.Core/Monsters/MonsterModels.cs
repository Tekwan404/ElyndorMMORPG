using Elyndor.Core.Combat;

namespace Elyndor.Core.Monsters;

public enum MonsterRank
{
    Normal,
    Elite,
    Boss
}

public sealed record MonsterDefinition(
    string Id,
    string Name,
    MonsterRank Rank,
    int Level,
    decimal MaxHp,
    CombatStats Stats,
    TimeSpan AutoAttackInterval,
    decimal AutoAttackBaseDamage,
    IReadOnlyList<string> AbilityIds,
    string AiProfileId,
    int Version = 1,
    decimal AutoAttackAttackPowerCoefficient = 0.5m,
    int XpReward = 0,
    string? LootTableId = null,
    int GoldRewardMin = 0,
    int GoldRewardMax = 0);

public sealed record MonsterAiProfile(
    string Id,
    IReadOnlyList<string> PriorityAbilityIds,
    int Version = 1);
