using Elyndor.Core.Combat.Effects;

namespace Elyndor.Core.Combat.SetPassives;

public static class SetPassiveCatalog
{
    public const string AncientMineGuardianSetId = "SET_ANCIENT_MINE_WARRIOR_GUARDIAN";
    public const string AncientMineGuardianTwoPieceId =
        "SET_ANCIENT_MINE_WARRIOR_GUARDIAN_2PC_PASSIVE";
    public const string AncientMineGuardianFourPieceId =
        "SET_ANCIENT_MINE_WARRIOR_GUARDIAN_4PC_PASSIVE";

    private static readonly TimeSpan BlockArmorDuration = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan BlockAbsorbDuration = TimeSpan.FromSeconds(8);

    public static IReadOnlyList<SetPassiveDefinition> Definitions { get; } =
    [
        new(
            AncientMineGuardianTwoPieceId,
            AncientMineGuardianSetId,
            RequiredPieces: 2,
            new SetPassiveTriggerDefinition(
                CombatEventType.DamageBlocked,
                SetPassiveActorRole.Target),
            new SetPassiveConditionDefinition(),
            [
                new SetPassiveActionDefinition(
                    SetPassiveActionKind.ApplyEffect,
                    "EFFECT_GUARDIAN_BLOCK_ARMOR",
                    Magnitude: 0.12m,
                    Duration: BlockArmorDuration,
                    ModifiedStat: EffectStat.Armor,
                    ModifierMode: EffectModifierMode.Percent,
                    StackPolicy: EffectStackPolicy.Refresh,
                    DisplayName: "Стойка Хранителя",
                    Description: "Блок повышает броню на 12% на 6 секунд.",
                    IconId: "effect_guardian_block_armor")
            ]),
        new(
            AncientMineGuardianFourPieceId,
            AncientMineGuardianSetId,
            RequiredPieces: 4,
            new SetPassiveTriggerDefinition(
                CombatEventType.DamageBlocked,
                SetPassiveActorRole.Target),
            new SetPassiveConditionDefinition(EveryNth: 3),
            [
                // Replace is deliberate for an absorb proc: a new proc creates a fresh
                // shield instead of extending a partially consumed shield indefinitely.
                new SetPassiveActionDefinition(
                    SetPassiveActionKind.AddShield,
                    "SHIELD_GUARDIAN_THIRD_BLOCK",
                    Magnitude: 0.06m,
                    Duration: BlockAbsorbDuration,
                    StackPolicy: EffectStackPolicy.Replace,
                    ScaleWithMaxHp: true,
                    DisplayName: "Оплот Хранителя",
                    Description: "Каждый третий блок поглощает урон, равный 6% максимального здоровья, в течение 8 секунд.",
                    IconId: "shield_guardian_third_block")
            ])
    ];
}
