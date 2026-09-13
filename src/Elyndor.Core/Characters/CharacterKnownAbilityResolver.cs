using Elyndor.Core.Content;

namespace Elyndor.Core.Characters;

public static class CharacterKnownAbilityResolver
{
    public static IReadOnlyList<string> Resolve(
        ClassProfile classProfile,
        int level,
        IReadOnlySet<string>? talentUnlockedAbilityIds = null)
    {
        ArgumentNullException.ThrowIfNull(classProfile);
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);

        HashSet<string> knownAbilityIds = new(StringComparer.Ordinal);

        foreach (string abilityId in classProfile.StartingAbilityIds ?? [])
        {
            if (!string.IsNullOrWhiteSpace(abilityId))
                knownAbilityIds.Add(abilityId);
        }

        foreach (AbilityUnlockDefinition unlock in classProfile.AbilityUnlocks ?? [])
        {
            if (level >= unlock.UnlockLevel && !string.IsNullOrWhiteSpace(unlock.AbilityId))
                knownAbilityIds.Add(unlock.AbilityId);
        }

        if (talentUnlockedAbilityIds is not null)
            knownAbilityIds.UnionWith(talentUnlockedAbilityIds.Where(id => !string.IsNullOrWhiteSpace(id)));

        return knownAbilityIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
    }
}
