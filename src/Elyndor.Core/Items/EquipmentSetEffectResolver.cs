using Elyndor.Core.Combat.SetPassives;

namespace Elyndor.Core.Items;

public static class EquipmentSetEffectResolver
{
    public static IReadOnlyList<SetPassiveDefinition> Resolve(IEnumerable<EquipmentSetDefinition> sets) =>
        sets.SelectMany(set => (set.SpecialEffects ?? []).Where(effect => set.Bonuses.Any(bonus =>
            bonus.RequiredPieces == effect.RequiredPieces && (bonus.SpecialEffectIds ?? []).Contains(effect.Id, StringComparer.Ordinal))))
            .ToArray();
}
