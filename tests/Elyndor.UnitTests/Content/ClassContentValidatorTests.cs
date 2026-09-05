using Elyndor.Core.Combat;
using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Content;

public sealed class ClassContentValidatorTests
{
    private static readonly DateTimeOffset PublishedAtUtc =
        new(2026, 9, 5, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidateRejectsInvalidPrimaryAttributeAndIdentity()
    {
        GameContentPackage package = CreatePackage(CreateProfile(
            "WARRIOR",
            "LUCK",
            "RAGE",
            "ONE_HAND_SWORD",
            "HEAVY") with
        {
            PrototypeIdentity = " "
        });

        IReadOnlyList<ContentValidationError> errors = GameContentPackageValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == "INVALID_CLASS_IDENTITY");
    }

    [Fact]
    public void ValidateRejectsClassBasedAbilityGrants()
    {
        ClassProfile warrior = CreateProfile(
            "WARRIOR",
            "STRENGTH",
            "RAGE",
            "ONE_HAND_SWORD",
            "HEAVY") with
        {
            StartingAbilityIds = ["STRIKE"],
            AbilityUnlocks = [new AbilityUnlockDefinition("HEAVY_BLOW", 2)]
        };

        IReadOnlyList<ContentValidationError> errors =
            GameContentPackageValidator.Validate(CreatePackage(warrior));

        Assert.Contains(errors, error => error.Code == "CLASS_ABILITY_GRANT_FORBIDDEN");
    }

    private static GameContentPackage CreatePackage(ClassProfile warrior) =>
        new(
            "0.2.0",
            "0.2.0",
            PublishedAtUtc,
            [
                new GameContentDefinition("CLASS", "WARRIOR", []),
                new GameContentDefinition("CLASS", "ARCHER", []),
                new GameContentDefinition("CLASS", "MAGE", [])
            ],
            [],
            ClassProfiles:
            [
                warrior,
                CreateProfile("ARCHER", "AGILITY", "FOCUS", "BOW", "LIGHT"),
                CreateProfile("MAGE", "INTELLECT", "MANA", "STAFF", "LIGHT")
            ],
            StatFormula: new StatFormulaProfile(
                "DEFAULT",
                100,
                10,
                1,
                1,
                1,
                1,
                1,
                1,
                1,
                5,
                0,
                1,
                95,
                0,
                1),
            ResourceProfiles:
            [
                new ResourceProfile("RAGE", 100, 0, 0, 0, 0, 5, 5),
                new ResourceProfile("FOCUS", 100, 100, 100, 8, 12, 0, 0),
                new ResourceProfile("MANA", 100, 100, 100, 4, 12, 0, 0)
            ]);

    private static ClassProfile CreateProfile(
        string id,
        string primaryAttribute,
        string resourceProfileId,
        string weaponCategory,
        string armorCategory) =>
        new(
            id,
            primaryAttribute,
            resourceProfileId,
            new PrimaryStats(5, 5, 5, 5),
            new PrimaryStats(1, 1, 1, 1),
            [weaponCategory],
            [armorCategory],
            $"{id} prototype");
}
