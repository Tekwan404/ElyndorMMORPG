using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Resources;

namespace Elyndor.Core.Combat.SetPassives;

/// <summary>Adapts captured content passives to the existing event evaluator and effect engine.</summary>
public sealed class SetPassiveCombatRuntime
{
    private readonly SetPassiveDefinition[] _definitions;
    private readonly SetPassiveRuntime _runtime;
    private readonly IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, int>> _pieces;
    private readonly Func<Guid, CombatActorState?> _actor;
    private readonly IReadOnlyDictionary<string, AbilityDefinition> _abilities;
    private readonly Func<Guid, IDictionary<string, DateTimeOffset>?> _cooldowns;
    private readonly IReadOnlyDictionary<Guid, Guid> _companionOwners;
    private readonly Dictionary<Guid, SetPassiveCharges> _charges = new();

    public SetPassiveCombatRuntime(IEnumerable<SetPassiveDefinition> definitions,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, int>> pieces,
        Func<Guid, CombatActorState?> actor,
        IReadOnlyDictionary<string, AbilityDefinition> abilities,
        Func<Guid, IDictionary<string, DateTimeOffset>?> cooldowns,
        IReadOnlyDictionary<Guid, Guid>? companionOwners = null)
    {
        _definitions = definitions.ToArray();
        _runtime = new(_definitions.Where(d => !d.Actions.All(a => a.Kind == SetPassiveActionKind.ModifyDirectDamage)));
        _pieces = pieces;
        _actor = actor;
        _abilities = abilities;
        _cooldowns = cooldowns;
        _companionOwners = companionOwners ?? new Dictionary<Guid, Guid>();
        foreach (Guid id in pieces.Keys)
        {
            _charges[id] = new();
            if (actor(id) is { } owner)
            {
                owner.SetPassiveEffectMultiplier = (ability, now) => _charges[id].Consume(ability, false, now, effects: true);
                owner.SetPassiveMultiplier = (ability, spell, target, now, healing) =>
                    ResolveMultiplier(id, ability, spell, target, now, healing);
            }
        }
    }

    public IReadOnlyList<CombatEvent> Process(CombatEvent input, ProcGuard? guard = null)
    {
        if (!_runtime.HandlesEventType(input.Type)) return [];
        var invocations = _runtime.Evaluate(input, _pieces, guard, ResolveOwner, Matches);
        List<CombatEvent> events = [];
        foreach (SetPassiveActionInvocation invocation in invocations)
        {
            if (_actor(invocation.ActorId) is not { IsDead: false } owner) continue;
            SetPassiveActionDefinition action = invocation.Action;
            switch (action.Kind)
            {
                case SetPassiveActionKind.EmpowerNextDirect:
                case SetPassiveActionKind.EmpowerNextEffect:
                    _charges[owner.ActorId].Arm(action, input.OccurredAtUtc);
                    break;
                case SetPassiveActionKind.ReduceCooldown:
                    if (action.ReferenceId is { } abilityId && _cooldowns(owner.ActorId) is { } cooldowns
                        && cooldowns.TryGetValue(abilityId, out DateTimeOffset readyAt))
                        cooldowns[abilityId] = readyAt - TimeSpan.FromSeconds((double)action.Magnitude) > input.OccurredAtUtc
                            ? readyAt - TimeSpan.FromSeconds((double)action.Magnitude) : input.OccurredAtUtc;
                    break;
                case SetPassiveActionKind.RestoreResource:
                    events.Add(CombatResourceRuntime.Change(owner, action.Magnitude, input.OccurredAtUtc, invocation.PassiveId, owner.ActorId));
                    break;
                case SetPassiveActionKind.ExtendTargetEffect:
                    if (input.TargetActorId is { } targetId && _actor(targetId) is { } target)
                        foreach (ActiveEffect effect in target.ActiveEffects.Where(e => e.SourceId == owner.ActorId
                            && e.ExpiresAtUtc > input.OccurredAtUtc && action.AbilityIds!.Contains(e.Definition.Id, StringComparer.Ordinal)))
                        {
                            DateTimeOffset cap = effect.AppliedAtUtc + effect.Definition.Duration + (action.Duration ?? TimeSpan.Zero);
                            DateTimeOffset extended = effect.ExpiresAtUtc + TimeSpan.FromSeconds((double)action.Magnitude);
                            effect.ExpiresAtUtc = extended < cap ? extended : cap;
                            events.Add(new(CombatEventType.EffectRefreshed, input.OccurredAtUtc, targetId,
                                effect.Definition.Id, SourceActorId: owner.ActorId, TargetActorId: targetId));
                        }
                    break;
                default:
                    events.AddRange(SetPassiveActionExecutor.Execute(invocation, owner));
                    break;
            }
        }
        return events;
    }

