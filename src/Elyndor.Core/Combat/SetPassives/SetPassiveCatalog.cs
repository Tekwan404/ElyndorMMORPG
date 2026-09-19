using Elyndor.Core.Combat.Effects;

namespace Elyndor.Core.Combat.SetPassives;

public static class SetPassiveCatalog
{
    public const string AncientMineGuardianSetId = "SET_ANCIENT_MINE_WARRIOR_GUARDIAN";
    public const string AncientMineGuardianTwoPieceId =
        "SET_ANCIENT_MINE_WARRIOR_GUARDIAN_2PC_PASSIVE";
    public const string AncientMineGuardianFourPieceId =
        "SET_ANCIENT_MINE_WARRIOR_GUARDIAN_4PC_PASSIVE";

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
                // Behaviour is production-ready, but balance values are intentionally
                // left unset until the dedicated set-passive balance pass.
                new SetPassiveActionDefinition(
                    SetPassiveActionKind.ApplyEffect,
                    "EFFECT_GUARDIAN_BLOCK_ARMOR",
                    Magnitude: 0m,
                    Duration: null,
                    ModifiedStat: EffectStat.Armor,
                    ModifierMode: EffectModifierMode.Percent,
                    StackPolicy: EffectStackPolicy.Refresh)
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
                // Magnitude/duration stay unset until balance supplies them.
                new SetPassiveActionDefinition(
                    SetPassiveActionKind.AddShield,
                    "SHIELD_GUARDIAN_THIRD_BLOCK",
                    Magnitude: 0m,
                    Duration: null,
                    StackPolicy: EffectStackPolicy.Replace)
            ])
    ];
}
