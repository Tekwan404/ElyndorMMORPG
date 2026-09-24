using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.World;

namespace Elyndor.UnitTests.Content;

public sealed class PlayerFacingTextValidatorTests
{
    [Fact]
    public void RejectsDevelopmentNotesInItemAndLocationDescriptions()
    {
        GameContentPackage package = CreatePackage(
            itemDescription: "Материал Elyndor. Источник указан в таблицах добычи.",
            locationDescription: "Визуальная тема: древний лес.");

        IReadOnlyList<ContentValidationError> errors = Validate(package);

        Assert.Equal(3, errors.Count);
        Assert.All(errors, error => Assert.Equal("PLAYER_FACING_TECHNICAL_TEXT", error.Code));
    }

    [Fact]
    public void RejectsOrdinaryEquipmentRestrictedByAClassInLore()
    {
        GameContentPackage package = CreatePackage(
            itemDescription: "Экипировка для WARRIOR ранней прогрессии.",
            itemType: ItemType.Equipment,
            locationDescription: "Глухая чаща, где слышен шёпот древних деревьев.");

        IReadOnlyList<ContentValidationError> errors = Validate(package);

        Assert.Contains(errors, error => error.Code == "PLAYER_FACING_CLASS_BOUND_EQUIPMENT_TEXT");
    }

    [Fact]
    public void AcceptsPlayerFacingItemAndLocationDescriptions()
    {
        GameContentPackage package = CreatePackage(
            itemDescription: "Этот материал получают в приключениях и используют в ремесле.",
            locationDescription: "Глухая чаща, где слышен шёпот древних деревьев.");

        Assert.Empty(Validate(package));
    }

    private static IReadOnlyList<ContentValidationError> Validate(GameContentPackage package) =>
        new ContentValidationPipeline([new PlayerFacingTextValidationStage()]).Validate(package);

    private static GameContentPackage CreatePackage(
        string itemDescription,
        string locationDescription,
        ItemType itemType = ItemType.Material) =>
        new(
            "0.1.0",
            "0.1.0",
            new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero),
            [],
            [new LocationDefinition(
                "TEST_LOCATION",
                "Тестовая чаща",
                "SAFE",
                1,
                [],
                Description: locationDescription)],
            Items:
            [
                new ItemDefinition(
                    "TEST_ITEM",
                    "Тестовый материал",
                    itemType,
                    ItemRarity.Common,
                    1,
                    itemType == ItemType.Material,
                    itemType == ItemType.Material ? 99 : 1,
                    itemType == ItemType.Material ? null : EquipmentSlot.Head,
                    new PrimaryStats(0, 0, 0, 0),
                    itemDescription)
            ]);
}
