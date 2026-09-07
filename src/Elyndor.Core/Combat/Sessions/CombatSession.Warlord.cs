using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private const string WarlordPermanentEffectId = "WARLORD_PERMANENT";
    private const string WarlordBattleCryEffectId = "WARLORD_BATTLE_CRY";
    private const string WarlordEnduranceEffectId = "WARLORD_ENDURANCE_CRY";
    private const string WarlordBannerEffectId = "WARLORD_WAR_BANNER";
    private const string WarlordVictoryFlagEffectId = "WARLORD_VICTORY_FLAG";
    private const string WarlordBattleStandardEffectId = "WARLORD_BATTLE_STANDARD";
    private const string WarlordPartyShieldEffectId = "WARLORD_PARTY_SHIELD";
    private const string WarlordVengeanceStackEffectId = "WARLORD_CRIT_OF_VENGEANCE";

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

        decimal resourceCost = ability.ResourceCost;
        TimeSpan cooldown = ability.Cooldown;
        if (IsWarlordCry(ability.Id)
            && TryGetWarlordHook("W-1-1", out ResolvedTalentEventHook cryCost))
        {
            resourceCost = Math.Max(0, resourceCost - cryCost.Value);
        }

        if (ability.Id is "WAR_BANNER" or "VICTORY_FLAG" or "BATTLE_STANDARD"
            && TryGetWarlordHook("W-5-2", out ResolvedTalentEventHook flagCooldown))
        {
            cooldown = MaxZero(cooldown - TimeSpan.FromSeconds((double)flagCooldown.Value));
        }

        IReadOnlyList<AbilityActionDefinition>? actions = ability.Actions;
        decimal durationBonus = 0;
        if (IsWarlordCry(ability.Id)
            && TryGetWarlordHook("W-4-4", out ResolvedTalentEventHook echo))
        {
            durationBonus += echo.Value;
        }
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

        return ability with
        {
            ResourceCost = resourceCost,
            Cooldown = cooldown,
            Actions = actions
        };
    }

    private void ApplyWarlordPassiveEffects(DateTimeOffset now)
    {
        if (!IsWarlord)
            return;

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

        foreach (TalentRuntimeAction action in PublishWarlordPartyEvent(
                     combatEvent.OccurredAtUtc,
                     _player.Actor.ActorId,
                     abilityId,
                     talentIds.Contains))
        {
            ApplyWarlordPartyAction(action, abilityId, combatEvent.OccurredAtUtc);
        }

        if (abilityId == "BATTLE_CRY")
        {
            ApplyWarlordPartyEffectFromHook("W-2-4", "WARLORD_UNIFIED_RHYTHM", EffectStat.Accuracy, combatEvent.OccurredAtUtc);
            ApplyWarlordPartyEffectFromHook("W-4-1", "WARLORD_STRENGTHENED_CRY", EffectStat.AttackSpeed, combatEvent.OccurredAtUtc);
            ApplyWarlordPartyEffectFromHook("W-5-4", "WARLORD_ADVANCE_ORDER", EffectStat.CriticalChance, combatEvent.OccurredAtUtc);
        }
        if (abilityId == "ENDURANCE_CRY")
        {
            ApplyWarlordPartyEffectFromHook("W-6-2", "WARLORD_ENDURANCE_BANNER", EffectStat.Armor, combatEvent.OccurredAtUtc);
            ApplyWarlordPartyEffectFromHook("W-6-2", "WARLORD_ENDURANCE_BANNER_MR", EffectStat.MagicResistance, combatEvent.OccurredAtUtc);
        }
        if (abilityId == "WAR_BANNER")
        {
            ApplyWarlordPartyEffectFromHook("W-3-4", "WARLORD_UNITY_BANNER", EffectStat.IncomingDamageMultiplier, combatEvent.OccurredAtUtc);
        }
        if (abilityId == "VICTORY_FLAG")
        {
            ApplyWarlordPartyEffectFromHook("W-8-2", "WARLORD_UNBREAKABLE_VANGUARD", null, combatEvent.OccurredAtUtc);
        }
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

        HashSet<string> talentIds = ["W-2-2", "W-7-4"];
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
            else if (action.TalentId == "W-7-4"
                && GetPartyActor(targetActorId) is { } affected
                && affected.CurrentHp / affected.MaxHp < 0.25m)
            {
                ApplyWarlordEffect(
                    affected,
                    "WARLORD_STAND_TO_THE_END",
                    EffectStat.IncomingDamageMultiplier,
                    Math.Max(0, 1 - action.Value / 100m),
                    combatEvent.OccurredAtUtc,
                    EffectModifierMode.Multiplicative,
                    TimeSpan.FromSeconds(10));
            }
        }

        if (combatEvent.DefinitionId == "AUTO_ATTACK")
            ApplyWarlordAutoAttackHooks(combatEvent, alreadyPublished: true);

        if (HasWarlordTalent("W-4-2")
            && GetPartyActor(targetActorId) is not null
            && targetActorId != _player.Actor.ActorId
            && _player.Actor.ActiveEffects.Any(effect => effect.Definition.Id == WarlordVengeanceStackEffectId))
        {
            return;
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
            string? selected = cries
                .Where(id => _playerRuntime.Cooldowns.TryGetValue(id, out DateTimeOffset ready) && ready > combatEvent.OccurredAtUtc)
                .OrderBy(id => id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (selected is not null)
            {
                _playerRuntime.Cooldowns[selected] = _playerRuntime.Cooldowns[selected] -
                    TimeSpan.FromSeconds((double)rhythm.Value);
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
                now);
            if (GetWarlordHook("W-7-2") is { } hook)
            {
                ApplyWarlordEffectToParty(
                    "WARLORD_UNBROKEN_FORMATION_DODGE",
                    EffectStat.Dodge,
                    hook.SecondaryValue,
                    now,
                    EffectModifierMode.Flat);
            }
        }
    }

    private void ApplyWarlordEnemyKilledHooks(CombatEvent death)
    {
        if (!IsWarlord)
            return;

        foreach (TalentRuntimeAction action in PublishWarlordPartyEvent(
                     death.OccurredAtUtc,
                     _player.Actor.ActorId,
                     "ENEMY_KILLED",
                     talentId => talentId == "W-9-1"))
        {
            if (action.TalentId == "W-9-1")
            {
                foreach (string abilityId in WarlordAbilityIds)
                {
                    if (_playerRuntime.Cooldowns.TryGetValue(abilityId, out DateTimeOffset ready)
                        && ready > death.OccurredAtUtc)
                    {
                        _playerRuntime.Cooldowns[abilityId] = ready - TimeSpan.FromSeconds(1);
                    }
                }
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
                ApplyKernelEvents(
                    EffectEngine.Dispel(_player.Actor, "POISON", now),
                    _player.Actor.ActorId,
                    _player.Actor.ActorId,
                    action.TalentId);
                if (_companion is not null)
                {
                    ApplyKernelEvents(
                        EffectEngine.Dispel(_companion.Actor, "POISON", now),
                        _player.Actor.ActorId,
                        _companion.Actor.ActorId,
                        action.TalentId);
                }
                break;
            case "W-8-1":
                foreach (CombatActorState actor in PartyActors)
                {
                    string resourceType = actor.ActorId == _player.Actor.ActorId
                        ? _player.ResourceType
                        : _companion?.ResourceType ?? string.Empty;
                    decimal percent = string.Equals(resourceType, "RAGE", StringComparison.Ordinal)
                        ? action.Value
                        : GetWarlordHook("W-8-1")?.SecondaryValue ?? action.Value;
                    AddResource(actor, actor.MaxResource * percent / 100m, now, action.TalentId);
                }
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

    private void ApplyWarlordPartyEffectFromHook(
        string talentId,
        string effectId,
        EffectStat? stat,
        DateTimeOffset now)
    {
        if (GetWarlordHook(talentId) is not { } hook)
            return;

        if (stat is null)
        {
            foreach (CombatActorState actor in PartyActors)
            {
                if (effectId == "WARLORD_UNBREAKABLE_VANGUARD")
                {
                    ApplyKernelEvents(
                        EffectEngine.Apply(
                            actor,
                            _player.Actor.ActorId,
                            new EffectDefinition(
                                effectId,
                                EffectKind.LethalDamagePrevention,
                                TimeSpan.FromSeconds((double)hook.Value),
                                1,
                                EffectStackPolicy.Replace,
                                1,
                                DispelCategory: "WARLORD"),
                            now),
                        _player.Actor.ActorId,
                        actor.ActorId,
                        effectId);
                    continue;
                }

                ApplyWarlordEffect(
                    actor,
                    effectId,
                    null,
                    1,
                    now,
                    EffectModifierMode.Flat,
                    TimeSpan.FromSeconds((double)hook.Value));
            }
            return;
        }

        decimal value = stat is EffectStat.IncomingDamageMultiplier
            ? Math.Max(0, 1 - hook.Value / 100m)
            : hook.Value / 100m;
        ApplyWarlordEffectToParty(effectId, stat.Value, value, now,
            stat is EffectStat.IncomingDamageMultiplier
                ? EffectModifierMode.Multiplicative
                : EffectModifierMode.Percent);
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

    private bool IsPartyActor(Guid actorId) =>
        actorId == _player.Actor.ActorId
        || _companion?.Actor.ActorId == actorId;

    private CombatActorState? GetPartyActor(Guid actorId) =>
        actorId == _player.Actor.ActorId
            ? _player.Actor
            : _companion?.Actor.ActorId == actorId
                ? _companion.Actor
                : null;

    private IEnumerable<CombatActorState> PartyActors =>
        _companion is null
            ? [_player.Actor]
            : [_player.Actor, _companion.Actor];

    private static bool IsWarlordCry(string abilityId) =>
        abilityId is "BATTLE_CRY" or "ENDURANCE_CRY" or "CRY_OF_VENGEANCE" or "RALLY_CRY";

    private static readonly string[] WarlordAbilityIds =
    [
        "BATTLE_CRY", "ENDURANCE_CRY", "WAR_BANNER", "CRY_OF_VENGEANCE",
        "VICTORY_FLAG", "RALLY_CRY", "BATTLE_STANDARD"
    ];

    private static TimeSpan MaxZero(TimeSpan value) =>
        value < TimeSpan.Zero ? TimeSpan.Zero : value;
}
