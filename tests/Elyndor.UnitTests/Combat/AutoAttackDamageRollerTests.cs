using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;

namespace Elyndor.UnitTests.Combat;

public sealed class AutoAttackDamageRollerTests
{
    [Fact]
    public void RollPlayerDamageAddsWeaponRollAndAttackPowerSeparately()
    {
        AutoAttackProfile profile = new(
            TimeSpan.FromSeconds(2),
            BaseDamage: 0,
            AttackPowerCoefficient: 0.5m,
            ResourceOnHit: 0,
            BaseDamageMin: 10,
            BaseDamageMax: 20);

        Assert.Equal(
            25m,
            AutoAttackDamageRoller.RollPlayerDamage(
                profile,
                attackPower: 30,
                new SequenceGameRandom(0m)));
        Assert.Equal(
            30m,
            AutoAttackDamageRoller.RollPlayerDamage(
                profile,
                attackPower: 30,
                new SequenceGameRandom(0.5m)));
        Assert.Equal(
            34.99999m,
            AutoAttackDamageRoller.RollPlayerDamage(
                profile,
                attackPower: 30,
                new SequenceGameRandom(0.999999m)),
            precision: 4);
    }

    [Fact]
    public void RollPlayerDamageBreakdownKeepsComponentsSeparate()
    {
        AutoAttackProfile profile = new(
            TimeSpan.FromSeconds(2),
            BaseDamage: 0,
            AttackPowerCoefficient: 0.5m,
            ResourceOnHit: 0,
            BaseDamageMin: 10,
            BaseDamageMax: 20);

        PlayerAutoAttackDamageBreakdown breakdown =
            AutoAttackDamageRoller.RollPlayerDamageBreakdown(
                profile,
                attackPower: 30,
                new SequenceGameRandom(0.5m));

        Assert.Equal(15m, breakdown.WeaponDamage);
        Assert.Equal(15m, breakdown.AttackPowerDamage);
        Assert.Equal(30m, breakdown.TotalDamage);
    }

    [Fact]
    public void WeaponDamageMultiplierChangesOnlyWeaponComponent()
    {
        AutoAttackProfile profile = new(
            TimeSpan.FromSeconds(2),
            BaseDamage: 0,
            AttackPowerCoefficient: 0.5m,
            ResourceOnHit: 0,
            BaseDamageMin: 10,
            BaseDamageMax: 20,
            WeaponDamageMultiplier: 1.25m);

        PlayerAutoAttackDamageBreakdown breakdown =
            AutoAttackDamageRoller.RollPlayerDamageBreakdown(
                profile,
                attackPower: 30,
                new SequenceGameRandom(0.5m));

        Assert.Equal(18.75m, breakdown.WeaponDamage);
        Assert.Equal(15m, breakdown.AttackPowerDamage);
        Assert.Equal(33.75m, breakdown.TotalDamage);
    }

    [Fact]
    public void RollBaseDamageUsesConfiguredRange()
    {
        AutoAttackProfile profile = new(
            TimeSpan.FromSeconds(2),
            BaseDamage: 0,
            AttackPowerCoefficient: 0.5m,
            ResourceOnHit: 0,
            BaseDamageMin: 10,
            BaseDamageMax: 20);

        Assert.Equal(
            10m,
            AutoAttackDamageRoller.RollBaseDamage(
                profile,
                new SequenceGameRandom(0m)));
        Assert.Equal(
            15m,
            AutoAttackDamageRoller.RollBaseDamage(
                profile,
                new SequenceGameRandom(0.5m)));
    }

    [Fact]
    public void RollBaseDamageDoesNotConsumeRandomForLegacyFixedDamage()
    {
        AutoAttackProfile profile = new(
            TimeSpan.FromSeconds(2),
            BaseDamage: 12,
            AttackPowerCoefficient: 0.5m,
            ResourceOnHit: 0);

        SequenceGameRandom random = new();

        Assert.Equal(12m, AutoAttackDamageRoller.RollBaseDamage(profile, random));
    }

    [Fact]
    public void RollBaseDamageRejectsInvertedRange()
    {
        AutoAttackProfile profile = new(
            TimeSpan.FromSeconds(2),
            BaseDamage: 0,
            AttackPowerCoefficient: 0.5m,
            ResourceOnHit: 0,
            BaseDamageMin: 20,
            BaseDamageMax: 10);

        Assert.Throws<InvalidOperationException>(() =>
            AutoAttackDamageRoller.RollBaseDamage(
                profile,
                new SequenceGameRandom(0m)));
    }

    [Fact]
    public void RollBaseDamageRejectsNegativeWeaponDamageMultiplier()
    {
        AutoAttackProfile profile = new(
            TimeSpan.FromSeconds(2),
            BaseDamage: 0,
            AttackPowerCoefficient: 0.5m,
            ResourceOnHit: 0,
            BaseDamageMin: 10,
            BaseDamageMax: 20,
            WeaponDamageMultiplier: -0.1m);

        Assert.Throws<InvalidOperationException>(() =>
            AutoAttackDamageRoller.RollBaseDamage(
                profile,
                new SequenceGameRandom(0m)));
    }
}
