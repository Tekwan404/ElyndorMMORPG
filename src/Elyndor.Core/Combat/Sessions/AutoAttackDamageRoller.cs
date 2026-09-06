using Elyndor.Core.Combat.Randomness;

namespace Elyndor.Core.Combat.Sessions;

public static class AutoAttackDamageRoller
{
    private const decimal PlayerVarianceMinMultiplier = 0.90m;
    private const decimal PlayerVarianceMaxMultiplier = 1.10m;

    public static decimal RollPlayerDamage(
        AutoAttackProfile profile,
        decimal attackPower,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegative(attackPower);

        decimal amount = RollBaseDamage(profile, random)
            + attackPower * profile.AttackPowerCoefficient;
        if (amount <= 0)
            return 0;

        decimal multiplier = PlayerVarianceMinMultiplier
            + (PlayerVarianceMaxMultiplier - PlayerVarianceMinMultiplier)
            * random.NextUnit();
        return amount * multiplier;
    }

    public static decimal RollBaseDamage(
        AutoAttackProfile profile,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(random);

        decimal minimum = profile.BaseDamageMin ?? profile.BaseDamage;
        decimal maximum = profile.BaseDamageMax ?? minimum;
        if (minimum < 0 || maximum < minimum)
            throw new InvalidOperationException("Auto attack damage range is invalid.");
        if (minimum == maximum)
            return minimum;

        return minimum + (maximum - minimum) * random.NextUnit();
    }
}
