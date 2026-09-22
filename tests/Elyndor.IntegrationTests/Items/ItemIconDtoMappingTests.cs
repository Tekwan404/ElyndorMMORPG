using Elyndor.Contracts.Combat;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Progression;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Progression;
using Elyndor.Server.Combat;
using Elyndor.Server.Items;

namespace Elyndor.IntegrationTests.Items;

public sealed class ItemIconDtoMappingTests
{
    [Fact]
    public void InventoryMerchantAndPendingLootResponsesPreserveDefinitionIconId()
    {
        ItemDefinition definition = Definition();
        InventoryItemSnapshot inventory = new(
            Guid.NewGuid(), definition, 1, DateTimeOffset.UnixEpoch, null, false);
        PendingLootItemSnapshot pending = new(
            Guid.NewGuid(), definition, 2, DateTimeOffset.UnixEpoch, null);
        MerchantSnapshot merchant = new(
            new MerchantDefinition("MERCHANT", "Merchant", "STARTER_TOWN", "", [definition.Id]),
            0,
            [new MerchantCatalogItem(definition, 3)]);

        Assert.Equal(definition.IconId, InventoryEndpoints.ToResponse(inventory).IconId);
        Assert.Equal(definition.IconId, InventoryEndpoints.ToMerchantResponse(merchant).Items.Single().IconId);
        Assert.Equal(definition.IconId, InventoryEndpoints.ToPendingLootResponse(pending).IconId);
    }

    [Fact]
    public void CombatRewardAndLootRollResponsesPreserveDefinitionIconId()
    {
        ItemDefinition definition = Definition();
        CombatRewardApplicationResult reward = new(
            true,
            10,
            5,
            new CharacterProgressionResult(1, 1, 0, 10, 10, 100),
            [new CombatRewardItemResult(
                definition.Id,
                definition.Name,
                definition.Type,
                definition.Rarity,
                1,
                definition.IconId)],
            LootRolls:
            [new CombatLootRollResult(
                Guid.NewGuid(),
                definition.Id,
                definition.Name,
                definition.Rarity,
                1,
                DateTimeOffset.UnixEpoch,
                [],
                true,
                IconId: definition.IconId,
                Type: definition.Type)]);
        CombatUpdateResponse response = CombatContractMapper.ToResponse(
            new CombatOperationResult(true, null, null, [], reward),
            new GameContentPackage("1", "1", DateTimeOffset.UnixEpoch, [], [], Items: [definition]));

        Assert.Equal(definition.IconId, response.Reward!.Items.Single().IconId);
        CombatLootRollResponse lootRoll = Assert.Single(response.Reward.LootRolls!);
        Assert.Equal(definition.IconId, lootRoll.IconId);
        Assert.Equal(definition.Type.ToString(), lootRoll.Type);
    }

    private static ItemDefinition Definition() => new(
        "ITEM_TEST",
        "Test item",
        ItemType.Equipment,
        ItemRarity.Rare,
        1,
        true,
        99,
        EquipmentSlot.MainHand,
        new PrimaryStats(0, 0, 0, 0),
        "Test",
        IconId: "sets/test_item");
}
