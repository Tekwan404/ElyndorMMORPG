using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.Core.Monsters;

public sealed class MonsterAbilitySchedulerState
{
    private readonly HashSet<string> _oncePerCombatUsed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _nextAllowedAtUtc = new(StringComparer.Ordinal);

    public bool WasUsedOnce(string abilityId) => _oncePerCombatUsed.Contains(abilityId);

    public bool IsReady(string abilityId, DateTimeOffset now) =>
        !_nextAllowedAtUtc.TryGetValue(abilityId, out DateTimeOffset readyAt) || readyAt <= now;

    public void MarkExecuted(
        MonsterAbilityRule rule,
        AbilityDefinition ability,
        DateTimeOffset now,
        IGameRandom? random = null)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(ability);

        if (rule.OncePerCombat)
            _oncePerCombatUsed.Add(rule.AbilityId);

        TimeSpan jitter = rule.CooldownJitter ?? TimeSpan.Zero;
        if (jitter < TimeSpan.Zero)
            throw new InvalidOperationException("Monster ability cooldown jitter cannot be negative.");
        if (jitter > TimeSpan.Zero && random is null)
            throw new InvalidOperationException("Cooldown jitter requires an injected game RNG.");

        TimeSpan resolvedJitter = jitter == TimeSpan.Zero
            ? TimeSpan.Zero
            : TimeSpan.FromTicks((long)(jitter.Ticks * random!.NextUnit()));
        _nextAllowedAtUtc[rule.AbilityId] = now + ability.Cooldown + resolvedJitter;
    }

    public void Reset()
    {
        _oncePerCombatUsed.Clear();
        _nextAllowedAtUtc.Clear();
    }
}

public static class MonsterAbilityScheduler
{
    public static MonsterAbilityRule? SelectRule(
        MonsterAiProfile profile,
        CombatActorState monster,
        DateTimeOffset combatStartedAtUtc,
        DateTimeOffset now,
        MonsterAbilitySchedulerState state) =>
        SelectEligibleRules(
                profile,
                monster,
                combatStartedAtUtc,
                now,
                state)
            .FirstOrDefault();

    public static IReadOnlyList<MonsterAbilityRule> SelectEligibleRules(
        MonsterAiProfile profile,
        CombatActorState monster,
        DateTimeOffset combatStartedAtUtc,
        DateTimeOffset now,
        MonsterAbilitySchedulerState state)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(monster);
        ArgumentNullException.ThrowIfNull(state);

        IReadOnlyList<MonsterAbilityRule> rules = profile.AbilityRules is { Count: > 0 }
            ? profile.AbilityRules
            : profile.PriorityAbilityIds
                .Select((abilityId, index) => new MonsterAbilityRule(
                    abilityId,
                    Priority: profile.PriorityAbilityIds.Count - index))
                .ToArray();

        decimal hpPercent = monster.MaxHp <= 0 ? 0 : monster.CurrentHp / monster.MaxHp * 100m;

        return rules
            .Select((rule, index) => new { rule, index })
            .Where(item => IsEligible(
                item.rule,
                monster,
                hpPercent,
                combatStartedAtUtc,
                now,
                state))
            .OrderByDescending(item => item.rule.Priority)
            .ThenBy(item => item.index)
            .Select(item => item.rule)
            .ToArray();
    }

    private static bool IsEligible(
        MonsterAbilityRule rule,
        CombatActorState monster,
        decimal hpPercent,
        DateTimeOffset combatStartedAtUtc,
        DateTimeOffset now,
        MonsterAbilitySchedulerState state)
    {
        if (string.IsNullOrWhiteSpace(rule.AbilityId))
            return false;
        if (rule.MinHpPercent is { } minHp && hpPercent < minHp)
            return false;
        if (rule.MaxHpPercent is { } maxHp && hpPercent > maxHp)
            return false;
        if (rule.InitialDelay is { } initialDelay && now < combatStartedAtUtc + initialDelay)
            return false;
        if (rule.OncePerCombat && state.WasUsedOnce(rule.AbilityId))
            return false;
        if (!state.IsReady(rule.AbilityId, now))
            return false;
        if (!string.IsNullOrWhiteSpace(rule.RequiredEffectId)
            && !HasEffect(monster, rule.RequiredEffectId, now))
        {
            return false;
        }
        if (!string.IsNullOrWhiteSpace(rule.ForbiddenEffectId)
            && HasEffect(monster, rule.ForbiddenEffectId, now))
        {
            return false;
        }

        return true;
    }

    private static bool HasEffect(
        CombatActorState monster,
        string effectId,
        DateTimeOffset now) =>
        monster.ActiveEffects.Any(effect =>
            effect.ExpiresAtUtc > now
            && string.Equals(effect.Definition.Id, effectId, StringComparison.Ordinal));
}
