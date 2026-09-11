using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Economy;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Economy;
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
        Assert.Single(await verify.PromoCodeRedemptions.ToArrayAsync());
        Assert.Single(await verify.CrystalLedgerEntries.Where(entry => entry.EntryType == CrystalLedgerEntryType.PromoCode).ToArrayAsync());
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
        PromoCodes: [new PromoCodeDefinition("WELCOME_2026", 30, [new PromoItemRewardDefinition("REFORGE_STONE", 3)])]);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
