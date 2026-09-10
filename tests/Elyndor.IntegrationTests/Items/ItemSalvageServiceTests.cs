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
public sealed class ItemSalvageServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SalvageRemovesUnequippedEquipmentAndReplayGrantsRewardsOnlyOnce()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync();
        Guid itemId = Guid.CreateVersion7();
        Guid mutationId = Guid.CreateVersion7();
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.CharacterItems.Add(new CharacterItem(
                itemId,
                characterId,
                "RECRUIT_IRON_SWORD",
                1,
                Now));
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        ItemSalvageService service = await CreateServiceAsync(context);

        ItemSalvageOperationResult first = await service.SalvageAsync(
            accountId,
            itemId,
            mutationId,
            confirmedHighValue: true,
            CancellationToken.None);
        ItemSalvageOperationResult replay = await service.SalvageAsync(
            accountId,
            itemId,
            mutationId,
            confirmedHighValue: true,
            CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(first.Reward, replay.Reward);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Empty(await verify.CharacterItems.Where(item => item.Id == itemId).ToArrayAsync());
        Assert.Equal(
            first.Reward!.ReforgeStoneQuantity,
            await QuantityAsync(verify, characterId, "REFORGE_STONE"));
        Assert.Equal(
            first.Reward.MaterialQuantity,
            await QuantityAsync(verify, characterId, "FORGE_SCRAP"));
        Assert.Single(await verify.CharacterMutations
            .Where(mutation => mutation.CharacterId == characterId)
            .ToArrayAsync());
    }

    [Fact]
    public async Task SalvageRejectsLockedEquipmentWithoutRemovingIt()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync();
        Guid itemId = Guid.CreateVersion7();
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            CharacterItem item = new(itemId, characterId, "RECRUIT_IRON_SWORD", 1, Now);
            item.SetLocked(true);
            setup.CharacterItems.Add(item);
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        ItemSalvageService service = await CreateServiceAsync(context);
        ItemSalvageOperationResult result = await service.SalvageAsync(
            accountId,
            itemId,
            Guid.CreateVersion7(),
            confirmedHighValue: true,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ItemSalvageErrorCodes.ItemLocked, result.ErrorCode);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.True(await verify.CharacterItems.AnyAsync(item => item.Id == itemId));
        Assert.Empty(await verify.CharacterMutations.ToArrayAsync());
    }

    [Fact]
    public async Task SalvageRequiresConfirmationForRareEquipment()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync();
        Guid itemId = Guid.CreateVersion7();
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.CharacterItems.Add(new CharacterItem(itemId, characterId, "DEEP_FOREST_CHARM", 1, Now));
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        ItemSalvageService service = await CreateServiceAsync(context);
        ItemSalvageOperationResult result = await service.SalvageAsync(
            accountId, itemId, Guid.CreateVersion7(), confirmedHighValue: false, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ItemSalvageErrorCodes.ConfirmationRequired, result.ErrorCode);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.True(await verify.CharacterItems.AnyAsync(item => item.Id == itemId));
    }

    [Fact]
    public async Task SalvageRejectsEquippedEquipmentWithoutRemovingIt()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync();
        Guid itemId = Guid.CreateVersion7();
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.CharacterItems.Add(new CharacterItem(itemId, characterId, "RECRUIT_IRON_SWORD", 1, Now));
            setup.CharacterEquipment.Add(new CharacterEquipment(characterId, EquipmentSlot.MainHand, itemId));
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        ItemSalvageService service = await CreateServiceAsync(context);
        ItemSalvageOperationResult result = await service.SalvageAsync(
            accountId, itemId, Guid.CreateVersion7(), confirmedHighValue: true, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ItemSalvageErrorCodes.ItemEquipped, result.ErrorCode);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.True(await verify.CharacterItems.AnyAsync(item => item.Id == itemId));
    }

    private async Task<(Guid AccountId, Guid CharacterId)> CreateCharacterAsync()
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
        context.Characters.Add(new Character(
            characterId,
            accountId,
            Guid.CreateVersion7(),
            "Salvager",
            $"SALVAGER{characterId:N}"[..16],
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now));
        await context.SaveChangesAsync();
        return (accountId, characterId);
    }

    private static async Task<ItemSalvageService> CreateServiceAsync(GameDbContext context)
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        Assert.NotNull(content.Itemization?.Salvage);
        Assert.Contains(content.Items!, item => item.Id == "REFORGE_STONE");
        Assert.Contains(content.Items!, item => item.Id == "FORGE_SCRAP");
        return new ItemSalvageService(context, content, new FixedTimeProvider(Now));
    }

    private static Task<int> QuantityAsync(GameDbContext context, Guid characterId, string definitionId) =>
        context.CharacterItems
            .Where(item => item.CharacterId == characterId && item.ItemDefinitionId == definitionId)
            .SumAsync(item => item.Quantity);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
