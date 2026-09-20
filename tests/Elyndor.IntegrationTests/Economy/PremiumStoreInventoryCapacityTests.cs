using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Economy;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Economy;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Economy;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class PremiumStoreInventoryCapacityTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 20, 8, 25, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task FullInventoryPurchaseUsesRoomInExistingStack()
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        PremiumStoreOfferDefinition offer = content.PremiumStoreOffers!
            .Single(candidate => candidate.Sku == "REFORGE_STONES_SMALL");
        ItemDefinition definition = content.Items!
            .Single(item => item.Id == offer.ItemDefinitionId);
        Assert.True(definition.Stackable);
        Assert.True(definition.MaxStack > offer.Quantity);
        int existingQuantity = definition.MaxStack - offer.Quantity;

        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.Accounts.Add(new Account(
                accountId,
                Random.Shared.NextInt64(1, long.MaxValue),
                Now));
            setup.Characters.Add(new Character(
                characterId,
                accountId,
                Guid.CreateVersion7(),
                "StoreCapacity",
                $"SHOP{characterId:N}"[..16],
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now));
            setup.CharacterItems.Add(new CharacterItem(
                Guid.CreateVersion7(),
                characterId,
                definition.Id,
                existingQuantity,
                Now,
                definition.Version));
            for (var index = 0; index < InventoryCapacity.DefaultCapacity - 1; index++)
            {
                setup.CharacterItems.Add(new CharacterItem(
                    Guid.CreateVersion7(),
                    characterId,
                    "RECRUIT_IRON_SWORD",
                    1,
                    Now.AddTicks(index + 1)));
            }
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        CrystalWalletService wallet = new(context, new FixedTimeProvider(Now));
        await wallet.GrantAsync(
            accountId,
            Guid.CreateVersion7(),
            CrystalLedgerEntryType.AdminGrant,
            offer.CrystalPrice + 10,
            "capacity-test",
            CancellationToken.None);
        PremiumStoreService store = new(
            context,
            new StaticContentSnapshotProvider(content),
            new FixedTimeProvider(Now));

        PremiumStorePurchaseResult result = await store.PurchaseAsync(
            accountId,
            offer.Sku,
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(
            InventoryCapacity.DefaultCapacity,
            await InventoryCapacity.CountUsedSlotsAsync(
                verify,
                characterId,
                CancellationToken.None));
        Assert.Equal(
            1,
            await verify.CharacterItems
                .AsNoTracking()
                .CountAsync(item =>
                    item.CharacterId == characterId
                    && item.ItemDefinitionId == definition.Id));
        Assert.Equal(
            definition.MaxStack,
            await verify.CharacterItems
                .AsNoTracking()
                .Where(item =>
                    item.CharacterId == characterId
                    && item.ItemDefinitionId == definition.Id)
                .SumAsync(item => item.Quantity));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
