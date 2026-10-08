using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Paladin;
using Elyndor.Core.Items;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private const string PaladinLightsGraceEffectId = "PALADIN_LIGHTS_GRACE";

    private AbilityDefinition ResolvePaladinAbility(AbilityDefinition ability, DateTimeOffset now)
    {
        if (!IsActivePaladin)
            return ability;

        bool directHeal = ability.Actions?.Any(action => action.Type == AbilityActionType.Healing
            && action.HealingCanCrit) == true;
        bool offensive = ability.Actions?.Any(action => action.Type == AbilityActionType.Damage) == true;
        _paladinSessionStates.TryGetValue(_player.Actor.ActorId, out var state);
        decimal cost = ability.ResourceCost;
        decimal criticalBonus = ability.CriticalChanceBonus;
        decimal criticalDamageBonus = ability.CriticalDamageBonus;
        decimal accuracyBonus = ability.AccuracyBonus;
        decimal damageMultiplier = ability.DamageMultiplier;
        TimeSpan cooldown = ability.Cooldown;
        TimeSpan castTime = ability.CastTime;

        if (ability.Id is "HOLY_LIGHT" or "FLASH_OF_LIGHT"
            && TryGetPaladinHook("H-1-3", out var economy))
            cost *= 1 - 0.03m * economy.Rank;
        if (offensive && TryGetPaladinHook("R-1-2", out var benediction))
            cost *= 1 - 0.02m * benediction.Rank;
        if (ability.Id == "TEMPLARS_VERDICT"
            && state?.Retribution.DivinePurposeArmed == true
            && ActivePaladinEffect(PaladinDivinePurposeEffectId, now) is not null)
            cost -= PaladinDivinePurposeManaDiscount;
        if (directHeal)
        {
            if (TryGetPaladinHook("H-2-2", out var precision)) criticalBonus += precision.Rank;
            if (TryGetPaladinHook("H-5-1", out var power)) criticalBonus += power.Rank;
            if (state?.Healing.DivineFavorArmed == true
                && ActivePaladinEffect("PALADIN_DIVINE_FAVOR_READY", now) is not null)
                criticalBonus += 100;
        }
        if (offensive && TryGetPaladinHook("R-1-3", out var conviction))
            criticalBonus += conviction.Rank;
        if (state?.UsesTwoHandedWeapon == true
            && ability.Actions?.Any(action => action.Type == AbilityActionType.Damage
                && action.DamageType == DamageType.Physical) == true
            && TryGetPaladinHook("R-2-3", out var specialization))
            damageMultiplier *= 1 + 0.03m * specialization.Rank;
        if (ability.Id == "DIVINE_FAVOR" && TryGetPaladinHook("H-3-2", out var favor))
            cooldown -= TimeSpan.FromSeconds(5 * favor.Rank);

        if (ability.Id == "LAY_ON_HANDS" && TryGetPaladinHook("H-3-4", out var layHands))
            cooldown -= TimeSpan.FromSeconds(60 * layHands.Rank);

        if (ability.Id is "HOLY_SHOCK" or "HOLY_SHOCK_OFFENSIVE"
            && TryGetPaladinHook("H-4-2", out var improvedShock))
        {
            cooldown -= TimeSpan.FromSeconds(improvedShock.Rank);
            criticalBonus += 2 * improvedShock.Rank;
        }

        if (ability.Id == "BLESSING_OF_PROTECTION"
            && TryGetPaladinHook("P-2-4", out var guardianFavor))
            cooldown -= TimeSpan.FromSeconds(5 * guardianFavor.Rank);

        if (ability.Id == "AVENGERS_SHIELD"
            && TryGetPaladinHook("P-5-2", out var improvedAvenger))
        {
            cooldown -= TimeSpan.FromSeconds(improvedAvenger.Rank);
            damageMultiplier *= 1 + 0.10m * improvedAvenger.Rank;
        }

        if (ability.Id == "CRUSADER_STRIKE"
            && TryGetPaladinHook("R-3-4", out var improvedCrusader))
        {
            cooldown -= TimeSpan.FromSeconds(0.5 * improvedCrusader.Rank);
            criticalBonus += 3 * improvedCrusader.Rank;
        }

        if (ability.Id == "JUDGEMENT" && TryGetPaladinHook("R-4-4", out var righteousVerdict))
        {
            damageMultiplier *= 1 + 0.06m * righteousVerdict.Rank;
            criticalBonus += 3 * righteousVerdict.Rank;
        }

        if (offensive && ActivePaladinEffect("PALADIN_AVENGING_WRATH", now) is not null
            && TryGetPaladinHook("R-7-2", out var wrathfulVengeance))
            criticalDamageBonus += 10 * wrathfulVengeance.Rank;
        if (ability.Id == "JUDGEMENT")
        {
            if (TryGetPaladinHook("R-2-1", out var judgement))
                cooldown -= TimeSpan.FromSeconds(0.75 * judgement.Rank);
            if (TryGetPaladinHook("R-5-2", out var fanaticism))
                criticalBonus += 3 * fanaticism.Rank;
        }
        if (ability.Id == "HOLY_LIGHT" && ActivePaladinEffect(PaladinLightsGraceEffectId, now) is { } grace)
            castTime -= TimeSpan.FromSeconds((double)grace.Definition.Magnitude);
        if (ability.Id == "FLASH_OF_LIGHT")
        {
            decimal instantRank = Math.Max(
                ActivePaladinEffect(PaladinArtOfWarEffectId, now)?.Definition.Magnitude ?? 0,
                ActivePaladinEffect("PALADIN_SURGE_OF_LIGHT", now)?.Definition.Magnitude ?? 0);
            if (instantRank >= 2)
                castTime = TimeSpan.Zero;
            else if (instantRank >= 1)
                castTime -= TimeSpan.FromSeconds(0.35);
            if (instantRank >= 1)
                cost *= 1 - 0.10m * instantRank;
        }
        if (ability.Id is "HOLY_SHOCK" or "HOLY_SHOCK_OFFENSIVE"
            && state?.Healing.NextHolyShockFree == true
            && ActivePaladinEffect(PaladinHeraldFreeShockEffectId, now) is not null)
            cost = 0;
        if (ability.Id == "TEMPLARS_VERDICT"
            && state?.Retribution.IsAvengingWrathActive(now) == true
            && HasPaladinTalent("R-9-1")
            && !_paladinIncarnationVerdictConsumed.Contains(_player.Actor.ActorId))
            criticalBonus += 100;

        var actions = ability.Actions?.Select(action => action.Effect is null ? action : action with
        {
            Effect = action.Effect.Id switch
            {
                PaladinHolyShieldEffectId => action.Effect with
                { Duration = TimeSpan.FromSeconds((double)ResolveHolyShieldDurationSeconds()) },
                "PALADIN_AVENGING_WRATH" when HasPaladinTalent("R-9-1") => action.Effect with
                { Duration = TimeSpan.FromSeconds(16) },
                PaladinDevotionAuraEffectId when TryGetPaladinHook("P-1-4", out var devotion) =>
                    action.Effect with { Magnitude = action.Effect.Magnitude + 0.03m * devotion.Rank },
                "PALADIN_BLESSING_PROTECTION" when TryGetPaladinHook("P-2-4", out var favorOfGuardian) =>
                    action.Effect with { Duration = action.Effect.Duration + TimeSpan.FromSeconds(favorOfGuardian.Rank) },
                "PALADIN_CONSECRATION_DAMAGE" when TryGetPaladinHook("P-3-4", out var consecrated) =>
                    action.Effect with { Magnitude = action.Effect.Magnitude * (1 + 0.10m * consecrated.Rank) },
                "PALADIN_BLESSING_SANCTUARY" when HasPaladinTalent("P-8-3") =>
                    action.Effect with { Magnitude = action.Effect.Magnitude - 0.05m },
                _ => action.Effect
            }
        }).ToArray();
        return ability with
        {
            ResourceCost = Math.Max(0, cost),
            Cooldown = cooldown < TimeSpan.Zero ? TimeSpan.Zero : cooldown,
            CastTime = castTime < TimeSpan.Zero ? TimeSpan.Zero : castTime,
            Type = ability.Type == AbilityType.Casted && castTime <= TimeSpan.Zero ? AbilityType.Instant : ability.Type,
            CriticalChanceBonus = criticalBonus,
            CriticalDamageBonus = criticalDamageBonus,
            AccuracyBonus = accuracyBonus,
            DamageMultiplier = damageMultiplier,
            Actions = actions
        };
    }

    private void InitializePaladinLoadouts(DateTimeOffset now)
    {
        CombatPlayerRuntimeState previous = _activePlayerState;
        try
        {
            foreach (CombatPlayerRuntimeState state in _playerStatesByActorId.Values)
            {
                _activePlayerState = state;
                InitializePaladinLoadout(now);
            }
        }
        finally
        {
            _activePlayerState = previous;
        }
    }

    private void InitializePaladinLoadout(DateTimeOffset now) =>
        InitializePaladinLoadout(_player.EquipmentArmor,
            EquipmentCategoryIds.UsesBothHands(_player.MainHandWeaponCategory), now);

    internal void InitializePaladinLoadout(decimal equipmentArmor, bool usesTwoHandedWeapon, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(equipmentArmor);
        if (!IsActivePaladin)
            return;
        ActivePaladinState().UsesTwoHandedWeapon = usesTwoHandedWeapon;
        if (TryGetPaladinHook("P-1-1", out var toughness) && equipmentArmor > 0)
            ApplyPaladinEffect(_player.Actor, new EffectDefinition("PALADIN_TOUGHNESS_ARMOR",
                EffectKind.StatModifier, TimeSpan.FromHours(12), 1, EffectStackPolicy.Replace,
                equipmentArmor * 0.02m * toughness.Rank, ModifiedStat: EffectStat.Armor,
                ModifierMode: EffectModifierMode.Flat), now);
    }

    private AbilityTargetModifier ResolvePaladinAutoAttackModifier(DateTimeOffset now)
    {
        if (!IsActivePaladin)
            return new();
        _paladinSessionStates.TryGetValue(_player.Actor.ActorId, out var state);
        decimal damage = state?.UsesTwoHandedWeapon == true && TryGetPaladinHook("R-2-3", out var specialization)
            ? 1 + 0.03m * specialization.Rank : 1;
        decimal critical = TryGetPaladinHook("R-1-3", out var conviction) ? conviction.Rank : 0;
        decimal criticalDamage = ActivePaladinEffect("PALADIN_AVENGING_WRATH", now) is not null
            && TryGetPaladinHook("R-7-2", out var wrathfulVengeance) ? 10 * wrathfulVengeance.Rank : 0;
        return new AbilityTargetModifier(
            DamageMultiplier: damage,
            CriticalChanceBonus: critical,
            CriticalDamageBonus: criticalDamage);
    }

    private void ConfigurePaladinIncomingDamage(PaladinSessionRuntimeState state)
    {
        CombatActorState actor = _player.Actor;
        var hooks = _playerTalents.EventHooks;
        int ardentRank = hooks.FirstOrDefault(hook => hook.TalentId == "P-5-3")?.Rank ?? 0;
        int consecratedRank = hooks.FirstOrDefault(hook => hook.TalentId == "P-7-2")?.Rank ?? 0;
        if (ardentRank == 0 && consecratedRank == 0)
            return;
        var previous = actor.IncomingDamageModifier;
        actor.IncomingDamageModifier = (context, random) =>
        {
            decimal amount = previous?.Invoke(context, random) ?? context.CurrentAmount;
            decimal ardent = PaladinProtectionRuntime.ResolveArdentDefenderReductionPercent(
                actor.CurrentHp, actor.MaxHp, 5 * ardentRank);
            decimal consecrated = state.Protection.ResolveConsecratedProtectionReductionPercent(
                context.OccurredAtUtc, 3 * consecratedRank);
            return amount * (1 - ardent / 100m) * (1 - consecrated / 100m);
        };
    }

    private readonly HashSet<Guid> _paladinIncarnationVerdictConsumed = [];

    private ActiveEffect? ActivePaladinEffect(string id, DateTimeOffset now) =>
        _player.Actor.ActiveEffects.FirstOrDefault(effect => effect.Definition.Id == id
            && effect.SourceId == _player.Actor.ActorId && effect.ExpiresAtUtc > now);

    private void ApplyPaladinAbilityStarted(PaladinSessionRuntimeState state, CombatEvent combatEvent)
    {
        if (combatEvent.ActorId != _player.Actor.ActorId
            || combatEvent.DefinitionId is not { } id || !_abilities.TryGetValue(id, out var ability))
            return;
        DateTimeOffset now = combatEvent.OccurredAtUtc;
        if (ability.Actions?.Any(action => action.Type == AbilityActionType.Healing && action.HealingCanCrit) == true
            && ActivePaladinEffect("PALADIN_DIVINE_FAVOR_READY", now) is not null)
        {
            state.Healing.ConsumeDivineFavorForDirectHeal();
            RemoveOwnedEffectFromActor(_player.Actor, "PALADIN_DIVINE_FAVOR_READY", now);
        }
        if (id == "HOLY_LIGHT") RemoveOwnedEffectFromActor(_player.Actor, PaladinLightsGraceEffectId, now);
        if (id == "FLASH_OF_LIGHT")
        {
            RemoveOwnedEffectFromActor(_player.Actor, PaladinArtOfWarEffectId, now);
            RemoveOwnedEffectFromActor(_player.Actor, "PALADIN_SURGE_OF_LIGHT", now);
        }
        if (id is "HOLY_SHOCK" or "HOLY_SHOCK_OFFENSIVE")
        {
            state.Healing.ConsumeFreeHolyShock();
            RemoveOwnedEffectFromActor(_player.Actor, PaladinHeraldFreeShockEffectId, now);
        }
        if (id == "TEMPLARS_VERDICT")
        {
            bool divinePurposeActive = ActivePaladinEffect(PaladinDivinePurposeEffectId, now) is not null;
            state.DivinePurposeVerdictDamagePending = state.Retribution.ConsumeDivinePurposeForTemplarsVerdict()
                && divinePurposeActive;
            RemoveOwnedEffectFromActor(_player.Actor, PaladinDivinePurposeEffectId, now);
            if (state.Retribution.ConsumeIncarnationTemplarCritical(now))
                _paladinIncarnationVerdictConsumed.Add(_player.Actor.ActorId);
        }
    }
}
