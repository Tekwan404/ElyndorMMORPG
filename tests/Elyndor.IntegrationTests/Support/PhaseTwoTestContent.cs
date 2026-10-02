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
                new("WARRIOR", "STRENGTH", "RAGE", new(12, 6, 4, 10), new(3, 1, 0.5m, 2), ["ONE_HAND_SWORD"], ["HEAVY"], "Warrior"),
                new("ARCHER", "AGILITY", "FOCUS", new(5, 9, 5, 7), new(1, 3, 1, 2), ["BOW"], ["MEDIUM"], "Archer"),
                new("MAGE", "INTELLECT", "MANA", new(3, 5, 11, 6), new(1, 1, 3, 2), ["STAFF"], ["LIGHT"], "Mage")
            ],
            new(
                "TEST_STATS",
                MaxHpBase: 50,
                MaxHpPerStamina: 10,
                AttackPowerPerStrength: 2,
                AttackPowerPerAgility: 1,
                SpellPowerPerIntellect: 2,
                MagicResistancePerStamina: 1,
                MagicResistancePerIntellect: 1,
                CriticalChanceBase: 5,
                CriticalChancePerAgility: 0.25m,
                CriticalDamageBase: 100,
                AccuracyBase: 95,
                DodgePerAgility: 0.2m,
                AttackSpeedBase: 1),
            [
                new("RAGE", 100, 0, 0, 0, 0, 5, 5),
                new("FOCUS", 100, 100, 100, 8, 12, 0, 0),
                new("MANA", 100, 100, 100, 4, 12, 0, 0)
            ]);
}
