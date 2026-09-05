using Elyndor.Core.World;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;
using Elyndor.Core.Monsters;
using Elyndor.Core.Items;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
        internal static void ValidateCharacterProfiles(
            GameContentPackage package,
            IReadOnlySet<ContentKey> definitions,
            List<ContentValidationError> errors)
        {
            if (package.ClassProfiles is null
                || package.StatFormula is null
                || package.ResourceProfiles is null)
            {
                if (Version.TryParse(package.ContentVersion, out Version? version)
                    && version >= new Version(0, 2, 0))
                {
                    errors.Add(new ContentValidationError(
                        "MISSING_CHARACTER_PROFILES",
                        "classProfiles",
                        "Content version 0.2.0 and newer requires class, stat, and resource profiles."));
                }

                return;
            }

            HashSet<string> resourceIds = [];
            for (var index = 0; index < package.ResourceProfiles.Count; index++)
            {
                ResourceProfile profile = package.ResourceProfiles[index];
                string path = $"resourceProfiles[{index}]";
                if (!resourceIds.Add(profile.Id))
                {
                    errors.Add(new ContentValidationError(
                        "DUPLICATE_RESOURCE_PROFILE",
                        path,
                        $"Resource profile '{profile.Id}' is duplicated."));
                }

                if (profile.MaxValue <= 0
                    || profile.StartValue < 0
                    || profile.StartValue > profile.MaxValue
                    || profile.RespawnValue < 0
                    || profile.RespawnValue > profile.MaxValue
                    || profile.CombatRegenPerSecond < 0
                    || profile.OutOfCombatRegenPerSecond < 0
                    || profile.OutOfCombatDecayPerSecond < 0
                    || profile.OutOfCombatDelaySeconds < 0)
                {
                    errors.Add(new ContentValidationError(
                        "INVALID_RESOURCE_PROFILE",
                        path,
                        $"Resource profile '{profile.Id}' contains values outside its valid range."));
                }
            }

            HashSet<string> classIds = [];
            for (var index = 0; index < package.ClassProfiles.Count; index++)
            {
                ClassProfile profile = package.ClassProfiles[index];
                string path = $"classProfiles[{index}]";
                if (!classIds.Add(profile.Id))
                {
                    errors.Add(new ContentValidationError(
                        "DUPLICATE_CLASS_PROFILE",
                        path,
                        $"Class profile '{profile.Id}' is duplicated."));
                }

                if (!definitions.Contains(new ContentKey("CLASS", profile.Id)))
                {
                    errors.Add(new ContentValidationError(
                        "MISSING_CLASS_DEFINITION",
                        path,
                        $"Class definition '{profile.Id}' does not exist."));
                }

                if (!resourceIds.Contains(profile.ResourceProfileId))
                {
                    errors.Add(new ContentValidationError(
                        "MISSING_RESOURCE_PROFILE",
                        path,
                        $"Resource profile '{profile.ResourceProfileId}' does not exist."));
                }

                if (profile.PrimaryAttribute is not ("STRENGTH" or "AGILITY" or "INTELLECT")
                    || string.IsNullOrWhiteSpace(profile.PrototypeIdentity))
                {
                    errors.Add(new ContentValidationError(
                        "INVALID_CLASS_IDENTITY",
                        path,
                        $"Class profile '{profile.Id}' contains an invalid primary attribute or identity."));
                }

                if (profile.BaseStats.Strength < 0
                    || profile.BaseStats.Agility < 0
                    || profile.BaseStats.Intellect < 0
                    || profile.BaseStats.Stamina < 0
                    || profile.LevelGrowth.Strength < 0
                    || profile.LevelGrowth.Agility < 0
                    || profile.LevelGrowth.Intellect < 0
                    || profile.LevelGrowth.Stamina < 0)
                {
                    errors.Add(new ContentValidationError(
                        "INVALID_CLASS_STATS",
                        path,
                        $"Class profile '{profile.Id}' contains negative stats."));
                }

                bool invalidWeaponCategories = profile.AllowedWeaponCategories.Count == 0
                    || profile.AllowedWeaponCategories.Distinct(StringComparer.Ordinal).Count()
                        != profile.AllowedWeaponCategories.Count
                    || profile.AllowedWeaponCategories.Any(category =>
                        !EquipmentCategoryIds.IsWeapon(category));
                bool invalidArmorCategories = profile.AllowedArmorCategories.Count == 0
                    || profile.AllowedArmorCategories.Distinct(StringComparer.Ordinal).Count()
                        != profile.AllowedArmorCategories.Count
                    || profile.AllowedArmorCategories.Any(category =>
                        !EquipmentCategoryIds.IsArmor(category));
                if (invalidWeaponCategories || invalidArmorCategories)
                {
                    errors.Add(new ContentValidationError(
                        "INVALID_CLASS_EQUIPMENT_CATEGORIES",
                        path,
                        $"Class profile '{profile.Id}' contains invalid equipment categories."));
                }

                if (profile.CombatAutoAttack is { } autoAttack
                    && (autoAttack.Interval <= TimeSpan.Zero
                        || autoAttack.BaseDamage < 0
                        || autoAttack.AttackPowerCoefficient < 0
                        || autoAttack.ResourceOnHit < 0))
                {
                    errors.Add(new ContentValidationError(
                        "INVALID_CLASS_AUTO_ATTACK",
                        path,
                        $"Class profile '{profile.Id}' contains an invalid combat auto attack."));
                }

                if ((profile.StartingAbilityIds?.Count ?? 0) > 0
                    || (profile.AbilityUnlocks?.Count ?? 0) > 0)
                {
                    errors.Add(new ContentValidationError(
                        "CLASS_ABILITY_GRANT_FORBIDDEN",
                        path,
                        $"Class profile '{profile.Id}' cannot grant active abilities. "
                        + "Active skills must be unlocked through talent modifiers."));
                }
            }

            foreach (string requiredClassId in new[] { "WARRIOR", "ARCHER", "MAGE" })
            {
                if (!classIds.Contains(requiredClassId))
                {
                    errors.Add(new ContentValidationError(
                        "MISSING_PROTOTYPE_CLASS_PROFILE",
                        "classProfiles",
                        $"Prototype class profile '{requiredClassId}' is required."));
                }
            }

            if (package.StatFormula.MaxHpBase <= 0
                || package.StatFormula.MaxHpPerStamina < 0
                || package.StatFormula.CriticalChanceBase is < 0 or > 100
                || package.StatFormula.CriticalDamageBase < 0
                || package.StatFormula.AccuracyBase < 0
                || package.StatFormula.AttackSpeedBase <= 0)
            {
                errors.Add(new ContentValidationError(
                    "INVALID_STAT_FORMULA",
                    "statFormula",
                    "Stat formula contains values outside its valid range."));
            }
        }

}
