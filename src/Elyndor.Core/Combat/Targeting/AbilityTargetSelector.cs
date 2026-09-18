using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.Core.Combat.Targeting;

public sealed record AbilityTargetCandidate(
    Guid ActorId,
    bool IsEnemy,
    bool IsTank,
    bool UsesMana,
    decimal CurrentHp,
    decimal MaxHp,
    decimal Threat = 0,
    Guid? OwnerId = null)
{
    public decimal HpPercent => MaxHp <= 0 ? 0 : CurrentHp / MaxHp * 100m;
}

public static class AbilityTargetSelector
{
    public static Guid? SelectSingle(
        AbilityTargetSelectorProfile profile,
        IReadOnlyList<AbilityTargetCandidate> candidates,
        IGameRandom? random = null,
        Guid? currentThreatTargetId = null,
        Guid? ownerLinkedTargetId = null)
    {
        IReadOnlyList<Guid> selected = SelectMany(
            profile,
            candidates,
            1,
            random,
            currentThreatTargetId,
            ownerLinkedTargetId);
        return selected.Count == 0 ? null : selected[0];
    }

    public static IReadOnlyList<Guid> SelectMany(
        AbilityTargetSelectorProfile profile,
        IReadOnlyList<AbilityTargetCandidate> candidates,
        int count,
        IGameRandom? random = null,
        Guid? currentThreatTargetId = null,
        Guid? ownerLinkedTargetId = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (count <= 0)
            return [];

        AbilityTargetCandidate[] living = candidates
            .Where(candidate => candidate.ActorId != Guid.Empty && candidate.CurrentHp > 0)
            .ToArray();
        if (living.Length == 0)
            return [];

        return profile switch
        {
            AbilityTargetSelectorProfile.EncounterOrder => living
                .Take(count)
                .Select(candidate => candidate.ActorId)
                .ToArray(),
            AbilityTargetSelectorProfile.RandomEnemy => SelectRandomMany(
                living.Where(candidate => candidate.IsEnemy).ToArray(),
                count,
                random),
            AbilityTargetSelectorProfile.CurrentThreatTarget => SelectCurrentThreatTargets(
                living,
                count,
                currentThreatTargetId),
            AbilityTargetSelectorProfile.HighestThreat => living
                .Where(candidate => candidate.IsEnemy)
                .OrderByDescending(candidate => candidate.Threat)
                .ThenBy(candidate => Array.IndexOf(living, candidate))
                .Take(count)
                .Select(candidate => candidate.ActorId)
                .ToArray(),
            AbilityTargetSelectorProfile.LowestHpAlly => living
                .Where(candidate => !candidate.IsEnemy)
                .OrderBy(candidate => candidate.HpPercent)
                .ThenBy(candidate => Array.IndexOf(living, candidate))
                .Take(count)
                .Select(candidate => candidate.ActorId)
                .ToArray(),
            AbilityTargetSelectorProfile.RandomManaUser => SelectRandomMany(
                living.Where(candidate => candidate.IsEnemy && candidate.UsesMana).ToArray(),
                count,
                random),
            AbilityTargetSelectorProfile.NonTankRandom => SelectRandomMany(
                living.Where(candidate => candidate.IsEnemy && !candidate.IsTank).ToArray(),
                count,
                random),
            AbilityTargetSelectorProfile.OwnerLinkedTarget => ResolveOwnerLinkedTargets(
                living,
                count,
                ownerLinkedTargetId),
            _ => throw new ArgumentOutOfRangeException(
                nameof(profile),
                profile,
                "Unknown target selector profile.")
        };
    }

    private static List<Guid> SelectCurrentThreatTargets(
        AbilityTargetCandidate[] candidates,
        int count,
        Guid? currentThreatTargetId)
    {
        AbilityTargetCandidate[] enemies = candidates
            .Where(candidate => candidate.IsEnemy)
            .OrderByDescending(candidate => candidate.Threat)
            .ThenBy(candidate => Array.IndexOf(candidates, candidate))
            .ToArray();
        if (enemies.Length == 0)
            return [];

        List<Guid> selected = [];
        if (currentThreatTargetId is { } current
            && enemies.Any(candidate => candidate.ActorId == current))
        {
            selected.Add(current);
        }

        selected.AddRange(enemies
            .Where(candidate => !selected.Contains(candidate.ActorId))
            .Take(Math.Max(0, count - selected.Count))
            .Select(candidate => candidate.ActorId));
        return selected;
    }

    private static Guid[] ResolveOwnerLinkedTargets(
        AbilityTargetCandidate[] candidates,
        int count,
        Guid? ownerLinkedTargetId)
    {
        Guid? resolved = null;
        if (ownerLinkedTargetId is { } explicitTarget
            && candidates.Any(candidate => candidate.ActorId == explicitTarget))
        {
            resolved = explicitTarget;
        }
        else
        {
            resolved = candidates
                .Where(candidate => candidate.OwnerId is not null)
                .Select(candidate => candidate.OwnerId)
                .FirstOrDefault(ownerId => ownerId is { } id
                    && candidates.Any(candidate => candidate.ActorId == id));
        }

        return resolved is { } actorId && count > 0 ? [actorId] : [];
    }

    private static Guid[] SelectRandomMany(
        AbilityTargetCandidate[] candidates,
        int count,
        IGameRandom? random)
    {
        if (candidates.Length == 0 || count <= 0)
            return [];
        if (random is null)
            throw new InvalidOperationException(
                "Random target selection requires an injected game RNG.");

        AbilityTargetCandidate[] pool = candidates.ToArray();
        int selectedCount = Math.Min(count, pool.Length);
        Guid[] selected = new Guid[selectedCount];
        for (var index = 0; index < selectedCount; index++)
        {
            int remaining = pool.Length - index;
            int offset = Math.Min(
                remaining - 1,
                (int)Math.Floor(random.NextUnit() * remaining));
            int selectedIndex = index + offset;
            (pool[index], pool[selectedIndex]) = (pool[selectedIndex], pool[index]);
            selected[index] = pool[index].ActorId;
        }

        return selected;
    }
}
