using System.Text.Json;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Contribution;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Core.Progression;
using Elyndor.Core.World;
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
        Assert.Equal(30, character.Experience);
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
        Assert.Equal(40, character.Experience);
        Assert.Equal(1, await context.CombatRewardGrants.CountAsync());
    }

    [Fact]
    public async Task MultiEnemyVictoryAggregatesEveryDefeatedEnemyOnce()
    {
        (Guid characterId, _) = await CreateCharacterAsync(0, 100, level: 2);
        Guid sessionId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        CombatRewardService service = await CreateServiceAsync(context);

        CombatRewardApplicationResult result = await service.ApplyVictoryAsync(
            characterId,
            MultiEnemyVictorySnapshot(sessionId),
            CancellationToken.None);

        Character character = await context.Characters.AsNoTracking().SingleAsync();
        CombatRewardGrant grant = await context.CombatRewardGrants
            .AsNoTracking()
            .SingleAsync();
        CombatRewardSourceAudit[] sources =
            JsonSerializer.Deserialize<CombatRewardSourceAudit[]>(
                grant.RewardSourcesJson)
            ?? [];

        Assert.True(result.Granted);
        Assert.Equal(100, result.XpEarned);
        Assert.Equal(9, result.GoldEarned);
        Assert.Equal(100, character.Experience);
        Assert.Equal(9, character.Gold);
        Assert.Contains(result.Items, item => item.ItemId == "WOLF_HIDE");
        Assert.Contains(result.Items, item => item.ItemId == "BOAR_HIDE");
        Assert.Contains(result.Items, item => item.ItemId == "WOLF_FANG");
        Assert.Contains(result.Items, item => item.ItemId == "BOAR_TUSK");
        Assert.Equal(2, sources.Length);
        Assert.Equal(["FOREST_WOLF_L1", "FOREST_BOAR_L2"], sources.Select(source => source.MonsterId));
        Assert.Equal([0, 1], sources.Select(source => source.EncounterOrder));
        Assert.Equal([40, 60], sources.Select(source => source.XpEarned));
        Assert.Equal([4, 5], sources.Select(source => source.GoldEarned));
        Assert.All(sources, source => Assert.NotEmpty(source.Items));
    }

    [Fact]
    public async Task MultiEnemyVictoryReplayDoesNotDuplicateAnySourceReward()
    {
        (Guid characterId, _) = await CreateCharacterAsync(0, 100, level: 2);
        Guid sessionId = Guid.CreateVersion7();
        CombatSessionSnapshot snapshot = MultiEnemyVictorySnapshot(sessionId);
        await using GameDbContext context = postgres.CreateDbContext();
        CombatRewardService service = await CreateServiceAsync(context);

        CombatRewardApplicationResult first = await service.ApplyVictoryAsync(
            characterId,
            snapshot,
            CancellationToken.None);
        CombatRewardApplicationResult replay = await service.ApplyVictoryAsync(
            characterId,
            snapshot,
            CancellationToken.None);

        Character character = await context.Characters.AsNoTracking().SingleAsync();
        Assert.True(first.Granted);
        Assert.False(replay.Granted);
        Assert.Equal(first.XpEarned, replay.XpEarned);
        Assert.Equal(first.GoldEarned, replay.GoldEarned);
        Assert.Equal(100, character.Experience);
        Assert.Equal(9, character.Gold);
        Assert.Equal(1, await context.CombatRewardGrants.CountAsync());

        int persistedLootQuantity = await context.CharacterItems
            .AsNoTracking()
            .SumAsync(item => item.Quantity);
        Assert.Equal(first.Items.Sum(item => item.Quantity), persistedLootQuantity);
    }

    [Fact]
    public async Task VictoryRewardRejectsDuplicateEnemyActorIdsBeforeMutation()
    {
        (Guid characterId, _) = await CreateCharacterAsync(0, 100);
        await using GameDbContext context = postgres.CreateDbContext();
        CombatRewardService service = await CreateServiceAsync(context);
        CombatSessionSnapshot snapshot = MultiEnemyVictorySnapshot(Guid.CreateVersion7());
        CombatActorSnapshot duplicate = snapshot.Enemies![0];
        snapshot = snapshot with { Enemies = [duplicate, duplicate] };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApplyVictoryAsync(
                characterId,
                snapshot,
                CancellationToken.None));

        Character character = await context.Characters.AsNoTracking().SingleAsync();
        Assert.Equal(0, character.Experience);
        Assert.Equal(0, character.Gold);
        Assert.Equal(0, await context.CombatRewardGrants.CountAsync());
        Assert.Equal(0, await context.CharacterItems.CountAsync());
    }

    [Fact]
    public async Task VictoryRewardRejectsLivingEnemyBeforeMutation()
    {
        (Guid characterId, _) = await CreateCharacterAsync(0, 100);
        await using GameDbContext context = postgres.CreateDbContext();
        CombatRewardService service = await CreateServiceAsync(context);
        CombatSessionSnapshot snapshot = MultiEnemyVictorySnapshot(Guid.CreateVersion7());
        CombatActorSnapshot living = snapshot.Enemies![1] with { Hp = 1 };
        snapshot = snapshot with { Enemies = [snapshot.Enemies[0], living] };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApplyVictoryAsync(
                characterId,
                snapshot,
                CancellationToken.None));

        Character character = await context.Characters.AsNoTracking().SingleAsync();
        Assert.Equal(0, character.Experience);
        Assert.Equal(0, character.Gold);
        Assert.Equal(0, await context.CombatRewardGrants.CountAsync());
        Assert.Equal(0, await context.CharacterItems.CountAsync());
    }

    [Fact]
    public async Task FullInventoryPersistsOverflowAsPendingLoot()
    {
        (Guid characterId, _) = await CreateCharacterAsync(0, 100);
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            for (var index = 0; index < 40; index++)
            {
                setup.CharacterItems.Add(new CharacterItem(
                    Guid.CreateVersion7(),
                    characterId,
                    "RECRUIT_IRON_SWORD",
                    1,
                    Now));
            }
            await setup.SaveChangesAsync();
        }

        Guid sessionId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        CombatRewardService service = await CreateServiceAsync(context);
        CombatRewardApplicationResult result = await service.ApplyVictoryAsync(
            characterId,
            VictorySnapshot(sessionId),
            CancellationToken.None);

        Assert.True(result.Granted);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(
            40,
            await InventoryCapacity.CountUsedSlotsAsync(
                verify,
                characterId,
                CancellationToken.None));
        PendingLootItem[] pending = await verify.PendingLootItems
            .AsNoTracking()
            .Where(item => item.CharacterId == characterId)
            .ToArrayAsync();
        Assert.NotEmpty(pending);
        Assert.All(
            pending,
            item => Assert.Equal(sessionId, item.RewardResolutionId));
        Assert.Equal(
            result.Items.Sum(item => item.Quantity),
            pending.Sum(item => item.Quantity));
        Assert.Equal(1, await verify.CombatRewardGrants.CountAsync());
    }

    [Fact]
    public async Task WarriorPersonalLootCanContainOffClassEquipment()
    {
        (Guid characterId, _) = await CreateCharacterAsync(0, 100);
        await using GameDbContext context = postgres.CreateDbContext();
        CombatRewardService service = await CreateServiceAsync(context);

        CombatRewardApplicationResult result = await service.ApplyVictoryAsync(
            characterId,
            VictorySnapshot(Guid.CreateVersion7()),
            CancellationToken.None);

        Assert.Contains(result.Items, item => item.ItemId == "HUNTER_SHORTBOW");
        Assert.Contains(result.Items, item => item.ItemId == "RANGER_TRAIL_LEGGINGS");
        Assert.Contains(result.Items, item => item.ItemId == "WOLF_HIDE");
        Assert.Contains(result.Items, item => item.ItemId == "WOLF_FANG");
    }

    [Fact]
    public async Task AcceptedBossContractCompletesExactlyOnceAndAddsContractReward()
    {
        (Guid characterId, _) = await CreateCharacterAsync(0, 100, level: 14);
        Guid sessionId = Guid.CreateVersion7();
        CombatSessionSnapshot snapshot = VictorySnapshot(
            sessionId,
            "SPIDER_BROODMOTHER_L14");
        await using GameDbContext context = postgres.CreateDbContext();
        context.CharacterContractAcceptances.Add(
            new CharacterContractAcceptance(
                characterId,
                "CONTRACT_BROODMOTHER_GATE",
                Now.AddMinutes(-5)));
        await context.SaveChangesAsync();
        CombatRewardService service = await CreateServiceAsync(context);

        CombatRewardApplicationResult first = await service.ApplyVictoryAsync(
            characterId,
            snapshot,
            CancellationToken.None);
        CombatRewardApplicationResult replay = await service.ApplyVictoryAsync(
            characterId,
            snapshot,
            CancellationToken.None);

        Assert.True(first.Granted);
        Assert.False(replay.Granted);
        Assert.Equal(10_500, first.XpEarned);
        Assert.Equal(240, first.GoldEarned);
        Assert.Contains(
            "CONTRACT_BROODMOTHER_GATE",
            first.CompletedContractIds ?? []);

        CharacterContractCompletion completion = await context.CharacterContractCompletions
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal("CONTRACT_BROODMOTHER_GATE", completion.ContractId);
        Assert.Equal("SPIDER_BROODMOTHER_L14", completion.TargetMonsterId);
        Assert.Equal(sessionId, completion.CombatSessionId);
    }

    [Fact]
    public async Task BossKillWithoutAcceptedContractDoesNotUnlockGateOrGrantContractReward()
    {
        (Guid characterId, _) = await CreateCharacterAsync(0, 100, level: 14);
        await using GameDbContext context = postgres.CreateDbContext();
        CombatRewardService service = await CreateServiceAsync(context);

        CombatRewardApplicationResult result = await service.ApplyVictoryAsync(
            characterId,
            VictorySnapshot(Guid.CreateVersion7(), "SPIDER_BROODMOTHER_L14"),
            CancellationToken.None);

        Assert.True(result.Granted);
        Assert.Equal(8_500, result.XpEarned);
        Assert.Equal(90, result.GoldEarned);
        Assert.Empty(result.CompletedContractIds ?? []);
        Assert.Empty(await context.CharacterContractCompletions.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task ConcurrentSameSessionGrantsXpGoldAndLootExactlyOnce()
    {
        (Guid characterId, _) = await CreateCharacterAsync(0, 100, level: 2);
        Guid sessionId = Guid.CreateVersion7();
        CombatSessionSnapshot snapshot = MultiEnemyVictorySnapshot(sessionId);

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
            results,
            result => result.Granted);
        CombatRewardApplicationResult replay = Assert.Single(
            results,
            result => !result.Granted);

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

    [Fact]
    public async Task ValuableGroupLootIsVisibleToEveryEligibleParticipantAndCreatedOnce()
    {
        (Guid firstCharacterId, _) = await CreateCharacterAsync(
            0,
            100,
            level: 14,
            name: "ArthasOwner");
        (Guid secondCharacterId, _) = await CreateCharacterAsync(
            0,
            100,
            level: 14,
            name: "ArthasOther");
        Guid ownerCharacterId = firstCharacterId.CompareTo(secondCharacterId) < 0
            ? firstCharacterId
            : secondCharacterId;
        Guid otherCharacterId = ownerCharacterId == firstCharacterId
            ? secondCharacterId
            : firstCharacterId;
        CombatSessionSnapshot snapshot = MultiplayerBossVictorySnapshot(
            Guid.CreateVersion7(),
            ownerCharacterId,
            otherCharacterId);

        await using GameDbContext context = postgres.CreateDbContext();
        CombatRewardService service = await CreateServiceAsync(context);

        CombatRewardApplicationResult otherResult = await service.ApplyVictoryAsync(
            otherCharacterId,
            snapshot,
            CancellationToken.None);

        Assert.Single(otherResult.LootRolls ?? []);
        Assert.Single(await context.CombatLootRolls.AsNoTracking().ToArrayAsync());

        CombatRewardApplicationResult ownerResult = await service.ApplyVictoryAsync(
            ownerCharacterId,
            snapshot,
            CancellationToken.None);

        Assert.Single(ownerResult.LootRolls ?? []);
        Assert.DoesNotContain(
            ownerResult.Items,
            item => item.ItemId == "BROODMOTHER_FANG_CHARM");
        Assert.Equal(1, await context.CombatLootRolls.CountAsync());
    }

    [Fact]
    public async Task SharedValuableLootIsRolledOnceWhenOnlyTheFirstPersonalRollFindsIt()
    {
        (Guid firstCharacterId, _) = await CreateCharacterAsync(
            0,
            100,
            level: 14,
            name: "FirstRoller");
        (Guid secondCharacterId, _) = await CreateCharacterAsync(
            0,
            100,
            level: 14,
            name: "SecondRoller");

        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        const string lootTableId = "TEST_SHARED_VALUABLE_LOOT";
        content = content with
        {
            Monsters = content.Monsters!
                .Select(monster => monster.Id == "SPIDER_BROODMOTHER_L14"
                    ? monster with { LootTableId = lootTableId }
                    : monster)
                .ToArray(),
            LootTables =
            [
                new LootTableDefinition(
                    lootTableId,
                    [new LootTableEntry(
                        "BROODMOTHER_FANG_CHARM",
                        0.5m,
                        1,
                        1)])
            ]
        };

        Guid sessionId = Guid.CreateVersion7();
        CombatSessionSnapshot snapshot = MultiplayerBossVictorySnapshot(
            sessionId,
            firstCharacterId,
            secondCharacterId);
        await using GameDbContext context = postgres.CreateDbContext();
        CombatRewardService service = await CreateServiceAsync(
            context,
            content,
            new QueueGameRandomFactory(
                new SequenceGameRandom(0.99m),
                new SequenceGameRandom(0m),
                new SequenceGameRandom(0m),
                new SequenceGameRandom(0.99m),
                new SequenceGameRandom(0.99m)));

        CombatRewardApplicationResult first = await service.ApplyVictoryAsync(
            firstCharacterId,
            snapshot,
            CancellationToken.None);
        CombatRewardApplicationResult second = await service.ApplyVictoryAsync(
            secondCharacterId,
            snapshot,
            CancellationToken.None);

        Assert.Single(first.LootRolls ?? []);
        Assert.Single(second.LootRolls ?? []);
        Assert.Equal(first.LootRolls![0].LootRollId, second.LootRolls![0].LootRollId);
        Assert.Empty(first.Items);
        Assert.Empty(second.Items);
        Assert.Single(await context.CombatSharedLootResolutions.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task OpenLootRollReportsNeedAvailabilityForCurrentCharacter()
    {
        (Guid characterId, Guid accountId) = await CreateCharacterAsync(
            0,
            100,
            classId: "MAGE",
            name: "Merlin");
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.CombatLootRolls.Add(new CombatLootRoll(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                "RECRUIT_IRON_SWORD",
                ItemRarity.Rare,
                1,
                Guid.CreateVersion7(),
                Now.AddSeconds(25),
                JsonSerializer.Serialize(new[] { characterId })));
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        CombatLootRollService service = await CreateLootRollServiceAsync(context);

        IReadOnlyList<CombatLootRollAvailability> rolls = await service.GetOpenAsync(
            accountId,
            CancellationToken.None);

        CombatLootRollAvailability roll = Assert.Single(rolls);
        Assert.False(roll.CanNeed);
    }

    [Fact]
    public async Task IneligibleCharacterCannotReadLootRollFromChoiceResponse()
    {
        (Guid eligibleCharacterId, _) = await CreateCharacterAsync(
            0,
            100,
            classId: "WARRIOR",
            name: "EligibleRoller");
        (_, Guid ineligibleAccountId) = await CreateCharacterAsync(
            0,
            100,
            classId: "MAGE",
            name: "IneligibleRoller");

        Guid lootRollId = Guid.CreateVersion7();
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.CombatLootRolls.Add(new CombatLootRoll(
                lootRollId,
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                "RECRUIT_IRON_SWORD",
                ItemRarity.Rare,
                1,
                Guid.CreateVersion7(),
                Now.AddSeconds(25),
                JsonSerializer.Serialize(new[] { eligibleCharacterId })));
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        CombatLootRollService service = await CreateLootRollServiceAsync(context);

        CombatLootRollChoiceResult result = await service.ChooseAsync(
            ineligibleAccountId,
            lootRollId,
            LootChoice.Pass,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("loot_roll_not_eligible", result.ErrorCode);
        Assert.Null(result.Roll);
        Assert.Null(result.WinnerCharacterId);
        Assert.Single(await context.CombatLootRolls.AsNoTracking().ToArrayAsync());
    }

    private async Task<(Guid CharacterId, Guid AccountId)> CreateCharacterAsync(
        long experience,
        decimal currentHp,
        int level = 1,
        string name = "Arthas",
        string classId = "WARRIOR")
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
        Character character = new(
            characterId,
            accountId,
            Guid.CreateVersion7(),
            name,
            $"{name.ToUpperInvariant()}{characterId:N}"[..16],
            "HUMAN", "MALE", classId, Now);
        character.SetLevel(level);
        character.SetExperience(experience);
        context.Characters.Add(character);
        context.CharacterVitals.Add(new CharacterVitals(
            characterId, currentHp, 0, Now.AddMinutes(-1), Now.AddMinutes(-1)));
        await context.SaveChangesAsync();
        return (characterId, accountId);
    }

    private static async Task<CombatRewardService> CreateServiceAsync(
        GameDbContext context,
        GameContentPackage? contentOverride = null,
        IGameRandomFactory? randomFactory = null)
    {
        GameContentPackage content = contentOverride ?? await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        TimeProvider timeProvider = new FixedTimeProvider(Now);
        InventoryEquipmentService inventory = new(context, content, timeProvider);
        CharacterDerivedStateService derived = new(context, content, inventory);
        return new CombatRewardService(
            context,
            content,
            derived,
            randomFactory ?? new FixedRandomFactory(),
            timeProvider);
    }

    private static async Task<CombatLootRollService> CreateLootRollServiceAsync(
        GameDbContext context)
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        TimeProvider timeProvider = new FixedTimeProvider(Now);
        InventoryEquipmentService inventory = new(context, content, timeProvider);
        CharacterDerivedStateService derived = new(context, content, inventory);
        return new CombatLootRollService(
            context,
            new StaticContentSnapshotProvider(content),
            derived,
            new FixedRandomFactory(),
            timeProvider);
    }

    private static CombatSessionSnapshot MultiEnemyVictorySnapshot(Guid sessionId)
    {
        CombatActorSnapshot player = Actor(
            Guid.CreateVersion7(),
            CombatActorKind.Player,
            "WARRIOR",
            "Arthas");
        CombatActorSnapshot wolf = Actor(
            Guid.Parse("71000000-0000-0000-0000-000000000001"),
            CombatActorKind.Monster,
            "FOREST_WOLF_L1",
            "Forest Wolf");
        CombatActorSnapshot boar = Actor(
            Guid.Parse("72000000-0000-0000-0000-000000000001"),
            CombatActorKind.Monster,
            "FOREST_BOAR_L2",
            "Forest Boar");
        return new CombatSessionSnapshot(
            sessionId,
            10,
            CombatSessionStatus.Victory,
            Now,
            player,
            wolf,
            Enemies: [wolf, boar],
            SelectedTargetActorId: wolf.ActorId);
    }

    private static CombatSessionSnapshot VictorySnapshot(
        Guid sessionId,
        string monsterId = "FOREST_WOLF_L1")
    {
        CombatActorSnapshot player = Actor(
            Guid.CreateVersion7(),
            CombatActorKind.Player,
            "WARRIOR",
            "Arthas");
        CombatActorSnapshot enemy = Actor(
            Guid.CreateVersion7(),
            CombatActorKind.Monster,
            monsterId,
            monsterId);
        return new CombatSessionSnapshot(
            sessionId,
            1,
            CombatSessionStatus.Victory,
            Now,
            player,
            enemy);
    }

    private static CombatSessionSnapshot MultiplayerBossVictorySnapshot(
        Guid sessionId,
        Guid ownerCharacterId,
        Guid otherCharacterId)
    {
        CombatActorSnapshot owner = Actor(
            ownerCharacterId,
            CombatActorKind.Player,
            "WARRIOR",
            "Owner");
        CombatActorSnapshot other = Actor(
            otherCharacterId,
            CombatActorKind.Player,
            "WARRIOR",
            "Other");
        CombatActorSnapshot enemy = Actor(
            Guid.CreateVersion7(),
            CombatActorKind.Monster,
            "SPIDER_BROODMOTHER_L14",
            "Broodmother");
        ContributionSnapshot ownerContribution = new(
            ownerCharacterId,
            Now.AddMinutes(-1),
            null,
            null,
            1,
            100,
            0,
            0,
            0);
        ContributionSnapshot otherContribution = ownerContribution with
        {
            CharacterId = otherCharacterId
        };

        return new CombatSessionSnapshot(
            sessionId,
            10,
            CombatSessionStatus.Victory,
            Now,
            owner,
            enemy,
            Enemies: [enemy],
            SelectedTargetActorId: enemy.ActorId,
            Players: [owner, other],
            ParticipantContributions:
            [
                new(true, "qualified", 100, ownerContribution),
                new(true, "qualified", 100, otherContribution)
            ]);
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
        public IGameRandom Create() =>
            new SequenceGameRandom(Enumerable.Repeat(0m, 64).ToArray());
    }

    private sealed class QueueGameRandomFactory(params IGameRandom[] randoms) : IGameRandomFactory
    {
        private int _index;

        public IGameRandom Create()
        {
            if (_index >= randoms.Length)
                throw new InvalidOperationException("The deterministic RNG queue is exhausted.");

            return randoms[_index++];
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
