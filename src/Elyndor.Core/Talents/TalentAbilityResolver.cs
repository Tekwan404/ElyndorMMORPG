using Elyndor.Core.Combat.Abilities;

namespace Elyndor.Core.Talents;

public static class TalentAbilityResolver
{
    public static AbilityDefinition Apply(
        AbilityDefinition definition,
        ResolvedTalentModifiers talents)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(talents);

        if (!talents.Abilities.TryGetValue(definition.Id, out TalentAbilityModifiers? modifier))
        {
            return definition;
        }

        decimal percentMultiplier = Math.Max(0, 1 - modifier.ResourceCostPercentReduction / 100m);
        decimal effectiveCost = Math.Max(
            0,
            definition.ResourceCost * percentMultiplier - modifier.ResourceCostFlatReduction);
        TimeSpan effectiveCooldown = definition.Cooldown
            - TimeSpan.FromSeconds((double)modifier.CooldownSecondsReduction);
        if (effectiveCooldown < TimeSpan.Zero) effectiveCooldown = TimeSpan.Zero;

        IReadOnlyList<AbilityActionDefinition>? actions = definition.Actions?.Select(action =>
        {
            decimal coefficient = action.Type == AbilityActionType.Damage
                ? action.AttackPowerCoefficient * (1 + modifier.DamagePercentBonus / 100m)
                : action.AttackPowerCoefficient;
            var effect = action.Effect;
            if (effect is not null)
            {
                effect = effect with
                {
                    Duration = effect.Duration
                        + TimeSpan.FromSeconds((double)modifier.EffectDurationSecondsBonus),
                    Magnitude = effect.Magnitude * (1 + modifier.EffectMagnitudePercentBonus / 100m)
                };
            }

            return action with
            {
                AttackPowerCoefficient = coefficient,
                ArmorPenetrationBonus = action.ArmorPenetrationBonus
                    + modifier.ArmorPenetrationPercent / 100m,
                Effect = effect
            };
        }).ToArray();

        return definition with
        {
            ResourceCost = effectiveCost,
            Cooldown = effectiveCooldown,
            Actions = actions
        };
    }
}
