using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;

namespace Elyndor.IntegrationTests.Characters;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class CharacterCompanionServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 12, 16, 10, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ArcherCanSelectAnyPhysicalCompanionProfile()
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.Accounts.Add(new Account(accountId, 123456, Now));
            setup.Characters.Add(new Character(
                characterId,
                accountId,
                Guid.CreateVersion7(),
                "Companion",
                "COMPANION",
                "HUMAN",
                "MALE",
                "ARCHER",
                Now));
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        CharacterCompanionService service = new(
            context,
            new StaticContentSnapshotProvider(content),
            new CharacterDerivedStateService(
                context,
                content,
                new InventoryEquipmentService(context, content, new FixedTimeProvider(Now))));

        CharacterCompanionSelectionResult result = await service.SelectAsync(
            accountId,
            "ARCHER_TRAPPER",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ARCHER_TRAPPER", result.Snapshot!.SelectedPhysicalProfileId);
        Assert.Equal("ARCHER_TRAPPER", result.Snapshot.EffectiveProfileId);
        Assert.Equal(3, result.Snapshot.AvailableProfiles.Count);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
