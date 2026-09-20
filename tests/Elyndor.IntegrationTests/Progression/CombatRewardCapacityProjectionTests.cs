using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Progression;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Progression;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class CombatRewardCapacityProjectionTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 20, 8, 10, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task MultipleRewardItemsCannotReuseSameUnsavedFreeSlot()
    {
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
                "Capacity",
                $"CAP{characterId:N}"[..16],
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now));
            setup.CharacterVitals.Add(new CharacterVitals(
                characterId,
                100,
                0,
                Now,
                Now));

            for (var index = 0; index < InventoryCapacity.DefaultCapacity - 1; index++)
            {
                setup.CharacterItems.Add(new CharacterItem(
                    Guid.CreateVersion7(),
                    characterId,
                    "RECRUIT_IRON_SWORD",
                    1,
                    Now.AddTicks(index)));
            }
            await setup.SaveChangesAsync();
        }

        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        await using GameDbContext context = postgres.CreateDbContext();
        TimeProvider timeProvider = new FixedTimeProvider(Now);
        InventoryEquipmentService inventory = new(context, content, timeProvider);
        CharacterDerivedStateService derived = new(context, content, inventory);
        CombatRewardService service = new(
            context,
            content,
            derived,
            new FixedRandomFactory(),
            timeProvider);

        CombatRewardApplicationResult result = await service.ApplyVictoryAsync(
            characterId,
            VictorySnapshot(Guid.CreateVersion7()),
            CancellationToken.None);

        Assert.True(result.Granted);
        Assert.True(result.Items.Count > 1);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(
            InventoryCapacity.DefaultCapacity,
            await InventoryCapacity.CountUsedSlotsAsync(
                verify,
                characterId,
                CancellationToken.None));
        Assert.True(await verify.PendingLootItems
            .AsNoTracking()
            .AnyAsync(item => item.CharacterId == characterId));
    }

    private static CombatSessionSnapshot VictorySnapshot(Guid sessionId)
    {
        CombatActorSnapshot player = Actor(
            Guid.CreateVersion7(),
            CombatActorKind.Player,
            "WARRIOR",
            "Capacity");
        CombatActorSnapshot enemy = Actor(
            Guid.CreateVersion7(),
            CombatActorKind.Monster,
            "FOREST_WOLF_L1",
            "Forest Wolf");
        return new CombatSessionSnapshot(
            sessionId,
            1,
            CombatSessionStatus.Victory,
            Now,
            player,
            enemy);
    }

    private static CombatActorSnapshot Actor(
        Guid id,
        CombatActorKind kind,
        string definitionId,
        string name) =>
        new(
            id,
            kind,
            definitionId,
            name,
            0,
            100,
            "NONE",
            0,
            0,
            false,
            null,
            new Dictionary<string, DateTimeOffset>(),
            new HashSet<string>(),
            [],
            []);

    private sealed class FixedRandomFactory : IGameRandomFactory
    {
        public IGameRandom Create() =>
            new SequenceGameRandom(Enumerable.Repeat(0m, 64).ToArray());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
