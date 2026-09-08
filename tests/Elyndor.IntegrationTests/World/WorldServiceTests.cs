using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.World;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Characters;
using Elyndor.Core.Progression;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.World;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class BootstrapServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private long _nextTelegramUserId = 1_000;

    private static readonly DateTimeOffset Now =
        new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task BootstrapRestoresAuthoritativeCharacterLocationAndContentVersions()
    {
        Guid accountId = await CreatePlayerAsync(withCharacter: true);
        await using GameDbContext context = postgres.CreateDbContext();
        TimeProvider timeProvider = new FixedTimeProvider(Now);
        InventoryEquipmentService inventory = new(context, Content, timeProvider);
        CharacterDerivedStateService derived = new(context, Content, inventory);
        BootstrapService service = new(
            context,
            Content,
            Map,
            derived,
            timeProvider);

        BootstrapSnapshot snapshot = await service.GetAsync(accountId, CancellationToken.None);

        Assert.Equal(accountId, snapshot.AccountId);
        Assert.Equal("0.1.0", snapshot.ContentVersion);
        Assert.Equal("0.1.0", snapshot.BalanceVersion);
        Assert.Equal(Now, snapshot.ServerTimeUtc);
        Assert.NotNull(snapshot.Character);
        Assert.Equal(snapshot.Character.Stats.MaxHp, snapshot.Character.Vitals.CurrentHp);
        Assert.Equal("STARTER_TOWN", snapshot.World!.CurrentLocation.Id);
        Assert.Equal(
            ["WHISPERING_FOREST"],
            snapshot.World.OutgoingTransitions.Select(location => location.Id));
    }

    [Fact]
    public async Task BootstrapRepairsMissingVitalsUnknownLocationAndInvalidTravel()
    {
        Guid accountId = await CreatePlayerAsync(withCharacter: true);
        Guid characterId;
        await using (GameDbContext corrupt = postgres.CreateDbContext())
        {
            Character character = await corrupt.Characters.SingleAsync();
            characterId = character.Id;
            CharacterVitals vitals = await corrupt.CharacterVitals.SingleAsync();
            corrupt.CharacterVitals.Remove(vitals);

            CharacterLocation location = await corrupt.CharacterLocations.SingleAsync();
            location.Relocate("REMOVED_LOCATION", Now);
            corrupt.CharacterTravelStates.Add(new CharacterTravelState(
                characterId,
                Guid.CreateVersion7(),
                "REMOVED_LOCATION",
                "WHISPERING_FOREST",
                Now,
                Now.AddSeconds(5)));
            await corrupt.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        TimeProvider timeProvider = new FixedTimeProvider(Now);
        InventoryEquipmentService inventory = new(context, Content, timeProvider);
        CharacterDerivedStateService derived = new(context, Content, inventory);
        BootstrapService service = new(
            context,
            Content,
            Map,
            derived,
            timeProvider);

        BootstrapSnapshot snapshot =
            await service.GetAsync(accountId, CancellationToken.None);

        Assert.Equal("STARTER_TOWN", snapshot.World!.CurrentLocation.Id);
        Assert.Null(snapshot.World.Travel);
        Assert.Equal(
            snapshot.Character!.Vitals.MaxHp,
            snapshot.Character.Vitals.CurrentHp);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(
            "STARTER_TOWN",
            await verify.CharacterLocations
                .Where(state => state.CharacterId == characterId)
                .Select(state => state.LocationId)
                .SingleAsync());
        Assert.Single(await verify.CharacterVitals
            .Where(state => state.CharacterId == characterId)
            .ToArrayAsync());
        Assert.Empty(await verify.CharacterTravelStates
            .Where(state => state.CharacterId == characterId)
            .ToArrayAsync());
    }

    [Fact]
    public async Task BootstrapExposesArcaneArcherEffectiveManaResource()
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        TalentTreeDefinition archerTree =
            content.TalentTrees!.Single(tree => tree.Id == "ARCHER_TREE");

        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.Accounts.Add(new Account(
                accountId,
                Interlocked.Increment(ref _nextTelegramUserId),
                Now));
            Character character = new(
                characterId,
                accountId,
                Guid.CreateVersion7(),
                "Archer",
                $"ARCHER{characterId:N}"[..16],
                "HUMAN",
                "MALE",
                "ARCHER",
                Now);
            character.SetLevel(60);
            setup.Characters.Add(character);
            setup.CharacterLocations.Add(
                new CharacterLocation(characterId, "STARTER_TOWN", 1, Now));
            setup.CharacterVitals.Add(
                new CharacterVitals(characterId, 150, 100, Now, Now));

            CharacterTalentState talents = new(
                characterId,
                archerTree.Id,
                archerTree.Version,
                Now);
            talents.ReplaceRanks(
                TalentLoadoutIds.Loadout1,
                new Dictionary<string, int> { ["A-1-1"] = 1 },
                Now);
            setup.CharacterTalentStates.Add(talents);
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        TimeProvider timeProvider = new FixedTimeProvider(Now);
        InventoryEquipmentService inventory = new(context, content, timeProvider);
        CharacterDerivedStateService derived = new(context, content, inventory);
        BootstrapService service = new(
            context,
            content,
            new WorldMap(content.Locations),
            derived,
            timeProvider);

        BootstrapSnapshot snapshot =
            await service.GetAsync(accountId, CancellationToken.None);

        Assert.Equal("MANA", snapshot.Character!.Vitals.ResourceType);
        Assert.Contains("ARCANE_ARROW", snapshot.Character.KnownAbilityIds);
        Assert.Equal("INTELLECT", snapshot.Character.PrimaryAttribute);
    }

    [Fact]
    public async Task BootstrapReturnsExplicitNoCharacterState()
    {
        Guid accountId = await CreatePlayerAsync(withCharacter: false);
        await using GameDbContext context = postgres.CreateDbContext();
        TimeProvider timeProvider = new FixedTimeProvider(Now);
        InventoryEquipmentService inventory = new(context, Content, timeProvider);
        CharacterDerivedStateService derived = new(context, Content, inventory);
        BootstrapService service = new(
            context,
            Content,
            Map,
            derived,
            timeProvider);

        BootstrapSnapshot snapshot = await service.GetAsync(accountId, CancellationToken.None);

        Assert.Null(snapshot.Character);
        Assert.Null(snapshot.World);
    }

    [Fact]
    public async Task TravelUsesActualWorldLinksAndReplaysExactRequest()
    {
        Guid accountId = await CreatePlayerAsync(withCharacter: true);
        Guid requestId = Guid.CreateVersion7();

        TravelResult first = await TravelAsync(accountId, requestId, "WHISPERING_FOREST");
        TravelResult replay = await TravelAsync(accountId, requestId, "WHISPERING_FOREST");
        TravelResult mismatch = await TravelAsync(accountId, requestId, "DEEP_FOREST");

        Assert.True(first.IsSuccess);
        Assert.False(first.IsTravelling);
        Assert.Equal("WHISPERING_FOREST", first.LocationId);
        Assert.Equal(2, first.Version);
        Assert.Null(first.TargetLocationId);
        Assert.Equal(first, replay);
        Assert.Equal(TravelErrorCodes.IdempotencyConflict, mismatch.ErrorCode);
    }

    [Fact]
    public async Task DirectTownToDeepAndUnknownTargetsAreRejected()
    {
        Guid accountId = await CreatePlayerAsync(withCharacter: true);

        TravelResult direct = await TravelAsync(
            accountId,
            Guid.CreateVersion7(),
            "DEEP_FOREST");
        TravelResult unknown = await TravelAsync(
            accountId,
            Guid.CreateVersion7(),
            "MISSING");

        Assert.Equal(TravelErrorCodes.InvalidTransition, direct.ErrorCode);
        Assert.Equal(TravelErrorCodes.UnknownLocation, unknown.ErrorCode);
    }

    [Fact]
    public async Task TravelRequiresMinimumTargetLevel()
    {
        Guid accountId = await CreatePlayerAsync(withCharacter: true);
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            Character character = await setup.Characters.SingleAsync();
            character.SetLevel(5);
            CharacterLocation location = await setup.CharacterLocations.SingleAsync();
            location.Relocate("WHISPERING_FOREST", Now);
            await setup.SaveChangesAsync();
        }

        TravelResult blocked = await TravelAsync(
            accountId,
            Guid.CreateVersion7(),
            "DEEP_FOREST",
            GatedMap);
        Assert.Equal(TravelErrorCodes.LevelRequired, blocked.ErrorCode);

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            Character character = await setup.Characters.SingleAsync();
            character.SetLevel(6);
            await setup.SaveChangesAsync();
        }

        TravelResult allowed = await TravelAsync(
            accountId,
            Guid.CreateVersion7(),
            "DEEP_FOREST",
            GatedMap);
        Assert.True(allowed.IsSuccess);
    }

    [Fact]
    public async Task CompletedBossContractUnlocksNextZone()
    {
        Guid accountId = await CreatePlayerAsync(withCharacter: true);
        Guid characterId;
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            Character character = await setup.Characters.SingleAsync();
            characterId = character.Id;
            character.SetLevel(15);
            CharacterLocation location = await setup.CharacterLocations.SingleAsync();
            location.Relocate("BROODMOTHER_LAIR", Now);
            await setup.SaveChangesAsync();
        }

        TravelResult blocked = await TravelAsync(
            accountId,
            Guid.CreateVersion7(),
            "BLIGHTED_GROVE",
            GatedMap);
        Assert.Equal(TravelErrorCodes.ContractRequired, blocked.ErrorCode);

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.CharacterContractCompletions.Add(
                new CharacterContractCompletion(
                    characterId,
                    "CONTRACT_BROODMOTHER_GATE",
                    "SPIDER_BROODMOTHER_L14",
                    Guid.CreateVersion7(),
                    Now));
            await setup.SaveChangesAsync();
        }

        TravelResult allowed = await TravelAsync(
            accountId,
            Guid.CreateVersion7(),
            "BLIGHTED_GROVE",
            GatedMap);
        Assert.True(allowed.IsSuccess);
    }

    [Fact]
    public async Task ConcurrentTravelFromSameVersionHasOneWinnerAndNoDuplicateOperation()
    {
        Guid accountId = await CreatePlayerAsync(withCharacter: true);

        Task<TravelResult>[] attempts =
        [
            TravelAsync(accountId, Guid.CreateVersion7(), "WHISPERING_FOREST"),
            TravelAsync(accountId, Guid.CreateVersion7(), "WHISPERING_FOREST")
        ];
        TravelResult[] results = await Task.WhenAll(attempts);

        Assert.Single(results, result =>
            result.IsSuccess
            && !result.IsTravelling
            && result.LocationId == "WHISPERING_FOREST");
        Assert.Single(
            results,
            result => result.ErrorCode is TravelErrorCodes.InvalidTransition
                or TravelErrorCodes.Conflict);

        await using GameDbContext context = postgres.CreateDbContext();
        Assert.Equal(1, await context.TravelOperations.CountAsync());
        Assert.Empty(await context.CharacterTravelStates.ToArrayAsync());
        CharacterLocation persisted = await context.CharacterLocations.SingleAsync();
        Assert.Equal("WHISPERING_FOREST", persisted.LocationId);
        Assert.Equal(2, persisted.Version);
    }

    private Task<TravelResult> TravelAsync(
        Guid accountId,
        Guid requestId,
        string targetLocationId) =>
        TravelAsync(accountId, requestId, targetLocationId, Map);

    private Task<TravelResult> TravelAsync(
        Guid accountId,
        Guid requestId,
        string targetLocationId,
        WorldMap map) =>
        TravelAsync(accountId, requestId, targetLocationId, map, Now);

    private async Task<TravelResult> TravelAsync(
        Guid accountId,
        Guid requestId,
        string targetLocationId,
        WorldMap map,
        DateTimeOffset now)
    {
        await using GameDbContext context = postgres.CreateDbContext();
        TravelService service = new(context, map, new FixedTimeProvider(now));
        return await service.TravelAsync(
            accountId,
            requestId,
            targetLocationId,
            CancellationToken.None);
    }

    private async Task<Guid> CreatePlayerAsync(bool withCharacter)
    {
        Guid accountId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(
            accountId,
            Interlocked.Increment(ref _nextTelegramUserId),
            Now));

        if (withCharacter)
        {
            Character character = new(
                Guid.CreateVersion7(),
                accountId,
                Guid.CreateVersion7(),
                "Arthas",
                $"ARTHAS{accountId:N}"[..16],
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now);
            context.Characters.Add(character);
            context.CharacterLocations.Add(
                new CharacterLocation(character.Id, "STARTER_TOWN", 1, Now));
            context.CharacterVitals.Add(new CharacterVitals(character.Id, 150, 0, Now, Now));
        }

        await context.SaveChangesAsync();
        return accountId;
    }

    private static readonly GameContentPackage Content = PhaseTwoTestContent.Create(
        Now,
        [],
        [
            new("STARTER_TOWN", "Starter Town", "SAFE", 1, ["WHISPERING_FOREST"]),
            new(
                "WHISPERING_FOREST",
                "Whispering Forest",
                "ADVENTURE",
                1,
                ["STARTER_TOWN", "DEEP_FOREST"]),
            new("DEEP_FOREST", "Deep Forest", "DANGEROUS", 3, ["WHISPERING_FOREST"])
        ]) with
    {
        LevelProgression = new LevelProgressionDefinition("DEFAULT_LEVELING", 60, 100, 1.5m),
        Items = [],
        LootTables = []
    };

    private static readonly WorldMap Map = new(Content.Locations);

    private static readonly WorldMap GatedMap = new(
    [
        new("STARTER_TOWN", "Стартовый город", "SAFE", 1, ["WHISPERING_FOREST"]),
        new(
            "WHISPERING_FOREST",
            "Шепчущий лес",
            "ADVENTURE",
            3,
            ["STARTER_TOWN", "DEEP_FOREST"],
            MinimumLevel: 1,
            MaximumLevel: 5),
        new(
            "DEEP_FOREST",
            "Глубокий лес",
            "DANGEROUS",
            9,
            ["WHISPERING_FOREST", "BROODMOTHER_LAIR"],
            MinimumLevel: 6,
            MaximumLevel: 11),
        new(
            "BROODMOTHER_LAIR",
            "Логово Прародительницы",
            "DANGEROUS",
            14,
            ["DEEP_FOREST", "BLIGHTED_GROVE"],
            MinimumLevel: 14,
            MaximumLevel: 14),
        new(
            "BLIGHTED_GROVE",
            "Осквернённая чаща",
            "DANGEROUS",
            17,
            ["BROODMOTHER_LAIR"],
            MinimumLevel: 15,
            MaximumLevel: 20,
            RequiredContractId: "CONTRACT_BROODMOTHER_GATE")
    ]);



    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
