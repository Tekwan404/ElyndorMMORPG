using Elyndor.Core.World;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;
using Elyndor.Core.Monsters;
using Elyndor.Core.Items;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
        internal static void ValidateCombatDefinitions(
            GameContentPackage package,
            List<ContentValidationError> errors)
        {
            bool requiresAbilityPresentation =
                Version.TryParse(package.ContentVersion, out Version? contentVersion)
                && contentVersion >= new Version(0, 9, 3);

            HashSet<string> effectIds = [];
            for (var index = 0; index < (package.Effects?.Count ?? 0); index++)
            {
                EffectDefinition effect = package.Effects![index];
                string path = $"effects[{index}]";
                bool idIsValid = ValidateIdentifier(
                    effect.Id, "INVALID_EFFECT_ID", $"{path}.id", errors);
                if (idIsValid && !effectIds.Add(effect.Id))
                {
                    errors.Add(new ContentValidationError(
                        "DUPLICATE_EFFECT_ID", path, $"Effect '{effect.Id}' is duplicated."));
                }

                bool periodic = effect.Kind is EffectKind.DamageOverTime or EffectKind.HealingOverTime;
                if (effect.Duration <= TimeSpan.Zero
                    || effect.MaxStacks <= 0
                    || effect.Magnitude < 0
                    || effect.Version <= 0
                    || periodic != effect.TickInterval.HasValue
                    || effect.TickInterval <= TimeSpan.Zero)
                {
                    errors.Add(new ContentValidationError(
                        "INVALID_EFFECT_DEFINITION", path,
                        $"Effect '{effect.Id}' contains values outside its valid range."));
                }
            }

            HashSet<string> abilityIds = [];
            for (var index = 0; index < (package.Abilities?.Count ?? 0); index++)
            {
                AbilityDefinition ability = package.Abilities![index];
                string path = $"abilities[{index}]";
                bool idIsValid = ValidateIdentifier(
                    ability.Id, "INVALID_ABILITY_ID", $"{path}.id", errors);
                if (idIsValid && !abilityIds.Add(ability.Id))
                {
                    errors.Add(new ContentValidationError(
                        "DUPLICATE_ABILITY_ID", path, $"Ability '{ability.Id}' is duplicated."));
                }

                if (ability.ResourceCost < 0
                    || ability.Cooldown < TimeSpan.Zero
                    || ability.CastTime < TimeSpan.Zero
                    || ability.Type == AbilityType.Casted && ability.CastTime <= TimeSpan.Zero
                    || ability.Type != AbilityType.Casted && ability.CastTime != TimeSpan.Zero
                    || ability.UsesGlobalCooldown && ability.GlobalCooldownCategory == GlobalCooldownCategory.None
                    || requiresAbilityPresentation
                        && (string.IsNullOrWhiteSpace(ability.DisplayName)
                            || string.IsNullOrWhiteSpace(ability.Description))
                    || ability.Actions?.Any(action => action.Amount < 0
                        || action.AttackPowerCoefficient < 0
                        || action.ArmorPenetrationBonus < 0
                        || action.Type == AbilityActionType.ApplyEffect && action.Effect is null
                        || action.Type != AbilityActionType.ApplyEffect && action.Effect is not null
                        || action.Type == AbilityActionType.Taunt && action.Duration <= TimeSpan.Zero) == true
                    || string.IsNullOrWhiteSpace(ability.School))
                {
                    errors.Add(new ContentValidationError(
                        "INVALID_ABILITY_DEFINITION", path,
                        $"Ability '{ability.Id}' contains values outside its valid range."));
                }
            }
        }

}
