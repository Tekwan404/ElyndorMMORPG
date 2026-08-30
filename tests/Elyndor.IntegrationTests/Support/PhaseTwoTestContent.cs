using Elyndor.Core.Content;
using Elyndor.Core.World;

namespace Elyndor.IntegrationTests.Support;

internal static class PhaseTwoTestContent
{
    public static GameContentPackage Create(
        DateTimeOffset publishedAtUtc,
        IReadOnlyList<GameContentDefinition> definitions,
        IReadOnlyList<LocationDefinition> locations) =>
        new(
            "0.1.0",
            "0.1.0",
            publishedAtUtc,
            definitions,
            locations,
            [
                new("WARRIOR", "STRENGTH", "RAGE", new(12, 6, 4, 10), new(3, 1, 0.5m, 2), ["SWORD"], ["HEAVY"], "Warrior"),
                new("ARCHER", "AGILITY", "FOCUS", new(5, 9, 5, 7), new(1, 3, 1, 2), ["BOW"], ["MEDIUM"], "Archer"),
                new("MAGE", "INTELLECT", "MANA", new(3, 5, 11, 6), new(1, 1, 3, 2), ["STAFF"], ["LIGHT"], "Mage")
            ],
            new(
                "TEST_STATS",
                50,
                10,
                2,
                1,
                2,
                2,
                1,
                1,
                1,
                5,
                0.25m,
                100,
                95,
                0.2m,
                1),
            [
                new("RAGE", 100, 0, 0, 0, 0, 5, 5),
                new("FOCUS", 100, 100, 100, 8, 12, 0, 0),
                new("MANA", 100, 100, 100, 4, 12, 0, 0)
            ]);
}
