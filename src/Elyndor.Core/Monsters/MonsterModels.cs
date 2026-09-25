using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;

namespace Elyndor.Core.Monsters;

public enum MonsterRank
{
    Normal,
    Elite,
    Boss
}

public enum MonsterAiState
{
    Idle,
    Engaged,
    InCombat,
    Dead,
    Resetting
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
    int GoldRewardMax = 0,
    string? DisplayName = null,
    string Description = "",
    string? ArtId = null,
    decimal? AutoAttackBaseDamageMin = null,
    decimal? AutoAttackBaseDamageMax = null,
    string? SummonMonsterId = null,
    decimal SummonIntervalSeconds = 0,
    int SummonCount = 0,
    int MaxActiveSummons = 0);

public sealed record MonsterAbilityRule(
    string AbilityId,
    int Priority = 0,
    decimal? MinHpPercent = null,
    decimal? MaxHpPercent = null,
    AbilityTargetSelectorProfile TargetSelector = AbilityTargetSelectorProfile.EncounterOrder,
    bool OncePerCombat = false,
    TimeSpan? InitialDelay = null,
    TimeSpan? CooldownJitter = null,
    string? RequiredEffectId = null,
    string? ForbiddenEffectId = null,
    decimal? TargetMinHpPercent = null,
    decimal? TargetMaxHpPercent = null);

public sealed record MonsterAiProfile(
    string Id,
    IReadOnlyList<string> PriorityAbilityIds,
    int Version = 1,
    IReadOnlyList<MonsterAbilityRule>? AbilityRules = null);
