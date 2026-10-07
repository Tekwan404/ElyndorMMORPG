using Elyndor.Core.Combat.Abilities;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private AbilityModifierComposer? _abilityComposer;

    // Bind adapters once, but read the active participant at each call. Party commands
    // and snapshots must not retain the first player's talents or runtime conditions.
    private AbilityModifierComposer AbilityComposer => _abilityComposer ??= new(new AbilityModifierStages(
        ResolveGuardianAbility,
        ResolveBerserkerAbility,
        ResolvePaladinAbility,
        ResolveWarlordAbility,
        ResolvePyromancerAbility,
        ResolveArcaneMageAbility,
        ResolveFrostMageAbility,
        ResolveArcherAbility,
        (ability, target, modifier, _) => ResolveBerserkerTargetAbilityModifier(ability, target, modifier),
        ResolvePyromancerTargetAbilityModifier,
        ResolveMageTargetAbilityModifier,
        ResolveArcherTargetAbilityModifier));

    private AbilityDefinition ComposePlayerAbility(AbilityDefinition baseAbility, DateTimeOffset now) =>
        AbilityComposer.Compose(baseAbility, new AbilityModifierContext(_playerTalents, _player.DefinitionId, now));
}
