using Elyndor.Core.Combat.Randomness;

namespace Elyndor.Core.Professions;

public static class ProfessionSkillRules
{
    public static bool ShouldIncrease(IGameRandom random, int currentSkill, int requiredSkill, int skillUpUntil)
    {
        if (currentSkill < requiredSkill || currentSkill >= skillUpUntil)
            return false;

        decimal chance = (decimal)(skillUpUntil - currentSkill) / (skillUpUntil - requiredSkill);
        return random.NextUnit() < chance;
    }
}
