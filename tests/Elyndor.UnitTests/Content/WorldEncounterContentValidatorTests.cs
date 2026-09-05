using Elyndor.Core.Combat;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;
using Elyndor.Core.World;

namespace Elyndor.UnitTests.Content;

public sealed class WorldEncounterContentValidatorTests
{
    [Fact]
    public void ValidateAcceptsNormalEncounterWithPresentation()
    {
        GameContentPackage package = Package(
            new LocationDefinition(
                "WHISPERING_FOREST",
                "Whispering Forest",
                "ADVENTURE",
                1,
                [],
                [new LocationEncounterDefinition("WOLF", 1)]),
            Monster("WOLF"));

        Assert.Empty(WorldEncounterContentValidator.Validate(package));
    }

    [Fact]
    public void ValidateRejectsNonSafeLocationWithoutEncounters()
    {
        GameContentPackage package = Package(
            new LocationDefinition(
                "WHISPERING_FOREST",
                "Whispering Forest",
                "ADVENTURE",
                1,
                []));

        IReadOnlyList<ContentValidationError> errors = WorldEncounterContentValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == "HOSTILE_LOCATION_HAS_NO_ENCOUNTERS");
    }

    [Fact]
    public void ValidateAllowsSafeLocationWithoutEncounters()
    {
        GameContentPackage package = Package(
            new LocationDefinition(
                "STARTER_TOWN",
                "Starter Town",
                "SAFE",
                1,
                []));

        Assert.Empty(WorldEncounterContentValidator.Validate(package));
    }

    [Fact]
    public void ValidateRejectsSafeMissingAndDuplicateEncounters()
    {
        GameContentPackage package = Package(
            new LocationDefinition(
                "STARTER_TOWN",
                "Starter Town",
                "SAFE",
                1,
                [],
                [
                    new LocationEncounterDefinition("MISSING", 1),
                    new LocationEncounterDefinition("MISSING", 1)
                ]));

        IReadOnlyList<ContentValidationError> errors = WorldEncounterContentValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == "SAFE_LOCATION_HAS_HOSTILE_ENCOUNTERS");
        Assert.Contains(errors, error => error.Code == "MISSING_LOCATION_ENCOUNTER_MONSTER");
        Assert.Contains(errors, error => error.Code == "INVALID_LOCATION_ENCOUNTER");
    }

    private static GameContentPackage Package(
        LocationDefinition location,
        params MonsterDefinition[] monsters) => new(
            "0.9.0",
            "0.7.0",
            new DateTimeOffset(2026, 9, 3, 17, 30, 0, TimeSpan.Zero),
            [],
            [location],
            Monsters: monsters);

    private static MonsterDefinition Monster(string id) => new(
        id,
        "Forest Wolf",
        MonsterRank.Normal,
        3,
        180,
        CombatStats.Default,
        TimeSpan.FromSeconds(2),
        5,
        [],
        "AI",
        DisplayName: "Волк",
        Description: "Дикий волк.",
        ArtId: "wolf");
}
