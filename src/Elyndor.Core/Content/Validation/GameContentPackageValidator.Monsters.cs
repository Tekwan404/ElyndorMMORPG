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
            HashSet<string> effectIds = (package.Effects ?? [])
                .Select(effect => effect.Id)
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

                ValidateMonsterAbilityRules(
                    profile,
                    abilityIds,
                    effectIds,
                    path,
                    errors);
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
                    || monster.AutoAttackBaseDamageMin is < 0
                    || monster.AutoAttackBaseDamageMax is < 0
                    || monster.AutoAttackBaseDamageMin.HasValue
                        && monster.AutoAttackBaseDamageMax.HasValue
                        && monster.AutoAttackBaseDamageMax < monster.AutoAttackBaseDamageMin
                    || monster.AutoAttackBaseDamageMax.HasValue
                        && !monster.AutoAttackBaseDamageMin.HasValue
                        && monster.AutoAttackBaseDamageMax < monster.AutoAttackBaseDamage
                    || monster.Stats.Armor < 0
                    || monster.Stats.MagicResistance < 0
                    || monster.Stats.ArmorPenetration is < 0 or > 1
                    || monster.Stats.MagicPenetration is < 0 or > 1
                    || monster.Stats.BlockChance is < 0 or > 100
                    || monster.Stats.BlockValueMin < 0
                    || monster.Stats.BlockValueMax < monster.Stats.BlockValueMin
                    || monster.Stats.BlockChance > 0 && monster.Stats.BlockValueMax <= 0
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

            for (var index = 0; index < (package.Monsters?.Count ?? 0); index++)
            {
                MonsterDefinition monster = package.Monsters![index];
                string path = $"monsters[{index}]";
                bool hasSummon = !string.IsNullOrWhiteSpace(monster.SummonMonsterId);
                if (!hasSummon)
                {
                    if (monster.SummonIntervalSeconds != 0
                        || monster.SummonCount != 0
                        || monster.MaxActiveSummons != 0)
                    {
                        errors.Add(new(
                            "INVALID_MONSTER_SUMMON_PROFILE",
                            path,
                            $"Monster '{monster.Id}' has partial summon configuration."));
                    }
                    continue;
                }

                if (!monsterIds.Contains(monster.SummonMonsterId!)
                    || string.Equals(monster.Id, monster.SummonMonsterId, StringComparison.Ordinal)
                    || monster.SummonIntervalSeconds <= 0
                    || monster.SummonCount <= 0
                    || monster.MaxActiveSummons < monster.SummonCount)
                {
                    errors.Add(new(
                        "INVALID_MONSTER_SUMMON_PROFILE",
                        path,
                        $"Monster '{monster.Id}' has an invalid summon configuration."));
                }
            }
        }

        private static void ValidateMonsterAbilityRules(
            MonsterAiProfile profile,
            HashSet<string> abilityIds,
            HashSet<string> effectIds,
            string path,
            List<ContentValidationError> errors)
        {
            if (profile.AbilityRules is not { Count: > 0 } rules)
                return;

            for (var ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
            {
                MonsterAbilityRule rule = rules[ruleIndex];
                string rulePath = $"{path}.abilityRules[{ruleIndex}]";
                bool hpRangeInvalid = rule.MinHpPercent is < 0 or > 100
                    || rule.MaxHpPercent is < 0 or > 100
                    || rule.MinHpPercent is { } minHp
                        && rule.MaxHpPercent is { } maxHp
                        && minHp > maxHp
                    || rule.TargetMinHpPercent is < 0 or > 100
                    || rule.TargetMaxHpPercent is < 0 or > 100
                    || rule.TargetMinHpPercent is { } targetMinHp
                        && rule.TargetMaxHpPercent is { } targetMaxHp
                        && targetMinHp > targetMaxHp;
                bool timingInvalid = rule.InitialDelay is { } initialDelay
                        && initialDelay < TimeSpan.Zero
                    || rule.CooldownJitter is { } cooldownJitter
                        && cooldownJitter < TimeSpan.Zero;
                bool effectReferenceInvalid = !string.IsNullOrWhiteSpace(rule.RequiredEffectId)
                        && !effectIds.Contains(rule.RequiredEffectId)
                    || !string.IsNullOrWhiteSpace(rule.ForbiddenEffectId)
                        && !effectIds.Contains(rule.ForbiddenEffectId);

                if (string.IsNullOrWhiteSpace(rule.AbilityId)
                    || !abilityIds.Contains(rule.AbilityId)
                    || hpRangeInvalid
                    || timingInvalid
                    || effectReferenceInvalid)
                {
                    errors.Add(new(
                        "INVALID_MONSTER_ABILITY_RULE",
                        rulePath,
                        $"Monster AI profile '{profile.Id}' contains an invalid ability rule for '{rule.AbilityId}'."));
                }
            }
        }
}
