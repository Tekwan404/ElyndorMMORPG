using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;

namespace Elyndor.IntegrationTests.Items;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ItemStarUpgradeServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);
    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UpgradeRejectsLockedItemWithoutCreatingMutation()
    {
        Guid accountId = Guid.CreateVersion7(); Guid characterId = Guid.CreateVersion7(); Guid itemId = Guid.CreateVersion7();
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
            setup.Characters.Add(new Character(characterId, accountId, Guid.CreateVersion7(), "Forger", "FORGER0003", "HUMAN", "MALE", "WARRIOR", Now));
            CharacterItem item = new(itemId, characterId, "RECRUIT_IRON_SWORD", 1, Now); item.SetLocked(true);
            setup.CharacterItems.Add(item); await setup.SaveChangesAsync();
        }
        await using GameDbContext context = postgres.CreateDbContext();
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        ItemStarUpgradeService service = new(context, new StaticContentSnapshotProvider(content), new FixedTimeProvider(Now));
        ItemStarUpgradeResult result = await service.UpgradeAsync(accountId, itemId, Guid.CreateVersion7(), CancellationToken.None);
        Assert.False(result.Succeeded); Assert.Equal(ItemStarUpgradeErrorCodes.ItemLocked, result.ErrorCode);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Empty(verify.CharacterMutations);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
