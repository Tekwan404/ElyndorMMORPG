using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private const string HunterMarkEffectId = "ARCHER_HUNTER_MARK";
    private const string SniperFocusEffectId = "ARCHER_SNIPER_FOCUS";
    private const string EfficientShotEffectId = "ARCHER_EFFICIENT_SHOT";
    private const string ExposedDefenseEffectId = "ARCHER_EXPOSED_DEFENSE";
    private const string DeadlyStreakEffectId = "ARCHER_DEADLY_STREAK";
    private const string HeavyArrowEffectId = "ARCHER_HEAVY_ARROW";
    private const string QuickDrawEffectId = "ARCHER_QUICK_DRAW";
    private const string BrokenArmorEffectId = "ARCHER_BROKEN_ARMOR";
    private const string PetCritShotEffectId = "ARCHER_PET_CRIT_SHOT";
    private const string CoordinationEffectId = "ARCHER_COORDINATION";
    private const string BeastSurgeEffectId = "ARCHER_BEAST_SURGE";
    private const string BeastSurgePetEffectId = "ARCHER_BEAST_SURGE_PET";
    private const string BeastSharedTargetEffectId = "ARCHER_BEAST_SHARED_TARGET";
    private const string BeastUnityTargetEffectId = "ARCHER_BEAST_UNITY_TARGET";
    private const string GuardianBarrierEffectId = "ARCHER_GUARDIAN_BARRIER";
    private const string TrapperAttackSpeedEffectId = "ARCHER_TRAPPER_ATTACK_SPEED";
    private const string TrapperAccuracyEffectId = "ARCHER_TRAPPER_ACCURACY";
    private const string TrapperDamageEffectId = "ARCHER_TRAPPER_DAMAGE";
    private const string PredatorBleedEffectId = "ARCHER_PREDATOR_BLEED";
    private const string ArcaneFlowEffectId = "ARCHER_ARCANE_FLOW";
    private const string SpiritAfterArrowEffectId = "ARCHER_SPIRIT_AFTER_ARROW";
    private const string SpiritOwnerResistanceEffectId = "ARCHER_SPIRIT_OWNER_RESISTANCE";
    private const string ArcaneVulnerabilityEffectId = "ARCHER_ARCANE_VULNERABILITY";
    private const string EtherealPoisonEffectId = "ARCHER_ETHEREAL_POISON";
    private const string PhantomBurnEffectId = "ARCHER_PHANTOM_BURN";
    private const string ArcaneExposureEffectId = "ARCHER_ARCANE_EXPOSURE";
    private const string EnchantedShotEffectId = "ARCHER_ENCHANTED_SHOT";
    private const string SpiritFlowEffectId = "ARCHER_SPIRIT_FLOW";
    private const string SpiritGuardShieldEffectId = "ARCHER_SPIRIT_GUARD_SHIELD";
    private const string PetSilenceImmunityEffectId = "ARCHER_PET_SILENCE_IMMUNITY";
    private const string PetControlRecoveryAttackSpeedEffectId = "ARCHER_PET_CONTROL_RECOVERY_AS";
    private const string PetControlRecoveryDamageEffectId = "ARCHER_PET_CONTROL_RECOVERY_DAMAGE";

    private int _archerShotSequence;
    private int _markedShotSequence;
    private int _arcaneArrowSequence;
    private Guid? _lastOwnerHitTargetId;
    private DateTimeOffset? _lastOwnerHitAtUtc;
    private Guid? _lastCompanionHitTargetId;
    private DateTimeOffset? _lastCompanionHitAtUtc;
    private bool _companionWasControlled;

    private bool IsArcher =>
        string.Equals(_player.DefinitionId, "ARCHER", StringComparison.Ordinal);

    private bool IsPhysicalCompanion =>
        _companion?.DefinitionId is "ARCHER_STARTER_PREDATOR" or "ARCHER_GUARDIAN" or "ARCHER_TRAPPER";

    private bool IsSpiritCompanion =>
        string.Equals(_companion?.DefinitionId, "ARCHER_SPIRIT", StringComparison.Ordinal);

    private string? CompanionArchetype => _companion?.DefinitionId switch
    {
        "ARCHER_STARTER_PREDATOR" => "PREDATOR",
        "ARCHER_GUARDIAN" => "GUARDIAN",
        "ARCHER_TRAPPER" => "TRAPPER",
        "ARCHER_SPIRIT" => "SPIRIT",
        _ => null
    };

    private AbilityDefinition ResolveArcherAbility(
        AbilityDefinition ability,
        DateTimeOffset now)
    {
        if (!IsArcher)
            return ability;

        decimal resourceCost = ability.ResourceCost;
        decimal damageMultiplier = ability.DamageMultiplier;
        decimal accuracyBonus = ability.AccuracyBonus;
        decimal criticalChanceBonus = ability.CriticalChanceBonus;
        decimal criticalDamageBonus = ability.CriticalDamageBonus;
        decimal magicPenetrationBonus = ability.MagicPenetrationBonus;
        TimeSpan castTime = ability.CastTime;

        bool physicalShot = IsPhysicalShotAbility(ability);
        bool magicalArrow = IsMagicalArrowAbility(ability);

        if (HasArcherEffect(_player.Actor, SniperFocusEffectId, now)
            && physicalShot)
        {
            accuracyBonus += 10;
            criticalChanceBonus += 8;
        }

        if (physicalShot)
        {
            if (TryGetArcherHook("M-1-2", out ResolvedTalentEventHook physicalCrit))
                criticalChanceBonus += physicalCrit.Value;

            if (HasArcherEffect(_player.Actor, EfficientShotEffectId, now)
                && TryGetArcherHook("M-3-4", out ResolvedTalentEventHook efficient)
                && string.Equals(_player.ResourceType, "FOCUS", StringComparison.Ordinal))
            {
                resourceCost *= Math.Max(0, 1 - efficient.Value / 100m);
            }

            if (string.Equals(ability.Id, "AIMED_SHOT", StringComparison.Ordinal))
            {
                if (TryGetArcherHook("M-4-2", out ResolvedTalentEventHook flawless))
                {
                    accuracyBonus += flawless.Value;
                    criticalChanceBonus += flawless.SecondaryValue;
                }

                if (SelectedTargetHasHunterMark(now)
                    && TryGetArcherHook("M-7-1", out ResolvedTalentEventHook perfect))
                {
                    damageMultiplier *= 1 + perfect.Value / 100m;
                    castTime = ClampArcherCast(
                        castTime - TimeSpan.FromSeconds((double)perfect.CastTimeSeconds));
                }
            }
        }

        if (magicalArrow)
        {
            if (TryGetArcherHook("A-1-2", out ResolvedTalentEventHook arcaneAccuracy))
                accuracyBonus += arcaneAccuracy.Value;
            if (TryGetArcherHook("A-3-3", out ResolvedTalentEventHook arcaneCrit))
                criticalChanceBonus += arcaneCrit.Value;
            if (TryGetArcherHook("A-5-3", out ResolvedTalentEventHook stable))
                resourceCost *= Math.Max(0, 1 - stable.Value / 100m);

            decimal manaPercent = ArcherResourcePercent();
            if (manaPercent < 30
                && TryGetArcherHook("A-6-3", out ResolvedTalentEventHook pureMana))
                resourceCost *= Math.Max(0, 1 - pureMana.Value / 100m);

            if (manaPercent > 70
                && TryGetArcherHook("A-7-3", out ResolvedTalentEventHook saturation))
                damageMultiplier *= 1 + saturation.Value / 100m;

            if (IsArcaneFlowActive(now)
                && TryGetArcherHook("A-5-1", out _))
            {
                resourceCost *= 0.85m;
                damageMultiplier *= 1.15m;
                magicPenetrationBonus += 0.10m;
            }

            if (IsArcaneFlowActive(now)
                && TryGetArcherHook(
                    "A-9-1",
                    "ARCANE_FLOW_CAPSTONE",
                    out ResolvedTalentEventHook capstone))
                criticalChanceBonus += capstone.Value;
        }

        ActiveEffect? quickDraw = FindArcherEffect(_player.Actor, QuickDrawEffectId, now);
        if (quickDraw is not null && ability.Type == AbilityType.Casted && IsShotAbility(ability))
        {
            castTime = ClampArcherCast(
                TimeSpan.FromTicks(
                    (long)(castTime.Ticks * Math.Max(
                        0.1m,
                        1 - quickDraw.Definition.Magnitude / 100m))));
            resourceCost *= Math.Max(0, 1 - quickDraw.RemainingMagnitude / 100m);
        }

        ActiveEffect? coordination =
            FindArcherEffect(_player.Actor, CoordinationEffectId, now);
        if (coordination is not null && IsShotAbility(ability))
        {
            damageMultiplier *= 1 + coordination.Definition.Magnitude / 100m;
            resourceCost *= Math.Max(0, 1 - coordination.RemainingMagnitude / 100m);
        }

        return ability with
        {
            ResourceCost = Math.Max(0, resourceCost),
            DamageMultiplier = Math.Max(0, damageMultiplier),
            AccuracyBonus = accuracyBonus,
            CriticalChanceBonus = criticalChanceBonus,
            CriticalDamageBonus = criticalDamageBonus,
            MagicPenetrationBonus = magicPenetrationBonus,
            CastTime = castTime
        };
    }

    private AbilityTargetModifier ResolveArcherTargetAbilityModifier(
        AbilityDefinition ability,
        CombatActorState target,
        AbilityTargetModifier modifier,
        DateTimeOffset now)
    {
        if (!IsArcher || target.IsDead)
            return modifier;

        decimal damageMultiplier = modifier.DamageMultiplier;
        decimal accuracyBonus = modifier.AccuracyBonus;
        decimal criticalChanceBonus = modifier.CriticalChanceBonus;
        decimal criticalDamageBonus = modifier.CriticalDamageBonus;
        decimal armorPenetrationBonus = modifier.ArmorPenetrationBonus;
        decimal magicPenetrationBonus = modifier.MagicPenetrationBonus;

        bool physicalShot = IsPhysicalShotAbility(ability);
        bool magicalArrow = IsMagicalArrowAbility(ability);
        bool marked = HasArcherEffect(target, HunterMarkEffectId, now);

        if (physicalShot
            && HasArcherEffect(_player.Actor, SniperFocusEffectId, now))
            armorPenetrationBonus += 0.10m;

        if (marked)
        {
            if (physicalShot)
                damageMultiplier *= 1.05m;
            else if (magicalArrow)
                damageMultiplier *= 1.03m;

            if (physicalShot
                && TryGetArcherHook("M-3-2", out ResolvedTalentEventHook deepMark))
                criticalDamageBonus += deepMark.Value;

            if (magicalArrow
                && TryGetArcherHook("A-2-4", out ResolvedTalentEventHook arcaneMark))
                damageMultiplier *= 1 + arcaneMark.Value / 100m;

            if (physicalShot
                && string.Equals(ability.Id, "AIMED_SHOT", StringComparison.Ordinal)
                && TryGetArcherHook("M-5-2", out ResolvedTalentEventHook victim))
                damageMultiplier *= 1 + victim.SecondaryValue / 100m;

            if (TryGetArcherHook("M-9-1", out ResolvedTalentEventHook master))
                criticalChanceBonus += master.SecondaryValue;
        }

        decimal hpPercent = HpPercent(target);
        if (physicalShot
            && hpPercent > 80
            && TryGetArcherHook("M-2-4", out ResolvedTalentEventHook coldCalc))
            criticalChanceBonus += coldCalc.Value;

        if (physicalShot
            && hpPercent < 30
            && TryGetArcherHook("M-6-2", out ResolvedTalentEventHook instinct))
        {
            accuracyBonus += instinct.Value;
            criticalChanceBonus += instinct.Value;
        }

        if (physicalShot
            && string.Equals(ability.Id, "AIMED_SHOT", StringComparison.Ordinal)
            && hpPercent < 25
            && TryGetArcherHook("M-8-1", out ResolvedTalentEventHook heartShot))
            damageMultiplier *= 1 + heartShot.Value / 100m;

        ActiveEffect? deadlyStreak =
            FindArcherEffect(_player.Actor, DeadlyStreakEffectId, now);
        if (deadlyStreak is not null && physicalShot)
            criticalDamageBonus += deadlyStreak.Definition.Magnitude * deadlyStreak.Stacks;

        ActiveEffect? brokenArmor =
            FindArcherEffect(target, BrokenArmorEffectId, now);
        if (brokenArmor is not null && physicalShot)
            armorPenetrationBonus += brokenArmor.Definition.Magnitude / 100m;

        ActiveEffect? exposed =
            FindArcherEffect(_player.Actor, ExposedDefenseEffectId, now);
        if (exposed is not null && physicalShot)
            armorPenetrationBonus += exposed.Definition.Magnitude / 100m;

        if (physicalShot
            && HasCompanionBleed(target, now)
            && TryGetArcherHook("B-4-3", out ResolvedTalentEventHook bloodAndFang))
            damageMultiplier *= 1 + bloodAndFang.Value / 100m;

        if (magicalArrow
            && HasArcherEffect(target, ArcaneVulnerabilityEffectId, now))
        {
            ActiveEffect vulnerability =
                FindArcherEffect(target, ArcaneVulnerabilityEffectId, now)!;
            magicPenetrationBonus += vulnerability.Definition.Magnitude / 100m;
        }

        if (magicalArrow
            && HasArcherEffect(target, ArcaneExposureEffectId, now))
        {
            ActiveEffect exposure =
                FindArcherEffect(target, ArcaneExposureEffectId, now)!;
            damageMultiplier *= 1 + exposure.Definition.Magnitude / 100m;
        }

        if (IsSharedTarget(target.ActorId, now))
        {
            if (IsPhysicalCompanion
                && TryGetArcherHook("B-2-3", out ResolvedTalentEventHook joint))
                damageMultiplier *= 1 + joint.Value / 100m;
            if (IsSpiritCompanion
                && magicalArrow
                && TryGetArcherHook("A-5-2", out ResolvedTalentEventHook spiritUnity))
                damageMultiplier *= 1 + spiritUnity.Value / 100m;
        }

        if (HasArcherEffect(target, BeastUnityTargetEffectId, now))
        {
            ActiveEffect unity =
                FindArcherEffect(target, BeastUnityTargetEffectId, now)!;
            damageMultiplier *= 1 + unity.Definition.Magnitude / 100m;
        }

        ActiveEffect? petCritShot =
            FindArcherEffect(_player.Actor, PetCritShotEffectId, now);
        if (petCritShot is not null && IsShotAbility(ability))
            damageMultiplier *= 1 + petCritShot.Definition.Magnitude / 100m;

        return modifier with
        {
            DamageMultiplier = damageMultiplier,
            AccuracyBonus = accuracyBonus,
            CriticalChanceBonus = criticalChanceBonus,
            CriticalDamageBonus = criticalDamageBonus,
            ArmorPenetrationBonus = armorPenetrationBonus,
            MagicPenetrationBonus = magicPenetrationBonus
        };
    }

    private void OnArcherAbilityStarted(AbilityDefinition ability, DateTimeOffset now)
    {
        if (!IsArcher) return;

        if (IsShotAbility(ability))
        {
            if (IsPhysicalShotAbility(ability))
                ConsumeArcherStackedEffect(_player.Actor, ExposedDefenseEffectId, 1, now);

            RemoveArcherEffect(_player.Actor, EfficientShotEffectId, now);
            RemoveArcherEffect(_player.Actor, CoordinationEffectId, now);
            RemoveArcherEffect(_player.Actor, PetCritShotEffectId, now);
            if (ability.Type == AbilityType.Casted)
                RemoveArcherEffect(_player.Actor, QuickDrawEffectId, now);
        }
    }

    private void OnArcherAbilityResolved(
        AbilityDefinition ability,
        AbilityExecutionResult execution,
        DateTimeOffset now)
    {
        if (!IsArcher || Status != CombatSessionStatus.Active)
            return;

        bool hit = DidHit(execution);
        bool critical = DidCrit(execution);

        if (string.Equals(ability.Id, "SNIPER_FOCUS", StringComparison.Ordinal))
        {
            ActivateSniperFocus(now);
            return;
        }
        if (string.Equals(ability.Id, "HEAVY_ARROW", StringComparison.Ordinal))
        {
            ApplyArcherEffect(
                _player.Actor,
                new EffectDefinition(
                    HeavyArrowEffectId,
                    EffectKind.Buff,
                    TimeSpan.FromHours(12),
                    1,
                    EffectStackPolicy.Replace,
                    1.75m),
                now);
            return;
        }
        if (string.Equals(ability.Id, "BEAST_SURGE", StringComparison.Ordinal))
        {
            ActivateBeastSurge(now);
            return;
        }
        if (string.Equals(ability.Id, "RETURN_TO_OWNER", StringComparison.Ordinal))
        {
            CleanseCompanion(now);
            return;
        }
        if (string.Equals(ability.Id, "ARCANE_FLOW", StringComparison.Ordinal))
        {
            ActivateArcaneFlow(now);
            return;
        }
        if (string.Equals(ability.Id, "ENCHANTED_SHOT", StringComparison.Ordinal))
        {
            ApplyArcherEffect(
                _player.Actor,
                new EffectDefinition(
                    EnchantedShotEffectId,
                    EffectKind.Buff,
                    TimeSpan.FromHours(12),
                    1,
                    EffectStackPolicy.Replace,
                    0.70m),
                now);
            return;
        }
        if (string.Equals(ability.Id, "COMMAND_ATTACK", StringComparison.Ordinal))
        {
            ResolveCommandAttack(execution, now);
            return;
        }

        if (hit && IsShotAbility(ability))
            ApplyShotHitHooks(ability, execution, critical, now);
        else if (!hit && IsShotAbility(ability))
            ApplyShotMissHooks(ability, now);

        if (critical && IsPhysicalShotAbility(ability)
            && TryGetArcherHook("M-3-4", out ResolvedTalentEventHook efficient))
        {
            ApplyArcherEffect(
                _player.Actor,
                new EffectDefinition(
                    EfficientShotEffectId,
                    EffectKind.Buff,
                    efficient.Duration,
                    1,
                    EffectStackPolicy.Replace,
                    efficient.Value),
                now);
        }

        if (critical && IsMagicalArrowAbility(ability))
            ApplyArcaneCriticalArrowHooks(execution, ability, now);
    }

    private void ApplyShotHitHooks(
        AbilityDefinition ability,
        AbilityExecutionResult execution,
        bool critical,
        DateTimeOffset now)
    {
        CombatActorState? target = ResolveExecutionEnemyTarget(execution);
        if (target is null)
            return;

        _lastOwnerHitTargetId = target.ActorId;
        _lastOwnerHitAtUtc = now;
        _archerShotSequence++;

        if (TryGetArcherHook("M-1-3", out ResolvedTalentEventHook concentration)
            && TalentCooldownReady(concentration.TalentId, now))
        {
            AddResource(_player.Actor, concentration.Value, now, concentration.TalentId);
            StartTalentCooldown(concentration, now);
        }

        if (TryGetArcherHook("M-5-4", out ResolvedTalentEventHook rhythm)
            && _archerShotSequence % Math.Max(1, rhythm.TriggerCount) == 0)
            AddResource(_player.Actor, rhythm.Value, now, rhythm.TalentId);

        if (HasArcherEffect(target, HunterMarkEffectId, now)
            && TryGetArcherHook("M-9-1", out ResolvedTalentEventHook master))
        {
            _markedShotSequence++;
            if (critical
                && TalentCooldownReady(master.TalentId + ":FOCUS", now))
            {
                ReduceCooldown(
                    _playerRuntime,
                    "SNIPER_FOCUS",
                    TimeSpan.FromSeconds(1),
                    now);
                _talentInternalCooldowns[master.TalentId + ":FOCUS"] =
                    now + TimeSpan.FromSeconds(2);
            }

            if (_markedShotSequence % Math.Max(1, master.TriggerCount) == 0)
                ResolveOwnerExtraArrow(target, 0.60m, master.TalentId, now);
        }

        if (string.Equals(ability.Id, "PIERCING_ARROW", StringComparison.Ordinal)
            && TryGetArcherHook("M-4-3", out ResolvedTalentEventHook exposed))
        {
            ApplyArcherEffect(
                _player.Actor,
                new EffectDefinition(
                    ExposedDefenseEffectId,
                    EffectKind.Buff,
                    exposed.Duration,
                    Math.Max(1, exposed.TriggerCount),
                    EffectStackPolicy.Replace,
                    exposed.Value),
                now);
            ActiveEffect? effect =
                FindArcherEffect(_player.Actor, ExposedDefenseEffectId, now);
            if (effect is not null)
                effect.Stacks = Math.Max(1, exposed.TriggerCount);
        }

        if (IsMagicalArrowAbility(ability))
        {
            if (TryGetArcherHook("A-3-4", out ResolvedTalentEventHook weaving)
                && _companion is not null && IsSpiritCompanion)
            {
                ApplyCompanionMultiplier(
                    SpiritAfterArrowEffectId,
                    EffectStat.OutgoingDamageMultiplier,
                    1 + weaving.Value / 100m,
                    weaving.Duration,
                    now);
            }

            if (TryGetArcherHook("A-6-1", out ResolvedTalentEventHook poison)
                && TalentCooldownReady(poison.TalentId, now)
                && _random.NextUnit() < poison.SecondaryValue / 100m)
            {
                ApplySpellPowerDot(
                    target,
                    EtherealPoisonEffectId,
                    poison.Value,
                    poison.Duration,
                    poison.TickInterval,
                    now,
                    _player.Actor.ActorId);
                StartTalentCooldown(poison, now);
            }

            _arcaneArrowSequence++;
            if (TryGetArcherHook("A-9-1", out ResolvedTalentEventHook capstone)
                && _arcaneArrowSequence % Math.Max(1, capstone.TriggerCount) == 0)
            {
                ResolveSpellPowerProc(
                    target,
                    capstone.Value / 100m,
                    "A-9-1",
                    now);
                ResolveCompanionExtraAttack(
                    target,
                    capstone.SecondaryValue / 100m,
                    "A-9-1_SPIRIT",
                    now);
            }

            if (TryGetArcherHook("A-7-2", out ResolvedTalentEventHook echo)
                && TalentCooldownReady(echo.TalentId, now)
                && _random.NextUnit() < echo.ChancePercent / 100m)
            {
                decimal damage = execution.Events
                    .Where(item => item.Type == CombatEventType.DamageDealt)
                    .Sum(item => item.AmountBeforeShields > 0
                        ? item.AmountBeforeShields
                        : item.Amount);
                if (damage > 0)
                {
                    ResolveCompanionFixedDamage(
                        target,
                        damage * echo.Value / 100m,
                        DamageType.Magical,
                        echo.TalentId,
                        now);
                    StartTalentCooldown(echo, now);
                }
            }
        }

        if (string.Equals(ability.Id, "GHOST_VOLLEY", StringComparison.Ordinal)
            && TryGetArcherHook("A-8-1", out ResolvedTalentEventHook rain))
        {
            foreach (CombatActorState hitTarget in HitTargets(execution))
            {
                ApplyArcherEffect(
                    hitTarget,
                    new EffectDefinition(
                        ArcaneExposureEffectId,
                        EffectKind.Debuff,
                        rain.Duration,
                        1,
                        EffectStackPolicy.Replace,
                        rain.SecondaryValue,
                        SourceSpecific: true),
                    now);
            }
        }
    }

    private void ApplyShotMissHooks(AbilityDefinition ability, DateTimeOffset now)
    {
        _archerShotSequence = 0;
        _markedShotSequence = 0;

        if (IsPhysicalShotAbility(ability)
            && ability.ResourceCost > 0
            && TryGetArcherHook("M-7-3", out ResolvedTalentEventHook noWaste))
        {
            AddResource(
                _player.Actor,
                ability.ResourceCost * noWaste.Value / 100m,
                now,
                noWaste.TalentId);
        }
    }

    private void ApplyArcaneCriticalArrowHooks(
        AbilityExecutionResult execution,
        AbilityDefinition ability,
        DateTimeOffset now)
    {
        CombatActorState? target = ResolveExecutionEnemyTarget(execution);
        if (target is null)
            return;

        if (TryGetArcherHook("A-5-4", out ResolvedTalentEventHook vulnerability))
        {
            ApplyArcherEffect(
                target,
                new EffectDefinition(
                    ArcaneVulnerabilityEffectId,
                    EffectKind.Debuff,
                    vulnerability.Duration,
                    1,
                    EffectStackPolicy.Replace,
                    vulnerability.Value,
                    SourceSpecific: true),
                now);
        }

        if (string.Equals(ability.Id, "PHANTOM_ARROW", StringComparison.Ordinal)
            && TryGetArcherHook("A-4-2", out ResolvedTalentEventHook burn))
        {
            ApplySpellPowerDot(
                target,
                PhantomBurnEffectId,
                burn.Value,
                burn.Duration,
                burn.TickInterval,
                now,
                _player.Actor.ActorId);
        }

        if (TryGetArcherHook("A-4-3", out ResolvedTalentEventHook response)
            && TalentCooldownReady(response.TalentId, now))
        {
            ResolveCompanionExtraAttack(
                target,
                response.Value / 100m,
                response.TalentId,
                now);
            StartTalentCooldown(response, now);
        }
    }

    private void ActivateSniperFocus(DateTimeOffset now)
    {
        ApplyArcherEffect(
            _player.Actor,
            new EffectDefinition(
                SniperFocusEffectId,
                EffectKind.Buff,
                TimeSpan.FromSeconds(10),
                1,
                EffectStackPolicy.Replace,
                1),
            now);

        if (HasArcherTalent("M-8-3"))
        {
            _playerRuntime.Cooldowns.Remove("PIERCING_ARROW");
            _playerRuntime.Cooldowns.Remove("AIMED_SHOT");
        }
    }

    private void ActivateBeastSurge(DateTimeOffset now)
    {
        if (_companion is null || !IsPhysicalCompanion)
            return;

        ApplyArcherEffect(
            _player.Actor,
            new EffectDefinition(
                BeastSurgeEffectId,
                EffectKind.Buff,
                TimeSpan.FromSeconds(10),
                1,
                EffectStackPolicy.Replace,
                15),
            now);
        ApplyCompanionMultiplier(
            BeastSurgePetEffectId,
            EffectStat.OutgoingDamageMultiplier,
            1.20m,
            TimeSpan.FromSeconds(10),
            now);
        ApplyCompanionMultiplier(
            BeastSurgePetEffectId + "_AS",
            EffectStat.AttackSpeed,
            1.20m,
            TimeSpan.FromSeconds(10),
            now);

        if (HasArcherTalent("B-8-3"))
        {
            _playerRuntime.Cooldowns.Remove("COMMAND_ATTACK");
            ApplyArcherEffectFrom(
                _companion.Actor,
                _companion.Actor.ActorId,
                new EffectDefinition(
                    PetSilenceImmunityEffectId,
                    EffectKind.Buff,
                    TimeSpan.FromSeconds(3),
                    1,
                    EffectStackPolicy.Replace,
                    0),
                now);
        }
    }

    private void ActivateArcaneFlow(DateTimeOffset now)
    {
        ApplyArcherEffect(
            _player.Actor,
            new EffectDefinition(
                ArcaneFlowEffectId,
                EffectKind.Buff,
                TimeSpan.FromSeconds(10),
                1,
                EffectStackPolicy.Replace,
                0),
            now);

        if (TryGetArcherHook("A-8-3", out ResolvedTalentEventHook perfect))
        {
            AddResource(
                _player.Actor,
                _player.Actor.MaxResource * perfect.Value / 100m,
                now,
                perfect.TalentId);
            _playerRuntime.Cooldowns.Remove("PHANTOM_ARROW");

            if (_companion is not null && IsSpiritCompanion)
            {
                ApplyCompanionMultiplier(
                    SpiritFlowEffectId,
                    EffectStat.OutgoingDamageMultiplier,
                    1 + perfect.SecondaryValue / 100m,
                    perfect.Duration,
                    now);
            }
        }

        if (_companion is not null && IsSpiritCompanion
            && TryGetArcherHook(
                "A-9-1",
                "ARCANE_FLOW_CAPSTONE",
                out ResolvedTalentEventHook capstone))
        {
            ApplyCompanionMultiplier(
                SpiritFlowEffectId + "_AS",
                EffectStat.AttackSpeed,
                1 + capstone.SecondaryValue / 100m,
                TimeSpan.FromSeconds(10),
                now);
        }
    }

    private void ResolveCommandAttack(
        AbilityExecutionResult execution,
        DateTimeOffset now)
    {
        if (_companion is null || _companion.Actor.IsDead || !IsPhysicalCompanion)
            return;

        CombatActorState? target = ResolveExecutionEnemyTarget(execution)
            ?? _enemiesById.GetValueOrDefault(_selectedTargetActorId)?.Actor;
        if (target is null || target.IsDead)
            return;

        switch (CompanionArchetype)
        {
            case "PREDATOR":
                ResolveCompanionExtraAttack(
                    target,
                    1.50m * ResolvePhysicalCompanionAbilityDamageMultiplier(),
                    "COMMAND_ATTACK",
                    now);
                break;

            case "GUARDIAN":
                ApplyArcherEffect(
                    _player.Actor,
                    new EffectDefinition(
                        GuardianBarrierEffectId,
                        EffectKind.StatModifier,
                        TimeSpan.FromSeconds(6),
                        1,
                        EffectStackPolicy.Replace,
                        0.90m,
                        ModifiedStat: EffectStat.IncomingDamageMultiplier,
                        ModifierMode: EffectModifierMode.Multiplicative),
                    now);
                break;

            case "TRAPPER":
                ApplyTrapperDebuff(target, now);
                if (TryGetArcherHook("B-6-3", out ResolvedTalentEventHook silent)
                    && TalentCooldownReady($"{silent.TalentId}:{target.ActorId}", now))
                {
                    ApplyArcherEffect(
                        target,
                        new EffectDefinition(
                            "ARCHER_TRAPPER_SILENCE",
                            EffectKind.Silence,
                            silent.Duration,
                            1,
                            EffectStackPolicy.Replace,
                            0,
                            SourceSpecific: true),
                        now);
                    _talentInternalCooldowns[$"{silent.TalentId}:{target.ActorId}"] =
                        now + silent.InternalCooldown;
                }
                break;
        }

        if (TryGetArcherHook("B-8-2", out ResolvedTalentEventHook coordination))
        {
            ApplyArcherEffect(
                _player.Actor,
                new EffectDefinition(
                    CoordinationEffectId,
                    EffectKind.Buff,
                    coordination.Duration,
                    1,
                    EffectStackPolicy.Replace,
                    coordination.Value),
                now);
            ActiveEffect? effect =
                FindArcherEffect(_player.Actor, CoordinationEffectId, now);
            if (effect is not null)
                effect.RemainingMagnitude = coordination.SecondaryValue;
        }
    }

    private void ApplyTrapperDebuff(CombatActorState target, DateTimeOffset now)
    {
        if (!TryGetArcherHook("B-3-3", out ResolvedTalentEventHook trapper))
            return;

        ApplyArcherEffect(
            target,
            new EffectDefinition(
                TrapperAttackSpeedEffectId,
                EffectKind.StatModifier,
                trapper.Duration,
                1,
                EffectStackPolicy.Replace,
                Math.Max(0.1m, 1 - trapper.Value / 100m),
                ModifiedStat: EffectStat.AttackSpeed,
                ModifierMode: EffectModifierMode.Multiplicative,
                SourceSpecific: true),
            now);
        ApplyArcherEffect(
            target,
            new EffectDefinition(
                TrapperAccuracyEffectId,
                EffectKind.StatModifier,
                trapper.Duration,
                1,
                EffectStackPolicy.Replace,
                Math.Max(0.1m, 1 - trapper.SecondaryValue / 100m),
                ModifiedStat: EffectStat.Accuracy,
                ModifierMode: EffectModifierMode.Multiplicative,
                SourceSpecific: true),
            now);

        if (TryGetArcherHook("B-7-3", out ResolvedTalentEventHook perfect))
        {
            ApplyArcherEffect(
                target,
                new EffectDefinition(
                    TrapperDamageEffectId,
                    EffectKind.StatModifier,
                    trapper.Duration,
                    1,
                    EffectStackPolicy.Replace,
                    Math.Max(0.1m, 1 - perfect.Value / 100m),
                    ModifiedStat: EffectStat.OutgoingDamageMultiplier,
                    ModifierMode: EffectModifierMode.Multiplicative,
                    SourceSpecific: true),
                now);
        }
    }

    private void CleanseCompanion(DateTimeOffset now)
    {
        if (_companion is null || _companion.Actor.IsDead)
            return;

        RemoveTalentEffects(
            EffectEngine.RemoveByKind(
                _companion.Actor,
                EffectKind.Silence,
                now));

        ActiveEffect? negative = _companion.Actor.ActiveEffects
            .Where(effect => effect.ExpiresAtUtc > now)
            .Where(effect => effect.Definition.Kind is
                EffectKind.Debuff or EffectKind.DamageOverTime or EffectKind.Stun)
            .OrderBy(effect => effect.Sequence)
            .FirstOrDefault();
        if (negative is not null)
            RemoveTalentEffects(
                EffectEngine.Remove(
                    _companion.Actor,
                    negative.Definition.Id,
                    now));

        SyncCompanionControlRecovery(now);
    }

    private void ApplyArcherCriticalHooks(CombatEvent combatEvent)
    {
        if (!IsArcher)
            return;

        DateTimeOffset now = combatEvent.OccurredAtUtc;

        if (combatEvent.SourceActorId == _player.Actor.ActorId)
        {
            CombatActorState? target = combatEvent.TargetActorId is { } targetId
                && _enemiesById.TryGetValue(targetId, out CombatParticipantDefinition? enemy)
                    ? enemy.Actor
                    : null;

            if (target is not null
                && IsPhysicalShotDefinition(combatEvent.DefinitionId)
                && TryGetArcherHook("M-7-2", out ResolvedTalentEventHook broken))
            {
                ApplyArcherEffect(
                    target,
                    new EffectDefinition(
                        BrokenArmorEffectId,
                        EffectKind.Debuff,
                        broken.Duration,
                        1,
                        EffectStackPolicy.Replace,
                        broken.Value,
                        SourceSpecific: true),
                    now);
            }

            if (IsPhysicalShotDefinition(combatEvent.DefinitionId)
                && TryGetArcherHook("M-6-1", out ResolvedTalentEventHook streak))
            {
                ApplyArcherEffect(
                    _player.Actor,
                    new EffectDefinition(
                        DeadlyStreakEffectId,
                        EffectKind.Buff,
                        streak.Duration,
                        3,
                        EffectStackPolicy.Stack,
                        streak.Value),
                    now);
            }

            if (_companion is not null && IsPhysicalCompanion
                && TryGetArcherHook("M-4-4", out ResolvedTalentEventHook sync)
                && TalentCooldownReady(sync.TalentId, now))
            {
                ApplyCompanionMultiplier(
                    "ARCHER_MARKSMAN_SYNC_AS",
                    EffectStat.AttackSpeed,
                    1 + sync.Value / 100m,
                    sync.Duration,
                    now);
                StartTalentCooldown(sync, now);
            }

            if (_companion is not null && IsPhysicalCompanion
                && TryGetArcherHook("B-4-1", out ResolvedTalentEventHook fury))
            {
                ApplyCompanionMultiplier(
                    "ARCHER_BESTIAL_FURY_AS",
                    EffectStat.AttackSpeed,
                    1 + fury.Value / 100m,
                    fury.Duration,
                    now);
            }

            if (_companion is not null && IsPhysicalCompanion
                && TryGetArcherHook(
                    "B-9-1",
                    "BEAST_MASTER_EXTRA_ATTACK",
                    out ResolvedTalentEventHook master)
                && TalentCooldownReady(master.TalentId + ":EXTRA", now)
                && _random.NextUnit() < master.Value / 100m
                && target is not null)
            {
                ResolveCompanionExtraAttack(target, 1, master.TalentId, now);
                _talentInternalCooldowns[master.TalentId + ":EXTRA"] =
                    now + master.InternalCooldown;
            }
        }

        if (_companion is not null
            && combatEvent.SourceActorId == _companion.Actor.ActorId)
        {
            CombatActorState? target = combatEvent.TargetActorId is { } targetId
                && _enemiesById.TryGetValue(targetId, out CombatParticipantDefinition? enemy)
                    ? enemy.Actor
                    : null;

            if (CompanionArchetype == "PREDATOR"
                && target is not null
                && TryGetArcherHook("B-3-1", out ResolvedTalentEventHook bleed))
            {
                decimal totalPercent = bleed.Value;
                TimeSpan duration = bleed.Duration;
                if (TryGetArcherHook("B-7-1", out ResolvedTalentEventHook perfect))
                {
                    totalPercent *= 1 + perfect.Value / 100m;
                    duration += perfect.Duration;
                }

                ApplyCompanionAttackPowerDot(
                    target,
                    PredatorBleedEffectId,
                    totalPercent,
                    duration,
                    TimeSpan.FromSeconds(1),
                    now);
            }

            decimal ownerShotBonus = 0;
            TimeSpan durationShot = TimeSpan.FromSeconds(5);
            if (TryGetArcherHook("B-4-2", out ResolvedTalentEventHook packHunter))
            {
                ownerShotBonus = Math.Max(ownerShotBonus, packHunter.Value);
                durationShot = packHunter.Duration == TimeSpan.Zero
                    ? durationShot
                    : packHunter.Duration;
            }
            if (TryGetArcherHook(
                    "B-9-1",
                    "BEAST_MASTER_OWNER_SHOT",
                    out ResolvedTalentEventHook beastMaster))
                ownerShotBonus = Math.Max(ownerShotBonus, beastMaster.Value);

            if (ownerShotBonus > 0)
            {
                ApplyArcherEffect(
                    _player.Actor,
                    new EffectDefinition(
                        PetCritShotEffectId,
                        EffectKind.Buff,
                        durationShot,
                        1,
                        EffectStackPolicy.Replace,
                        ownerShotBonus),
                    now);
            }
        }
    }

    private void ApplyArcherIncomingCriticalHooks(CombatEvent combatEvent)
    {
        if (!IsArcher || combatEvent.TargetActorId != _player.Actor.ActorId)
            return;

        DateTimeOffset now = combatEvent.OccurredAtUtc;
        CombatActorState? target = _enemiesById.GetValueOrDefault(
            combatEvent.SourceActorId ?? Guid.Empty)?.Actor;

        if (_companion is not null
            && IsPhysicalCompanion
            && target is not null
            && TryGetArcherHook("M-6-4", out ResolvedTalentEventHook cover)
            && TalentCooldownReady(cover.TalentId, now))
        {
            ResolveCompanionExtraAttack(target, cover.Value / 100m, cover.TalentId, now);
            StartTalentCooldown(cover, now);
        }

        if (_companion is not null
            && IsSpiritCompanion
            && TryGetArcherHook("A-6-2", out ResolvedTalentEventHook shield)
            && TalentCooldownReady(shield.TalentId, now))
        {
            ApplyArcherEffect(
                _player.Actor,
                new EffectDefinition(
                    SpiritGuardShieldEffectId,
                    EffectKind.Shield,
                    shield.Duration,
                    1,
                    EffectStackPolicy.Replace,
                    _player.Actor.MaxHp * shield.Value / 100m),
                now);
            StartTalentCooldown(shield, now);
        }
    }

    private void ApplyArcherDamageTakenHooks(CombatEvent combatEvent)
    {
        if (!IsArcher
            || combatEvent.TargetActorId != _player.Actor.ActorId
            || combatEvent.Amount <= 0
            || combatEvent.IsPeriodic
            || _companion is null
            || _companion.Actor.IsDead)
            return;

        DateTimeOffset now = combatEvent.OccurredAtUtc;

        if (CompanionArchetype == "GUARDIAN"
            && TryGetArcherHook("B-7-2", out ResolvedTalentEventHook unbreakable)
            && combatEvent.Amount > _player.Actor.MaxHp * unbreakable.Threshold / 100m
            && TalentCooldownReady(unbreakable.TalentId, now))
        {
            decimal restored = combatEvent.Amount * unbreakable.Value / 100m;
            RestoreArcherHp(_player.Actor, restored, now, unbreakable.TalentId);
            DamageCompanionDirect(
                _companion.Actor.MaxHp * unbreakable.SecondaryValue / 100m,
                unbreakable.TalentId,
                now);
            StartTalentCooldown(unbreakable, now);
            return;
        }

        if (CompanionArchetype == "GUARDIAN"
            && TryGetArcherHook("B-3-2", out ResolvedTalentEventHook intercept)
            && TalentCooldownReady(intercept.TalentId, now)
            && _random.NextUnit() < intercept.Value / 100m)
        {
            decimal redirected = combatEvent.Amount * intercept.SecondaryValue / 100m;
            RestoreArcherHp(_player.Actor, redirected, now, intercept.TalentId);
            DamageCompanionDirect(redirected, intercept.TalentId, now);
            StartTalentCooldown(intercept, now);
        }
    }

    private void ApplyArcherHealingHooks(CombatEvent combatEvent)
    {
        if (!IsArcher
            || combatEvent.TargetActorId != _player.Actor.ActorId
            || combatEvent.Amount <= 0
            || _companion is null
            || _companion.Actor.IsDead
            || !TryGetArcherHook("B-7-4", out ResolvedTalentEventHook oneBlood))
            return;

        decimal before = _companion.Actor.CurrentHp;
        _companion.Actor.ApplyHealing(combatEvent.Amount * oneBlood.Value / 100m);
        decimal actual = _companion.Actor.CurrentHp - before;
        if (actual > 0)
        {
            Append(new CombatEvent(
                CombatEventType.HealingApplied,
                combatEvent.OccurredAtUtc,
                _companion.Actor.ActorId,
                oneBlood.TalentId,
                actual,
                SourceActorId: _player.Actor.ActorId,
                TargetActorId: _companion.Actor.ActorId));
        }
    }

    private void ApplyArcherCompanionDamageHooks(CombatEvent combatEvent)
    {
        if (!IsArcher
            || _companion is null
            || combatEvent.SourceActorId != _companion.Actor.ActorId
            || combatEvent.Amount <= 0
            || combatEvent.TargetActorId is not { } targetId)
            return;

        DateTimeOffset now = combatEvent.OccurredAtUtc;
        _lastCompanionHitTargetId = targetId;
        _lastCompanionHitAtUtc = now;

        if (IsPhysicalCompanion
            && TryGetArcherHook("B-1-3", out ResolvedTalentEventHook rhythm)
            && TalentCooldownReady(rhythm.TalentId, now)
            && _random.NextUnit() < rhythm.Value / 100m)
        {
            AddResource(_player.Actor, rhythm.SecondaryValue, now, rhythm.TalentId);
            StartTalentCooldown(rhythm, now);
        }

        if (IsSpiritCompanion
            && TryGetArcherHook("A-3-2", out ResolvedTalentEventHook spiritImpulse)
            && TalentCooldownReady(spiritImpulse.TalentId, now)
            && _random.NextUnit() < spiritImpulse.Value / 100m)
        {
            AddResource(
                _player.Actor,
                spiritImpulse.SecondaryValue,
                now,
                spiritImpulse.TalentId);
            StartTalentCooldown(spiritImpulse, now);
        }

        if (_lastOwnerHitTargetId == targetId
            && _lastOwnerHitAtUtc is { } ownerAt
            && Math.Abs((now - ownerAt).TotalSeconds) <= 2
            && TryGetArcherHook("B-5-3", out ResolvedTalentEventHook unity)
            && _enemiesById.TryGetValue(targetId, out CombatParticipantDefinition? target))
        {
            ApplyArcherEffect(
                target.Actor,
                new EffectDefinition(
                    BeastUnityTargetEffectId,
                    EffectKind.Debuff,
                    unity.Duration,
                    1,
                    EffectStackPolicy.Replace,
                    unity.Value,
                    SourceSpecific: true),
                now);
        }
    }

    private void ApplyArcherEnemyKilledHooks(DateTimeOffset now)
    {
        if (!IsArcher
            || !TryGetArcherHook("M-7-4", out ResolvedTalentEventHook quickDraw))
            return;

        ApplyArcherEffect(
            _player.Actor,
            new EffectDefinition(
                QuickDrawEffectId,
                EffectKind.Buff,
                quickDraw.Duration,
                1,
                EffectStackPolicy.Replace,
                quickDraw.Value),
            now);
        ActiveEffect? effect =
            FindArcherEffect(_player.Actor, QuickDrawEffectId, now);
        if (effect is not null)
            effect.RemainingMagnitude = quickDraw.SecondaryValue;
    }

    private void ApplyArcherResourceThresholdHooks(CombatEvent combatEvent)
    {
        if (!IsArcher
            || !string.Equals(_player.ResourceType, "MANA", StringComparison.Ordinal)
            || combatEvent.ActorId != _player.Actor.ActorId
            || combatEvent.Amount >= 0
            || !TryGetArcherHook("A-7-4", out ResolvedTalentEventHook lastSpark)
            || ArcherResourcePercent() >= lastSpark.Threshold
            || !TalentCooldownReady(lastSpark.TalentId, combatEvent.OccurredAtUtc))
            return;

        AddResource(
            _player.Actor,
            _player.Actor.MaxResource * lastSpark.Value / 100m,
            combatEvent.OccurredAtUtc,
            lastSpark.TalentId);
        StartTalentCooldown(lastSpark, combatEvent.OccurredAtUtc);
    }

    private void SyncArcherConditionalEffects(DateTimeOffset now)
    {
        if (!IsArcher)
            return;

        SyncCompanionSilenceImmunity(now);
        SyncCompanionControlRecovery(now);

        if (_companion is not null && !_companion.Actor.IsDead
            && IsSpiritCompanion
            && TryGetArcherHook("A-4-4", out ResolvedTalentEventHook defense))
        {
            EnsureArcherPlayerMultiplier(
                SpiritOwnerResistanceEffectId,
                EffectStat.MagicResistance,
                1 + defense.Value / 100m,
                now);
        }
        else
        {
            RemoveArcherEffect(_player.Actor, SpiritOwnerResistanceEffectId, now);
        }

        if (_companion is not null
            && !_companion.Actor.IsDead
            && CompanionArchetype == "GUARDIAN"
            && HpPercent(_companion.Actor) > 50
            && TryGetArcherHook("B-6-2", out ResolvedTalentEventHook barrier))
        {
            EnsureArcherPlayerMultiplier(
                GuardianBarrierEffectId + "_PASSIVE",
                EffectStat.IncomingDamageMultiplier,
                Math.Max(0.1m, 1 - barrier.Value / 100m),
                now);
        }
        else
        {
            RemoveArcherEffect(_player.Actor, GuardianBarrierEffectId + "_PASSIVE", now);
        }
    }

    private void SyncCompanionSilenceImmunity(DateTimeOffset now)
    {
        if (_companion is null
            || _companion.Actor.IsDead
            || !HasArcherEffect(
                _companion.Actor,
                PetSilenceImmunityEffectId,
                now))
        {
            return;
        }

        RemoveTalentEffects(
            EffectEngine.RemoveByKind(
                _companion.Actor,
                EffectKind.Silence,
                now));
    }

    private void SyncCompanionControlRecovery(DateTimeOffset now)
    {
        if (_companion is null
            || _companion.Actor.IsDead
            || !IsPhysicalCompanion)
        {
            _companionWasControlled = false;
            return;
        }

        bool controlled =
            EffectEngine.HasControl(
                _companion.Actor,
                EffectKind.Stun,
                now)
            || EffectEngine.HasControl(
                _companion.Actor,
                EffectKind.Silence,
                now);

        if (_companionWasControlled
            && !controlled
            && TryGetArcherHook(
                "B-6-4",
                out ResolvedTalentEventHook recovery))
        {
            ApplyCompanionMultiplier(
                PetControlRecoveryAttackSpeedEffectId,
                EffectStat.AttackSpeed,
                1 + recovery.Value / 100m,
                recovery.Duration,
                now);
            ApplyCompanionMultiplier(
                PetControlRecoveryDamageEffectId,
                EffectStat.OutgoingDamageMultiplier,
                1 + recovery.SecondaryValue / 100m,
                recovery.Duration,
                now);
        }

        _companionWasControlled = controlled;
    }

    private decimal ResolvePhysicalCompanionAbilityDamageMultiplier()
    {
        if (!IsPhysicalCompanion
            || !TryGetArcherHook(
                "B-3-4",
                out ResolvedTalentEventHook training))
        {
            return 1;
        }

        return 1 + training.Value / 100m;
    }

    private decimal EffectiveArcherResourceRegenPerSecond(
        decimal regen,
        DateTimeOffset now)
    {
        if (!IsArcher || regen <= 0)
            return regen;

        if (string.Equals(_player.ResourceType, "MANA", StringComparison.Ordinal)
            && TryGetArcherHook("A-1-4", out ResolvedTalentEventHook mind))
            regen *= 1 + mind.Value / 100m;

        if (HasArcherEffect(_player.Actor, BeastSurgeEffectId, now))
            regen *= 1.15m;

        return regen;
    }

    private ArcherAutoAttackModifier ResolveArcherAutoAttackModifier(
        CombatParticipantDefinition target,
        decimal baseDamage,
        DateTimeOffset now)
    {
        if (!IsArcher)
            return new(1, 0, false, false);

        decimal multiplier = 1;
        decimal armorPenetration = 0;
        decimal accuracyBonus = 0;
        decimal criticalChanceBonus = 0;
        decimal criticalDamageBonus = 0;
        bool enchanted = HasArcherEffect(_player.Actor, EnchantedShotEffectId, now);
        bool heavy = HasArcherEffect(_player.Actor, HeavyArrowEffectId, now);

        if (heavy)
            multiplier *= 1.75m;

        if (HasArcherEffect(_player.Actor, SniperFocusEffectId, now))
        {
            accuracyBonus += 10;
            criticalChanceBonus += 8;
            armorPenetration += 0.10m;
        }

        if (HpPercent(target.Actor) > 80
            && TryGetArcherHook("M-2-4", out ResolvedTalentEventHook coldCalc))
            criticalChanceBonus += coldCalc.Value;

        if (HpPercent(target.Actor) < 30
            && TryGetArcherHook("M-6-2", out ResolvedTalentEventHook instinct))
        {
            accuracyBonus += instinct.Value;
            criticalChanceBonus += instinct.Value;
        }

        ActiveEffect? deadly =
            FindArcherEffect(_player.Actor, DeadlyStreakEffectId, now);
        if (deadly is not null)
            criticalDamageBonus += deadly.Definition.Magnitude * deadly.Stacks;

        bool marked = HasArcherEffect(target.Actor, HunterMarkEffectId, now);
        if (marked)
        {
            multiplier *= 1.05m;
            if (TryGetArcherHook("M-5-2", out ResolvedTalentEventHook victim))
                multiplier *= 1 + victim.Value / 100m;
            if (TryGetArcherHook("M-3-2", out ResolvedTalentEventHook deepMark))
                criticalDamageBonus += deepMark.Value;
            if (TryGetArcherHook("M-9-1", out ResolvedTalentEventHook master))
                criticalChanceBonus += master.SecondaryValue;
        }

        ActiveEffect? broken =
            FindArcherEffect(target.Actor, BrokenArmorEffectId, now);
        if (broken is not null)
            armorPenetration += broken.Definition.Magnitude / 100m;

        ActiveEffect? petCrit =
            FindArcherEffect(_player.Actor, PetCritShotEffectId, now);
        if (petCrit is not null)
            multiplier *= 1 + petCrit.Definition.Magnitude / 100m;

        return new(
            multiplier,
            armorPenetration,
            accuracyBonus,
            criticalChanceBonus,
            criticalDamageBonus,
            heavy,
            enchanted);
    }

    private void ApplyArcherAutoAttackResolved(
        CombatParticipantDefinition target,
        AutoAttackProfile profile,
        decimal ordinaryBaseDamage,
        DamageResult damage,
        ArcherAutoAttackModifier modifier,
        DateTimeOffset now)
    {
        if (!IsArcher)
            return;

        ConsumeArcherStackedEffect(_player.Actor, ExposedDefenseEffectId, 1, now);
        RemoveArcherEffect(_player.Actor, PetCritShotEffectId, now);

        if (modifier.Heavy)
            RemoveArcherEffect(_player.Actor, HeavyArrowEffectId, now);

        if (modifier.Enchanted)
        {
            RemoveArcherEffect(_player.Actor, EnchantedShotEffectId, now);
            ResolveSpellPowerProc(target.Actor, 0.70m, "ENCHANTED_SHOT", now);
        }

        if (damage.Avoidance != DamageAvoidance.None || damage.HpDamage <= 0)
        {
            _archerShotSequence = 0;
            _markedShotSequence = 0;
            return;
        }

        _lastOwnerHitTargetId = target.Actor.ActorId;
        _lastOwnerHitAtUtc = now;
        _archerShotSequence++;

        if (TryGetArcherHook("M-1-3", out ResolvedTalentEventHook concentration)
            && TalentCooldownReady(concentration.TalentId, now))
        {
            AddResource(_player.Actor, concentration.Value, now, concentration.TalentId);
            StartTalentCooldown(concentration, now);
        }

        if (TryGetArcherHook("M-5-4", out ResolvedTalentEventHook rhythm)
            && _archerShotSequence % Math.Max(1, rhythm.TriggerCount) == 0)
            AddResource(_player.Actor, rhythm.Value, now, rhythm.TalentId);

        if (TryGetArcherHook("M-4-1", out ResolvedTalentEventHook doubleRelease)
            && _random.NextUnit() < doubleRelease.SecondaryValue / 100m)
            ResolveOwnerExtraArrow(target.Actor, 0.40m, doubleRelease.TalentId, now);

        if (HasArcherEffect(target.Actor, HunterMarkEffectId, now)
            && TryGetArcherHook("M-9-1", out ResolvedTalentEventHook master))
        {
            _markedShotSequence++;
            if (_markedShotSequence % Math.Max(1, master.TriggerCount) == 0)
                ResolveOwnerExtraArrow(target.Actor, 0.60m, master.TalentId, now);
        }
    }

    private decimal ResolveArcherCompanionDamageMultiplier(
        CombatActorState target,
        DateTimeOffset now)
    {
        if (!IsArcher || _companion is null || _companion.Actor.IsDead)
            return 1;

        decimal multiplier = 1;

        if (IsSharedTarget(target.ActorId, now))
        {
            if (IsPhysicalCompanion
                && TryGetArcherHook("B-2-3", out ResolvedTalentEventHook joint))
                multiplier *= 1 + joint.Value / 100m;

            if (IsSpiritCompanion
                && TryGetArcherHook("A-5-2", out ResolvedTalentEventHook unity))
                multiplier *= 1 + unity.Value / 100m;
        }

        if (CompanionArchetype == "PREDATOR"
            && HpPercent(target) < 20
            && TryGetArcherHook("B-6-1", out ResolvedTalentEventHook execute))
            multiplier *= 1 + execute.Value / 100m;

        if (HasArcherEffect(target, BeastUnityTargetEffectId, now))
        {
            ActiveEffect effect =
                FindArcherEffect(target, BeastUnityTargetEffectId, now)!;
            multiplier *= 1 + effect.Definition.Magnitude / 100m;
        }

        return multiplier;
    }

    private void ResolveCompanionExtraAttack(
        CombatActorState target,
        decimal multiplier,
        string definitionId,
        DateTimeOffset now)
    {
        if (_companion is null
            || _companion.Actor.IsDead
            || target.IsDead
            || multiplier <= 0)
            return;

        decimal attackPower = EffectEngine.CalculateStat(
            _companion.Actor,
            EffectStat.AttackPower,
            _companion.Actor.Stats.AttackPower,
            now);
        decimal spellPower = EffectEngine.CalculateStat(
            _companion.Actor,
            EffectStat.SpellPower,
            _companion.Actor.Stats.SpellPower,
            now);
        decimal ordinary =
            ((_companion.AutoAttack.BaseDamageMin ?? 0)
                + (_companion.AutoAttack.BaseDamageMax ?? 0)) / 2m
            + attackPower * _companion.AutoAttack.AttackPowerCoefficient
            + spellPower * _companion.AutoAttack.SpellPowerCoefficient;

        ResolveCompanionFixedDamage(
            target,
            ordinary * multiplier,
            _companion.AutoAttack.DamageType,
            definitionId,
            now);
    }

    private void ResolveCompanionFixedDamage(
        CombatActorState target,
        decimal amount,
        DamageType type,
        string definitionId,
        DateTimeOffset now)
    {
        if (_companion is null || _companion.Actor.IsDead || target.IsDead || amount <= 0)
            return;

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(
                _companion.Actor,
                target,
                amount,
                type,
                CanMiss: false,
                CanDodge: false,
                CanCrit: false,
                MinimumDamage: 0),
            _random,
            now);
        ApplyKernelEvents(
            result.Events,
            _companion.Actor.ActorId,
            target.ActorId,
            definitionId);
    }

    private void ResolveOwnerExtraArrow(
        CombatActorState target,
        decimal multiplier,
        string definitionId,
        DateTimeOffset now)
    {
        if (target.IsDead || multiplier <= 0)
            return;

        decimal attackPower = EffectEngine.CalculateStat(
            _player.Actor,
            EffectStat.AttackPower,
            _player.Actor.Stats.AttackPower,
            now);
        decimal baseDamage = _player.AutoAttack.BaseDamage
            + attackPower * _player.AutoAttack.AttackPowerCoefficient;
        if (_player.AutoAttack.BaseDamageMin is { } min
            && _player.AutoAttack.BaseDamageMax is { } max)
            baseDamage = (min + max) / 2m
                + attackPower * _player.AutoAttack.AttackPowerCoefficient;

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(
                _player.Actor,
                target,
                baseDamage * multiplier,
                DamageType.Physical,
                CanMiss: false,
                CanDodge: false,
                CanCrit: false,
                MinimumDamage: 0),
            _random,
            now);
        ApplyKernelEvents(
            result.Events,
            _player.Actor.ActorId,
            target.ActorId,
            definitionId,
            _player.AutoAttack.WeaponHand,
            _player.AutoAttack.WeaponDefinitionId);
    }

    private void ResolveSpellPowerProc(
        CombatActorState target,
        decimal coefficient,
        string definitionId,
        DateTimeOffset now)
    {
        if (target.IsDead || coefficient <= 0)
            return;

        decimal spellPower = EffectEngine.CalculateStat(
            _player.Actor,
            EffectStat.SpellPower,
            _player.Actor.Stats.SpellPower,
            now);
        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(
                _player.Actor,
                target,
                spellPower * coefficient,
                DamageType.Magical,
                CanMiss: false,
                CanDodge: false,
                CanCrit: false,
                MinimumDamage: 0),
            _random,
            now);
        ApplyKernelEvents(
            result.Events,
            _player.Actor.ActorId,
            target.ActorId,
            definitionId);
    }

    private void ApplySpellPowerDot(
        CombatActorState target,
        string effectId,
        decimal totalPercent,
        TimeSpan duration,
        TimeSpan tickInterval,
        DateTimeOffset now,
        Guid sourceId)
    {
        if (target.IsDead || duration <= TimeSpan.Zero || tickInterval <= TimeSpan.Zero)
            return;

        decimal ticks = Math.Max(1m, (decimal)(duration.TotalSeconds / tickInterval.TotalSeconds));
        decimal spellPower = EffectEngine.CalculateStat(
            _player.Actor,
            EffectStat.SpellPower,
            _player.Actor.Stats.SpellPower,
            now);
        decimal tickDamage = spellPower * totalPercent / 100m / ticks;

        ApplyArcherEffectFrom(
            target,
            sourceId,
            new EffectDefinition(
                effectId,
                EffectKind.DamageOverTime,
                duration,
                1,
                EffectStackPolicy.Refresh,
                tickDamage,
                tickInterval,
                SourceSpecific: true,
                PeriodicDamageType: DamageType.Magical),
            now);
    }

    private void ApplyCompanionAttackPowerDot(
        CombatActorState target,
        string effectId,
        decimal totalPercent,
        TimeSpan duration,
        TimeSpan tickInterval,
        DateTimeOffset now)
    {
        if (_companion is null || target.IsDead)
            return;

        decimal ticks = Math.Max(1m, (decimal)(duration.TotalSeconds / tickInterval.TotalSeconds));
        decimal attackPower = EffectEngine.CalculateStat(
            _companion.Actor,
            EffectStat.AttackPower,
            _companion.Actor.Stats.AttackPower,
            now);
        decimal tickDamage = attackPower * totalPercent / 100m / ticks;

        ApplyArcherEffectFrom(
            target,
            _companion.Actor.ActorId,
            new EffectDefinition(
                effectId,
                EffectKind.DamageOverTime,
                duration,
                1,
                EffectStackPolicy.Refresh,
                tickDamage,
                tickInterval,
                SourceSpecific: true,
                PeriodicDamageType: DamageType.Physical),
            now);
    }

    private void DamageCompanionDirect(
        decimal amount,
        string definitionId,
        DateTimeOffset now)
    {
        if (_companion is null || _companion.Actor.IsDead || amount <= 0)
            return;

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(
                _player.Actor,
                _companion.Actor,
                amount,
                DamageType.True,
                CanMiss: false,
                CanDodge: false,
                CanCrit: false,
                IgnoreShields: true,
                MinimumDamage: 0),
            _random,
            now);
        ApplyKernelEvents(
            result.Events,
            _player.Actor.ActorId,
            _companion.Actor.ActorId,
            definitionId);
        SyncArcherConditionalEffects(now);
    }

    private void RestoreArcherHp(
        CombatActorState actor,
        decimal amount,
        DateTimeOffset now,
        string definitionId)
    {
        decimal before = actor.CurrentHp;
        actor.ApplyHealing(amount);
        decimal actual = actor.CurrentHp - before;
        if (actual <= 0) return;

        Append(new CombatEvent(
            CombatEventType.HealingApplied,
            now,
            actor.ActorId,
            definitionId,
            actual,
            SourceActorId: _companion?.Actor.ActorId ?? _player.Actor.ActorId,
            TargetActorId: actor.ActorId));
    }

    private void ApplyCompanionMultiplier(
        string effectId,
        EffectStat stat,
        decimal multiplier,
        TimeSpan duration,
        DateTimeOffset now)
    {
        if (_companion is null || _companion.Actor.IsDead || duration <= TimeSpan.Zero)
            return;

        ApplyArcherEffectFrom(
            _companion.Actor,
            _player.Actor.ActorId,
            new EffectDefinition(
                effectId,
                EffectKind.StatModifier,
                duration,
                1,
                EffectStackPolicy.Replace,
                multiplier,
                ModifiedStat: stat,
                ModifierMode: EffectModifierMode.Multiplicative,
                SourceSpecific: true),
            now);
    }

    private void EnsureArcherPlayerMultiplier(
        string effectId,
        EffectStat stat,
        decimal multiplier,
        DateTimeOffset now)
    {
        if (HasArcherEffect(_player.Actor, effectId, now))
            return;

        ApplyArcherEffect(
            _player.Actor,
            new EffectDefinition(
                effectId,
                EffectKind.StatModifier,
                TimeSpan.FromHours(12),
                1,
                EffectStackPolicy.Replace,
                multiplier,
                ModifiedStat: stat,
                ModifierMode: EffectModifierMode.Multiplicative),
            now);
    }

    private bool IsSharedTarget(Guid targetId, DateTimeOffset now) =>
        _lastOwnerHitTargetId == targetId
        && _lastCompanionHitTargetId == targetId
        && _lastOwnerHitAtUtc is { } ownerAt
        && _lastCompanionHitAtUtc is { } companionAt
        && Math.Abs((ownerAt - companionAt).TotalSeconds) <= 2
        && Math.Abs((now - ownerAt).TotalSeconds) <= 4;

    private bool HasCompanionBleed(CombatActorState target, DateTimeOffset now) =>
        _companion is not null
        && target.ActiveEffects.Any(effect =>
            string.Equals(effect.Definition.Id, PredatorBleedEffectId, StringComparison.Ordinal)
            && effect.SourceId == _companion.Actor.ActorId
            && effect.ExpiresAtUtc > now);

    private bool SelectedTargetHasHunterMark(DateTimeOffset now) =>
        _enemiesById.TryGetValue(_selectedTargetActorId, out CombatParticipantDefinition? target)
        && HasArcherEffect(target.Actor, HunterMarkEffectId, now);

    private bool IsArcaneFlowActive(DateTimeOffset now) =>
        HasArcherEffect(_player.Actor, ArcaneFlowEffectId, now);

    private decimal ArcherResourcePercent() =>
        _player.Actor.MaxResource <= 0
            ? 0
            : _player.Actor.CurrentResource / _player.Actor.MaxResource * 100m;

    private bool HasArcherTalent(string talentId) =>
        _playerTalents.EventHooks.Any(hook =>
            string.Equals(hook.TalentId, talentId, StringComparison.Ordinal))
        || _playerTalents.UnlockedAbilityIds.Contains(TalentAbilityForArcherNode(talentId));

    private bool TryGetArcherHook(
        string talentId,
        out ResolvedTalentEventHook hook)
    {
        hook = IsArcher
            ? _playerTalents.EventHooks.FirstOrDefault(item =>
                string.Equals(item.TalentId, talentId, StringComparison.Ordinal))!
            : null!;
        return hook is not null;
    }

    private bool TryGetArcherHook(
        string talentId,
        string targetId,
        out ResolvedTalentEventHook hook)
    {
        hook = IsArcher
            ? _playerTalents.EventHooks.FirstOrDefault(item =>
                string.Equals(item.TalentId, talentId, StringComparison.Ordinal)
                && string.Equals(item.TargetId, targetId, StringComparison.Ordinal))!
            : null!;
        return hook is not null;
    }

    private ActiveEffect? FindArcherEffect(
        CombatActorState actor,
        string effectId,
        DateTimeOffset now) =>
        actor.ActiveEffects.FirstOrDefault(effect =>
            string.Equals(effect.Definition.Id, effectId, StringComparison.Ordinal)
            && effect.SourceId == _player.Actor.ActorId
            && effect.ExpiresAtUtc > now);

    private bool HasArcherEffect(
        CombatActorState actor,
        string effectId,
        DateTimeOffset now) =>
        FindArcherEffect(actor, effectId, now) is not null;

    private void ApplyArcherEffect(
        CombatActorState target,
        EffectDefinition effect,
        DateTimeOffset now) =>
        ApplyArcherEffectFrom(target, _player.Actor.ActorId, effect, now);

    private void ApplyArcherEffectFrom(
        CombatActorState target,
        Guid sourceId,
        EffectDefinition effect,
        DateTimeOffset now) =>
        ApplyTalentEffect(target, sourceId, effect, now);

    private void RemoveArcherEffect(
        CombatActorState target,
        string effectId,
        DateTimeOffset now) =>
        RemoveTalentEffects(
            EffectEngine.RemoveOwned(
                target,
                effectId,
                _player.Actor.ActorId,
                now));

    private static TimeSpan ClampArcherCast(TimeSpan castTime) =>
        castTime < TimeSpan.FromMilliseconds(100)
            ? TimeSpan.FromMilliseconds(100)
            : castTime;

    private static string TalentAbilityForArcherNode(string talentId) => talentId switch
    {
        "M-5-1" => "SNIPER_FOCUS",
        "M-8-3" => "SNIPER_FOCUS",
        "B-5-1" => "BEAST_SURGE",
        "B-8-3" => "BEAST_SURGE",
        "A-5-1" => "ARCANE_FLOW",
        "A-8-3" => "ARCANE_FLOW",
        _ => string.Empty
    };

    private static bool IsPhysicalShotDefinition(string? definitionId) =>
        definitionId is "AUTO_ATTACK" or "PIERCING_ARROW" or "AIMED_SHOT";

    private static bool IsPhysicalShotAbility(AbilityDefinition ability) =>
        ability.Actions?.Any(action =>
            action.Type == AbilityActionType.Damage
            && action.DamageType == DamageType.Physical) == true;

    private static bool IsMagicalArrowAbility(AbilityDefinition ability) =>
        string.Equals(ability.School, "ARCANE_ARROW", StringComparison.Ordinal)
        && ability.Actions?.Any(action =>
            action.Type == AbilityActionType.Damage
            && action.DamageType == DamageType.Magical) == true;

    private static bool IsShotAbility(AbilityDefinition ability) =>
        IsPhysicalShotAbility(ability) || IsMagicalArrowAbility(ability);

    private void ConsumeArcherStackedEffect(
        CombatActorState actor,
        string effectId,
        int count,
        DateTimeOffset now)
    {
        ActiveEffect? effect = FindArcherEffect(actor, effectId, now);
        if (effect is null) return;

        effect.Stacks = Math.Max(0, effect.Stacks - count);
        if (effect.Stacks == 0)
            RemoveArcherEffect(actor, effectId, now);
    }

    private sealed record ArcherAutoAttackModifier(
        decimal DamageMultiplier,
        decimal ArmorPenetrationBonus,
        decimal AccuracyBonus,
        decimal CriticalChanceBonus,
        decimal CriticalDamageBonus,
        bool Heavy,
        bool Enchanted);
}
