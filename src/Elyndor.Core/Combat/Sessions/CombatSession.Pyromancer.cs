using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private const string FireballId = "MAGE_FIREBALL";
    private const string ArcaneSparkId = "MAGE_ARCANE_SPARK";
    private const string IceShardId = "MAGE_ICE_SHARD";
    private const string FlameFlashId = "FLAME_FLASH";
    private const string FireWaveId = "FIRE_WAVE";
    private const string CombustionId = "COMBUSTION";
    private const string FireCometId = "FIRE_COMET";

    private const string BurnEffectId = "PYRO_BURN";
    private const string FireballStreakEffectId = "PYRO_FIREBALL_STREAK";
    private const string HeatLimitEffectId = "PYRO_HEAT_LIMIT";
    private const string QuickKindlingEffectId = "PYRO_QUICK_KINDLING";
    private const string HotBloodEffectId = "PYRO_HOT_BLOOD";
    private const string FireRhythmEffectId = "PYRO_FIRE_RHYTHM";
    private const string FlameTrailEffectId = "PYRO_FLAME_TRAIL";
    private const string CombustionEffectId = "PYRO_COMBUSTION";
    private const string InfernoEffectId = "PYRO_INFERNO";
    private const string BlazingResponseEffectId = "PYRO_BLAZING_RESPONSE";
    private const string AshenMarkEffectId = "PYRO_ASHEN_MARK";
    private const string PerfectCombustionFireballEffectId = "PYRO_PERFECT_COMBUSTION_FIREBALL";
    private const string AvatarFireballEffectId = "PYRO_AVATAR_FIREBALL";
    private const string CometAftershockEffectId = "PYRO_COMET_AFTERSHOCK";

    private static readonly HashSet<string> FireDamageDefinitionIds = new(StringComparer.Ordinal)
    {
        FireballId,
        FlameFlashId,
        FireWaveId,
        FireCometId,
        BurnEffectId,
        CometAftershockEffectId
    };

    private int _pyroFireCastSequence;

    private bool IsMage =>
        string.Equals(_player.DefinitionId, "MAGE", StringComparison.Ordinal);

    private bool IsPlayerAbilityKnown(string abilityId, DateTimeOffset now) =>
        _player.KnownAbilityIds.Contains(abilityId)
        || string.Equals(abilityId, FireCometId, StringComparison.Ordinal)
            && IsHeatLimitActive(now)
            && HasPyromancerTalent("F-6-1");

    private IReadOnlySet<string> GetPlayerKnownAbilityIds(DateTimeOffset now)
    {
        HashSet<string> ids = new(_player.KnownAbilityIds, StringComparer.Ordinal);
        if (IsHeatLimitActive(now) && HasPyromancerTalent("F-6-1"))
        {
            ids.Add(FireCometId);
        }

        return ids;
    }

    private AbilityDefinition ResolvePyromancerAbility(
        AbilityDefinition ability,
        DateTimeOffset now)
    {
        if (!IsMage) return ability;

        bool offensiveFire = IsOffensiveFireAbility(ability);
        if (!offensiveFire) return ability;

        decimal damageMultiplier = Math.Max(0, ability.DamageMultiplier);
        decimal accuracyBonus = ability.AccuracyBonus;
        decimal criticalChanceBonus = ability.CriticalChanceBonus;
        decimal criticalDamageBonus = ability.CriticalDamageBonus;
        decimal magicPenetrationBonus = ability.MagicPenetrationBonus;
        decimal resourceCost = ability.ResourceCost;
        TimeSpan castTime = ability.CastTime;
        IReadOnlyList<AbilityActionDefinition>? actions = ability.Actions;

        if (TryGetPyromancerHook("F-1-1", out ResolvedTalentEventHook fireAccuracy))
            accuracyBonus += fireAccuracy.Value;
        if (TryGetPyromancerHook("F-1-2", out ResolvedTalentEventHook fireCrit))
            criticalChanceBonus += fireCrit.Value;
        if (TryGetPyromancerHook("F-1-3", out ResolvedTalentEventHook innerHeat))
            actions = ScaleSpellPower(actions, 1 + innerHeat.Value / 100m);
        if (TryGetPyromancerHook("F-3-4", out ResolvedTalentEventHook penetration))
            magicPenetrationBonus += penetration.Value / 100m;
        if (TryGetPyromancerHook("F-5-4", out ResolvedTalentEventHook destructiveFire))
            criticalDamageBonus += destructiveFire.Value;
        if (TryGetPyromancerHook("F-9-1", out ResolvedTalentEventHook avatar))
            damageMultiplier *= 1 + avatar.Value / 100m;

        if (TryGetPyromancerHook("F-2-3", out ResolvedTalentEventHook firstBurn)
            && HpPercent(_enemy.Actor) > 80m)
            damageMultiplier *= 1 + firstBurn.Value / 100m;
        if (TryGetPyromancerHook("F-4-4", out ResolvedTalentEventHook searingFinale)
            && HpPercent(_enemy.Actor) < 25m)
            damageMultiplier *= 1 + searingFinale.Value / 100m;
        if (TryGetPyromancerHook("F-3-2", out ResolvedTalentEventHook devouringFlame)
            && HasOwnBurn(now))
            damageMultiplier *= 1 + devouringFlame.Value / 100m;
        if (TryGetPyromancerHook("F-8-2", out ResolvedTalentEventHook heartOfFire)
            && HasOwnBurn(now))
            criticalDamageBonus += heartOfFire.Value;
        if (HasOwnEffect(_enemy.Actor, AshenMarkEffectId, now)
            && TryGetPyromancerHook("F-7-2", out ResolvedTalentEventHook ashenMark))
            magicPenetrationBonus += ashenMark.Value / 100m;

        ActiveEffect? inferno = FindOwnEffect(_player.Actor, InfernoEffectId, now);
        if (inferno is not null
            && TryGetPyromancerHook("F-7-3", out ResolvedTalentEventHook infernoHook))
            damageMultiplier *= 1 + inferno.Stacks * infernoHook.Value / 100m;

        ActiveEffect? response = FindOwnEffect(_player.Actor, BlazingResponseEffectId, now);
        if (response is not null)
            damageMultiplier *= 1 + response.Definition.Magnitude / 100m;

        ActiveEffect? rhythm = FindOwnEffect(_player.Actor, FireRhythmEffectId, now);
        if (rhythm is not null)
            criticalChanceBonus += rhythm.Definition.Magnitude;

        ActiveEffect? hotBlood = FindOwnEffect(_player.Actor, HotBloodEffectId, now);
        if (hotBlood is not null)
            resourceCost *= Math.Max(0, 1 - hotBlood.Definition.Magnitude / 100m);

        if (string.Equals(ability.Id, FireballId, StringComparison.Ordinal))
        {
            if (TryGetPyromancerHook("F-2-1", out ResolvedTalentEventHook heatedFireball))
                damageMultiplier *= 1 + heatedFireball.Value / 100m;

            ActiveEffect? quickKindling = FindOwnEffect(_player.Actor, QuickKindlingEffectId, now);
            if (quickKindling is not null)
                castTime = ClampCastTime(castTime - TimeSpan.FromSeconds((double)quickKindling.Definition.Magnitude));

            ActiveEffect? flameTrail = FindOwnEffect(_player.Actor, FlameTrailEffectId, now);
            if (flameTrail is not null)
                damageMultiplier *= 1 + flameTrail.Definition.Magnitude / 100m;

            if (IsCombustionActive(now)
                && TryGetPyromancerHook("F-6-3", out ResolvedTalentEventHook induction))
                castTime = ClampCastTime(castTime - TimeSpan.FromSeconds((double)induction.Value));

            if (HasOwnEffect(_player.Actor, PerfectCombustionFireballEffectId, now))
                criticalChanceBonus += 15;

            if (HasOwnEffect(_player.Actor, AvatarFireballEffectId, now))
            {
                castTime = TimeSpan.FromSeconds(1);
                resourceCost *= 0.5m;
            }
        }
        else if (string.Equals(ability.Id, FireCometId, StringComparison.Ordinal))
        {
            if (TryGetPyromancerHook("F-6-2", out ResolvedTalentEventHook overheat))
                criticalChanceBonus += overheat.Value;
            if (TryGetPyromancerHook("F-8-3", out ResolvedTalentEventHook ashStar)
                && HpPercent(_enemy.Actor) < 30m)
                damageMultiplier *= 1 + ashStar.Value / 100m;
        }

        if (IsCombustionActive(now))
        {
            damageMultiplier *= 1.15m;
            criticalChanceBonus += 8;
        }

        return ability with
        {
            ResourceCost = Math.Max(0, resourceCost),
            CastTime = castTime,
            Actions = actions,
            DamageMultiplier = damageMultiplier,
            AccuracyBonus = accuracyBonus,
            CriticalChanceBonus = criticalChanceBonus,
            CriticalDamageBonus = criticalDamageBonus,
            MagicPenetrationBonus = magicPenetrationBonus
        };
    }

    private void OnPyromancerAbilityStarted(AbilityDefinition ability, DateTimeOffset now)
    {
        if (!IsMage || !IsOffensiveFireAbility(ability)) return;

        RemovePyroEffect(_player.Actor, FireRhythmEffectId, now);
        RemovePyroEffect(_player.Actor, HotBloodEffectId, now);
        RemovePyroEffect(_player.Actor, BlazingResponseEffectId, now);

        if (string.Equals(ability.Id, FireballId, StringComparison.Ordinal))
        {
            RemovePyroEffect(_player.Actor, QuickKindlingEffectId, now);
            RemovePyroEffect(_player.Actor, FlameTrailEffectId, now);
            RemovePyroEffect(_player.Actor, PerfectCombustionFireballEffectId, now);
            RemovePyroEffect(_player.Actor, AvatarFireballEffectId, now);
        }

        if (string.Equals(ability.Id, FireCometId, StringComparison.Ordinal))
        {
            RemovePyroEffect(_player.Actor, HeatLimitEffectId, now);
            if (HasPyromancerTalent("F-9-1"))
            {
                ApplyPyroEffect(
                    _player.Actor,
                    new EffectDefinition(
                        AvatarFireballEffectId,
                        EffectKind.Buff,
                        TimeSpan.FromSeconds(5),
                        1,
                        EffectStackPolicy.Replace,
                        0),
                    now);
            }
        }
    }

    private void OnPyromancerAbilityResolved(
        AbilityDefinition ability,
        AbilityExecutionResult execution,
        DateTimeOffset now)
    {
        if (!IsMage || Status != CombatSessionStatus.Active) return;

        if (string.Equals(ability.Id, CombustionId, StringComparison.Ordinal)
            && HasPyromancerTalent("F-5-1"))
        {
            ActivateCombustion(now);
            return;
        }

        if (string.Equals(ability.Id, ArcaneSparkId, StringComparison.Ordinal)
            || string.Equals(ability.Id, IceShardId, StringComparison.Ordinal))
        {
            if (DidHit(execution)
                && TryGetPyromancerHook("F-2-4", out ResolvedTalentEventHook kindling))
            {
                ApplyPyroEffect(
                    _player.Actor,
                    new EffectDefinition(
                        QuickKindlingEffectId,
                        EffectKind.Buff,
                        TimeSpan.FromSeconds(5),
                        1,
                        EffectStackPolicy.Replace,
                        kindling.Value),
                    now);
            }
            return;
        }

        if (!IsOffensiveFireAbility(ability)) return;

        bool hit = DidHit(execution);
        bool critical = DidCrit(execution);

        if (string.Equals(ability.Id, FireballId, StringComparison.Ordinal))
        {
            UpdateHeatLimitStreak(hit, critical, now);
            if (hit) RefreshBurnFromFireball(now);
        }

        if (string.Equals(ability.Id, FlameFlashId, StringComparison.Ordinal)
            && hit
            && TryGetPyromancerHook("F-5-2", out ResolvedTalentEventHook flameTrail))
        {
            ApplyPyroEffect(
                _player.Actor,
                new EffectDefinition(
                    FlameTrailEffectId,
                    EffectKind.Buff,
                    TimeSpan.FromSeconds(5),
                    1,
                    EffectStackPolicy.Replace,
                    flameTrail.Value),
                now);
        }

        if (string.Equals(ability.Id, FireCometId, StringComparison.Ordinal)
            && hit
            && TryGetPyromancerHook("F-6-2", out ResolvedTalentEventHook overheat)
            && PyromancerTalentRuntimeCatalog.TryGetRule(
                overheat.TalentId,
                out PyromancerTalentRuntimeRule overheatRule))
        {
            ApplyBurn(
                overheatRule.SecondaryValueForRank(overheat.Rank),
                now);
        }

        if (TryGetPyromancerHook("F-4-3", out ResolvedTalentEventHook rhythmHook))
        {
            if (hit)
            {
                _pyroFireCastSequence++;
                if (_pyroFireCastSequence >= 2)
                {
                    _pyroFireCastSequence = 0;
                    ApplyPyroEffect(
                        _player.Actor,
                        new EffectDefinition(
                            FireRhythmEffectId,
                            EffectKind.Buff,
                            TimeSpan.FromHours(12),
                            1,
                            EffectStackPolicy.Replace,
                            rhythmHook.Value),
                        now);
                }
            }
            else
            {
                _pyroFireCastSequence = 0;
                RemovePyroEffect(_player.Actor, FireRhythmEffectId, now);
            }
        }

        if (hit && IsCombustionActive(now)
            && TryGetPyromancerHook("F-7-3", out ResolvedTalentEventHook infernoHook))
        {
            ActiveEffect? combustion = FindOwnEffect(_player.Actor, CombustionEffectId, now);
            TimeSpan remaining = combustion is null
                ? TimeSpan.FromMilliseconds(1)
                : combustion.ExpiresAtUtc - now;
            if (remaining > TimeSpan.Zero)
            {
                ApplyPyroEffect(
                    _player.Actor,
                    new EffectDefinition(
                        InfernoEffectId,
                        EffectKind.Buff,
                        remaining,
                        3,
                        EffectStackPolicy.Stack,
                        infernoHook.Value),
                    now);
            }
        }
    }

    private void ApplyPyromancerCriticalHooks(CombatEvent combatEvent)
    {
        if (!IsMage
            || combatEvent.SourceActorId != _player.Actor.ActorId
            || !IsFireDamageDefinition(combatEvent.DefinitionId))
            return;

        DateTimeOffset now = combatEvent.OccurredAtUtc;

        if (TryGetPyromancerHook("F-1-4", out ResolvedTalentEventHook economicalBurn)
            && TalentCooldownReady(economicalBurn.TalentId, now))
        {
            AddResource(_player.Actor, economicalBurn.Value, now, economicalBurn.TalentId);
            StartTalentCooldown(economicalBurn, now);
        }

        if (string.Equals(combatEvent.DefinitionId, FireballId, StringComparison.Ordinal))
        {
            if (TryGetPyromancerHook("F-2-2", out ResolvedTalentEventHook ignitionSpark))
                ApplyBurn(ignitionSpark.Value, now);

            if (TryGetPyromancerHook("F-3-3", out ResolvedTalentEventHook hotBlood))
            {
                ApplyPyroEffect(
                    _player.Actor,
                    new EffectDefinition(
                        HotBloodEffectId,
                        EffectKind.Buff,
                        TimeSpan.FromSeconds(5),
                        1,
                        EffectStackPolicy.Replace,
                        hotBlood.Value),
                    now);
            }

            if (TryGetPyromancerHook("F-7-2", out ResolvedTalentEventHook ashenMark))
            {
                ApplyPyroEffect(
                    _enemy.Actor,
                    new EffectDefinition(
                        AshenMarkEffectId,
                        EffectKind.Debuff,
                        TimeSpan.FromSeconds(6),
                        1,
                        EffectStackPolicy.Refresh,
                        ashenMark.Value,
                        SourceSpecific: true),
                    now);
            }
        }

        if (TryGetPyromancerHook("F-5-3", out ResolvedTalentEventHook heatThirst)
            && TalentCooldownReady(heatThirst.TalentId, now)
            && ReduceCooldown(
                _playerRuntime,
                FlameFlashId,
                TimeSpan.FromSeconds((double)heatThirst.Value),
                now))
        {
            StartTalentCooldown(heatThirst, now);
        }

        if (string.Equals(combatEvent.DefinitionId, FireCometId, StringComparison.Ordinal))
        {
            if (TryGetPyromancerHook("F-7-1", out ResolvedTalentEventHook cometStrike))
            {
                decimal avatarMultiplier = HasPyromancerTalent("F-9-1") ? 1.08m : 1m;
                decimal magnitude = EffectiveFireSpellPower(now)
                    * cometStrike.Value / 100m
                    * avatarMultiplier;
                ApplyPyroEffect(
                    _enemy.Actor,
                    new EffectDefinition(
                        CometAftershockEffectId,
                        EffectKind.DamageOverTime,
                        TimeSpan.FromSeconds(1),
                        1,
                        EffectStackPolicy.Replace,
                        magnitude,
                        TimeSpan.FromSeconds(1),
                        SourceSpecific: true,
                        PeriodicDamageType: DamageType.Magical),
                    now);
            }

            if (TryGetPyromancerHook("F-9-1", out ResolvedTalentEventHook avatar)
                && TalentCooldownReady(avatar.TalentId, now))
            {
                if (ReduceCooldown(
                    _playerRuntime,
                    CombustionId,
                    TimeSpan.FromSeconds(3),
                    now))
                {
                    StartTalentCooldown(avatar, now);
                }
            }
        }
    }

    private void ApplyPyromancerIncomingCriticalHooks(CombatEvent combatEvent)
    {
        if (!IsMage
            || _player.Actor.IsDead
            || combatEvent.TargetActorId != _player.Actor.ActorId
            || combatEvent.DamageType != DamageType.Magical
            || !TryGetPyromancerHook("F-6-4", out ResolvedTalentEventHook blazingResponse)
            || !TalentCooldownReady(blazingResponse.TalentId, combatEvent.OccurredAtUtc))
        {
            return;
        }

        ApplyPyroEffect(
            _player.Actor,
            new EffectDefinition(
                BlazingResponseEffectId,
                EffectKind.Buff,
                TimeSpan.FromSeconds(6),
                1,
                EffectStackPolicy.Replace,
                blazingResponse.Value),
            combatEvent.OccurredAtUtc);
        StartTalentCooldown(blazingResponse, combatEvent.OccurredAtUtc);
    }

    private void ApplyPyromancerEnemyKilledHooks(CombatEvent death)
    {
        if (!IsMage
            || !IsFireDamageDefinition(death.DefinitionId)
            || !TryGetPyromancerHook("F-7-4", out ResolvedTalentEventHook wildfire)
            || !TalentCooldownReady(wildfire.TalentId, death.OccurredAtUtc))
            return;

        AddResource(
            _player.Actor,
            _player.Actor.MaxResource * wildfire.Value / 100m,
            death.OccurredAtUtc,
            wildfire.TalentId);
        _playerRuntime.Cooldowns.Remove(FlameFlashId);
        StartTalentCooldown(wildfire, death.OccurredAtUtc);
    }

    private void OnPyromancerAbilityInterrupted(CombatEvent combatEvent)
    {
        if (!IsMage || combatEvent.ActorId != _player.Actor.ActorId) return;
        if (string.Equals(combatEvent.DefinitionId, FireballId, StringComparison.Ordinal))
            ResetFireballStreak(combatEvent.OccurredAtUtc);
        if (combatEvent.DefinitionId is not null
            && _abilities.TryGetValue(combatEvent.DefinitionId, out AbilityDefinition? ability)
            && IsOffensiveFireAbility(ability))
        {
            _pyroFireCastSequence = 0;
            RemovePyroEffect(_player.Actor, FireRhythmEffectId, combatEvent.OccurredAtUtc);
        }
    }

    private void ActivateCombustion(DateTimeOffset now)
    {
        TimeSpan duration = TimeSpan.FromSeconds(10);
        if (TryGetPyromancerHook("F-9-1", out ResolvedTalentEventHook avatar)
            && PyromancerTalentRuntimeCatalog.TryGetRule(
                avatar.TalentId,
                out PyromancerTalentRuntimeRule avatarRule))
        {
            duration += TimeSpan.FromSeconds((double)avatarRule.SecondaryValueForRank(avatar.Rank));
        }

        RemovePyroEffect(_player.Actor, InfernoEffectId, now);
        ApplyPyroEffect(
            _player.Actor,
            new EffectDefinition(
                CombustionEffectId,
                EffectKind.Buff,
                duration,
                1,
                EffectStackPolicy.Replace,
                0),
            now);

        if (HasPyromancerTalent("F-8-1"))
        {
            _playerRuntime.Cooldowns.Remove(FlameFlashId);
            _playerRuntime.Cooldowns.Remove(FireWaveId);
            ApplyPyroEffect(
                _player.Actor,
                new EffectDefinition(
                    PerfectCombustionFireballEffectId,
                    EffectKind.Buff,
                    duration,
                    1,
                    EffectStackPolicy.Replace,
                    15),
                now);
        }
    }

    private void UpdateHeatLimitStreak(bool hit, bool critical, DateTimeOffset now)
    {
        if (!HasPyromancerTalent("F-6-1")) return;
        if (!hit || !critical)
        {
            ResetFireballStreak(now);
            return;
        }

        ApplyPyroEffect(
            _player.Actor,
            new EffectDefinition(
                FireballStreakEffectId,
                EffectKind.Buff,
                TimeSpan.FromHours(12),
                3,
                EffectStackPolicy.Stack,
                0),
            now);
        ActiveEffect? streak = FindOwnEffect(_player.Actor, FireballStreakEffectId, now);
        if (streak?.Stacks < 3) return;

        RemovePyroEffect(_player.Actor, FireballStreakEffectId, now);
        ApplyPyroEffect(
            _player.Actor,
            new EffectDefinition(
                HeatLimitEffectId,
                EffectKind.Buff,
                TimeSpan.FromSeconds(8),
                1,
                EffectStackPolicy.Replace,
                0),
            now);
    }

    private void ResetFireballStreak(DateTimeOffset now) =>
        RemovePyroEffect(_player.Actor, FireballStreakEffectId, now);

    private void ApplyBurn(decimal spellPowerPercentPerSecond, DateTimeOffset now)
    {
        if (_enemy.Actor.IsDead || Status != CombatSessionStatus.Active) return;

        decimal burnMultiplier = 1;
        if (TryGetPyromancerHook("F-4-2", out ResolvedTalentEventHook fanning))
            burnMultiplier *= 1 + fanning.Value / 100m;
        if (TryGetPyromancerHook("F-9-1", out ResolvedTalentEventHook avatar))
            burnMultiplier *= 1 + avatar.Value / 100m;

        decimal magnitude = EffectiveFireSpellPower(now)
            * spellPowerPercentPerSecond / 100m
            * burnMultiplier;
        ApplyPyroEffect(
            _enemy.Actor,
            new EffectDefinition(
                BurnEffectId,
                EffectKind.DamageOverTime,
                TimeSpan.FromSeconds(4),
                1,
                EffectStackPolicy.Refresh,
                magnitude,
                TimeSpan.FromSeconds(1),
                SourceSpecific: true,
                PeriodicDamageType: DamageType.Magical),
            now);
    }

    private void RefreshBurnFromFireball(DateTimeOffset now)
    {
        if (!HasPyromancerTalent("F-8-2")) return;
        ActiveEffect? burn = FindOwnEffect(_enemy.Actor, BurnEffectId, now);
        if (burn is null) return;
        ApplyKernelEvents(
            EffectEngine.Apply(_enemy.Actor, _player.Actor.ActorId, burn.Definition, now),
            _player.Actor.ActorId,
            _enemy.Actor.ActorId,
            BurnEffectId);
    }

    private decimal EffectiveFireSpellPower(DateTimeOffset now)
    {
        decimal value = _player.Actor.Stats.SpellPower;
        if (TryGetPyromancerHook("F-1-3", out ResolvedTalentEventHook innerHeat))
            value *= 1 + innerHeat.Value / 100m;
        if (IsCombustionActive(now))
            value *= 1.15m;
        return value;
    }

    private bool IsCombustionActive(DateTimeOffset now) =>
        HasOwnEffect(_player.Actor, CombustionEffectId, now);

    private bool IsHeatLimitActive(DateTimeOffset now) =>
        HasOwnEffect(_player.Actor, HeatLimitEffectId, now);

    private bool HasOwnBurn(DateTimeOffset now) =>
        HasOwnEffect(_enemy.Actor, BurnEffectId, now);

    private bool HasPyromancerTalent(string talentId) =>
        _playerTalents.EventHooks.Any(hook =>
            string.Equals(hook.TalentId, talentId, StringComparison.Ordinal));

    private bool TryGetPyromancerHook(
        string talentId,
        out ResolvedTalentEventHook hook)
    {
        hook = _playerTalents.EventHooks.FirstOrDefault(item =>
            string.Equals(item.TalentId, talentId, StringComparison.Ordinal))!;
        return hook is not null;
    }

    private ActiveEffect? FindOwnEffect(
        CombatActorState actor,
        string effectId,
        DateTimeOffset now) =>
        actor.ActiveEffects.FirstOrDefault(effect =>
            string.Equals(effect.Definition.Id, effectId, StringComparison.Ordinal)
            && effect.SourceId == _player.Actor.ActorId
            && effect.ExpiresAtUtc > now);

    private bool HasOwnEffect(
        CombatActorState actor,
        string effectId,
        DateTimeOffset now) =>
        FindOwnEffect(actor, effectId, now) is not null;

    private void ApplyPyroEffect(
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

    private void RemovePyroEffect(
        CombatActorState target,
        string effectId,
        DateTimeOffset now)
    {
        ApplyKernelEvents(
            EffectEngine.Remove(target, effectId, now),
            _player.Actor.ActorId,
            target.ActorId,
            effectId);
    }

    private static IReadOnlyList<AbilityActionDefinition>? ScaleSpellPower(
        IReadOnlyList<AbilityActionDefinition>? actions,
        decimal multiplier) =>
        actions?.Select(action => action.Type == AbilityActionType.Damage
            ? action with { SpellPowerCoefficient = action.SpellPowerCoefficient * multiplier }
            : action).ToArray();

    private static TimeSpan ClampCastTime(TimeSpan castTime) =>
        castTime < TimeSpan.FromMilliseconds(100)
            ? TimeSpan.FromMilliseconds(100)
            : castTime;

    private static bool DidHit(AbilityExecutionResult execution) =>
        execution.Events.Any(item => item.Type == CombatEventType.DamageDealt);

    private static bool DidCrit(AbilityExecutionResult execution) =>
        execution.Events.Any(item => item.Type == CombatEventType.CriticalHit);

    private static bool IsOffensiveFireAbility(AbilityDefinition ability) =>
        string.Equals(ability.School, "FIRE", StringComparison.Ordinal)
        && ability.Actions?.Any(action => action.Type == AbilityActionType.Damage) == true;

    private static bool IsFireDamageDefinition(string? definitionId) =>
        definitionId is not null && FireDamageDefinitionIds.Contains(definitionId);
}
