using Elyndor.Core.Combat.SetPassives;
using Elyndor.Core.Items;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
    private static void ValidateSetEffects(EquipmentSetDefinition set, string path,
        HashSet<string> effectIds, HashSet<string> abilityIds, List<ContentValidationError> errors)
    {
        var effects = set.SpecialEffects ?? [];
        foreach (var effect in effects)
        {
            bool invalid = !IsCanonicalIdentifier(effect.Id) || !effectIds.Add(effect.Id)
                || effect.SetId != set.Id
                || !set.Bonuses.Any(b => b.RequiredPieces == effect.RequiredPieces
                    && (b.SpecialEffectIds ?? []).Contains(effect.Id, StringComparer.Ordinal));
            try { SetPassiveEvaluator.Validate(effect); }
            catch (ArgumentException) { invalid = true; }
            if ((effect.Conditions.AbilityIds ?? []).Any(id => id != "AUTO_ATTACK" && !abilityIds.Contains(id))
                || effect.Actions.Any(action => action.Kind == SetPassiveActionKind.ReduceCooldown
                    && !abilityIds.Contains(action.ReferenceId ?? string.Empty)
                    || action.Kind == SetPassiveActionKind.ApplyEffect
                        && (string.IsNullOrWhiteSpace(action.ReferenceId) || action.ModifiedStat is null
                            || !Enum.IsDefined(action.ModifiedStat.Value) || action.Duration is not { Ticks: > 0 })))
                invalid = true;
            if (invalid)
                errors.Add(new("INVALID_SET_SPECIAL_EFFECT", path, $"Set '{set.Id}' has an invalid effect '{effect.Id}'."));
        }
        if (set.Bonuses.Select(b => b.RequiredPieces).Distinct().Count() != set.Bonuses.Count)
            errors.Add(new("DUPLICATE_SET_BONUS_THRESHOLD", path, "Set bonus thresholds must be unique."));
        foreach (var bonus in set.Bonuses)
        {
            if (bonus.RequiredPieces <= 0 || (bonus.SpecialEffectIds ?? []).Any(id =>
                !effects.Any(e => e.Id == id && e.RequiredPieces == bonus.RequiredPieces)))
                errors.Add(new("INVALID_SET_BONUS_EFFECT_REFERENCE", path, "Set bonus references an unknown effect or threshold."));
        }
    }
}
