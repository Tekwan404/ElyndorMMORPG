using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Items;

public sealed class ItemReforgeSelectionTests
{
    [Fact]
    public void SelectionCanMoveBetweenMutableAffixSlotsAndBeCleared()
    {
        CharacterItem item = CreateGeneratedItem();

        item.SelectReforgeSlot("AFFIX_1");
        item.SelectReforgeSlot("AFFIX_2");

        Assert.Equal("AFFIX_2", item.ReforgeSlotKey);
        Assert.False(item.Affixes.Single(affix => affix.SlotKey == "AFFIX_1").IsReforgeSlot);
        Assert.True(item.Affixes.Single(affix => affix.SlotKey == "AFFIX_2").IsReforgeSlot);

        ItemRolledAffix selected = item.Affixes.Single(affix => affix.SlotKey == "AFFIX_2");
        GeneratedItemAffix replacement = selected.ToGeneratedAffix() with
        {
            AffixDefinitionId = "AGILITY",
            StatId = ItemStatIds.Agility,
            Value = 6m
        };

        item.ApplyReforge(replacement);
        item.ClearReforgeSlot();

        Assert.Null(item.ReforgeSlotKey);
        Assert.All(item.Affixes, affix => Assert.False(affix.IsReforgeSlot));
        Assert.Equal(ItemStatIds.Agility, item.Affixes.Single(affix => affix.SlotKey == "AFFIX_2").StatId);
    }

    private static CharacterItem CreateGeneratedItem()
    {
        CharacterItem item = new(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "TEST_ITEM",
            1,
            new DateTimeOffset(2026, 10, 4, 0, 0, 0, TimeSpan.Zero),
            1);

        GeneratedItemInstance generated = new(
            60,
            [
                new GeneratedItemAffix(
                    "AFFIX_1",
                    "STRENGTH",
                    ItemStatIds.Strength,
                    12m,
                    5m,
                    20m,
                    1m,
                    1,
                    false,
                    false,
                    0),
                new GeneratedItemAffix(
                    "AFFIX_2",
                    "CRITICAL_CHANCE",
                    ItemStatIds.CriticalChance,
                    4m,
                    2m,
                    8m,
                    0.1m,
                    1,
                    false,
                    false,
                    1)
            ],
            100m,
            150m,
            220m,
            55.15m,
            3,
            false,
            null,
            null,
            null,
            "Test Item",
            1);

        item.ApplyGeneratedInstance(
            generated,
            "TEST_HASH",
            "DROP",
            Guid.CreateVersion7(),
            "TEST_SOURCE");

        return item;
    }
}
