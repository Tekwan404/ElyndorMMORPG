using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Items;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class AutomaticEquipmentSlotSelectionTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 19, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SecondRingWithoutExplicitTargetUsesFreeRingSlot()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync();
        Guid firstRingId = await AddItemAsync(characterId, "ACC_COMMON_2_RING");
        Guid secondRingId = await AddItemAsync(characterId, "ACC_COMMON_2_RING");

        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = await CreateServiceAsync(context);

        InventoryOperationResult first = await service.EquipAsync(
            accountId,
            firstRingId,
            Guid.CreateVersion7(),
            CancellationToken.None);
        InventoryOperationResult second = await service.EquipAsync(
            accountId,
            secondRingId,
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Null(second.ErrorCode);

        await using GameDbContext verify = postgres.CreateDbContext();
        CharacterEquipment[] rings = await verify.CharacterEquipment
            .AsNoTracking()
            .Where(equipment => equipment.CharacterId == characterId
                && (equipment.Slot == EquipmentSlot.Ring1
                    || equipment.Slot == EquipmentSlot.Ring2))
            .OrderBy(equipment => equipment.Slot)
            .ToArrayAsync();

        Assert.Equal(2, rings.Length);
        Assert.Equal(
            firstRingId,
            Assert.Single(rings, equipment => equipment.Slot == EquipmentSlot.Ring1)
                .CharacterItemId);
        Assert.Equal(
            secondRingId,
            Assert.Single(rings, equipment => equipment.Slot == EquipmentSlot.Ring2)
                .CharacterItemId);
    }

    [Fact]
    public async Task AutomaticRingEquipReplacesDefinitionSlotWhenBothRingSlotsAreOccupied()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync();
        Guid firstRingId = await AddItemAsync(characterId, "ACC_COMMON_2_RING");
        Guid secondRingId = await AddItemAsync(characterId, "ACC_COMMON_2_RING");
        Guid replacementRingId = await AddItemAsync(characterId, "ACC_COMMON_2_RING");

        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = await CreateServiceAsync(context);

        Assert.True((await service.EquipAsync(
            accountId,
            firstRingId,
            Guid.CreateVersion7(),
            CancellationToken.None)).IsSuccess);
        Assert.True((await service.EquipAsync(
            accountId,
            secondRingId,
            Guid.CreateVersion7(),
            CancellationToken.None)).IsSuccess);

        InventoryOperationResult replacement = await service.EquipAsync(
            accountId,
            replacementRingId,
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.True(replacement.IsSuccess);
        Assert.Null(replacement.ErrorCode);

        await using GameDbContext verify = postgres.CreateDbContext();
        CharacterEquipment[] rings = await verify.CharacterEquipment
            .AsNoTracking()
            .Where(equipment => equipment.CharacterId == characterId
                && (equipment.Slot == EquipmentSlot.Ring1
                    || equipment.Slot == EquipmentSlot.Ring2))
            .ToArrayAsync();

        Assert.Equal(2, rings.Length);
        Assert.Equal(
            replacementRingId,
            Assert.Single(rings, equipment => equipment.Slot == EquipmentSlot.Ring1)
                .CharacterItemId);
        Assert.Equal(
            secondRingId,
            Assert.Single(rings, equipment => equipment.Slot == EquipmentSlot.Ring2)
                .CharacterItemId);
        Assert.DoesNotContain(rings, equipment => equipment.CharacterItemId == firstRingId);
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
        Character character = new(
            characterId,
            accountId,
            Guid.CreateVersion7(),
            "Ring Regression",
            $"RNG{Guid.NewGuid():N}"[..16],
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now);
        character.SetLevel(25);
        context.Characters.Add(character);
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

    private static async Task<InventoryEquipmentService> CreateServiceAsync(GameDbContext context)
    {
        var content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        return new InventoryEquipmentService(
            context,
            content,
            new FixedTimeProvider(Now));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
