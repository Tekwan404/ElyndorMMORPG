using Elyndor.Core.Characters;
using Elyndor.Core.Content;

namespace Elyndor.UnitTests.Characters;

public sealed class CharacterKnownAbilityResolverTests
{
    [Fact]
    public void ResolveCombinesStartingLevelAndTalentAbilitiesWithoutDuplicates()
    {
        ClassProfile profile = new(
            "WARRIOR",
            "STRENGTH",
            "RAGE",
            new PrimaryStats(12, 6, 4, 10),
            new PrimaryStats(3, 1, 0.5m, 2),
            ["ONE_HAND_SWORD"],
            ["HEAVY"],
            "Tank test profile",
            StartingAbilityIds: ["PROVOKE"],
            AbilityUnlocks:
            [
                new AbilityUnlockDefinition("SHIELD_BASH", 5),
                new AbilityUnlockDefinition("PROVOKE", 3),
                new AbilityUnlockDefinition("HEAVY_BLOW", 10)
            ]);

        IReadOnlyList<string> result = CharacterKnownAbilityResolver.Resolve(
            profile,
            level: 5,
            new HashSet<string>(["BASTION", "PROVOKE"], StringComparer.Ordinal));

        Assert.Equal(["BASTION", "PROVOKE", "SHIELD_BASH"], result);
    }

    [Fact]
    public void ResolveDoesNotGrantLevelAbilityEarly()
    {
        ClassProfile profile = new(
            "WARRIOR",
            "STRENGTH",
            "RAGE",
            new PrimaryStats(12, 6, 4, 10),
            new PrimaryStats(3, 1, 0.5m, 2),
            ["ONE_HAND_SWORD"],
            ["HEAVY"],
            "Tank test profile",
            StartingAbilityIds: ["PROVOKE"],
            AbilityUnlocks: [new AbilityUnlockDefinition("SHIELD_BASH", 5)]);

        IReadOnlyList<string> result = CharacterKnownAbilityResolver.Resolve(
            profile,
            level: 4);

        Assert.Equal(["PROVOKE"], result);
    }
}
