namespace Elyndor.Core.Talents;

public static class TalentModifierResolver
{
    public static ResolvedTalentModifiers Resolve(
        TalentTreeDefinition tree,
        IReadOnlyDictionary<string, int> selectedRanks)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(selectedRanks);

        TalentStatModifiers stats = new();
        TalentCombatModifiers combat = new();
        HashSet<string> abilities = new(StringComparer.Ordinal);
        Dictionary<string, TalentAbilityModifiers> abilityModifiers = new(StringComparer.Ordinal);
        List<ResolvedTalentEventHook> eventHooks = [];
        List<TalentModifierDefinition> deferredHooks = [];

        foreach (TalentDefinition node in tree.Nodes)
        {
            int rank = selectedRanks.GetValueOrDefault(node.Id);
            if (rank <= 0) continue;

            foreach (TalentModifierDefinition modifier in node.Modifiers ?? [])
            {
                if (modifier.RuntimeStatus == TalentModifierRuntimeStatus.Deferred)
                {
                    bool runtimeOwned =
                        BerserkerTalentRuntimeCatalog.SupportsLegacyDeferred(node, modifier)
                        || PyromancerTalentRuntimeCatalog.SupportsLegacyDeferred(node, modifier);
                    if (runtimeOwned && modifier.Values.Count >= rank)
                        eventHooks.Add(CreateEventHook(node, modifier, rank));
                    else
                        deferredHooks.Add(modifier);

                    continue;
                }

                if (modifier.Values.Count < rank)
                {
                    continue;
                }

                decimal value = modifier.Values[rank - 1];
                if (modifier.Type == TalentModifierType.EventTriggered)
                {
                    eventHooks.Add(CreateEventHook(node, modifier, rank));
                    continue;
                }

                if (modifier.Type == TalentModifierType.StatModifier
                    || modifier.Type == TalentModifierType.ResourceModifier)
                {
                    stats = ApplyStat(stats, modifier.Key, value);
                    combat = ApplyCombat(combat, modifier.Key, value);
                    continue;
                }

                if (modifier.Type != TalentModifierType.AbilityModifier
                    || string.IsNullOrWhiteSpace(modifier.TargetId))
                {
                    continue;
                }

                if (modifier.Key == TalentModifierKeys.UnlockAbility)
                {
                    abilities.Add(modifier.TargetId);
                    continue;
                }

                TalentAbilityModifiers current =
                    abilityModifiers.GetValueOrDefault(modifier.TargetId)
                    ?? new TalentAbilityModifiers();
                abilityModifiers[modifier.TargetId] =
                    ApplyAbility(current, modifier.Key, value);
            }
        }

