using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Progression;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Progression;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class CombatRewardServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 2, 6, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task VictoryLevelUpFullyHealsToNewAuthoritativeMaxHp()
    {
        (Guid characterId, _) = await CreateCharacterAsync(90, 10);
        await using GameDbContext context = postgres.CreateDbContext();
        CombatRewardService service = await CreateServiceAsync(context);

        CombatRewardApplicationResult result = await service.ApplyVictoryAsync(
            characterId,
            VictorySnapshot(Guid.CreateVersion7()),
            CancellationToken.None);

        Character character = await context.Characters.AsNoTracking().SingleAsync();
        CharacterVitals vitals = await context.CharacterVitals.AsNoTracking().SingleAsync();
        Assert.True(result.Progression!.LeveledUp);
        Assert.Equal(2, character.Level);
        Assert.Equal(25, character.Experience);
        Assert.Equal(170, vitals.CurrentHp);
    }

    [Fact]
    public async Task SameCombatSessionGrantsPermanentRewardOnlyOnce()
    {
        (Guid characterId, _) = await CreateCharacterAsync(0, 100);
        Guid sessionId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        CombatRewardService service = await CreateServiceAsync(context);

        CombatRewardApplicationResult first = await service.ApplyVictoryAsync(
            characterId, VictorySnapshot(sessionId), CancellationToken.None);
        CombatRewardApplicationResult replay = await service.ApplyVictoryAsync(
            characterId, VictorySnapshot(sessionId), CancellationToken.None);

        Character character = await context.Characters.AsNoTracking().SingleAsync();
        Assert.True(first.Granted);
        Assert.False(replay.Granted);
        Assert.Equal(35, character.Experience);
        Assert.Equal(1, await context.CombatRewardGrants.CountAsync());
    }

    [Fact]
    public async Task ConcurrentSameSessionGrantsXpGoldAndLootExactlyOnce()
    {
        (Guid characterId, _) = await CreateCharacterAsync(0, 100);
        Guid sessionId = Guid.CreateVersion7();
        CombatSessionSnapshot snapshot = VictorySnapshot(sessionId);

        await using GameDbContext firstContext = postgres.CreateDbContext();
        await using GameDbContext secondContext = postgres.CreateDbContext();
        CombatRewardService firstService = await CreateServiceAsync(firstContext);
        CombatRewardService secondService = await CreateServiceAsync(secondContext);

        CombatRewardApplicationResult[] results = await Task.WhenAll(
            firstService.ApplyVictoryAsync(
                characterId,
                snapshot,
                CancellationToken.None),
            secondService.ApplyVictoryAsync(
                characterId,
                snapshot,
                CancellationToken.None));

        CombatRewardApplicationResult granted = Assert.Single(
            results.Where(result => result.Granted));
        CombatRewardApplicationResult replay = Assert.Single(
            results.Where(result => !result.Granted));

        Assert.Equal(granted.XpEarned, replay.XpEarned);
        Assert.Equal(granted.GoldEarned, replay.GoldEarned);
        Assert.NotEmpty(granted.Items);

        await using GameDbContext verify = postgres.CreateDbContext();
        Character character = await verify.Characters.AsNoTracking().SingleAsync();
        Assert.Equal(granted.XpEarned, character.Experience);
        Assert.Equal(granted.GoldEarned, character.Gold);
        Assert.Equal(1, await verify.CombatRewardGrants.CountAsync());

        int persistedLootQuantity = await verify.CharacterItems
            .AsNoTracking()
            .SumAsync(item => item.Quantity);
        Assert.Equal(granted.Items.Sum(item => item.Quantity), persistedLootQuantity);
    }

    private async Task<(Guid CharacterId, Guid AccountId)> CreateCharacterAsync(
        long experience,
        decimal currentHp)
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
        Character character = new(
            characterId, accountId, Guid.CreateVersion7(), "Arthas", $"ARTHAS{characterId:N}"[..16],
            "HUMAN", "MALE", "WARRIOR", Now);
        character.SetExperience(experience);
        context.Characters.Add(character);
        context.CharacterVitals.Add(new CharacterVitals(
            characterId, currentHp, 0, Now.AddMinutes(-1), Now.AddMinutes(-1)));
        await context.SaveChangesAsync();
        return (characterId, accountId);
    }

    private static async Task<CombatRewardService> CreateServiceAsync(GameDbContext context)
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        TimeProvider timeProvider = new FixedTimeProvider(Now);
        InventoryEquipmentService inventory = new(context, content, timeProvider);
        CharacterDerivedStateService derived = new(context, content, inventory);
        return new CombatRewardService(
            context,
            content,
            derived,
            new FixedRandomFactory(),
            timeProvider);
    }

    private static CombatSessionSnapshot VictorySnapshot(Guid sessionId)
    {
        CombatActorSnapshot player = Actor(Guid.CreateVersion7(), CombatActorKind.Player, "WARRIOR", "Arthas");
        CombatActorSnapshot enemy = Actor(Guid.CreateVersion7(), CombatActorKind.Monster, "WOLF", "Wolf");
        return new CombatSessionSnapshot(sessionId, 1, CombatSessionStatus.Victory, Now, player, enemy);
    }

    private static CombatActorSnapshot Actor(
        Guid id,
        CombatActorKind kind,
        string definitionId,
        string name) =>
        new(id, kind, definitionId, name, 0, 100, "NONE", 0, 0, false,
            null, new Dictionary<string, DateTimeOffset>(), new HashSet<string>(), [], []);

    private sealed class FixedRandomFactory : IGameRandomFactory
    {
        public IGameRandom Create() => new SequenceGameRandom(0, 0, 0, 0, 0, 0);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
