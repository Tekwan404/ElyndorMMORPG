using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Progression;

namespace Elyndor.UnitTests.Progression;

public sealed class PhaseFiveProgressionTests
{
    [Fact]
    public void ExperienceCrossingThresholdLevelsUpAndCarriesRemainder()
    {
        Character character = CreateCharacter();
        character.SetExperience(90);

        CharacterProgressionResult result = CharacterProgression.GrantExperience(
            character,
            35,
            new LevelProgressionDefinition("DEFAULT_LEVELING", 60, 100, 1.5m));

        Assert.True(result.LeveledUp);
        Assert.Equal(2, character.Level);
        Assert.Equal(25, character.Experience);
        Assert.Equal(150, result.XpToNextLevel);
    }

    [Fact]
    public void EquippedStrengthFlowsThroughExistingCharacterStatCalculator()
    {
        ItemDefinition weapon = new(
            "WOLF_FANG_BLADE",
            "Wolf Fang Blade",
            ItemType.Equipment,
            ItemRarity.Uncommon,
            1,
            false,
            1,
            EquipmentSlot.Weapon,
            new PrimaryStats(2, 0, 0, 0),
            "Weapon");
        PrimaryStats equipment = EquipmentStatModifierResolver.Resolve([weapon]);
        StatFormulaProfile formula = new(
            "TEST", 50, 10, 2, 1, 2, 2, 1, 1, 1, 5, 0.25m, 100, 95, 0.2m, 1);
        ClassProfile warrior = new(
            "WARRIOR", "STRENGTH", "RAGE",
            new PrimaryStats(12, 6, 4, 10),
            new PrimaryStats(3, 1, 0.5m, 2),
            [], [], "Test");

        CharacterStats stats = new CharacterStatCalculator(formula, [warrior]).Calculate(
            "WARRIOR",
            1,
            CharacterStatInputs.Empty with { Equipment = equipment });

        Assert.Equal(14, stats.Strength);
        Assert.Equal(34, stats.AttackPower);
    }

    private static Character CreateCharacter() => new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        "Arthas",
        "ARTHAS",
        "HUMAN",
        "MALE",
        "WARRIOR",
        new DateTimeOffset(2026, 9, 2, 6, 0, 0, TimeSpan.Zero));
}
