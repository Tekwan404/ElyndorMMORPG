using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Core.Quests;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Quests;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Quests;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class QuestServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 8, 8, 45, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task LevelOneToTwentyContentFormsSingleValidatedChain()
    {
        GameContentPackage content = await LoadContentAsync();
        IReadOnlyList<QuestDefinition> quests = QuestCatalog.Resolve(content);

        Assert.Equal(20, quests.Count);
        Assert.Equal("QUEST_01_FIRST_HUNT", quests[0].Id);
        Assert.Equal("CONTRACT_BROODMOTHER_GATE", quests.Single(q => q.RequiredLevel == 14).Id);
        Assert.Equal("QUEST_20_BLIGHTED_ALPHA", quests[^1].Id);

        for (var index = 1; index < quests.Count; index++)
        {
            Assert.Contains(
                quests[index - 1].Id,
                quests[index].PrerequisiteQuestIds ?? []);
        }
    }

    [Fact]
    public async Task AcceptProgressClaimAndReplayAreDurable()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(
            level: 1,
            locationId: "WHISPERING_FOREST");

        await using GameDbContext context = postgres.CreateDbContext();
        (QuestService service, GameContentPackage content) =
            await CreateServiceAsync(context);

        QuestJournalSnapshot before = await service.GetAsync(
            accountId,
            CancellationToken.None);
        Assert.Equal(
            "AVAILABLE",
            before.Quests.Single(q => q.Id == "QUEST_01_FIRST_HUNT").Status);

        QuestMutationResult accepted = await service.AcceptAsync(
            accountId,
            "QUEST_01_FIRST_HUNT",
            CancellationToken.None);
        Assert.True(accepted.IsSuccess);

        QuestProgressUpdateResult progressed =
            await QuestProgression.ApplyKillsAsync(
                context,
                characterId,
                Guid.CreateVersion7(),
                ["FOREST_WOLF_L1", "FOREST_WOLF_L1", "FOREST_WOLF_L1"],
                content,
                Now,
                CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.Contains("QUEST_01_FIRST_HUNT", progressed.ReadyQuestIds);
        QuestJournalSnapshot ready = await service.GetAsync(
            accountId,
            CancellationToken.None);
        Assert.Equal(
            QuestStateStatuses.ReadyToClaim,
            ready.Quests.Single(q => q.Id == "QUEST_01_FIRST_HUNT").Status);

        Guid mutationId = Guid.CreateVersion7();
        QuestClaimResult first = await service.ClaimAsync(
            accountId,
            "QUEST_01_FIRST_HUNT",
            mutationId,
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(first.Granted);
        Assert.Equal(90, first.XpEarned);
        Assert.Equal(1, await context.QuestRewardGrants.CountAsync());

        long experienceAfterFirst = await context.Characters
            .AsNoTracking()
            .Where(character => character.Id == characterId)
            .Select(character => character.Experience)
            .SingleAsync();

        QuestClaimResult replay = await service.ClaimAsync(
            accountId,
            "QUEST_01_FIRST_HUNT",
            mutationId,
            CancellationToken.None);

        Assert.True(replay.IsSuccess);
        Assert.False(replay.Granted);
        Assert.Equal(90, replay.XpEarned);
        Assert.Equal(1, await context.QuestRewardGrants.CountAsync());
        Assert.Equal(
            experienceAfterFirst,
            await context.Characters
                .AsNoTracking()
                .Where(character => character.Id == characterId)
                .Select(character => character.Experience)
                .SingleAsync());

        await using GameDbContext reloadedContext = postgres.CreateDbContext();
        (QuestService reloadedService, _) = await CreateServiceAsync(reloadedContext);
        QuestJournalSnapshot persisted = await reloadedService.GetAsync(
            accountId,
            CancellationToken.None);
        Assert.Equal(
            QuestStateStatuses.Completed,
            persisted.Quests.Single(q => q.Id == "QUEST_01_FIRST_HUNT").Status);
    }

    [Fact]
    public async Task CollectQuestUsesInventoryOwnershipAndConsumesTurnInItems()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(
            level: 2,
            locationId: "WHISPERING_FOREST");

        await using GameDbContext context = postgres.CreateDbContext();
        GameContentPackage content = await LoadContentAsync();
        context.QuestRewardGrants.Add(new QuestRewardGrant(
            characterId,
            "QUEST_01_FIRST_HUNT",
            Guid.CreateVersion7(),
            0,
            0,
            "[]",
            Now));
        context.CharacterItems.Add(new CharacterItem(
            Guid.CreateVersion7(),
            characterId,
            "WOLF_HIDE",
            4,
            Now,
            content.Items!.Single(item => item.Id == "WOLF_HIDE").Version,
            null));
        await context.SaveChangesAsync();

        (QuestService service, _) = await CreateServiceAsync(context);
        QuestJournalSnapshot available = await service.GetAsync(
            accountId,
            CancellationToken.None);
        Assert.Equal(
            "AVAILABLE",
            available.Quests.Single(q => q.Id == "QUEST_02_WOLF_HIDES").Status);

        Assert.True((await service.AcceptAsync(
            accountId,
            "QUEST_02_WOLF_HIDES",
            CancellationToken.None)).IsSuccess);

        QuestJournalSnapshot ready = await service.GetAsync(
            accountId,
            CancellationToken.None);
        QuestJournalEntry quest =
            ready.Quests.Single(q => q.Id == "QUEST_02_WOLF_HIDES");
        Assert.Equal(QuestStateStatuses.ReadyToClaim, quest.Status);
        Assert.Equal(4, quest.Objectives.Single().CurrentCount);

        QuestClaimResult claim = await service.ClaimAsync(
            accountId,
            "QUEST_02_WOLF_HIDES",
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.True(claim.IsSuccess);
        Assert.True(claim.Granted);
        Assert.False(await context.CharacterItems
            .AsNoTracking()
            .AnyAsync(item => item.CharacterId == characterId
                && item.ItemDefinitionId == "WOLF_HIDE"));
        Assert.Equal(
            2,
            await context.CharacterItems
                .AsNoTracking()
                .Where(item => item.CharacterId == characterId
                    && item.ItemDefinitionId == "SMALL_HEALING_POTION")
                .SumAsync(item => item.Quantity));
    }

    [Fact]
    public async Task BroodmotherContractRequiresChainAndMirrorsLegacyWorldGate()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(
            level: 14,
            locationId: "BROODMOTHER_LAIR");

        await using GameDbContext context = postgres.CreateDbContext();
        (QuestService service, GameContentPackage content) =
            await CreateServiceAsync(context);

        QuestJournalSnapshot locked = await service.GetAsync(
            accountId,
            CancellationToken.None);
        Assert.Equal(
            "LOCKED",
            locked.Quests.Single(q => q.Id == "CONTRACT_BROODMOTHER_GATE").Status);

        string[] prerequisites =
        [
            "QUEST_01_FIRST_HUNT",
            "QUEST_02_WOLF_HIDES",
            "QUEST_03_BOAR_TRAIL",
            "QUEST_04_SILKEN_THREAT",
            "QUEST_05_ALPHA_OF_WHISPERS",
            "QUEST_06_DEEPER_TRACKS",
            "QUEST_07_CORRUPTED_TUSKS",
            "QUEST_08_VENOM_IN_DARK",
            "QUEST_09_GOBLIN_SCOUTS",
            "QUEST_10_PACK_PRESSURE",
            "QUEST_11_OLD_ALPHA",
            "QUEST_12_FANG_PROOF",
            "QUEST_13_BROODMOTHER_TRACE"
        ];
        foreach (string prerequisite in prerequisites)
        {
            context.QuestRewardGrants.Add(new QuestRewardGrant(
                characterId,
                prerequisite,
                Guid.CreateVersion7(),
                0,
                0,
                "[]",
                Now));
        }
        await context.SaveChangesAsync();

        QuestJournalSnapshot available = await service.GetAsync(
            accountId,
            CancellationToken.None);
        Assert.Equal(
            "AVAILABLE",
            available.Quests.Single(q => q.Id == "CONTRACT_BROODMOTHER_GATE").Status);

        Assert.True((await service.AcceptAsync(
            accountId,
            "CONTRACT_BROODMOTHER_GATE",
            CancellationToken.None)).IsSuccess);
        Assert.True(await context.CharacterContractAcceptances.AnyAsync(
            acceptance => acceptance.CharacterId == characterId
                && acceptance.ContractId == "CONTRACT_BROODMOTHER_GATE"));

        Guid combatSessionId = Guid.CreateVersion7();
        QuestProgressUpdateResult progress =
            await QuestProgression.ApplyKillsAsync(
                context,
                characterId,
                combatSessionId,
                ["SPIDER_BROODMOTHER_L14"],
                content,
                Now,
                CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.Contains("CONTRACT_BROODMOTHER_GATE", progress.ReadyQuestIds);
        Assert.Contains(
            "CONTRACT_BROODMOTHER_GATE",
            progress.CompletedLegacyContractIds);
        Assert.True(await context.CharacterContractCompletions.AnyAsync(
            completion => completion.CharacterId == characterId
                && completion.ContractId == "CONTRACT_BROODMOTHER_GATE"
                && completion.CombatSessionId == combatSessionId));

        QuestJournalSnapshot ready = await service.GetAsync(
            accountId,
            CancellationToken.None);
        Assert.Equal(
            QuestStateStatuses.ReadyToClaim,
            ready.Quests.Single(q => q.Id == "CONTRACT_BROODMOTHER_GATE").Status);

        QuestClaimResult claimed = await service.ClaimAsync(
            accountId,
            "CONTRACT_BROODMOTHER_GATE",
            Guid.CreateVersion7(),
            CancellationToken.None);
        Assert.True(claimed.IsSuccess);
        Assert.True(claimed.Granted);
    }

    private async Task<(Guid AccountId, Guid CharacterId)> CreateCharacterAsync(
        int level,
        string locationId)
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(
            accountId,
            Random.Shared.NextInt64(1, long.MaxValue),
            Now));
        Character character = new(
            characterId,
            accountId,
            Guid.CreateVersion7(),
            "QuestHero",
            $"QUEST{characterId:N}"[..16],
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now);
        character.SetLevel(level);
        context.Characters.Add(character);
        context.CharacterVitals.Add(new CharacterVitals(
            characterId,
            250,
            0,
            Now,
            Now));
        context.CharacterLocations.Add(
            new CharacterLocation(characterId, locationId, 1, Now));
        await context.SaveChangesAsync();
        return (accountId, characterId);
    }

    private static async Task<(QuestService Service, GameContentPackage Content)>
        CreateServiceAsync(GameDbContext context)
    {
        GameContentPackage content = await LoadContentAsync();
        StaticContentSnapshotProvider provider = new(content);
        CharacterDerivedStateService derivedState = new(
            context,
            content,
            null);
        QuestService service = new(
            context,
            provider,
            derivedState,
            new SystemGameRandomFactory(),
            new FixedTimeProvider(Now));
        return (service, content);
    }

    private static Task<GameContentPackage> LoadContentAsync() =>
        GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
