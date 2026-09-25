using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Targeting;

namespace Elyndor.Core.Monsters;

public sealed record MonsterAiDecision(
    MonsterAbilityRule Rule,
    AbilityDefinition Ability,
    IReadOnlyList<Guid> TargetActorIds)
{
    public Guid PrimaryTargetActorId => TargetActorIds[0];
}

public static class MonsterAiDecisionEngine
{
    public static MonsterAiDecision? Select(
        MonsterAiProfile profile,
        CombatActorState monster,
        IReadOnlySet<string> knownAbilityIds,
        IReadOnlyDictionary<string, AbilityDefinition> abilities,
        IReadOnlyList<AbilityTargetCandidate> candidates,
        DateTimeOffset combatStartedAtUtc,
        DateTimeOffset now,
        MonsterAbilitySchedulerState schedulerState,
        IGameRandom random,
        Guid? currentThreatTargetId = null,
        Guid? ownerLinkedTargetId = null,
        IReadOnlySet<string>? excludedAbilityIds = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(monster);
        ArgumentNullException.ThrowIfNull(knownAbilityIds);
        ArgumentNullException.ThrowIfNull(abilities);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(schedulerState);
        ArgumentNullException.ThrowIfNull(random);

        AbilityTargetCandidate[] targetCandidates = candidates
            .Where(candidate => candidate.ActorId != Guid.Empty)
            .ToArray();

        foreach (MonsterAbilityRule rule in MonsterAbilityScheduler.SelectEligibleRules(
                     profile,
                     monster,
                     combatStartedAtUtc,
                     now,
                     schedulerState))
        {
            if (excludedAbilityIds?.Contains(rule.AbilityId) == true
                || !knownAbilityIds.Contains(rule.AbilityId)
                || !abilities.TryGetValue(rule.AbilityId, out AbilityDefinition? ability))
            {
                continue;
            }

            AbilityTargetCandidate[] ruleTargetCandidates = targetCandidates
                .Where(candidate => IsWithinTargetHpRange(rule, candidate))
                .ToArray();
            Guid[] targetActorIds = ResolveTargets(
                rule,
                ability,
                monster,
                ruleTargetCandidates,
                random,
                currentThreatTargetId,
                ownerLinkedTargetId);
            if (targetActorIds.Length == 0)
                continue;

            return new MonsterAiDecision(rule, ability, targetActorIds);
        }

        return null;
    }

    private static bool IsWithinTargetHpRange(
        MonsterAbilityRule rule,
        AbilityTargetCandidate candidate)
    {
        if (rule.TargetMinHpPercent is null && rule.TargetMaxHpPercent is null)
            return true;
        if (candidate.CurrentHp <= 0 || candidate.MaxHp <= 0)
            return false;

        decimal hpPercent = candidate.CurrentHp / candidate.MaxHp * 100m;
        return (rule.TargetMinHpPercent is not { } minHp || hpPercent >= minHp)
            && (rule.TargetMaxHpPercent is not { } maxHp || hpPercent <= maxHp);
    }

