using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private const string WarlordBannerEffectId = "WARLORD_WAR_BANNER";
    private const string WarlordPartyShieldEffectId = "WARLORD_PARTY_SHIELD";
    private const string WarlordVengeanceActiveEffectId = "WARLORD_CRIT_OF_VENGEANCE";
    private const string WarlordVengeanceStackEffectId = "WARLORD_VENGEANCE_STACKS";

    private bool IsWarlord =>
        string.Equals(_player.DefinitionId, "WARRIOR", StringComparison.Ordinal)
        && (_playerTalents.EventHooks.Any(hook => hook.TalentId.StartsWith("W-", StringComparison.Ordinal))
            || _playerTalents.UnlockedAbilityIds.Any(id => WarlordAbilityIds.Contains(id, StringComparer.Ordinal)));

    private AbilityDefinition ResolveWarlordAbility(
        AbilityDefinition ability,
        DateTimeOffset now)
    {
        if (!IsWarlord)
            return ability;

        ability = WarlordStaticAbilityHookResolver.Apply(ability, _playerTalents);
        decimal resourceCost = ability.ResourceCost;
        TimeSpan cooldown = ability.Cooldown;
        if (IsWarlordCry(ability.Id) && GetWarlordHook("W-7-3") is { } cadence)
            cooldown = MaxZero(cooldown - TimeSpan.FromSeconds((double)cadence.Value));
        IReadOnlyList<AbilityActionDefinition>? actions = ability.Actions;
        decimal durationBonus = 0;
        if (IsWarlordCry(ability.Id)
            && HasWarlordTalent("W-9-1"))
        {
            durationBonus += 5;
        }
        if (durationBonus > 0 && actions is not null)
        {
            actions = actions.Select(action => action.Effect is null
                    || action.Effect.Duration <= TimeSpan.Zero
                ? action
                : action with
                {
                    Effect = action.Effect with
                    {
                        Duration = action.Effect.Duration +
                            TimeSpan.FromSeconds((double)durationBonus)
                    }
                }).ToArray();
        }

        List<AbilityActionDefinition> resolvedActions = (actions ?? []).ToList();
        TimeSpan effectDuration = resolvedActions.Where(a => a.Effect is not null)
            .Select(a => a.Effect!.Duration).DefaultIfEmpty(TimeSpan.Zero).Max();
        void AddUpgrade(string talentId, string effectId, EffectStat stat,
            EffectModifierMode mode = EffectModifierMode.Percent)
        {
            if (GetWarlordHook(talentId) is not { } hook || effectDuration <= TimeSpan.Zero)
                return;
            decimal magnitude = mode == EffectModifierMode.Multiplicative
                ? Math.Max(0, 1 - hook.Value / 100m)
                : mode == EffectModifierMode.Flat ? hook.Value : hook.Value / 100m;
            resolvedActions.Add(new(AbilityActionType.ApplyEffect,
                Effect: new EffectDefinition(effectId, EffectKind.StatModifier, effectDuration,
                    1, EffectStackPolicy.Replace, magnitude, ModifiedStat: stat, ModifierMode: mode,
                    DispelCategory: "WARLORD")));
        }
        if (ability.Id == "BATTLE_CRY")
        {
            AddUpgrade("W-2-4", "WARLORD_UNIFIED_RHYTHM", EffectStat.Accuracy);
            AddUpgrade("W-4-1", "WARLORD_STRENGTHENED_CRY", EffectStat.AttackSpeed);
            AddUpgrade("W-5-4", "WARLORD_ADVANCE_ORDER", EffectStat.CriticalChance, EffectModifierMode.Flat);
        }
        if (ability.Id == "ENDURANCE_CRY")
        {
            AddUpgrade("W-6-2", "WARLORD_ENDURANCE_BANNER", EffectStat.Armor);
            AddUpgrade("W-6-2", "WARLORD_ENDURANCE_BANNER_MR", EffectStat.MagicResistance);
        }
        if (ability.Id is "WAR_BANNER" or "VICTORY_FLAG" or "BATTLE_STANDARD")
            AddUpgrade("W-3-4", "WARLORD_UNITY_BANNER", EffectStat.IncomingDamageMultiplier, EffectModifierMode.Multiplicative);
        if (ability.Id == "VICTORY_FLAG" && GetWarlordHook("W-8-2") is { } vanguard)
            resolvedActions.Add(new(AbilityActionType.ApplyEffect,
                Effect: new EffectDefinition("WARLORD_UNBREAKABLE_VANGUARD", EffectKind.LethalDamagePrevention,
                    TimeSpan.FromSeconds((double)vanguard.Value), 1, EffectStackPolicy.Replace, 1,
                    DispelCategory: "WARLORD")));

        return ability with
        {
            ResourceCost = resourceCost,
            Cooldown = cooldown,
            Actions = resolvedActions
        };
    }

    private void ApplyWarlordPassiveEffects(DateTimeOffset now)
    {
        if (!IsWarlord)
            return;
        SyncWarlordConditionalEffects(now);

        if (TryGetWarlordHook("W-1-2", out ResolvedTalentEventHook attackPower))
        {
            ApplyWarlordEffectToParty(
                "WARLORD_INSPIRATION",
                EffectStat.AttackPower,
                attackPower.Value / 100m,
                now);
        }
        if (TryGetWarlordHook("W-1-4", out ResolvedTalentEventHook resistance))
        {
            ApplyWarlordEffectToParty(
                "WARLORD_MILITARY_FORM",
                EffectStat.MagicResistance,
                resistance.Value / 100m,
                now);
        }
        if (TryGetWarlordHook("W-4-3", out ResolvedTalentEventHook dodge))
        {
            ApplyWarlordEffectToParty(
                "WARLORD_IRON_DISCIPLINE",
                EffectStat.Dodge,
                dodge.Value,
                now,
                EffectModifierMode.Flat);
        }
    }

    private void ApplyWarlordAbilityHooks(CombatEvent combatEvent)
    {
        if (!IsWarlord || string.IsNullOrWhiteSpace(combatEvent.DefinitionId))
            return;

        string abilityId = combatEvent.DefinitionId!;
        HashSet<string> talentIds = [];
        if (IsWarlordCry(abilityId))
        {
            talentIds.Add("W-6-3");
            talentIds.Add("W-9-1");
        }
        if (abilityId == "BATTLE_CRY")
            talentIds.Add("W-8-1");
        if (abilityId is "WAR_BANNER" or "VICTORY_FLAG" or "BATTLE_STANDARD")
            talentIds.Add("W-9-1");
        if (abilityId is "WAR_BANNER" or "VICTORY_FLAG" or "BATTLE_STANDARD")
            talentIds.Add("W-8-4");

        foreach (TalentRuntimeAction action in PublishWarlordPartyEvent(
                     combatEvent.OccurredAtUtc,
                     _player.Actor.ActorId,
                     abilityId,
                     talentIds.Contains))
        {
            ApplyWarlordPartyAction(action, abilityId, combatEvent.OccurredAtUtc);
        }

        if (abilityId == "RALLY_CRY" && HasWarlordTalent("W-9-1"))
            AddResource(_player.Actor, _player.Actor.MaxResource * 0.20m, combatEvent.OccurredAtUtc, "W-9-1-RALLY");

    }

    private void ApplyWarlordPartyDamageHooks(CombatEvent combatEvent)
    {
        if (!IsWarlord
            || combatEvent.TargetActorId is not { } targetActorId
            || !IsPartyActor(targetActorId)
            || combatEvent.IsPeriodic)
        {
            return;
        }

        HashSet<string> talentIds = ["W-2-2"];
        CombatActorState? ally = GetPartyActor(targetActorId);
        foreach (TalentRuntimeAction action in PublishWarlordPartyEvent(
                     combatEvent.OccurredAtUtc,
                     targetActorId,
                     combatEvent.DefinitionId ?? "DAMAGE_TAKEN",
                     talentIds.Contains,
                     combatEvent.Amount))
        {
            if (action.TalentId == "W-2-2"
                && ally is not null
                && ally.CurrentHp / ally.MaxHp < 0.2m
                && _random.NextUnit() < action.Value / 100m)
            {
                decimal shield = ally.MaxHp * (GetWarlordHook("W-2-2")?.SecondaryValue ?? 4) / 100m;
                ApplyWarlordShield(ally, shield, TimeSpan.FromSeconds(5), combatEvent.OccurredAtUtc);
            }
        }

        if (combatEvent.DefinitionId == "AUTO_ATTACK")
            ApplyWarlordAutoAttackHooks(combatEvent, alreadyPublished: true);

        if (HasWarlordAbility("CRY_OF_VENGEANCE")
            && targetActorId != _player.Actor.ActorId
            && combatEvent.Amount > 0
            && _player.Actor.ActiveEffects.FirstOrDefault(effect => effect.Definition.Id == WarlordVengeanceActiveEffectId
                && effect.ExpiresAtUtc > combatEvent.OccurredAtUtc) is { } vengeance
            && _procGuard.IsReady(_player.Actor.ActorId, "warlord-vengeance-stack", combatEvent.OccurredAtUtc))
        {
            _procGuard.StartCooldown(_player.Actor.ActorId, "warlord-vengeance-stack",
                combatEvent.OccurredAtUtc, TimeSpan.FromSeconds((double)(
                    _abilities["CRY_OF_VENGEANCE"].RuntimeParameters?.GetValueOrDefault("VENGEANCE_STACK_ICD_SECONDS") ?? 0.5m)));
            ApplyKernelEvents(
                EffectEngine.Apply(
                    _player.Actor,
                    _player.Actor.ActorId,
                    new EffectDefinition(
                        WarlordVengeanceStackEffectId,
                        EffectKind.Buff,
                        vengeance.ExpiresAtUtc - combatEvent.OccurredAtUtc,
                        vengeance.Definition.MaxStacks,
                        EffectStackPolicy.Stack,
                        vengeance.Definition.Magnitude,
                        DispelCategory: "WARLORD"),
                    combatEvent.OccurredAtUtc),
                _player.Actor.ActorId,
                _player.Actor.ActorId,
                WarlordVengeanceStackEffectId);
            // Stack refresh must never extend the cry's original window.
            _player.Actor.ActiveEffects.First(effect => effect.Definition.Id == WarlordVengeanceStackEffectId)
                .ExpiresAtUtc = vengeance.ExpiresAtUtc;
        }
    }

    private void ApplyWarlordAutoAttackHooks(
        CombatEvent combatEvent,
        bool alreadyPublished = false)
    {
        if (!IsWarlord
            || combatEvent.Type != CombatEventType.DamageDealt
            || combatEvent.DefinitionId != "AUTO_ATTACK"
            || combatEvent.SourceActorId is not { } sourceActorId
            || !IsPartyActor(sourceActorId)
            || combatEvent.Amount <= 0)
        {
            return;
        }

        if (!alreadyPublished
            && combatEvent.TargetActorId is { } targetId
            && IsPartyActor(targetId))
        {
            return;
        }

        if (TryGetWarlordHook("W-3-2", out ResolvedTalentEventHook rhythm)
            && _random.NextUnit() < (rhythm.Rank == 1 ? 0.15m : 0.25m))
        {
            string[] cries = ["BATTLE_CRY", "ENDURANCE_CRY", "RALLY_CRY"];
            string[] available = cries
                .Where(id => _playerRuntime.Cooldowns.TryGetValue(id, out DateTimeOffset ready) && ready > combatEvent.OccurredAtUtc)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            if (available.Length > 0)
            {
                string selected = available[(int)(_random.NextUnit() * available.Length)];
                _playerRuntime.ModifyCooldown(selected, -TimeSpan.FromSeconds((double)rhythm.Value), combatEvent.OccurredAtUtc);
            }
        }
    }

    private void ApplyWarlordPartyCriticalHooks(CombatEvent combatEvent)
    {
        if (!IsWarlord || combatEvent.SourceActorId is not { } source || !IsPartyActor(source))
            return;

        foreach (TalentRuntimeAction action in PublishWarlordPartyEvent(
                     combatEvent.OccurredAtUtc,
                     source,
                     "CRITICAL_HIT",
                     talentId => talentId == "W-8-3"))
        {
            AddResource(_player.Actor, action.Value, combatEvent.OccurredAtUtc, action.TalentId);
        }
    }

    private void ApplyWarlordPartyDeathHooks(DateTimeOffset now, Guid deadActorId)
    {
        if (!IsWarlord)
            return;

        foreach (TalentRuntimeAction action in PublishWarlordPartyEvent(
                     now,
                     deadActorId,
                     "PARTY_MEMBER_DIED",
                     talentId => talentId == "W-7-2"))
        {
            if (action.TalentId != "W-7-2")
                continue;

            ApplyWarlordEffectToParty(
                "WARLORD_UNBROKEN_FORMATION",
                EffectStat.AttackPower,
                action.Value / 100m,
                now, duration: TimeSpan.FromSeconds(10));
            if (GetWarlordHook("W-7-2") is { } hook)
            {
                ApplyWarlordEffectToParty(
                    "WARLORD_UNBROKEN_FORMATION_DODGE",
                    EffectStat.Dodge,
                    hook.SecondaryValue,
                    now,
                    EffectModifierMode.Flat, TimeSpan.FromSeconds(10));
            }
        }
    }

    private void ApplyWarlordEnemyKilledHooks(CombatEvent death)
    {
        if (!IsWarlord)
            return;

        if (HasWarlordTalent("W-9-1")
            && _procGuard.IsReady(_player.Actor.ActorId, "warlord-capstone-kill", death.OccurredAtUtc))
        {
            _procGuard.StartCooldown(_player.Actor.ActorId, "warlord-capstone-kill", death.OccurredAtUtc, TimeSpan.FromSeconds(1));
            ReduceWarlordCryCooldowns(1, death.OccurredAtUtc);
        }

        if (_player.Actor.ActiveEffects.Any(effect => effect.Definition.Id == WarlordBannerEffectId
                && effect.SourceId == _player.Actor.ActorId && effect.ExpiresAtUtc > death.OccurredAtUtc)
            && HasWarlordAbility("WAR_BANNER"))
        {
            foreach (CombatActorState actor in PartyActors)
            {
                AddResource(actor, actor.MaxResource * 2 / 100m, death.OccurredAtUtc, "W-3-3");
            }
        }
    }

    private IReadOnlyList<TalentRuntimeAction> PublishWarlordPartyEvent(
        DateTimeOffset now,
        Guid targetActorId,
        string eventId,
        Func<string, bool> talentFilter,
        decimal amount = 0) =>
        _talentRuntimeEngine.Publish(
            new CombatRuntimeEvent(
                CombatRuntimeEventKind.PartyEvent,
                now,
                _player.Actor.ActorId,
                targetActorId,
                eventId,
                Amount: amount,
                Sequence: Sequence),
            _playerTalents,
            hook => hook.TalentId.StartsWith("W-", StringComparison.Ordinal)
                && talentFilter(hook.TalentId));

    private void ApplyWarlordPartyAction(
        TalentRuntimeAction action,
        string abilityId,
        DateTimeOffset now)
    {
        switch (action.TalentId)
        {
            case "W-6-3":
                AddResource(_player.Actor, action.Value, now, action.TalentId);
                RemoveOnePoison(_player.Actor, now);
                CombatActorState[] poisonedAllies = PartyActors.Where(actor => actor.ActorId != _player.Actor.ActorId
                    && actor.ActiveEffects.Any(effect => effect.Definition.DispelCategory == "POISON" && effect.ExpiresAtUtc > now)).ToArray();
                if (poisonedAllies.Length > 0)
                {
                    RemoveOnePoison(poisonedAllies[(int)(_random.NextUnit() * poisonedAllies.Length)], now);
                }
                break;
            case "W-8-1":
                foreach (CombatActorState actor in PartyActors)
                {
                    string resourceType = _playerStatesByActorId.TryGetValue(actor.ActorId, out CombatPlayerRuntimeState? state)
                        ? state.Definition.ResourceType : _companion?.ResourceType ?? string.Empty;
                    decimal percent = string.Equals(resourceType, "RAGE", StringComparison.Ordinal)
                        ? action.Value
                        : GetWarlordHook("W-8-1")?.SecondaryValue ?? action.Value;
                    AddResource(actor, actor.MaxResource * percent / 100m, now, action.TalentId);
                }
                break;
            case "W-8-4":
                ReduceWarlordCryCooldowns(action.Value, now);
                break;
            case "W-9-1":
                decimal duration = GetWarlordHook("W-9-1")?.SecondaryValue ?? 6;
                foreach (CombatActorState actor in PartyActors)
                {
                    ApplyWarlordShield(
                        actor,
                        actor.MaxHp * action.Value / 100m,
                        TimeSpan.FromSeconds((double)duration),
                        now);
                }
                break;
        }
    }

    private void RemoveOnePoison(CombatActorState actor, DateTimeOffset now)
    {
        ActiveEffect? poison = actor.ActiveEffects.FirstOrDefault(effect =>
            effect.Definition.DispelCategory == "POISON" && effect.ExpiresAtUtc > now);
        if (poison is not null)
            ApplyKernelEvents(EffectEngine.RemoveInstance(actor, poison.InstanceId, now),
                _player.Actor.ActorId, actor.ActorId, "W-6-3");
    }

    private void ApplyWarlordEffectToParty(
        string effectId,
        EffectStat stat,
        decimal magnitude,
        DateTimeOffset now,
        EffectModifierMode mode = EffectModifierMode.Percent,
        TimeSpan? duration = null)
    {
        foreach (CombatActorState actor in PartyActors)
        {
            ApplyWarlordEffect(
                actor,
                effectId,
                stat,
                magnitude,
                now,
                mode,
                duration ?? TimeSpan.FromHours(24));
        }
    }

    private void ApplyWarlordEffect(
        CombatActorState target,
        string effectId,
        EffectStat? stat,
        decimal magnitude,
        DateTimeOffset now,
        EffectModifierMode mode,
        TimeSpan duration)
    {
        EffectDefinition definition = new(
            effectId,
            stat is null ? EffectKind.Buff : EffectKind.StatModifier,
            duration,
            1,
            EffectStackPolicy.Replace,
            magnitude,
            ModifiedStat: stat,
            ModifierMode: mode,
            DispelCategory: "WARLORD");
        ApplyKernelEvents(
            EffectEngine.Apply(target, _player.Actor.ActorId, definition, now),
            _player.Actor.ActorId,
            target.ActorId,
            effectId);
    }

    private void ApplyWarlordShield(
        CombatActorState target,
        decimal amount,
        TimeSpan duration,
        DateTimeOffset now)
    {
        EffectDefinition definition = new(
            WarlordPartyShieldEffectId,
            EffectKind.Shield,
            duration,
            1,
            EffectStackPolicy.Replace,
            Math.Max(0, amount),
            DispelCategory: "WARLORD");
        ApplyKernelEvents(
            EffectEngine.Apply(target, _player.Actor.ActorId, definition, now),
            _player.Actor.ActorId,
            target.ActorId,
            WarlordPartyShieldEffectId);
    }

    private ResolvedTalentEventHook? GetWarlordHook(string talentId) =>
        _playerTalents.EventHooks.FirstOrDefault(hook =>
            string.Equals(hook.TalentId, talentId, StringComparison.Ordinal));

    private bool TryGetWarlordHook(string talentId, out ResolvedTalentEventHook hook)
    {
        hook = GetWarlordHook(talentId)!;
        return hook is not null;
    }

    private bool HasWarlordTalent(string talentId) =>
        GetWarlordHook(talentId) is not null
        || _playerTalents.UnlockedAbilityIds.Contains(talentId);

    private bool HasWarlordAbility(string abilityId) =>
        _playerTalents.UnlockedAbilityIds.Contains(abilityId);

    private decimal ResolveWarlordAutoAttackResource(decimal amount) =>
        amount * (1 + (GetWarlordHook("W-5-3")?.Value ?? 0) / 100m);

    private decimal ResolveWarlordVengeanceMultiplier(DateTimeOffset now)
    {
        ActiveEffect? stacks = _player.Actor.ActiveEffects.FirstOrDefault(effect =>
            effect.Definition.Id == WarlordVengeanceStackEffectId && effect.ExpiresAtUtc > now);
        if (stacks is null)
            return 1;

        ApplyKernelEvents(EffectEngine.Remove(_player.Actor, WarlordVengeanceStackEffectId, now),
            _player.Actor.ActorId, _player.Actor.ActorId, WarlordVengeanceStackEffectId);
        bool active = _player.Actor.ActiveEffects.Any(effect =>
            effect.Definition.Id == WarlordVengeanceActiveEffectId && effect.ExpiresAtUtc > now);
        return active ? 1 + stacks.Stacks * stacks.Definition.Magnitude : 1;
    }

    private void ReduceWarlordCryCooldowns(decimal seconds, DateTimeOffset now)
    {
        TimeSpan reduction = TimeSpan.FromSeconds((double)Math.Max(0, seconds));
        foreach (string abilityId in WarlordAbilityIds.Where(IsWarlordCry))
        {
            if (_playerRuntime.Cooldowns.TryGetValue(abilityId, out DateTimeOffset ready)
                && ready > now)
            {
                _playerRuntime.Cooldowns[abilityId] = ready - reduction;
            }
        }
    }

    private decimal ScaleWarlordResource(string definitionId, decimal amount) =>
        definitionId is "W-3-3" or "W-6-3" or "W-8-1" or "W-8-3" or "W-9-1-RALLY"
            ? amount * (1 + (GetWarlordHook("W-6-4")?.Value ?? 0) / 100m)
            : amount;

    private void SyncWarlordConditionalEffects(DateTimeOffset now)
    {
        if (!IsWarlord || GetWarlordHook("W-7-4") is not { } hook) return;
        Guid ownerId = _player.Actor.ActorId;
        string effectId = $"WARLORD_STAND_TO_THE_END_{ownerId:N}";
        foreach (CombatActorState actor in PartyActors.Append(_player.Actor).DistinctBy(actor => actor.ActorId).ToArray())
        {
            bool active = IsActiveParticipant(ownerId) && !_player.Actor.IsDead
                && !actor.IsDead && actor.CurrentHp / actor.MaxHp < 0.25m;
            bool exists = actor.ActiveEffects.Any(e => e.Definition.Id == effectId && e.ExpiresAtUtc > now);
            if (active && !exists)
                ApplyWarlordEffect(actor, effectId, EffectStat.IncomingDamageMultiplier,
                    Math.Max(0, 1 - hook.Value / 100m), now, EffectModifierMode.Multiplicative, TimeSpan.FromHours(24));
            else if (!active && exists)
                ApplyKernelEvents(EffectEngine.RemoveOwned(actor, effectId, ownerId, now), ownerId, actor.ActorId, "W-7-4");
        }
    }

    private bool IsPartyActor(Guid actorId) => GetPartyActor(actorId) is not null;

    private CombatActorState? GetPartyActor(Guid actorId) =>
        _playerStatesByActorId.TryGetValue(actorId, out CombatPlayerRuntimeState? state)
            ? state.Definition.Actor
            : _companion?.Actor.ActorId == actorId
                ? _companion.Actor
                : null;

    private IEnumerable<CombatActorState> PartyActors => ActivePlayerActorIds()
        .Select(id => _playerStatesByActorId[id].Definition.Actor)
        .Concat(_companion is not null && !_companion.Actor.IsDead ? [_companion.Actor] : []);

    private static bool IsWarlordCry(string abilityId) =>
        abilityId is "BATTLE_CRY" or "ENDURANCE_CRY" or "CRY_OF_VENGEANCE" or "RALLY_CRY";

    private static readonly string[] WarlordAbilityIds =
    [
        "BATTLE_CRY", "ENDURANCE_CRY", "WAR_BANNER", "CRY_OF_VENGEANCE",
        "VICTORY_FLAG", "RALLY_CRY", "BATTLE_STANDARD"
    ];

    private static TimeSpan MaxZero(TimeSpan value) =>
        value < TimeSpan.Zero ? TimeSpan.Zero : value;

    private void ForEachOtherWarlord(Action action)
    {
        CombatPlayerRuntimeState previous = _activePlayerState;
        try
        {
            foreach (CombatPlayerRuntimeState state in _playerStatesByActorId.Values
                         .Where(state => state != previous && !state.Definition.Actor.IsDead).ToArray())
            {
                _activePlayerState = state;
                if (IsWarlord) action();
            }
        }
        finally { _activePlayerState = previous; }
    }
}
