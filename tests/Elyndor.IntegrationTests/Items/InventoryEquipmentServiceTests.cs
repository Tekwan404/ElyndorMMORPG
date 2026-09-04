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
public sealed class InventoryEquipmentServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 8, 45, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SameConsumableMutationIsAppliedOnlyOnce()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(currentHp: 25);
        Guid itemId = await AddItemAsync(characterId, "SMALL_HEALING_POTION", 2);
        Guid mutationId = Guid.CreateVersion7();

        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = await CreateServiceAsync(context);

        InventoryOperationResult first = await service.UseConsumableOutOfCombatAsync(
            accountId, itemId, mutationId, 200, Now, CancellationToken.None);
        InventoryOperationResult replay = await service.UseConsumableOutOfCombatAsync(
            accountId, itemId, mutationId, 200, Now, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(replay.IsSuccess);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(75, await verify.CharacterVitals
            .Where(v => v.CharacterId == characterId)
            .Select(v => v.CurrentHp)
            .SingleAsync());
        Assert.Equal(1, await verify.CharacterItems
            .Where(i => i.Id == itemId)
            .Select(i => i.Quantity)
            .SingleAsync());
        Assert.Equal(1, await verify.CharacterMutations.CountAsync());
    }

    [Fact]
    public async Task ReusingInventoryMutationIdForDifferentOperationIsRejected()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(currentHp: 25);
        Guid itemId = await AddItemAsync(characterId, "RANGER_FANG_BLADE", 1);
        Guid mutationId = Guid.CreateVersion7();

        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = await CreateServiceAsync(context);

        Assert.True((await service.EquipAsync(
            accountId, itemId, mutationId, CancellationToken.None)).IsSuccess);

        InventoryOperationResult conflict = await service.UnequipAsync(
            accountId, EquipmentSlot.Weapon, mutationId, CancellationToken.None);

        Assert.False(conflict.IsSuccess);
        Assert.Equal(InventoryErrorCodes.MutationConflict, conflict.ErrorCode);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Single(await verify.CharacterEquipment
            .Where(e => e.CharacterId == characterId)
            .ToArrayAsync());
    }

    [Fact]
    public async Task ConcurrentConsumableUsesPreserveStackQuantity()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(currentHp: 25);
        Guid itemId = await AddItemAsync(characterId, "SMALL_HEALING_POTION", 2);

        await using GameDbContext firstContext = postgres.CreateDbContext();
        await using GameDbContext secondContext = postgres.CreateDbContext();
        InventoryEquipmentService first = await CreateServiceAsync(firstContext);
        InventoryEquipmentService second = await CreateServiceAsync(secondContext);

        InventoryOperationResult[] results = await Task.WhenAll(
            first.UseConsumableOutOfCombatAsync(
                accountId, itemId, Guid.CreateVersion7(), 200, Now, CancellationToken.None),
            second.UseConsumableOutOfCombatAsync(
                accountId, itemId, Guid.CreateVersion7(), 200, Now.AddSeconds(1), CancellationToken.None));

        Assert.All(results, result => Assert.True(result.IsSuccess));

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(125, await verify.CharacterVitals
            .Where(v => v.CharacterId == characterId)
            .Select(v => v.CurrentHp)
            .SingleAsync());
        Assert.Empty(await verify.CharacterItems.Where(i => i.Id == itemId).ToArrayAsync());
        Assert.Equal(2, await verify.CharacterMutations.CountAsync());
    }

    [Fact]
    public async Task ConcurrentUseOfSingleConsumableSucceedsOnlyOnce()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(currentHp: 25);
        Guid itemId = await AddItemAsync(characterId, "SMALL_HEALING_POTION", 1);

        await using GameDbContext firstContext = postgres.CreateDbContext();
        await using GameDbContext secondContext = postgres.CreateDbContext();
        InventoryEquipmentService first = await CreateServiceAsync(firstContext);
        InventoryEquipmentService second = await CreateServiceAsync(secondContext);

        InventoryOperationResult[] results = await Task.WhenAll(
            first.UseConsumableOutOfCombatAsync(
                accountId, itemId, Guid.CreateVersion7(), 200, Now, CancellationToken.None),
            second.UseConsumableOutOfCombatAsync(
                accountId, itemId, Guid.CreateVersion7(), 200, Now.AddSeconds(1), CancellationToken.None));

        Assert.Single(results.Where(result => result.IsSuccess));
        Assert.Single(results.Where(result => result.ErrorCode == InventoryErrorCodes.ItemNotFound));

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(75, await verify.CharacterVitals
            .Where(v => v.CharacterId == characterId)
            .Select(v => v.CurrentHp)
            .SingleAsync());
        Assert.Empty(await verify.CharacterItems.Where(i => i.Id == itemId).ToArrayAsync());
        Assert.Equal(1, await verify.CharacterMutations.CountAsync());
    }

    [Fact]
    public async Task ConcurrentEquipWritesLeaveOneAuthoritativeSlot()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(currentHp: 100);
        Guid firstItemId = await AddItemAsync(characterId, "RANGER_FANG_BLADE", 1);
        Guid secondItemId = await AddItemAsync(characterId, "RANGER_FANG_BLADE", 1);

        await using GameDbContext firstContext = postgres.CreateDbContext();
        await using GameDbContext secondContext = postgres.CreateDbContext();
        InventoryEquipmentService first = await CreateServiceAsync(firstContext);
        InventoryEquipmentService second = await CreateServiceAsync(secondContext);

        InventoryOperationResult[] results = await Task.WhenAll(
            first.EquipAsync(accountId, firstItemId, Guid.CreateVersion7(), CancellationToken.None),
            second.EquipAsync(accountId, secondItemId, Guid.CreateVersion7(), CancellationToken.None));

        Assert.All(results, result => Assert.True(result.IsSuccess));

        await using GameDbContext verify = postgres.CreateDbContext();
        CharacterEquipment equipment = await verify.CharacterEquipment
            .SingleAsync(e => e.CharacterId == characterId && e.Slot == EquipmentSlot.Weapon);
        Assert.Contains(equipment.CharacterItemId, new[] { firstItemId, secondItemId });
        Assert.Equal(2, await verify.CharacterMutations.CountAsync());
    }

    private async Task<(Guid AccountId, Guid CharacterId)> CreateCharacterAsync(decimal currentHp)
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();

        context.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
        context.Characters.Add(new Character(
            characterId, accountId, Guid.CreateVersion7(), "Inventory", $"INV{characterId:N}"[..16],
            "HUMAN", "MALE", "WARRIOR", Now));
        context.CharacterVitals.Add(new CharacterVitals(
            characterId, currentHp, 0, Now, Now));
        await context.SaveChangesAsync();
        return (accountId, characterId);
    }

    private async Task<Guid> AddItemAsync(Guid characterId, string definitionId, int quantity)
    {
        Guid itemId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.CharacterItems.Add(new CharacterItem(itemId, characterId, definitionId, quantity, Now));
        await context.SaveChangesAsync();
        return itemId;
    }

    private static async Task<InventoryEquipmentService> CreateServiceAsync(GameDbContext context)
    {
        var content = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        return new InventoryEquipmentService(context, content, new FixedTimeProvider(Now));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
