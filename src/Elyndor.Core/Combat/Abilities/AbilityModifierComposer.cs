using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Abilities;

// Composition only: stages read current class state and return immutable definitions.
// Effect synchronization, target selection and execution stay with their owners.
internal sealed class AbilityModifierComposer(AbilityModifierStages stages)
{
    public AbilityDefinition Compose(AbilityDefinition baseAbility, AbilityModifierContext context)
    {
        AbilityDefinition ability = TalentAbilityResolver.Apply(baseAbility, context.Talents);
        ability = stages.Berserker(ability, context.Now);
        ability = stages.Paladin(ability, context.Now);
        ability = stages.Warlord(ability, context.Now);
        ability = stages.Pyromancer(ability, context.Now);
        if (string.Equals(context.ClassId, "MAGE", StringComparison.Ordinal) && ability.IsSpell)
        {
            ability = stages.ArcaneMage(ability, context.Now);
            ability = stages.FrostMage(ability, context.Now);
        }
        return stages.Archer(ability, context.Now);
    }

    public AbilityTargetModifier ComposeTarget(AbilityDefinition ability, CombatActorState target, DateTimeOffset now)
    {
        AbilityTargetModifier modifier = new();
        modifier = stages.BerserkerTarget(ability, target, modifier, now);
        modifier = stages.PyromancerTarget(ability, target, modifier, now);
        modifier = stages.MageTarget(ability, target, modifier, now);
        return stages.ArcherTarget(ability, target, modifier, now);
    }
}

internal readonly record struct AbilityModifierContext(
    ResolvedTalentModifiers Talents, string ClassId, DateTimeOffset Now);

internal sealed record AbilityModifierStages(
    Func<AbilityDefinition, DateTimeOffset, AbilityDefinition> Berserker,
    Func<AbilityDefinition, DateTimeOffset, AbilityDefinition> Paladin,
    Func<AbilityDefinition, DateTimeOffset, AbilityDefinition> Warlord,
    Func<AbilityDefinition, DateTimeOffset, AbilityDefinition> Pyromancer,
    Func<AbilityDefinition, DateTimeOffset, AbilityDefinition> ArcaneMage,
    Func<AbilityDefinition, DateTimeOffset, AbilityDefinition> FrostMage,
    Func<AbilityDefinition, DateTimeOffset, AbilityDefinition> Archer,
    Func<AbilityDefinition, CombatActorState, AbilityTargetModifier, DateTimeOffset, AbilityTargetModifier> BerserkerTarget,
    Func<AbilityDefinition, CombatActorState, AbilityTargetModifier, DateTimeOffset, AbilityTargetModifier> PyromancerTarget,
    Func<AbilityDefinition, CombatActorState, AbilityTargetModifier, DateTimeOffset, AbilityTargetModifier> MageTarget,
    Func<AbilityDefinition, CombatActorState, AbilityTargetModifier, DateTimeOffset, AbilityTargetModifier> ArcherTarget);
