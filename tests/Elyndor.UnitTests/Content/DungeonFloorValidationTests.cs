using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Content;

public sealed class DungeonFloorValidationTests
{
    [Theory]
    [InlineData("open", null)]
    [InlineData("gated", null)]
    [InlineData("gap", "INVALID_DUNGEON_FLOORS")]
    [InlineData("overlap", "INVALID_DUNGEON_FLOORS")]
    [InlineData("duplicate", "INVALID_DUNGEON_FLOORS")]
    [InlineData("missing", "INVALID_DUNGEON_FLOOR_UNLOCK")]
    [InlineData("self", "INVALID_DUNGEON_FLOOR_UNLOCK")]
    [InlineData("cycle", "INVALID_DUNGEON_FLOOR_UNLOCK")]
    public async Task ValidatesCoverageAndReachableUnlocks(string scenario, string? expectedCode)
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "content", "package.json"))) root = root.Parent;
        Assert.NotNull(root);
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(Path.Combine(root.FullName, "content", "package.json"));
        DungeonDefinition mine = package.Dungeons!.Single(dungeon => dungeon.Id == "ANCIENT_MINE");
        DungeonFloorDefinition[] floors = [new("ENTRY", "Entry", 0, 0), new("HALL", "Hall", 1, 2), new("DEPTHS", "Depths", 3, 4)];
        floors = scenario switch
        {
            "gated" => [floors[0], floors[1] with { RequiredEncounterId = mine.Encounters[0].Id }, floors[2]],
            "gap" => [floors[0], floors[1] with { FirstEncounterIndex = 2 }, floors[2]],
            "overlap" => [floors[0], floors[1] with { FirstEncounterIndex = 0 }, floors[2]],
            "duplicate" => [floors[0], floors[1] with { Id = "ENTRY" }, floors[2]],
            "missing" => [floors[0], floors[1] with { RequiredEncounterId = "MISSING" }, floors[2]],
            "self" => [floors[0], floors[1] with { RequiredEncounterId = mine.Encounters[1].Id }, floors[2]],
            "cycle" => [floors[0], floors[1] with { RequiredEncounterId = mine.Encounters[3].Id },
                floors[2] with { RequiredEncounterId = mine.Encounters[1].Id }],
            _ => floors
        };
        package = package with { Dungeons = package.Dungeons!.Select(dungeon => dungeon.Id == mine.Id ? mine with { Floors = floors } : dungeon).ToArray() };
        IReadOnlyList<ContentValidationError> errors = GameContentPackageValidator.Validate(package);
        if (expectedCode is null) Assert.Empty(errors);
        else Assert.Contains(errors, error => error.Code == expectedCode);
    }
}
