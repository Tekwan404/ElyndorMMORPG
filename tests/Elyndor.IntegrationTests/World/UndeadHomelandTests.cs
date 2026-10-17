using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Quests;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Dungeons;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.World;
using Elyndor.Infrastructure.Quests;
using Elyndor.Infrastructure.Combat;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.World;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class UndeadHomelandTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = new FixedClock();
    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("HUMAN", "STARTER_TOWN")]
    [InlineData("UNDEAD", "UNDEAD_STARTER_TOWN")]
    public async Task CreationStartsInOwnCity(string raceId, string town)
    {
        var content = await LoadContentAsync();
        await using var db = postgres.CreateDbContext();
        Guid accountId = Guid.CreateVersion7();
        db.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
        await db.SaveChangesAsync();
        var inventory = new InventoryEquipmentService(db, content, Clock);
        var derived = new CharacterDerivedStateService(db, content, inventory);
        var creation = new CharacterCreationService(db, Clock, content, derived);
        var result = await creation.CreateAsync(accountId,
            new(Guid.CreateVersion7(), "Traveler", raceId, "MALE", "WARRIOR"), CancellationToken.None);
        Assert.True(result.IsSuccess, result.ErrorCode);
        Assert.Equal(town, (await db.CharacterLocations.SingleAsync()).LocationId);
    }

    [Theory]
    [InlineData("HUMAN", "UNDEAD_WHISPERING_FOREST")]
    [InlineData("UNDEAD", "WHISPERING_FOREST")]
    public async Task DirectTravelCannotBypassFactionRestriction(string raceId, string target)
    {
        var content = await LoadContentAsync();
        Guid accountId = await SeedAsync(raceId, WorldLocationAccess.HomeTown(raceId));
        await using var db = postgres.CreateDbContext();
        var service = new TravelService(db, new WorldMap(content.Locations, directTravel: true), Clock);
        var result = await service.TravelAsync(accountId, Guid.CreateVersion7(), target, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(TravelErrorCodes.FactionRestricted, result.ErrorCode);
        Assert.Equal(WorldLocationAccess.HomeTown(raceId), (await db.CharacterLocations.SingleAsync()).LocationId);
        Assert.Empty(await db.CharacterTravelStates.ToArrayAsync());
        Assert.Empty(await db.TravelOperations.ToArrayAsync());
    }

    [Theory]
    [InlineData("HUMAN", "WHISPERING_FOREST")]
    [InlineData("UNDEAD", "UNDEAD_WHISPERING_FOREST")]
    [InlineData("HUMAN", "BLACK_CRAGS")]
    [InlineData("UNDEAD", "BLACK_CRAGS")]
    [InlineData("HUMAN", "DEAD_REACHES")]
    [InlineData("UNDEAD", "DEAD_REACHES")]
    public async Task OwnRegionAndSharedContinentRemainAccessible(string raceId, string target)
    {
        var content = await LoadContentAsync();
        Guid accountId = await SeedAsync(raceId, WorldLocationAccess.HomeTown(raceId));
        await using var db = postgres.CreateDbContext();
        var service = new TravelService(db, new WorldMap(content.Locations, directTravel: true), Clock);
        var result = await service.TravelAsync(accountId, Guid.CreateVersion7(), target, CancellationToken.None);
        Assert.True(result.IsSuccess, result.ErrorCode);
        Assert.Equal(target, result.TargetLocationId ?? result.LocationId);
    }

    [Theory]
    [InlineData("UNDEAD", "WHISPERING_FOREST", "UNDEAD_WHISPERING_FOREST")]
    [InlineData("HUMAN", "UNDEAD_WHISPERING_FOREST", "WHISPERING_FOREST")]
    [InlineData("UNDEAD", "BLACK_CRAGS", "BLACK_CRAGS")]
    public async Task BootstrapRepairsLegacyFactionPositionAndFiltersTransitions(string raceId, string oldLocation, string expected)
    {
        var content = await LoadContentAsync();
        Guid accountId = await SeedAsync(raceId, oldLocation);
        await using var db = postgres.CreateDbContext();
        var inventory = new InventoryEquipmentService(db, content, Clock);
        var derived = new CharacterDerivedStateService(db, content, inventory);
        var service = new BootstrapService(db, content, new WorldMap(content.Locations, directTravel: true), derived, Clock);
        var result = await service.GetAsync(accountId, CancellationToken.None);
        Assert.Equal(expected, result.World!.CurrentLocation.Id);
        var byId = content.Locations.ToDictionary(location => location.Id);
        Assert.All(result.World.OutgoingTransitions,
            target => Assert.True(WorldLocationAccess.CanEnter(byId[target.Id], raceId)));
        Assert.All(result.World.Contracts, contract => Assert.True(WorldLocationAccess.CanEnter(
            byId[contract.OfferLocationId ?? contract.UnlockLocationId], raceId)));
    }

    [Theory]
    [InlineData("UNDEAD", "ANCIENT_MINE")]
    [InlineData("HUMAN", "UNDEAD_ANCIENT_MINE")]
    public async Task DungeonTeleportCannotEnterOppositeFactionRegion(string raceId, string dungeonId)
    {
        var content = await LoadContentAsync();
        Guid accountId = await SeedAsync(raceId, WorldLocationAccess.HomeTown(raceId));
        await using var db = postgres.CreateDbContext();
        var service = new DungeonService(db, new PartyService(db, Clock), new StaticContentSnapshotProvider(content), Clock);
        var result = await service.TeleportToEntryAsync(accountId, dungeonId, Guid.CreateVersion7(), CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.Equal("dungeon_member_cannot_enter", result.ErrorCode);
        Assert.Equal(WorldLocationAccess.HomeTown(raceId), (await db.CharacterLocations.SingleAsync()).LocationId);
    }

    [Fact]
    public async Task ExistingUndeadQuestHistoryKeepsCanonicalIdentityAndCannotBeRewardedAgain()
    {
        var content = await LoadContentAsync();
        Guid accountId = await SeedAsync("UNDEAD", "UNDEAD_STARTER_TOWN");
        await using var db = postgres.CreateDbContext();
        var character = await db.Characters.SingleAsync();
        var completed = new CharacterQuestState(character.Id, "QUEST_01_FIRST_HUNT", Now);
        completed.MarkCompleted(Now);
        db.CharacterQuestStates.Add(completed);
        await db.SaveChangesAsync();
        var inventory = new InventoryEquipmentService(db, content, Clock);
        var derived = new CharacterDerivedStateService(db, content, inventory);
        var service = new QuestService(db, new StaticContentSnapshotProvider(content), derived, new SystemGameRandomFactory(), Clock);
        var journal = await service.GetAsync(accountId, CancellationToken.None);
        var firstQuest = Assert.Single(journal.Quests, quest => quest.Id == "QUEST_01_FIRST_HUNT");
        Assert.Equal("COMPLETED", firstQuest.Status);
        Assert.Equal("UNDEAD_STARTER_TOWN", firstQuest.OfferLocationId);
        Assert.DoesNotContain(journal.Quests, quest => quest.Id.StartsWith("UNDEAD_", StringComparison.Ordinal));
        var retry = await service.AcceptAsync(accountId, "QUEST_01_FIRST_HUNT", CancellationToken.None);
        Assert.False(retry.IsSuccess);
    }

    [Fact]
    public async Task LegacySoloDungeonKeepsRunAndCheckpointWhenMovingToUndeadEntrance()
    {
        var content = await LoadContentAsync();
        Guid accountId = await SeedAsync("UNDEAD", "ANCIENT_MINE");
        await using var db = postgres.CreateDbContext();
        var character = await db.Characters.SingleAsync();
        var run = DungeonRun.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), character.Id, "ANCIENT_MINE", Now);
        run.AddMember(character.Id, Now);
        run.AdvanceEncounter(Now);
        db.DungeonRuns.Add(run);
        await db.SaveChangesAsync();
        var inventory = new InventoryEquipmentService(db, content, Clock);
        var derived = new CharacterDerivedStateService(db, content, inventory);
        var bootstrap = new BootstrapService(db, content, new WorldMap(content.Locations), derived, Clock);
        var result = await bootstrap.GetAsync(accountId, CancellationToken.None);
        Assert.Equal("UNDEAD_ANCIENT_MINE", result.World!.CurrentLocation.Id);
        var restored = await db.DungeonRuns.SingleAsync();
        Assert.Equal(run.Id, restored.Id);
        Assert.Equal("UNDEAD_ANCIENT_MINE", restored.DungeonId);
        Assert.Equal(1, restored.CurrentEncounterIndex);
        Assert.Equal(DungeonRunState.Active, restored.State);
    }

    [Fact]
    public async Task BootstrapLeavesNonEquivalentDungeonAndStillLogsCharacterIn()
    {
        var content = await LoadContentAsync();
        Guid accountId = await SeedAsync("UNDEAD", "ANCIENT_MINE");
        await using var db = postgres.CreateDbContext();
        var character = await db.Characters.SingleAsync();
        var run = DungeonRun.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), character.Id, "ANCIENT_MINE", Now);
        run.AddMember(character.Id, Now);
        db.DungeonRuns.Add(run);
        await db.SaveChangesAsync();

        // Simulate a content release where the mirrored encounter sequence drifted.
        var drifted = content with
        {
            Dungeons = content.Dungeons!.Select(dungeon =>
                dungeon.Id == "UNDEAD_ANCIENT_MINE"
                    ? dungeon with { Encounters = dungeon.Encounters.Skip(1).ToArray() }
                    : dungeon).ToArray()
        };
        var inventory = new InventoryEquipmentService(db, content, Clock);
        var derived = new CharacterDerivedStateService(db, content, inventory);
        var bootstrap = new BootstrapService(db, content, new WorldMap(content.Locations), derived, Clock);
        var result = await bootstrap.GetAsync(accountId, GameContentSnapshot.Create(drifted), CancellationToken.None);

        Assert.NotNull(result.Character);
        Assert.Equal("UNDEAD_ANCIENT_MINE", result.World!.CurrentLocation.Id);
        var member = Assert.Single((await db.DungeonRuns.Include(candidate => candidate.Members).SingleAsync()).Members);
        Assert.Equal(DungeonRunMemberState.Left, member.State);
        Assert.Equal("ANCIENT_MINE", (await db.DungeonRuns.SingleAsync()).DungeonId);
    }

    [Fact]
    public async Task PartyDungeonCreationRejectsForeignRegionBeforeMovingAnyMember()
    {
        var content = await LoadContentAsync();
        Guid accountId = await SeedAsync("HUMAN", "STARTER_TOWN");
        await using var db = postgres.CreateDbContext();
        var party = new PartyService(db, Clock);
        Assert.True((await party.CreateAsync(accountId, Guid.CreateVersion7(), CancellationToken.None)).IsSuccess);
        var coordinator = new PartyDungeonRunCoordinator(db, party, new StaticContentSnapshotProvider(content), new NoCombat(), Clock);
        var result = await coordinator.StartAsync(accountId, "UNDEAD_ANCIENT_MINE", Guid.CreateVersion7(), CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.Equal(DungeonErrorCodes.MemberCannotEnter, result.ErrorCode);
        Assert.Equal("STARTER_TOWN", (await db.CharacterLocations.SingleAsync()).LocationId);
        Assert.Empty(await db.DungeonRuns.ToArrayAsync());
    }

    private sealed class NoCombat : ICombatActivityReader { public bool HasActiveCombat(Guid accountId) => false; }

    [Theory]
    [InlineData("HUMAN", 41, "BLACK_CRAGS", true)]
    [InlineData("UNDEAD", 41, "BLACK_CRAGS", true)]
    [InlineData("HUMAN", 59, "DEAD_REACHES", false)]
    [InlineData("UNDEAD", 59, "DEAD_REACHES", false)]
    public async Task SharedContinentUsesLevelGatesForBothRaces(string raceId, int level, string target, bool allowed)
    {
        var content = await LoadContentAsync();
        Guid accountId = await SeedAsync(raceId, WorldLocationAccess.HomeTown(raceId), level);
        await using var db = postgres.CreateDbContext();
        var service = new TravelService(db, new WorldMap(content.Locations, directTravel: true), Clock);
        var result = await service.TravelAsync(accountId, Guid.CreateVersion7(), target, default);
        Assert.Equal(allowed, result.IsSuccess);
        if (!allowed)
        {
            Assert.Equal(TravelErrorCodes.LevelRequired, result.ErrorCode);
            Assert.Equal(WorldLocationAccess.HomeTown(raceId), (await db.CharacterLocations.SingleAsync()).LocationId);
        }
    }

    private async Task<Guid> SeedAsync(string raceId, string locationId, int level = 60)
    {
        await using var db = postgres.CreateDbContext();
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        db.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
        var character = new Character(characterId, accountId, Guid.CreateVersion7(), "Traveler", "TRAVELER", raceId, "MALE", "WARRIOR", Now);
        character.SetLevel(level);
        db.Characters.Add(character);
        db.CharacterLocations.Add(new CharacterLocation(characterId, locationId, 1, Now));
        await db.SaveChangesAsync();
        return accountId;
    }

    private static Task<GameContentPackage> LoadContentAsync() => GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
