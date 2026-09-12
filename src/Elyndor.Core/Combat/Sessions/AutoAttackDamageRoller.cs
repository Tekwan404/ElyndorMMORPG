using Elyndor.Core.Combat.Randomness;

namespace Elyndor.Core.Combat.Sessions;

public sealed record PlayerAutoAttackDamageBreakdown(
    decimal WeaponDamage,
    decimal AttackPowerDamage)
{
    public decimal TotalDamage => WeaponDamage + AttackPowerDamage;
}

public static class AutoAttackDamageRoller
{
    public static decimal RollPlayerDamage(
        AutoAttackProfile profile,
        decimal attackPower,
        IGameRandom random) =>
        RollPlayerDamageBreakdown(profile, attackPower, random).TotalDamage;

    public static PlayerAutoAttackDamageBreakdown RollPlayerDamageBreakdown(
        AutoAttackProfile profile,
        decimal attackPower,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegative(attackPower);

        decimal weaponDamage = RollBaseDamage(profile, random);
        decimal attackPowerDamage = attackPower * profile.AttackPowerCoefficient;
        return new PlayerAutoAttackDamageBreakdown(
            weaponDamage,
            attackPowerDamage);
    }

    public static decimal RollBaseDamage(
        AutoAttackProfile profile,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(random);
        if (profile.WeaponDamageMultiplier < 0)
            throw new InvalidOperationException("Weapon damage multiplier cannot be negative.");

        decimal minimum = profile.BaseDamageMin ?? profile.BaseDamage;
        decimal maximum = profile.BaseDamageMax ?? minimum;
        if (minimum < 0 || maximum < minimum)
            throw new InvalidOperationException("Auto attack damage range is invalid.");

        decimal rolledDamage = minimum == maximum
            ? minimum
            : minimum + (maximum - minimum) * random.NextUnit();
        return rolledDamage * profile.WeaponDamageMultiplier;
    }
}
