using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Paladin;
using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private const string PaladinSealRighteousnessEffectId = "PALADIN_SEAL_RIGHTEOUSNESS";
    private const string PaladinSealCommandEffectId = "PALADIN_SEAL_COMMAND";
    private const string PaladinDevotionAuraEffectId = "PALADIN_DEVOTION_AURA";
    private const string PaladinConcentrationAuraEffectId = "PALADIN_CONCENTRATION_AURA";
    private const string PaladinSanctityAuraEffectId = "PALADIN_SANCTITY_AURA";
    private const string PaladinBeaconEffectId = "PALADIN_BEACON_OF_LIGHT";
    private const string PaladinHolyShieldEffectId = "PALADIN_HOLY_SHIELD_BLOCK";
    private const string PaladinConsecrationActiveEffectId = "PALADIN_CONSECRATION_ACTIVE";
    private const string PaladinHeraldFreeShockEffectId = "PALADIN_HERALD_HOLY_SHOCK_FREE";
    private const string PaladinReckoningReadyEffectId = "PALADIN_RECKONING_READY";
    private const string PaladinVengeanceEffectId = "PALADIN_VENGEANCE";
    private const string PaladinArtOfWarEffectId = "PALADIN_ART_OF_WAR";

    private sealed class PaladinSessionRuntimeState
    {
        public PaladinCombatState Combat { get; } = new();
        public PaladinHealingRuntime Healing { get; } = new();
        public PaladinProtectionRuntime Protection { get; } = new();
        public PaladinRetributionRuntime Retribution { get; } = new();
        public Dictionary<string, decimal> LastResourceSpendByAbility { get; } =
            new(StringComparer.Ordinal);
        public int BastionBlockCounter { get; set; }
    }

    private readonly Dictionary<Guid, PaladinSessionRuntimeState> _paladinSessionStates = [];

    private bool IsActivePaladin =>
        string.Equals(_player.DefinitionId, "PALADIN", StringComparison.Ordinal);

    private PaladinSessionRuntimeState ActivePaladinState()
    {
        Guid actorId = _player.Actor.ActorId;
        if (_paladinSessionStates.TryGetValue(actorId, out PaladinSessionRuntimeState? state))
            return state;

        state = new PaladinSessionRuntimeState();
        _paladinSessionStates[actorId] = state;
        return state;
    }

    private void ProcessPaladinKernelEvent(CombatEvent combatEvent)
    {
        ApplySharedPaladinIntercession(combatEvent);
        if (!IsActivePaladin)
            return;

        PaladinSessionRuntimeState state = ActivePaladinState();
        DateTimeOffset now = combatEvent.OccurredAtUtc;

        if (combatEvent.Type == CombatEventType.ResourceChanged
            && combatEvent.ActorId == _player.Actor.ActorId
            && combatEvent.Amount < 0
            && !string.IsNullOrWhiteSpace(combatEvent.DefinitionId))
        {
            state.LastResourceSpendByAbility[combatEvent.DefinitionId] = -combatEvent.Amount;
            return;
        }

        switch (combatEvent.Type)
        {
            case CombatEventType.AbilityCompleted:
                ApplyPaladinAbilityCompleted(state, combatEvent, now);
                break;
            case CombatEventType.HealingApplied:
                ApplyPaladinHealingEvent(state, combatEvent, now);
                break;
            case CombatEventType.CriticalHit:
                ApplyPaladinCriticalEvent(state, combatEvent, now);
                break;
            case CombatEventType.DamageBlocked:
                ApplyPaladinBlockEvent(state, combatEvent, now);
                break;
            case CombatEventType.DamageDealt:
                ApplyPaladinDamageEvent(state, combatEvent, now);
                break;
        }
    }

    private void ApplyPaladinAbilityCompleted(
        PaladinSessionRuntimeState state,
        CombatEvent combatEvent,
        DateTimeOffset now)
    {
        if (combatEvent.ActorId != _player.Actor.ActorId
            || string.IsNullOrWhiteSpace(combatEvent.DefinitionId))
        {
            return;
        }

        string abilityId = combatEvent.DefinitionId;
        switch (abilityId)
        {
            case "SEAL_OF_RIGHTEOUSNESS":
                RemoveOwnedEffectFromActor(_player.Actor, PaladinSealCommandEffectId, now);
                state.Combat.ActivateSeal(PaladinSealRighteousnessEffectId);
                break;
            case "SEAL_OF_COMMAND":
                RemoveOwnedEffectFromActor(_player.Actor, PaladinSealRighteousnessEffectId, now);
                state.Combat.ActivateSeal(PaladinSealCommandEffectId);
                break;
            case "DEVOTION_AURA":
                ReplacePaladinAura(state, PaladinDevotionAuraEffectId, now);
                break;
            case "CONCENTRATION_AURA":
                ReplacePaladinAura(state, PaladinConcentrationAuraEffectId, now);
                break;
            case "SANCTITY_AURA":
                ReplacePaladinAura(state, PaladinSanctityAuraEffectId, now);
                break;
            case "BEACON_OF_LIGHT":
                if (combatEvent.TargetActorId is { } beaconTargetId)
                    state.Combat.SetBeacon(beaconTargetId);
                break;
            case "DIVINE_FAVOR":
                state.Healing.ArmDivineFavor();
                break;
            case "CONSECRATION":
                state.Protection.ActivateConsecration(now, TimeSpan.FromSeconds(6));
                ApplyPaladinEffect(
                    _player.Actor,
                    new EffectDefinition(
                        PaladinConsecrationActiveEffectId,
                        EffectKind.Buff,
                        TimeSpan.FromSeconds(6),
                        1,
                        EffectStackPolicy.Refresh,
                        1),
                    now);
                break;
            case "HOLY_SHIELD":
                state.Protection.ActivateHolyShield(
                    now,
                    TimeSpan.FromSeconds((double)ResolveHolyShieldDurationSeconds()));
                break;
            case "JUDGEMENT":
                ApplyPaladinJudgement(state, combatEvent, now);
                break;
            case "AVENGING_WRATH":
                state.Retribution.ActivateAvengingWrath(
                    now,
                    TimeSpan.FromSeconds(HasPaladinTalent("R-9-1") ? 16 : 12),
                    HasPaladinTalent("R-9-1"));
                break;
            case "CLEANSE":
                if (combatEvent.TargetActorId is { } cleanseTargetId
                    && ResolvePaladinActor(cleanseTargetId) is { } cleanseTarget)
                {
                    ApplyPaladinCleanse(cleanseTarget, now);
                }
                break;
            case "DIVINE_PROTECTION":
                ApplyDivineBastionPartyShields(now);
                break;
        }

        state.LastResourceSpendByAbility.Remove(abilityId);
    }

    private void ApplyPaladinJudgement(
        PaladinSessionRuntimeState state,
        CombatEvent combatEvent,
        DateTimeOffset now)
    {
        bool hasDivinePurpose = HasPaladinTalent("R-8-3");
        bool divinePurposeArmed = state.Retribution.RecordSuccessfulJudgement(hasDivinePurpose);
        if (divinePurposeArmed)
        {
            ApplyPaladinEffect(
                _player.Actor,
                new EffectDefinition(
                    "PALADIN_DIVINE_PURPOSE_READY",
                    EffectKind.Buff,
                    TimeSpan.FromSeconds(20),
                    1,
                    EffectStackPolicy.Replace,
                    1),
                now);
        }

        if (TryGetPaladinHook("R-6-4", out ResolvedTalentEventHook sanctified))
        {
            AddResource(
                _player.Actor,
                4m * sanctified.Rank,
                now,
                sanctified.TalentId);
        }

        if (state.Retribution.ConsumeIncarnationJudgementCrusaderReset(now))
            _playerRuntime.Cooldowns.Remove("CRUSADER_STRIKE");

        if (combatEvent.TargetActorId is { } targetId
            && ResolvePaladinActor(targetId) is { } target)
        {
            if (HasPaladinTalent("R-3-1"))
            {
                ApplyPaladinEffect(
                    target,
                    new EffectDefinition(
                        "PALADIN_CRUSADERS_JUDGEMENT",
                        EffectKind.StatModifier,
                        TimeSpan.FromSeconds(12),
                        1,
                        EffectStackPolicy.Refresh,
                        1.10m,
                        ModifiedStat: EffectStat.IncomingMagicalDamageMultiplier,
                        ModifierMode: EffectModifierMode.Multiplicative,
                        SourceSpecific: true),
                    now);
            }

            if (HasPaladinTalent("P-4-3"))
            {
                decimal magnitude = IsBossActor(targetId) ? 0.92m : 0.82m;
                ApplyPaladinEffect(
                    target,
                    new EffectDefinition(
                        "PALADIN_JUDGEMENT_OF_JUSTICE",
                        EffectKind.StatModifier,
                        TimeSpan.FromSeconds(8),
                        1,
                        EffectStackPolicy.Refresh,
                        magnitude,
                        ModifiedStat: EffectStat.AttackSpeed,
                        ModifierMode: EffectModifierMode.Multiplicative,
                        SourceSpecific: true),
                    now);
            }
        }
    }

    private void ApplyPaladinHealingEvent(
        PaladinSessionRuntimeState state,
        CombatEvent combatEvent,
        DateTimeOffset now)
    {
        if (combatEvent.SourceActorId != _player.Actor.ActorId
            || combatEvent.TargetActorId is not { } targetId
            || combatEvent.Amount <= 0)
        {
            return;
        }

        HealingOrigin origin = combatEvent.HealingOrigin
            ?? (combatEvent.IsPeriodic ? HealingOrigin.Periodic : HealingOrigin.Direct);
        HealingResult healing = new(
            combatEvent.Amount,
            combatEvent.Amount,
            combatEvent.Amount,
            0,
            ResolvePaladinActor(targetId)?.CurrentHp ?? 0,
            [],
            combatEvent.IsCritical,
            combatEvent.Amount,
            _player.Actor.ActorId,
            origin);

        if (origin == HealingOrigin.Direct)
        {
            decimal bonusPercent = 0;
            if (TryGetPaladinHook("H-1-2", out ResolvedTalentEventHook healingLight)
                && combatEvent.DefinitionId is "HOLY_LIGHT" or "FLASH_OF_LIGHT")
            {
                bonusPercent += 3m * healingLight.Rank;
            }

            if (ResolvePaladinActor(targetId) is { } target)
            {
                decimal hpBefore = Math.Max(0, target.CurrentHp - combatEvent.Amount);
                decimal hpBeforePercent = target.MaxHp <= 0 ? 100 : hpBefore / target.MaxHp * 100m;
                if (combatEvent.DefinitionId == "FLASH_OF_LIGHT"
                    && hpBeforePercent < 50
                    && TryGetPaladinHook("H-2-3", out ResolvedTalentEventHook merciful))
                {
                    bonusPercent += 6m * merciful.Rank;
                }

                if (hpBeforePercent < 35
                    && TryGetPaladinHook("H-4-3", out ResolvedTalentEventHook lastHope))
                {
                    bonusPercent += 8m * lastHope.Rank;
                }
            }

            if (HasPaladinTalent("H-9-1"))
                bonusPercent += 5m;

            if (bonusPercent > 0)
                ApplySecondaryPaladinHealing(targetId, combatEvent.Amount * bonusPercent / 100m, now, "PALADIN_HEALING_BONUS");
        }

        if (combatEvent.IsCritical && origin == HealingOrigin.Direct)
        {
            if (TryGetPaladinHook("H-2-1", out ResolvedTalentEventHook illumination))
            {
                decimal refundPercent = 15m * illumination.Rank;
                if (illumination.Rank >= 5 && HasPaladinTalent("H-8-1"))
                    refundPercent = 90m;
                decimal spent = state.LastResourceSpendByAbility.GetValueOrDefault(
                    combatEvent.DefinitionId ?? string.Empty);
                decimal refund = PaladinHealingRuntime.CalculateIlluminationRefund(
                    healing,
                    spent,
                    refundPercent);
                if (refund > 0)
                    AddResource(_player.Actor, refund, now, illumination.TalentId);
            }

            if (TryGetPaladinHook("H-6-2", out ResolvedTalentEventHook afterglow)
                && ResolvePaladinActor(targetId) is { } afterglowTarget)
            {
                decimal total = PaladinHealingRuntime.CalculateAfterglowTotalHealing(
                    healing,
                    4m * afterglow.Rank);
                if (total > 0)
                {
                    ApplyPaladinEffect(
                        afterglowTarget,
                        new EffectDefinition(
                            "PALADIN_AFTERGLOW",
                            EffectKind.HealingOverTime,
                            TimeSpan.FromSeconds(4),
                            1,
                            EffectStackPolicy.Refresh,
                            Math.Max(1, decimal.Round(total / 4m, 0, MidpointRounding.AwayFromZero)),
                            TimeSpan.FromSeconds(1),
                            SourceSpecific: true),
                        now);
                }
            }

            if (combatEvent.DefinitionId == "HOLY_SHOCK"
                && TryGetPaladinHook("H-6-1", out ResolvedTalentEventHook surge))
            {
                ApplyPaladinEffect(
                    _player.Actor,
                    new EffectDefinition(
                        "PALADIN_SURGE_OF_LIGHT",
                        EffectKind.Buff,
                        TimeSpan.FromSeconds(12),
                        1,
                        EffectStackPolicy.Replace,
                        surge.Rank),
                    now);
            }
        }

        if (state.Combat.BeaconTargetId is { } beaconTargetId
            && beaconTargetId != targetId
            && origin == HealingOrigin.Direct
            && ResolvePaladinActor(beaconTargetId) is { } beaconTarget
            && !beaconTarget.IsDead)
        {
            decimal copyPercent = 35m;
            if (TryGetPaladinHook("H-5-3", out ResolvedTalentEventHook empoweredBeacon))
                copyPercent += 10m * empoweredBeacon.Rank;
            if (HasPaladinTalent("H-8-2"))
                copyPercent += 10m;
            if (combatEvent.DefinitionId == "HOLY_SHOCK" && HasPaladinTalent("H-8-2"))
                copyPercent += 10m;

            HealingResult copied = HealingPipeline.Resolve(
                new HealingRequest(
                    beaconTarget,
                    combatEvent.Amount * copyPercent / 100m,
                    OccurredAtUtc: now,
                    Source: _player.Actor,
                    CanCrit: false,
                    Origin: HealingOrigin.Copied,
                    DefinitionId: "PALADIN_BEACON_COPY"));
            ApplyKernelEvents(
                copied.Events,
                _player.Actor.ActorId,
                beaconTarget.ActorId,
                "PALADIN_BEACON_COPY");
        }

        if (HasPaladinTalent("H-9-1")
            && state.Healing.RecordHeraldEligibleDirectHeal(healing))
        {
            _playerRuntime.Cooldowns.Remove("HOLY_SHOCK");
            _playerRuntime.Cooldowns.Remove("HOLY_SHOCK_OFFENSIVE");
            ApplyPaladinEffect(
                _player.Actor,
                new EffectDefinition(
                    PaladinHeraldFreeShockEffectId,
                    EffectKind.Buff,
                    TimeSpan.FromSeconds(20),
                    1,
                    EffectStackPolicy.Replace,
                    1),
                now);
        }

        if (origin == HealingOrigin.Direct
            && targetId != _player.Actor.ActorId
            && ResolvePaladinActor(targetId) is { } protectedTarget)
        {
            decimal hpBefore = Math.Max(0, protectedTarget.CurrentHp - combatEvent.Amount);
            if (protectedTarget.MaxHp > 0
                && hpBefore / protectedTarget.MaxHp < 0.30m
                && TryGetPaladinHook("H-7-3", out ResolvedTalentEventHook protector))
            {
                ApplySecondaryPaladinHealing(
                    _player.Actor.ActorId,
                    combatEvent.Amount * (5m * protector.Rank) / 100m,
                    now,
                    protector.TalentId);
            }
        }
    }

    private void ApplyPaladinCriticalEvent(
        PaladinSessionRuntimeState state,
        CombatEvent combatEvent,
        DateTimeOffset now)
    {
        if (combatEvent.SourceActorId != _player.Actor.ActorId)
            return;

        if (TryGetPaladinHook("R-3-2", out ResolvedTalentEventHook vengeance))
        {
            TimeSpan duration = TimeSpan.FromSeconds(HasPaladinTalent("R-8-1") ? 18 : 12);
            state.Retribution.RecordPhysicalOrHolyCritical(now, duration);
            decimal perStackPercent = 2m * vengeance.Rank;
            ApplyPaladinEffect(
                _player.Actor,
                new EffectDefinition(
                    PaladinVengeanceEffectId,
                    EffectKind.StatModifier,
                    duration,
                    1,
                    EffectStackPolicy.Replace,
                    1 + state.Retribution.VengeanceStacks * perStackPercent / 100m,
                    ModifiedStat: EffectStat.OutgoingDamageMultiplier,
                    ModifierMode: EffectModifierMode.Multiplicative),
                now);
        }

        if (combatEvent.DamageType == DamageType.Physical
            && TryGetPaladinHook("R-6-1", out ResolvedTalentEventHook artOfWar))
        {
            ApplyPaladinEffect(
                _player.Actor,
                new EffectDefinition(
                    PaladinArtOfWarEffectId,
                    EffectKind.Buff,
                    TimeSpan.FromSeconds(12),
                    1,
                    EffectStackPolicy.Replace,
                    artOfWar.Rank),
                now);
        }

        if (combatEvent.DefinitionId == "CRUSADER_STRIKE"
            && HasPaladinTalent("R-8-2"))
        {
            ReduceCooldown(_playerRuntime, "JUDGEMENT", TimeSpan.FromSeconds(2), now);
        }
    }

    private void ApplyPaladinBlockEvent(
        PaladinSessionRuntimeState state,
        CombatEvent combatEvent,
        DateTimeOffset now)
    {
        if (combatEvent.TargetActorId != _player.Actor.ActorId)
            return;

        if (state.Protection.CanTriggerHolyShieldBlockEffect(now)
            && combatEvent.SourceActorId is { } sourceId
            && _enemiesById.TryGetValue(sourceId, out CombatParticipantDefinition? sourceEnemy)
            && !sourceEnemy.Actor.IsDead)
        {
            decimal attackPower = EffectEngine.CalculateStat(
                _player.Actor,
                EffectStat.AttackPower,
                _player.Actor.Stats.AttackPower,
                now);
            decimal retaliationMultiplier = 1m;
            if (TryGetPaladinHook("P-3-2", out ResolvedTalentEventHook improvedShield))
                retaliationMultiplier += 0.15m * improvedShield.Rank;
            DamageResult retaliation = DamagePipeline.Resolve(
                new DamageRequest(
                    _player.Actor,
                    sourceEnemy.Actor,
                    (18m + attackPower * 0.25m) * retaliationMultiplier,
                    DamageType.Magical,
                    CanMiss: false,
                    CanDodge: false,
                    CanCrit: false,
                    MinimumDamage: 0),
                _random,
                now);
            ApplyKernelEvents(
                retaliation.Events,
                _player.Actor.ActorId,
                sourceEnemy.Actor.ActorId,
                "PALADIN_HOLY_SHIELD_RETALIATION");
        }

        if (TryGetPaladinHook("P-4-1", out ResolvedTalentEventHook reckoning)
            && _random.NextUnit() < 0.10m * reckoning.Rank)
        {
            ApplyPaladinEffect(
                _player.Actor,
                new EffectDefinition(
                    PaladinReckoningReadyEffectId,
                    EffectKind.Buff,
                    TimeSpan.FromSeconds(10),
                    1,
                    EffectStackPolicy.Replace,
                    0.50m + 0.15m * reckoning.Rank),
                now);
        }

        if (state.Protection.IsHolyShieldActive(now)
            && TryGetPaladinHook("P-6-3", out ResolvedTalentEventHook lightsRetribution)
            && TalentCooldownReady(lightsRetribution.TalentId, now))
        {
            ApplySecondaryPaladinHealing(
                _player.Actor.ActorId,
                _player.Actor.MaxHp * (2m * lightsRetribution.Rank) / 100m,
                now,
                lightsRetribution.TalentId);
            StartTalentCooldown(
                lightsRetribution with { InternalCooldown = TimeSpan.FromSeconds(2) },
                now);
        }

        if (state.Protection.IsHolyShieldActive(now)
            && TryGetPaladinHook("P-7-1", out ResolvedTalentEventHook perfectShield))
        {
            ReduceCooldown(
                _playerRuntime,
                "HOLY_SHIELD",
                TimeSpan.FromSeconds(0.5 * perfectShield.Rank),
                now);
        }

        if (state.Protection.IsHolyShieldActive(now) && HasPaladinTalent("P-9-1"))
        {
            state.BastionBlockCounter++;
            if (state.BastionBlockCounter >= 3)
            {
                state.BastionBlockCounter = 0;
                ApplyBastionOfDawnShield(now);
            }
        }
    }

    private void ApplyPaladinDamageEvent(
        PaladinSessionRuntimeState state,
        CombatEvent combatEvent,
        DateTimeOffset now)
    {
        if (combatEvent.SourceActorId != _player.Actor.ActorId
            || combatEvent.TargetActorId is not { } targetId
            || combatEvent.Amount <= 0)
        {
            return;
        }

        if (combatEvent.DefinitionId == "AUTO_ATTACK"
            && _enemiesById.TryGetValue(targetId, out CombatParticipantDefinition? targetEnemy)
            && !targetEnemy.Actor.IsDead)
        {
            decimal extraMultiplier = 0;
            string? definitionId = null;
            if (state.Combat.ActiveSealId == PaladinSealRighteousnessEffectId)
            {
                extraMultiplier += 0.20m;
                definitionId = "PALADIN_SEAL_RIGHTEOUSNESS_PROC";
            }
            if (state.Combat.ActiveSealId == PaladinSealCommandEffectId
                && TryGetPaladinHook("R-1-4", out _)
                && _random.NextUnit() < 0.25m)
            {
                decimal commandMultiplier = 0.45m;
                if (TryGetPaladinHook("R-2-4", out ResolvedTalentEventHook improvedCommand))
                    commandMultiplier += 0.10m * improvedCommand.Rank;
                extraMultiplier += commandMultiplier;
                definitionId = "PALADIN_SEAL_COMMAND_PROC";
            }
            if (state.Retribution.ConsumeZealForNextAutoAttack()
                && TryGetPaladinHook("R-4-3", out ResolvedTalentEventHook zeal))
            {
                extraMultiplier += 0.15m * zeal.Rank;
                definitionId = "PALADIN_ZEAL_PROC";
            }

            ActiveEffect? reckoningReady = _player.Actor.ActiveEffects.FirstOrDefault(effect =>
                effect.ExpiresAtUtc > now
                && effect.Definition.Id == PaladinReckoningReadyEffectId);
            if (reckoningReady is not null)
            {
                extraMultiplier += Math.Max(0, reckoningReady.Definition.Magnitude);
                definitionId = "PALADIN_RECKONING_PROC";
                RemoveOwnedEffectFromActor(_player.Actor, PaladinReckoningReadyEffectId, now);
            }

            if (extraMultiplier > 0)
            {
                DamageResult extra = DamagePipeline.Resolve(
                    new DamageRequest(
                        _player.Actor,
                        targetEnemy.Actor,
                        combatEvent.Amount * extraMultiplier,
                        DamageType.Magical,
                        CanMiss: false,
                        CanDodge: false,
                        CanCrit: false,
                        MinimumDamage: 0),
                    _random,
                    now);
                ApplyKernelEvents(
                    extra.Events,
                    _player.Actor.ActorId,
                    targetEnemy.Actor.ActorId,
                    definitionId ?? "PALADIN_AUTO_PROC");
            }
        }

        if (combatEvent.DefinitionId == "DIVINE_STORM")
            ApplyDivineStormHealing(combatEvent.Amount, now);

        if (combatEvent.DefinitionId == "TEMPLARS_VERDICT")
        {
            bool divinePurpose = state.Retribution.ConsumeDivinePurposeForTemplarsVerdict();
            bool incarnationCrit = state.Retribution.ConsumeIncarnationTemplarCritical(now);
            decimal extraPercent = divinePurpose ? 25m : 0m;
            if (_enemiesById.TryGetValue(targetId, out CombatParticipantDefinition? verdictTarget)
                && verdictTarget.Actor.MaxHp > 0
                && verdictTarget.Actor.CurrentHp / verdictTarget.Actor.MaxHp < 0.30m
                && HasPaladinTalent("R-7-4"))
            {
                extraPercent += 30m;
            }
            if (incarnationCrit)
                extraPercent += Math.Max(0, _player.Actor.Stats.CriticalDamage) * 100m;

            if (divinePurpose)
                AddResource(_player.Actor, 12m, now, "R-8-3");

            if (extraPercent > 0
                && verdictTarget is not null
                && !verdictTarget.Actor.IsDead)
            {
                DamageResult extra = DamagePipeline.Resolve(
                    new DamageRequest(
                        _player.Actor,
                        verdictTarget.Actor,
                        combatEvent.Amount * extraPercent / 100m,
                        DamageType.Magical,
                        CanMiss: false,
                        CanDodge: false,
                        CanCrit: false,
                        MinimumDamage: 0),
                    _random,
                    now);
                ApplyKernelEvents(
                    extra.Events,
                    _player.Actor.ActorId,
                    verdictTarget.Actor.ActorId,
                    "PALADIN_VERDICT_BONUS");
            }
        }
    }

    private void ApplySharedPaladinIntercession(CombatEvent combatEvent)
    {
        if (combatEvent.Type != CombatEventType.DamageDealt
            || combatEvent.TargetActorId is not { } targetId
            || combatEvent.Amount <= 0
            || !_playerStatesByActorId.TryGetValue(targetId, out CombatPlayerRuntimeState? targetState))
        {
            return;
        }

        ActiveEffect? intercession = targetState.Definition.Actor.ActiveEffects
            .Where(effect =>
                effect.ExpiresAtUtc > combatEvent.OccurredAtUtc
                && effect.Definition.Id == "PALADIN_INTERCESSION")
            .OrderByDescending(effect => effect.AppliedAtUtc)
            .FirstOrDefault();
        if (intercession is null
            || !_playerStatesByActorId.TryGetValue(intercession.SourceId, out CombatPlayerRuntimeState? paladinState)
            || paladinState.Definition.Actor.IsDead)
        {
            return;
        }

        decimal redirectPercent = Math.Clamp(intercession.Definition.Magnitude * 100m, 0, 100);
        decimal redirect = PaladinProtectionRuntime.ResolveIntercessionRedirectDamage(
            combatEvent.Amount,
            redirectPercent);
        if (redirect <= 0)
            return;

        // Redirect is applied after mitigation. We restore the redirected slice on the ally
        // and apply it as true damage to the protecting Paladin. If the original hit was
        // already lethal, the stale ActorDied event remains authoritative for this v1 path.
        if (!targetState.Definition.Actor.IsDead)
            targetState.Definition.Actor.ApplyHealing(redirect);

        DamageResult redirected = DamagePipeline.Resolve(
            new DamageRequest(
                paladinState.Definition.Actor,
                paladinState.Definition.Actor,
                redirect,
                DamageType.True,
                CanMiss: false,
                CanDodge: false,
                CanCrit: false,
                IgnoreShields: true,
                SkipDefenseMitigation: true,
                MinimumDamage: 0,
                CanBlock: false,
                IsUnblockable: true),
            _random,
            combatEvent.OccurredAtUtc);
        ApplyKernelEvents(
            redirected.Events,
            intercession.SourceId,
            intercession.SourceId,
            "PALADIN_INTERCESSION_REDIRECT");
    }

    private void ApplyDivineStormHealing(decimal dealtDamage, DateTimeOffset now)
    {
        if (!HasPaladinTalent("R-6-2") || dealtDamage <= 0)
            return;

        decimal conversionPercent = 20m;
        if (TryGetPaladinHook("R-6-3", out ResolvedTalentEventHook improved))
            conversionPercent += 5m * improved.Rank;
        CombatActorState[] recipients = _playerStatesByActorId.Values
            .Select(state => state.Definition.Actor)
            .Where(actor => !actor.IsDead)
            .ToArray();
        if (recipients.Length == 0)
            return;

        decimal each = dealtDamage * conversionPercent / 100m / recipients.Length;
        foreach (CombatActorState recipient in recipients)
            ApplySecondaryPaladinHealing(recipient.ActorId, each, now, "PALADIN_DIVINE_STORM_HEAL");
    }

    private void ApplyDivineBastionPartyShields(DateTimeOffset now)
    {
        if (!HasPaladinTalent("P-8-2"))
            return;

        decimal shield = Math.Max(1, decimal.Round(_player.Actor.MaxHp * 0.08m, 0, MidpointRounding.AwayFromZero));
        foreach (CombatPlayerRuntimeState playerState in _playerStatesByActorId.Values.Where(state => !state.Definition.Actor.IsDead))
        {
            ApplyPaladinEffect(
                playerState.Definition.Actor,
                new EffectDefinition(
                    "PALADIN_DIVINE_BASTION_SHIELD",
                    EffectKind.Shield,
                    TimeSpan.FromSeconds(8),
                    1,
                    EffectStackPolicy.StrongestWins,
                    shield,
                    SourceSpecific: true),
                now);
        }
    }

    private void ApplyBastionOfDawnShield(DateTimeOffset now)
    {
        CombatActorState? target = _playerStatesByActorId.Values
            .Select(state => state.Definition.Actor)
            .Where(actor => !actor.IsDead)
            .OrderBy(actor => actor.MaxHp <= 0 ? 1m : actor.CurrentHp / actor.MaxHp)
            .FirstOrDefault();
        if (target is null)
            return;

        decimal shield = Math.Max(1, decimal.Round(_player.Actor.MaxHp * 0.05m, 0, MidpointRounding.AwayFromZero));
        ApplyPaladinEffect(
            target,
            new EffectDefinition(
                "PALADIN_BASTION_OF_DAWN_SHIELD",
                EffectKind.Shield,
                TimeSpan.FromSeconds(8),
                1,
                EffectStackPolicy.StrongestWins,
                shield,
                SourceSpecific: true),
            now);
    }

    private void ApplyPaladinCleanse(CombatActorState target, DateTimeOffset now)
    {
        ActiveEffect? negative = target.ActiveEffects
            .Where(effect =>
                effect.Definition.Kind is EffectKind.Debuff or EffectKind.Stun or EffectKind.Silence
                && !string.IsNullOrWhiteSpace(effect.Definition.DispelCategory))
            .OrderBy(effect => effect.Sequence)
            .FirstOrDefault();
        if (negative is not null && negative.Definition.DispelCategory is { } category)
        {
            ApplyKernelEvents(
                EffectEngine.Dispel(target, category, now),
                _player.Actor.ActorId,
                target.ActorId,
                "CLEANSE");
        }

        if (TryGetPaladinHook("H-7-2", out ResolvedTalentEventHook sacredCleansing))
        {
            ApplySecondaryPaladinHealing(
                target.ActorId,
                30m * sacredCleansing.Rank,
                now,
                sacredCleansing.TalentId);
        }
    }

    private void ReplacePaladinAura(
        PaladinSessionRuntimeState state,
        string auraId,
        DateTimeOffset now)
    {
        string? previousAura = state.Combat.ActiveAuraId;
        if (!string.IsNullOrWhiteSpace(previousAura)
            && !string.Equals(previousAura, auraId, StringComparison.Ordinal))
        {
            foreach (CombatPlayerRuntimeState playerState in _playerStatesByActorId.Values)
            {
                ApplyKernelEvents(
                    EffectEngine.RemoveOwned(
                        playerState.Definition.Actor,
                        previousAura,
                        _player.Actor.ActorId,
                        now),
                    _player.Actor.ActorId,
                    playerState.Definition.Actor.ActorId,
                    previousAura);
            }
        }

        state.Combat.ActivateAura(auraId);
    }

    private void ApplySecondaryPaladinHealing(
        Guid targetId,
        decimal amount,
        DateTimeOffset now,
        string definitionId)
    {
        if (amount <= 0 || ResolvePaladinActor(targetId) is not { } target || target.IsDead)
            return;

        HealingResult healing = HealingPipeline.Resolve(
            new HealingRequest(
                target,
                amount,
                OccurredAtUtc: now,
                Source: _player.Actor,
                CanCrit: false,
                Origin: HealingOrigin.Secondary,
                DefinitionId: definitionId));
        ApplyKernelEvents(
            healing.Events,
            _player.Actor.ActorId,
            target.ActorId,
            definitionId);
    }

    private void ApplyPaladinEffect(
        CombatActorState target,
        EffectDefinition effect,
        DateTimeOffset now)
    {
        ApplyKernelEvents(
            EffectEngine.Apply(target, _player.Actor.ActorId, effect, now),
            _player.Actor.ActorId,
            target.ActorId,
            effect.Id);
    }

    private void RemoveOwnedEffectFromActor(
        CombatActorState target,
        string effectId,
        DateTimeOffset now)
    {
        ApplyKernelEvents(
            EffectEngine.RemoveOwned(target, effectId, _player.Actor.ActorId, now),
            _player.Actor.ActorId,
            target.ActorId,
            effectId);
    }

    private CombatActorState? ResolvePaladinActor(Guid actorId)
    {
        if (_playerStatesByActorId.TryGetValue(actorId, out CombatPlayerRuntimeState? playerState))
            return playerState.Definition.Actor;
        if (_companion?.Actor.ActorId == actorId)
            return _companion.Actor;
        return _enemiesById.TryGetValue(actorId, out CombatParticipantDefinition? enemy)
            ? enemy.Actor
            : null;
    }

    private bool TryGetPaladinHook(string talentId, out ResolvedTalentEventHook hook)
    {
        hook = IsActivePaladin
            ? _playerTalents.EventHooks.FirstOrDefault(item =>
                string.Equals(item.TalentId, talentId, StringComparison.Ordinal))!
            : null!;
        return hook is not null;
    }

    private bool HasPaladinTalent(string talentId) =>
        IsActivePaladin
        && (_playerTalents.EventHooks.Any(item => string.Equals(item.TalentId, talentId, StringComparison.Ordinal))
            || talentId is "H-8-1" or "H-8-2" or "H-9-1" or "P-8-2" or "P-9-1" or "R-7-4" or "R-8-2" or "R-8-3" or "R-9-1"
                && _playerTalents.EventHooks.Any(item => string.Equals(item.TalentId, talentId, StringComparison.Ordinal)));

    private decimal ResolveHolyShieldDurationSeconds()
    {
        decimal duration = 8;
        if (TryGetPaladinHook("P-3-2", out ResolvedTalentEventHook improved))
            duration += improved.Rank;
        if (HasPaladinTalent("P-9-1"))
            duration += 2;
        return duration;
    }

    private bool IsBossActor(Guid actorId) =>
        _enemiesById.TryGetValue(actorId, out CombatParticipantDefinition? enemy)
        && enemy.MonsterRank == Monsters.MonsterRank.Boss;
}
