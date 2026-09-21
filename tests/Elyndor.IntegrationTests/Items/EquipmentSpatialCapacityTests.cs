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
public sealed class EquipmentSpatialCapacityTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 21, 4, 45, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task EquipmentCanBeEquippedAboveBaseCapacityWhenSpatialArtifactProvidesRoom()
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        StaticContentSnapshotProvider contentProvider = new(content);

        (Guid accountId, Guid characterId) = await CreateCharacterAsync();
        Guid artifactId = await AddItemAsync(characterId, "SPATIAL_EXPANDED_RING");

        await using (GameDbContext spatialContext = postgres.CreateDbContext())
        {
            SpatialInventoryService spatial = new(spatialContext, contentProvider);
            SpatialInventoryOperationResult equippedArtifact = await spatial.EquipAsync(
                accountId,
                artifactId,
                CancellationToken.None);

            Assert.True(equippedArtifact.IsSuccess);
            Assert.Equal(45, equippedArtifact.Snapshot!.Capacity.Capacity);
        }

        Guid chestId = await AddItemAsync(characterId, "RECRUIT_HEAVY_CHEST");
        await FillSlotsAsync(characterId, 33);

        await using (GameDbContext before = postgres.CreateDbContext())
        {
            InventoryCapacityState state = await InventoryCapacity.GetStateAsync(
                before,
                characterId,
                contentProvider.GetCurrent(),
                CancellationToken.None);
            Assert.Equal(30, state.BaseCapacity);
            Assert.Equal(15, state.ArtifactCapacityBonus);
            Assert.Equal(45, state.Capacity);
            Assert.Equal(34, state.UsedSlots);
        }

        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService equipment = new(
            context,
            contentProvider,
            new FixedTimeProvider(Now));
        InventoryOperationResult result = await equipment.EquipAsync(
            accountId,
            chestId,
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorCode);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Contains(
            await verify.CharacterEquipment
                .AsNoTracking()
                .Where(item => item.CharacterId == characterId)
                .ToArrayAsync(),
            item => item.Slot == EquipmentSlot.Chest
                && item.CharacterItemId == chestId);

        InventoryCapacityState finalState = await InventoryCapacity.GetStateAsync(
            verify,
            characterId,
            contentProvider.GetCurrent(),
            CancellationToken.None);
        Assert.Equal(45, finalState.Capacity);
        Assert.Equal(33, finalState.UsedSlots);
    }

    private async Task<(Guid AccountId, Guid CharacterId)> CreateCharacterAsync()
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();

        context.Accounts.Add(new Account(
            accountId,
            Random.Shared.NextInt64(1, long.MaxValue),
            Now));
        context.Characters.Add(new Character(
            characterId,
            accountId,
            Guid.CreateVersion7(),
            "Capacity Tester",
            $"CAP{characterId:N}"[..16],
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now));
        context.CharacterVitals.Add(new CharacterVitals(
            characterId,
            100,
            0,
            Now,
            Now));
        await context.SaveChangesAsync();
        return (accountId, characterId);
    }

    private async Task<Guid> AddItemAsync(Guid characterId, string definitionId)
    {
        Guid itemId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.CharacterItems.Add(new CharacterItem(
            itemId,
            characterId,
            definitionId,
            1,
            Now));
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
