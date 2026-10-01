namespace Elyndor.Core.Combat.Effects;

/// <summary>
/// A null definition rejects application with EffectImmune. OnApplied commits policy
/// state only after the effect is added or refreshed, under the combat single writer.
/// </summary>
public sealed record EffectApplicationPolicyResult(
    EffectDefinition? Definition,
    Action? OnApplied = null);