        return new(
            stats,
            combat,
            abilities,
            abilityModifiers,
            eventHooks,
            deferredHooks);
    }

    private static ResolvedTalentEventHook CreateEventHook(
        TalentDefinition node,
        TalentModifierDefinition modifier,
        int rank)
    {
        decimal secondaryValue = modifier.SecondaryValues is { Count: > 0 } secondary
            && secondary.Count >= rank
                ? secondary[rank - 1]
                : 0;

        return new ResolvedTalentEventHook(
            node.Id,
            modifier.Key,
            rank,
            modifier.Values[rank - 1],
            modifier.TargetId,
            TimeSpan.FromSeconds((double)modifier.InternalCooldownSeconds),
            modifier.CanTriggerFromProc,
            secondaryValue,
            modifier.Threshold,
            modifier.ChancePercent,
            TimeSpan.FromSeconds((double)modifier.DurationSeconds),
            TimeSpan.FromSeconds((double)modifier.TickIntervalSeconds),
            modifier.TriggerCount,
            modifier.CastTimeSeconds,
            modifier.ResourceCostReductionPercent);
    }

    private static TalentStatModifiers ApplyStat(
        TalentStatModifiers stats,
        string key,
        decimal value) => key switch
    {
        TalentModifierKeys.StrengthPercent =>
            stats with { StrengthPercent = stats.StrengthPercent + value },
        TalentModifierKeys.StaminaPercent =>
            stats with { StaminaPercent = stats.StaminaPercent + value },
        TalentModifierKeys.AttackPowerPercent =>
            stats with { AttackPowerPercent = stats.AttackPowerPercent + value },
        TalentModifierKeys.ArmorPercent =>
            stats with { ArmorPercent = stats.ArmorPercent + value },
        TalentModifierKeys.MagicResistancePercent =>
            stats with { MagicResistancePercent = stats.MagicResistancePercent + value },
        TalentModifierKeys.AccuracyPercent =>
            stats with { AccuracyPercent = stats.AccuracyPercent + value },
        TalentModifierKeys.DodgePercent =>
            stats with { DodgePercent = stats.DodgePercent + value },
        TalentModifierKeys.CriticalChancePercent =>
            stats with { CriticalChancePercent = stats.CriticalChancePercent + value },
        TalentModifierKeys.CriticalDamagePercent =>
            stats with { CriticalDamagePercent = stats.CriticalDamagePercent + value },
        TalentModifierKeys.ArmorPenetrationPercent =>
            stats with { ArmorPenetrationPercent = stats.ArmorPenetrationPercent + value },
        TalentModifierKeys.AttackSpeedPercent =>
            stats with { AttackSpeedPercent = stats.AttackSpeedPercent + value },
        TalentModifierKeys.MaxHpPercent =>
            stats with { MaxHpPercent = stats.MaxHpPercent + value },
        TalentModifierKeys.MaxResourceFlat =>
            stats with { MaxResourceFlat = stats.MaxResourceFlat + value },
        _ => stats
    };

    private static TalentAbilityModifiers ApplyAbility(
        TalentAbilityModifiers ability,
        string key,
        decimal value) => key switch
    {
        TalentModifierKeys.AbilityResourceCostFlat => ability with
        {
            ResourceCostFlatReduction = ability.ResourceCostFlatReduction + value
        },
        TalentModifierKeys.AbilityResourceCostPercent => ability with
        {
            ResourceCostPercentReduction = ability.ResourceCostPercentReduction + value
        },
        TalentModifierKeys.AbilityCooldownSeconds => ability with
        {
            CooldownSecondsReduction = ability.CooldownSecondsReduction + value
        },
        TalentModifierKeys.AbilityDamagePercent => ability with
        {
            DamagePercentBonus = ability.DamagePercentBonus + value
        },
        TalentModifierKeys.AbilityArmorPenetrationPercent => ability with
        {
            ArmorPenetrationPercent = ability.ArmorPenetrationPercent + value
        },
        TalentModifierKeys.EffectDurationSeconds => ability with
        {
            EffectDurationSecondsBonus = ability.EffectDurationSecondsBonus + value
        },
        TalentModifierKeys.EffectMagnitudePercent => ability with
        {
            EffectMagnitudePercentBonus = ability.EffectMagnitudePercentBonus + value
        },
        _ => ability
    };

    private static TalentCombatModifiers ApplyCombat(
        TalentCombatModifiers combat,
        string key,
        decimal value) => key switch
    {
        TalentModifierKeys.IncomingPhysicalDamageReductionPercent => combat with
        {
            IncomingPhysicalDamageReductionPercent =
                combat.IncomingPhysicalDamageReductionPercent + value
        },
        TalentModifierKeys.IncomingMagicalDamageReductionPercent => combat with
        {
            IncomingMagicalDamageReductionPercent =
                combat.IncomingMagicalDamageReductionPercent + value
        },
        TalentModifierKeys.DamageDealtPercent => combat with
        {
            DamageDealtPercent = combat.DamageDealtPercent + value
        },
        TalentModifierKeys.HealingReceivedPercent => combat with
        {
            HealingReceivedPercent = combat.HealingReceivedPercent + value
        },
        TalentModifierKeys.VampirismPercent => combat with
        {
            VampirismPercent = combat.VampirismPercent + value
        },
        _ => combat
    };
}