    private Guid? ResolveOwner(SetPassiveDefinition definition, CombatEvent input)
    {
        if (definition.Trigger.ActorRole == SetPassiveActorRole.Target) return input.TargetActorId;
        Guid? source = input.SourceActorId ?? input.ActorId;
        bool companion = source is { } id && _companionOwners.ContainsKey(id);
        return definition.Trigger.ActorRole switch
        {
            SetPassiveActorRole.CompanionOwner => companion ? _companionOwners[source!.Value] : null,
            SetPassiveActorRole.OwnerOrCompanion => companion ? _companionOwners[source!.Value] : source,
            _ => source
        };
    }

    private bool Matches(SetPassiveDefinition definition, Guid ownerId, CombatEvent input)
    {
        SetPassiveConditionDefinition condition = definition.Conditions;
        _abilities.TryGetValue(input.DefinitionId ?? string.Empty, out AbilityDefinition? ability);
        if (condition.School is { } school && ability?.School != school
            || condition.ClassAbilityOnly && ability is null
            || condition.ResourceAbilityOnly && ability is not { ResourceCost: > 0 }
            || input.Type == CombatEventType.HealingApplied && input.HealingOrigin != HealingOrigin.Direct)
            return false;
        if (_actor(ownerId) is not { IsDead: false } owner) return false;
        if (!HasEffects(owner, condition.OwnerEffectIds, input.OccurredAtUtc, ownerId)) return false;
        CombatActorState? target = input.TargetActorId is { } targetId ? _actor(targetId) : null;
        return MatchesTarget(target, condition.TargetEffectIds, condition.ControlledTargetOnly,
            input.OccurredAtUtc, ownerId);
    }

    private decimal ResolveMultiplier(Guid ownerId, string abilityId, bool spell,
        CombatActorState target, DateTimeOffset now, bool healing)
    {
        decimal multiplier = _charges[ownerId].Consume(abilityId, spell, now, healing, filter: action =>
            MatchesTarget(target, action.TargetEffectIds, action.ControlledTargetOnly, now, ownerId));
        if (healing) return multiplier;
        foreach (SetPassiveDefinition definition in _definitions)
        {
            if (!_pieces[ownerId].TryGetValue(definition.SetId, out int pieces) || pieces < definition.RequiredPieces) continue;
            foreach (SetPassiveActionDefinition action in definition.Actions.Where(a => a.Kind == SetPassiveActionKind.ModifyDirectDamage))
            {
                if (action.AbilityIds is { Count: > 0 } ids && !ids.Contains(abilityId, StringComparer.Ordinal)
                    || action.SpellOnly && !spell || action.ClassAbilityOnly && abilityId == "AUTO_ATTACK"
                    || !MatchesTarget(target, action.TargetEffectIds, action.ControlledTargetOnly, now, ownerId)) continue;
                multiplier *= 1 + action.Magnitude;
            }
        }
        return multiplier;
    }

    private static bool MatchesTarget(CombatActorState? target, IReadOnlyList<string>? effects,
        bool controlled, DateTimeOffset now, Guid ownerId) =>
        HasEffects(target, effects, now, ownerId)
        && (!controlled || target is not null && target.ActiveEffects.Any(e => e.ExpiresAtUtc > now
            && e.Definition.Kind is EffectKind.Stun or EffectKind.Root or EffectKind.Fear));

    private static bool HasEffects(CombatActorState? actor, IReadOnlyList<string>? ids, DateTimeOffset now, Guid ownerId) =>
        ids is not { Count: > 0 } || actor is not null && actor.ActiveEffects.Any(e => e.ExpiresAtUtc > now
            && e.SourceId == ownerId && ids.Contains(e.Definition.Id, StringComparer.Ordinal));
}
