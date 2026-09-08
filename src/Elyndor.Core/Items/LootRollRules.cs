using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;

namespace Elyndor.Core.Items;

public enum LootChoice
{
    Need,
    Greed,
    Pass
}

public sealed record LootRollEntry(Guid CharacterId, LootChoice Choice, int Roll);

public sealed record LootRollResolution(
    Guid? WinnerCharacterId,
    IReadOnlyList<LootRollEntry> Entries);

public static class LootRollRules
{
    public static bool IsValuableWearable(ItemDefinition item) =>
        item.Type == ItemType.Equipment
        && item.Slot is not null
        && item.Rarity >= ItemRarity.Rare;

    public static bool CanNeed(
        ItemDefinition item,
        string classId,
        int level,
        ClassProfile classProfile,
        bool hasDualWieldPermission)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(classId);
        ArgumentNullException.ThrowIfNull(classProfile);
        if (item.Type != ItemType.Equipment
            || item.Slot is null
            || level < item.RequiredLevel)
            return false;
        if (item.AllowedClassIds is { Count: > 0 }
            && !item.AllowedClassIds.Contains(classId, StringComparer.Ordinal))
            return false;
        if (item.WeaponCategory is not null
            && !classProfile.AllowedWeaponCategories.Contains(
                item.WeaponCategory,
                StringComparer.Ordinal))
            return false;
        if (item.ArmorCategory is not null
            && !classProfile.AllowedArmorCategories.Contains(
                item.ArmorCategory,
                StringComparer.Ordinal))
            return false;
        if (item.OffHandCategory is not null
            && !(classProfile.AllowedOffHandCategories ?? [])
                .Contains(item.OffHandCategory, StringComparer.Ordinal))
            return false;
        if (item.Slot == EquipmentSlot.OffHand
            && item.WeaponCategory is not null
            && (!EquipmentCategoryIds.IsOneHandedWeapon(item.WeaponCategory)
                || !hasDualWieldPermission))
            return false;
        return true;
    }

    public static LootRollResolution Resolve(
        IReadOnlyDictionary<Guid, LootChoice> choices,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(random);

        List<LootRollEntry> entries = [];
        foreach ((Guid characterId, LootChoice choice) in choices.OrderBy(item => item.Key))
        {
            int roll = choice == LootChoice.Pass
                ? 0
                : 1 + (int)decimal.Floor(random.NextUnit() * 100m);
            entries.Add(new LootRollEntry(characterId, choice, roll));
        }

        LootRollEntry[] eligible = entries
            .Where(entry => entry.Choice is LootChoice.Need or LootChoice.Greed)
            .ToArray();
        LootChoice[] priority = [LootChoice.Need, LootChoice.Greed];
        foreach (LootChoice choice in priority)
        {
            LootRollEntry[] candidates = eligible
                .Where(entry => entry.Choice == choice)
                .ToArray();
            if (candidates.Length == 0) continue;
            int winningRoll = candidates.Max(entry => entry.Roll);
            return new LootRollResolution(
                candidates.First(entry => entry.Roll == winningRoll).CharacterId,
                entries);
        }

        return new LootRollResolution(null, entries);
    }
}
