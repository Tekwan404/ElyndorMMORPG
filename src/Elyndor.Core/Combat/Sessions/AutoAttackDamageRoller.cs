using Elyndor.Core.Combat.Randomness;

namespace Elyndor.Core.Combat.Sessions;

public static class AutoAttackDamageRoller
{
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
