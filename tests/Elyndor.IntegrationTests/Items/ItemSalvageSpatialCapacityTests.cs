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
public sealed class ItemSalvageSpatialCapacityTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 4, 30, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SalvageUsesEffectiveCapacityFromEquippedSpatialArtifact()
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        Guid salvageItemId = Guid.CreateVersion7();
        Guid artifactItemId = Guid.CreateVersion7();

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
            setup.Characters.Add(new Character(
                characterId,
                accountId,
                Guid.CreateVersion7(),
                "Salvage Capacity",
                $"SALVCAP{characterId:N}"[..16],
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now));

            setup.CharacterItems.Add(new CharacterItem(
                salvageItemId,
                characterId,
                "RECRUIT_IRON_SWORD",
                1,
                Now));
            setup.CharacterItems.Add(new CharacterItem(
                artifactItemId,
                characterId,
                "SPATIAL_EXPANDED_RING",
                1,
                Now));
            setup.CharacterSpatialArtifacts.Add(new CharacterSpatialArtifact(characterId, artifactItemId));

            for (var index = 0; index < 30; index++)
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

        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        await using GameDbContext context = postgres.CreateDbContext();
        GameContentSnapshot snapshot = new StaticContentSnapshotProvider(content).GetCurrent();
        InventoryCapacityState capacity = await InventoryCapacity.GetStateAsync(
            context,
            characterId,
            snapshot,
            CancellationToken.None);

        Assert.Equal(30, capacity.BaseCapacity);
        Assert.Equal(15, capacity.ArtifactCapacityBonus);
        Assert.Equal(45, capacity.Capacity);
        Assert.Equal(31, capacity.UsedSlots);

        ItemSalvageService service = new(context, content, new FixedTimeProvider(Now));
        ItemSalvageOperationResult result = await service.SalvageAsync(
            accountId,
            salvageItemId,
            Guid.CreateVersion7(),
            confirmedHighValue: true,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorCode);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