    private static Guid[] ResolveTargets(
        MonsterAbilityRule rule,
        AbilityDefinition ability,
        CombatActorState monster,
        AbilityTargetCandidate[] candidates,
        IGameRandom random,
        Guid? currentThreatTargetId,
        Guid? ownerLinkedTargetId)
    {
        AbilityTargetSelectorProfile selector = rule.TargetSelector != AbilityTargetSelectorProfile.EncounterOrder
            ? rule.TargetSelector
            : ability.TargetSelectorProfile;

        return ability.TargetType switch
        {
            AbilityTargetType.Self => [monster.ActorId],
            AbilityTargetType.SingleEnemy => SelectEnemyTargets(
                selector,
                AbilityTargetSelectorProfile.CurrentThreatTarget,
                candidates,
                1,
                random,
                currentThreatTargetId,
                ownerLinkedTargetId),
            AbilityTargetType.AllEnemiesInCombat => SelectAllEnemies(
                ability,
                candidates),
            AbilityTargetType.NEnemiesInCombat when ability.TargetCount > 0 => SelectEnemyTargets(
                selector,
                AbilityTargetSelectorProfile.EncounterOrder,
                candidates,
                ability.TargetCount,
                random,
                currentThreatTargetId,
                ownerLinkedTargetId),
            AbilityTargetType.SingleAlly => SelectSingleAlly(
                selector,
                ability.AllowSelfTarget,
                monster,
                candidates,
                random,
                ownerLinkedTargetId),
            AbilityTargetType.SelfAndPartyMembersInCombat => SelectPartyTargets(
                ability,
                monster,
                candidates),
            AbilityTargetType.Owner => AbilityTargetSelector.SelectMany(
                    AbilityTargetSelectorProfile.OwnerLinkedTarget,
                    candidates,
                    1,
                    random,
                    ownerLinkedTargetId: ownerLinkedTargetId)
                .ToArray(),
            _ => []
        };
    }

    private static Guid[] SelectEnemyTargets(
        AbilityTargetSelectorProfile selector,
        AbilityTargetSelectorProfile fallbackSelector,
        AbilityTargetCandidate[] candidates,
        int count,
        IGameRandom random,
        Guid? currentThreatTargetId,
        Guid? ownerLinkedTargetId)
    {
        AbilityTargetSelectorProfile effectiveSelector = selector == AbilityTargetSelectorProfile.EncounterOrder
            ? fallbackSelector
            : selector;
        return AbilityTargetSelector.SelectMany(
                effectiveSelector,
                candidates,
                count,
                random,
                currentThreatTargetId,
                ownerLinkedTargetId)
            .ToArray();
    }

    private static Guid[] SelectAllEnemies(
        AbilityDefinition ability,
        AbilityTargetCandidate[] candidates)
    {
        int count = ability.TargetCount > 0 ? ability.TargetCount : int.MaxValue;
        return candidates
            .Where(candidate => candidate.IsEnemy && candidate.CurrentHp > 0)
            .Take(count)
            .Select(candidate => candidate.ActorId)
            .ToArray();
    }

    private static Guid[] SelectSingleAlly(
        AbilityTargetSelectorProfile selector,
        bool allowSelfTarget,
        CombatActorState monster,
        AbilityTargetCandidate[] candidates,
        IGameRandom random,
        Guid? ownerLinkedTargetId)
    {
        List<AbilityTargetCandidate> allies = candidates
            .Where(candidate => !candidate.IsEnemy && candidate.CurrentHp > 0)
            .ToList();
        if (allowSelfTarget && allies.All(candidate => candidate.ActorId != monster.ActorId))
        {
            allies.Insert(0, new AbilityTargetCandidate(
                monster.ActorId,
                IsEnemy: false,
                IsTank: false,
                UsesMana: monster.MaxResource > 0,
                CurrentHp: monster.CurrentHp,
                MaxHp: monster.MaxHp));
        }
        if (allies.Count == 0)
            return [];

        if (selector == AbilityTargetSelectorProfile.EncounterOrder)
            return [allies[0].ActorId];

        return AbilityTargetSelector.SelectMany(
                selector,
                allies,
                1,
                random,
                ownerLinkedTargetId: ownerLinkedTargetId)
            .ToArray();
    }

    private static Guid[] SelectPartyTargets(
        AbilityDefinition ability,
        CombatActorState monster,
        AbilityTargetCandidate[] candidates)
    {
        IEnumerable<Guid> actorIds = new[] { monster.ActorId }
            .Concat(candidates
                .Where(candidate => !candidate.IsEnemy
                    && candidate.ActorId != monster.ActorId
                    && candidate.CurrentHp > 0)
                .Select(candidate => candidate.ActorId));
        if (ability.TargetCount > 0)
            actorIds = actorIds.Take(ability.TargetCount);
        return actorIds.ToArray();
    }
}
