using Elyndor.Core.Characters;
using Elyndor.Core.Content;

namespace Elyndor.UnitTests.Characters;

public sealed class CharacterResourceProfileResolverTests
{
    [Fact]
    public void ScalesManaFromIntellectAndKeepsFullStartAndRespawn()
    {
        ResourceProfile mana = new(
            "MANA",
            MaxValue: 100,
            StartValue: 100,
            RespawnValue: 100,
            CombatRegenPerSecond: 4,
            OutOfCombatRegenPerSecond: 12,
            OutOfCombatDecayPerSecond: 0,
            OutOfCombatDelaySeconds: 0);
        CharacterStats levelSixtyMage = Stats(intellect: 188);

        ResourceProfile result = CharacterResourceProfileResolver.Resolve(
            mana,
            new ResourceScalingProfile(ManaBase: 100, ManaPerIntellect: 5),
            levelSixtyMage);

        Assert.Equal(1040, result.MaxValue);
        Assert.Equal(1040, result.StartValue);
        Assert.Equal(1040, result.RespawnValue);
        Assert.Equal(4, result.CombatRegenPerSecond);
        Assert.Equal(12, result.OutOfCombatRegenPerSecond);
    }

    [Fact]
    public void AddsFlatMaxResourceAfterIntellectScaling()
    {
        ResourceProfile mana = new(
            "MANA",
            MaxValue: 100,
            StartValue: 100,
            RespawnValue: 100,
            CombatRegenPerSecond: 4,
            OutOfCombatRegenPerSecond: 12,
            OutOfCombatDecayPerSecond: 0,
            OutOfCombatDelaySeconds: 0);

        ResourceProfile result = CharacterResourceProfileResolver.Resolve(
            mana,
            new ResourceScalingProfile(ManaBase: 100, ManaPerIntellect: 5),
            Stats(intellect: 80),
            maxResourceFlat: 25);

        Assert.Equal(525, result.MaxValue);
    }

    [Fact]
    public void LeavesNonManaResourceCapacityUnchanged()
    {
        ResourceProfile rage = new(
            "RAGE",
            MaxValue: 100,
            StartValue: 0,
            RespawnValue: 0,
            CombatRegenPerSecond: 0,
            OutOfCombatRegenPerSecond: 0,
            OutOfCombatDecayPerSecond: 5,
            OutOfCombatDelaySeconds: 5);

        ResourceProfile result = CharacterResourceProfileResolver.Resolve(
            rage,
            new ResourceScalingProfile(ManaBase: 100, ManaPerIntellect: 5),
            Stats(intellect: 999));

        Assert.Equal(100, result.MaxValue);
        Assert.Equal(0, result.StartValue);
        Assert.Equal(0, result.RespawnValue);
    }

    private static CharacterStats Stats(decimal intellect) => new(
        Strength: 0,
        Agility: 0,
        Intellect: intellect,
        Stamina: 0,
        MaxHp: 1,
        AttackPower: 0,
        SpellPower: 0,
        CriticalChance: 0,
        CriticalDamage: 100,
        Accuracy: 100,
        ArmorPenetration: 0,
        MagicPenetration: 0,
        AttackSpeed: 1,
        Armor: 0,
        MagicResistance: 0,
        Dodge: 0);
}
