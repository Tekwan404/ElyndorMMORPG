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
        ArgumentNullException.ThrowIfNull(candidates);

        AbilityTargetCandidate[] living = candidates
            .Where(candidate => candidate.ActorId != Guid.Empty && candidate.CurrentHp > 0)
            .ToArray();
        if (living.Length == 0)
            return null;

        return profile switch
        {
            AbilityTargetSelectorProfile.EncounterOrder => living[0].ActorId,
            AbilityTargetSelectorProfile.RandomEnemy =>
                SelectRandom(living.Where(candidate => candidate.IsEnemy).ToArray(), random),
            AbilityTargetSelectorProfile.CurrentThreatTarget =>
                ResolveCurrentThreatTarget(living, currentThreatTargetId),
            AbilityTargetSelectorProfile.HighestThreat => living
                .Where(candidate => candidate.IsEnemy)
                .OrderByDescending(candidate => candidate.Threat)
                .ThenBy(candidate => Array.IndexOf(living, candidate))
                .Select(candidate => (Guid?)candidate.ActorId)
                .FirstOrDefault(),
            AbilityTargetSelectorProfile.LowestHpAlly => living
                .Where(candidate => !candidate.IsEnemy)
                .OrderBy(candidate => candidate.HpPercent)
                .ThenBy(candidate => Array.IndexOf(living, candidate))
                .Select(candidate => (Guid?)candidate.ActorId)
                .FirstOrDefault(),
            AbilityTargetSelectorProfile.RandomManaUser =>
                SelectRandom(living.Where(candidate => candidate.IsEnemy && candidate.UsesMana).ToArray(), random),
            AbilityTargetSelectorProfile.NonTankRandom =>
                SelectRandom(living.Where(candidate => candidate.IsEnemy && !candidate.IsTank).ToArray(), random),
            AbilityTargetSelectorProfile.OwnerLinkedTarget =>
                ResolveOwnerLinkedTarget(living, ownerLinkedTargetId),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown target selector profile.")
        };
    }

    private static Guid? ResolveCurrentThreatTarget(
        IReadOnlyList<AbilityTargetCandidate> candidates,
        Guid? currentThreatTargetId)
    {
        if (currentThreatTargetId is { } current
            && candidates.Any(candidate => candidate.IsEnemy && candidate.ActorId == current))
        {
            return current;
        }

        return candidates
            .Where(candidate => candidate.IsEnemy)
            .OrderByDescending(candidate => candidate.Threat)
            .Select(candidate => (Guid?)candidate.ActorId)
            .FirstOrDefault();
    }

    private static Guid? ResolveOwnerLinkedTarget(
        IReadOnlyList<AbilityTargetCandidate> candidates,
        Guid? ownerLinkedTargetId)
    {
        if (ownerLinkedTargetId is { } explicitTarget
            && candidates.Any(candidate => candidate.ActorId == explicitTarget))
        {
            return explicitTarget;
        }

        return candidates
            .Where(candidate => candidate.OwnerId is not null)
            .Select(candidate => candidate.OwnerId)
            .FirstOrDefault(ownerId => ownerId is { } id
                && candidates.Any(candidate => candidate.ActorId == id));
    }

    private static Guid? SelectRandom(
        IReadOnlyList<AbilityTargetCandidate> candidates,
        IGameRandom? random)
    {
        if (candidates.Count == 0)
            return null;
        if (random is null)
            throw new InvalidOperationException("Random target selection requires an injected game RNG.");

        int index = Math.Min(
            candidates.Count - 1,
            (int)Math.Floor(random.NextUnit() * candidates.Count));
        return candidates[index].ActorId;
    }
}