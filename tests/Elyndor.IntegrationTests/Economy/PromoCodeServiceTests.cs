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
public sealed class PromoCodeServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 16, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RedeemNormalizesCodeAndReplayGrantsRewardsOnlyOnce()
    {
        Guid accountId = await CreateAccountAsync();
        await using GameDbContext context = postgres.CreateDbContext();
        PromoCodeService service = new(context, new StaticContentSnapshotProvider(CreateContent()), new FixedTimeProvider(Now));
        Guid mutationId = Guid.CreateVersion7();

        PromoCodeRedemptionResult first = await service.RedeemAsync(accountId, "  welcome_2026 ", mutationId, CancellationToken.None);
        PromoCodeRedemptionResult replay = await service.RedeemAsync(accountId, "WELCOME_2026", mutationId, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(30, first.CrystalBalance);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(3, await verify.CharacterItems.Where(item => item.CharacterId == first.CharacterId && item.ItemDefinitionId == "REFORGE_STONE").SumAsync(item => item.Quantity));
        Assert.Equal(2500, await verify.Characters.Where(character => character.Id == first.CharacterId).Select(character => character.Gold).SingleAsync());
        Assert.Single(await verify.PromoCodeRedemptions.ToArrayAsync());
        Assert.Single(await verify.CrystalLedgerEntries.Where(entry => entry.EntryType == CrystalLedgerEntryType.PromoCode).ToArrayAsync());
    }

    [Fact]
    public async Task GoldOnlyPromoIgnoresFullInventory()
    {
        Guid accountId = await CreateAccountAsync();
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            Guid characterId = await setup.Characters
                .Where(character => character.AccountId == accountId)
                .Select(character => character.Id)
                .SingleAsync();
            for (var index = 0; index < InventoryCapacity.DefaultCapacity; index++)
            {
                setup.CharacterItems.Add(new CharacterItem(
                    Guid.CreateVersion7(),
                    characterId,
                    "REFORGE_STONE",
                    1,
                    Now));
            }
            await setup.SaveChangesAsync();
        }

        GameContentPackage content = CreateContent() with
        {
            PromoCodes = [new PromoCodeDefinition("GOLD_ONLY", GoldAmount: 10_000_999)]
        };
        await using GameDbContext context = postgres.CreateDbContext();
        PromoCodeService service = new(
            context,
            new StaticContentSnapshotProvider(content),
            new FixedTimeProvider(Now));

        PromoCodeRedemptionResult result = await service.RedeemAsync(
            accountId,
            "gold_only",
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(
            10_000_999,
            await verify.Characters
                .Where(character => character.AccountId == accountId)
                .Select(character => character.Gold)
                .SingleAsync());
    }

    [Fact]
    public async Task ItemPromoUsesSpatialArtifactCapacity()
    {
        Guid accountId = await CreateAccountAsync();
        Guid characterId;
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            characterId = await setup.Characters
                .Where(character => character.AccountId == accountId)
                .Select(character => character.Id)
                .SingleAsync();
            Guid artifactId = Guid.CreateVersion7();
            setup.CharacterItems.Add(new CharacterItem(
                artifactId,
                characterId,
                "SPATIAL_EXPANDED_RING",
                1,
                Now));
            setup.CharacterSpatialArtifacts.Add(new CharacterSpatialArtifact(characterId, artifactId));
            for (var index = 0; index < InventoryCapacity.DefaultCapacity; index++)
            {
                setup.CharacterItems.Add(new CharacterItem(
                    Guid.CreateVersion7(),
                    characterId,
                    "RECRUIT_IRON_SWORD",
                    1,
                    Now.AddSeconds(index + 1)));
            }
            await setup.SaveChangesAsync();
        }

        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        package = package with
        {
            PromoCodes =
            [
                new PromoCodeDefinition(
                    "ARTIFACT_CAPACITY",
                    ItemRewards: [new PromoItemRewardDefinition("REFORGE_STONE", 1)])
            ]
        };

        await using GameDbContext context = postgres.CreateDbContext();
        PromoCodeService service = new(
            context,
            new StaticContentSnapshotProvider(package),
            new FixedTimeProvider(Now));

        PromoCodeRedemptionResult result = await service.RedeemAsync(
            accountId,
            "artifact_capacity",
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(
            1,
            await verify.CharacterItems
                .Where(item => item.CharacterId == characterId
                    && item.ItemDefinitionId == "REFORGE_STONE")
                .SumAsync(item => item.Quantity));
    }

    [Fact]
    public async Task PromoEquipmentRewardUsesProceduralItemGenerator()
    {
        Guid accountId = await CreateAccountAsync();
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        package = package with
        {
            PromoCodes =
            [
                new PromoCodeDefinition(
                    "BLACKHEART_PROMO",
                    ItemRewards: [new PromoItemRewardDefinition("UNIQUE_WARRIOR_BLACKHEART", 1)])
            ]
        };

        await using GameDbContext context = postgres.CreateDbContext();
        PromoCodeService service = new(
            context,
            new StaticContentSnapshotProvider(package),
            new FixedTimeProvider(Now));
        Guid operationId = Guid.CreateVersion7();

        PromoCodeRedemptionResult result = await service.RedeemAsync(
            accountId,
            "blackheart_promo",
            operationId,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        await using GameDbContext verify = postgres.CreateDbContext();
        CharacterItem item = await verify.CharacterItems
            .Include(candidate => candidate.Affixes)
            .SingleAsync(candidate => candidate.CharacterId == result.CharacterId
                && candidate.ItemDefinitionId == "UNIQUE_WARRIOR_BLACKHEART");
        Assert.Equal(60, item.ItemLevel);
        Assert.Equal(2, item.GenerationVersion);
        Assert.Equal("PROMO_CODE", item.SourceType);
        Assert.Equal(operationId, item.SourceOperationId);
        Assert.Equal("BLACKHEART_PROMO", item.SourceEntryId);
        Assert.Equal(5, item.Affixes.Count);
        Assert.Equal(3, item.Affixes.Count(affix => affix.IsGuaranteed));
    }

    private async Task<Guid> CreateAccountAsync()
    {
        Guid accountId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
        context.Characters.Add(new Character(Guid.CreateVersion7(), accountId, Guid.CreateVersion7(), "Promo", "PROMO00000000001", "HUMAN", "MALE", "WARRIOR", Now));
        await context.SaveChangesAsync();
        return accountId;
    }

    private static GameContentPackage CreateContent() => new(
        "test", "test", Now, [], [], Items: [
            new ItemDefinition("REFORGE_STONE", "Камень перековки", ItemType.Material, ItemRarity.Common, 1, true, 99, null, new PrimaryStats(0, 0, 0, 0), "")],
        PromoCodes: [new PromoCodeDefinition("WELCOME_2026", 30, [new PromoItemRewardDefinition("REFORGE_STONE", 3)], GoldAmount: 2500)]);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
