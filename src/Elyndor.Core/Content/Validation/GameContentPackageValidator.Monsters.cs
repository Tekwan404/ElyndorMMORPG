using Elyndor.Core.World;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;
using Elyndor.Core.Monsters;
using Elyndor.Core.Items;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
        internal static void ValidateMonsterDefinitions(
            GameContentPackage package,
            List<ContentValidationError> errors)
        {
            HashSet<string> abilityIds = (package.Abilities ?? [])
                .Select(ability => ability.Id)
                .ToHashSet(StringComparer.Ordinal);
            HashSet<string> aiProfileIds = [];
            for (var index = 0; index < (package.MonsterAiProfiles?.Count ?? 0); index++)
            {
                MonsterAiProfile profile = package.MonsterAiProfiles![index];
                string path = $"monsterAiProfiles[{index}]";
                bool idIsValid = ValidateIdentifier(
                    profile.Id, "INVALID_MONSTER_AI_PROFILE_ID", $"{path}.id", errors);
                if (idIsValid && !aiProfileIds.Add(profile.Id))
                {
                    errors.Add(new("DUPLICATE_MONSTER_AI_PROFILE_ID", path,
                        $"Monster AI profile '{profile.Id}' is duplicated."));
                }

                if (profile.Version <= 0 || profile.PriorityAbilityIds.Any(id => !abilityIds.Contains(id)))
                {
                    errors.Add(new("INVALID_MONSTER_AI_PROFILE", path,
                        $"Monster AI profile '{profile.Id}' is invalid."));
                }
            }

            HashSet<string> monsterIds = [];
            for (var index = 0; index < (package.Monsters?.Count ?? 0); index++)
            {
                MonsterDefinition monster = package.Monsters![index];
                string path = $"monsters[{index}]";
                bool idIsValid = ValidateIdentifier(
                    monster.Id, "INVALID_MONSTER_ID", $"{path}.id", errors);
                if (idIsValid && !monsterIds.Add(monster.Id))
                {
                    errors.Add(new("DUPLICATE_MONSTER_ID", path,
                        $"Monster '{monster.Id}' is duplicated."));
                }

                if (string.IsNullOrWhiteSpace(monster.Name)
                    || monster.Level <= 0
                    || monster.MaxHp <= 0
                    || monster.AutoAttackInterval <= TimeSpan.Zero
                    || monster.AutoAttackBaseDamage < 0
                    || monster.AutoAttackAttackPowerCoefficient < 0
                    || monster.Version <= 0)
                {
                    errors.Add(new("INVALID_MONSTER_DEFINITION", path,
                        $"Monster '{monster.Id}' contains values outside its valid range."));
                }

                foreach (string abilityId in monster.AbilityIds)
                {
                    if (!abilityIds.Contains(abilityId))
                    {
                        errors.Add(new("MISSING_MONSTER_ABILITY", path,
                            $"Monster '{monster.Id}' references missing ability '{abilityId}'."));
                    }
                }

                if (!aiProfileIds.Contains(monster.AiProfileId))
                {
                    errors.Add(new("MISSING_MONSTER_AI_PROFILE", path,
                        $"Monster '{monster.Id}' references missing AI profile '{monster.AiProfileId}'."));
                }
            }
        }

}
