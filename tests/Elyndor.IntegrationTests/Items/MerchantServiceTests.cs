using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Items;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class MerchantServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string MerchantId = "MARCUS_SUPPLIES";
    private const string PotionId = "SMALL_HEALING_POTION";
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 6, 40, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SameBuyMutationIsAppliedOnlyOnce()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(100);
        Guid mutationId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        MerchantService service = await CreateServiceAsync(context);

        Assert.True((await service.BuyAsync(accountId, MerchantId, PotionId, 1, mutationId, CancellationToken.None)).IsSuccess);
        Assert.True((await service.BuyAsync(accountId, MerchantId, PotionId, 1, mutationId, CancellationToken.None)).IsSuccess);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(80, await verify.Characters.Where(c => c.Id == characterId).Select(c => c.Gold).SingleAsync());
        Assert.Equal(1, await verify.CharacterMutations.CountAsync());
        Assert.Equal(1, await verify.CharacterItems
            .Where(i => i.CharacterId == characterId && i.ItemDefinitionId == PotionId)
            .SumAsync(i => i.Quantity));
    }

    [Fact]
    public async Task ReusingMutationIdForDifferentPayloadIsRejected()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(100);
        Guid mutationId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        MerchantService service = await CreateServiceAsync(context);

        Assert.True((await service.BuyAsync(accountId, MerchantId, PotionId, 1, mutationId, CancellationToken.None)).IsSuccess);
        MerchantOperationResult conflict = await service.BuyAsync(
            accountId, MerchantId, PotionId, 2, mutationId, CancellationToken.None);

        Assert.False(conflict.IsSuccess);
        Assert.Equal(MerchantErrorCodes.MutationConflict, conflict.ErrorCode);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(80, await verify.Characters.Where(c => c.Id == characterId).Select(c => c.Gold).SingleAsync());
        Assert.Equal(1, await verify.CharacterMutations.CountAsync());
    }

    [Fact]
    public async Task ConcurrentBuysCannotOverspendGold()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(20);
        await using GameDbContext c1 = postgres.CreateDbContext();
        await using GameDbContext c2 = postgres.CreateDbContext();
        MerchantService s1 = await CreateServiceAsync(c1);
        MerchantService s2 = await CreateServiceAsync(c2);

        MerchantOperationResult[] results = await Task.WhenAll(
            s1.BuyAsync(accountId, MerchantId, PotionId, 1, Guid.CreateVersion7(), CancellationToken.None),
            s2.BuyAsync(accountId, MerchantId, PotionId, 1, Guid.CreateVersion7(), CancellationToken.None));

        Assert.Single(results.Where(r => r.IsSuccess));
        Assert.Single(results.Where(r => r.ErrorCode == MerchantErrorCodes.NotEnoughGold));

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(0, await verify.Characters.Where(c => c.Id == characterId).Select(c => c.Gold).SingleAsync());
        Assert.Equal(1, await verify.CharacterMutations.CountAsync());
        Assert.Equal(1, await verify.CharacterItems
            .Where(i => i.CharacterId == characterId && i.ItemDefinitionId == PotionId)
            .SumAsync(i => i.Quantity));
    }

    [Fact]
    public async Task ConcurrentBuysPreserveStackQuantity()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(40);
        await using GameDbContext c1 = postgres.CreateDbContext();
        await using GameDbContext c2 = postgres.CreateDbContext();
        MerchantService s1 = await CreateServiceAsync(c1);
        MerchantService s2 = await CreateServiceAsync(c2);

        MerchantOperationResult[] results = await Task.WhenAll(
            s1.BuyAsync(accountId, MerchantId, PotionId, 1, Guid.CreateVersion7(), CancellationToken.None),
            s2.BuyAsync(accountId, MerchantId, PotionId, 1, Guid.CreateVersion7(), CancellationToken.None));

        Assert.All(results, r => Assert.True(r.IsSuccess));

        await using GameDbContext verify = postgres.CreateDbContext();
        CharacterItem[] stacks = await verify.CharacterItems
            .Where(i => i.CharacterId == characterId && i.ItemDefinitionId == PotionId)
            .ToArrayAsync();
        Assert.Equal(0, await verify.Characters.Where(c => c.Id == characterId).Select(c => c.Gold).SingleAsync());
        Assert.Equal(2, await verify.CharacterMutations.CountAsync());
        Assert.Single(stacks);
        Assert.Equal(2, stacks[0].Quantity);
    }

    [Fact]
    public async Task ConcurrentMaterialSalesCannotSellSameUnitTwice()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(0);
        Guid itemId = Guid.CreateVersion7();
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.CharacterItems.Add(new CharacterItem(itemId, characterId, "WOLF_HIDE", 1, Now));
            await setup.SaveChangesAsync();
        }

        await using GameDbContext c1 = postgres.CreateDbContext();
        await using GameDbContext c2 = postgres.CreateDbContext();
        MerchantService s1 = await CreateServiceAsync(c1);
        MerchantService s2 = await CreateServiceAsync(c2);

        MerchantOperationResult[] results = await Task.WhenAll(
            s1.SellMaterialAsync(accountId, MerchantId, itemId, 1, Guid.CreateVersion7(), CancellationToken.None),
            s2.SellMaterialAsync(accountId, MerchantId, itemId, 1, Guid.CreateVersion7(), CancellationToken.None));

        Assert.Single(results.Where(r => r.IsSuccess));
        Assert.Single(results.Where(r => r.ErrorCode == MerchantErrorCodes.ItemNotOwned));

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(2, await verify.Characters.Where(c => c.Id == characterId).Select(c => c.Gold).SingleAsync());
        Assert.Empty(await verify.CharacterItems.Where(i => i.Id == itemId).ToArrayAsync());
        Assert.Equal(1, await verify.CharacterMutations.CountAsync());
    }

    private async Task<(Guid AccountId, Guid CharacterId)> CreateCharacterAsync(long gold)
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();

        context.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
        Character character = new(
            characterId, accountId, Guid.CreateVersion7(), "Trader", $"TRADER{characterId:N}"[..16],
            "HUMAN", "MALE", "WARRIOR", Now);
        if (gold > 0) character.AddGold(gold);
        context.Characters.Add(character);
        context.CharacterLocations.Add(new CharacterLocation(characterId, "STARTER_TOWN", 1, Now));
        await context.SaveChangesAsync();
        return (accountId, characterId);
    }

    private static async Task<MerchantService> CreateServiceAsync(GameDbContext context)
    {
        var content = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        return new MerchantService(context, content, new FixedTimeProvider(Now));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
