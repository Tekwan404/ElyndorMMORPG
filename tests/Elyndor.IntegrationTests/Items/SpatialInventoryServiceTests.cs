using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Items;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class SpatialInventoryServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 11, 30, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task EquippingArtifactAddsCapacityAndRemovesItFromUsedSlots()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync();
        Guid artifactId = await AddItemAsync(characterId, "SPATIAL_EXPANDED_RING");
        await FillSlotsAsync(characterId, 29);

        await using GameDbContext context = postgres.CreateDbContext();
        SpatialInventoryService service = await CreateServiceAsync(context);
        SpatialInventoryOperationResult result = await service.EquipAsync(
            accountId,
            artifactId,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Snapshot);
        Assert.Equal(30, result.Snapshot!.Capacity.BaseCapacity);
        Assert.Equal(15, result.Snapshot.Capacity.ArtifactCapacityBonus);
        Assert.Equal(45, result.Snapshot.Capacity.Capacity);
        Assert.Equal(29, result.Snapshot.Capacity.UsedSlots);
        Assert.False(result.Snapshot.Capacity.IsOverflow);
    }

    [Fact]
    public async Task UnequipIsRejectedWhenReturnedArtifactWouldExceedBaseCapacity()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync();
        Guid artifactId = await AddItemAsync(characterId, "SPATIAL_EXPANDED_RING");
        await FillSlotsAsync(characterId, 30);

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.CharacterSpatialArtifacts.Add(new CharacterSpatialArtifact(characterId, artifactId));
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        SpatialInventoryService service = await CreateServiceAsync(context);
        SpatialInventoryOperationResult result = await service.UnequipAsync(
            accountId,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SpatialInventoryErrorCodes.InventoryFull, result.ErrorCode);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.True(await verify.CharacterSpatialArtifacts.AnyAsync(
            artifact => artifact.CharacterId == characterId));
    }

    [Fact]
    public async Task UnequipSucceedsWhenReturnedArtifactFitsBaseCapacity()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync();
        Guid artifactId = await AddItemAsync(characterId, "SPATIAL_EXPANDED_RING");
        await FillSlotsAsync(characterId, 28);

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.CharacterSpatialArtifacts.Add(new CharacterSpatialArtifact(characterId, artifactId));
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        SpatialInventoryService service = await CreateServiceAsync(context);
        SpatialInventoryOperationResult result = await service.UnequipAsync(
            accountId,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(30, result.Snapshot!.Capacity.Capacity);
        Assert.Equal(29, result.Snapshot.Capacity.UsedSlots);
        Assert.Null(result.Snapshot.EquippedArtifact);
    }

    [Fact]
    public async Task ReplacingWithSmallerArtifactIsRejectedWhenNewCapacityWouldOverflow()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync();
        Guid largeId = await AddItemAsync(characterId, "SPATIAL_POCKET_SHARD");
        Guid smallId = await AddItemAsync(characterId, "SPATIAL_MINOR_RING");
        await FillSlotsAsync(characterId, 40);

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.CharacterSpatialArtifacts.Add(new CharacterSpatialArtifact(characterId, largeId));
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        SpatialInventoryService service = await CreateServiceAsync(context);
        SpatialInventoryOperationResult result = await service.EquipAsync(
            accountId,
            smallId,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SpatialInventoryErrorCodes.InventoryFull, result.ErrorCode);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(largeId, await verify.CharacterSpatialArtifacts
            .Where(artifact => artifact.CharacterId == characterId)
            .Select(artifact => artifact.CharacterItemId)
            .SingleAsync());
    }

    private async Task<(Guid AccountId, Guid CharacterId)> CreateCharacterAsync()
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(accountId, 900000000 + Random.Shared.Next(1000000), Now));
        context.Characters.Add(new Character(characterId, accountId, "Spatial Tester", "WARRIOR", "MALE", Now));
        await context.SaveChangesAsync();
        return (accountId, characterId);
    }

    private async Task<Guid> AddItemAsync(Guid characterId, string definitionId)
    {
        Guid itemId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.CharacterItems.Add(new CharacterItem(itemId, characterId, definitionId, 1, Now));
        await context.SaveChangesAsync();
        return itemId;
    }

    private async Task FillSlotsAsync(Guid characterId, int count)
    {
        await using GameDbContext context = postgres.CreateDbContext();
        for (var index = 0; index < count; index++)
        {
            context.CharacterItems.Add(new CharacterItem(
                Guid.CreateVersion7(),
                characterId,
                "RECRUIT_IRON_SWORD",
                1,
                Now.AddSeconds(index + 1)));
        }
        await context.SaveChangesAsync();
    }

    private static async Task<SpatialInventoryService> CreateServiceAsync(GameDbContext context)
    {
        GameContentPackage content = await GameContentPackageLoader.LoadDefaultAsync(CancellationToken.None);
        return new SpatialInventoryService(
            context,
            new StaticContentSnapshotProvider(content));
    }
}
