using System.Text.Json;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Core.Talents;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Infrastructure.Administration;
using Elyndor.Server.Administration;
using Elyndor.Server.Combat;
using Microsoft.Extensions.Logging.Abstractions;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Administration;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class CharacterBuildSnapshotTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly ItemDefinition Hat = new("TEST_HAT", "Test Hat", ItemType.Equipment, ItemRarity.Epic,
        1, false, 1, EquipmentSlot.Head, new(0, 0, 10, 0), "Hat", SetId: "TEST_SET",
        CriticalChancePercent: 120, MagicPenetrationPercent: 118.4m, ArmorCategory: EquipmentCategoryIds.Cloth);
    private static readonly ItemDefinition Artifact = new("TEST_BAG", "Test Bag", ItemType.SpatialArtifact,
        ItemRarity.Rare, 1, false, 1, null, new(0, 0, 0, 0), "Bag", InventoryCapacityBonus: 10);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task BuildDiffShowsRawAndEffectiveDeltasAndRemovedGearTalentsAndArtifact()
    {
        await SeedAsync();
        await using var context = postgres.CreateDbContext();
        var before = (await CreateService(context).CaptureForTelegramAsync(123456, CancellationToken.None))!;
        var stats = new Dictionary<string, BuildStat>(before.Stats)
        {
            ["magicPenetration"] = new(68.5m, 68.5m, "%")
        };
        var after = before with { Stats = stats, Equipment = [], SpatialArtifact = null, Talents = [],
            Sets = [], AbilityPanel = [], TalentAbilityIds = [], Abilities = [] };
        string report = CharacterBuildDiffFormatter.Format(before, after);
        Assert.Contains("118.4% → 68.5% (-49.9%)", report);
        Assert.Contains("100% → 68.5% (-31.5%)", report);
        Assert.Contains("HEAD", report.ToUpperInvariant());
        Assert.Contains("TEST_HAT", report);
        Assert.Contains("TEST_BAG", report);
        Assert.Contains("TEST_INTELLECT: 2 → 0", report);
        Assert.Contains("TEST_SET", report);
        Assert.Contains("TEST_SPELL", report);
    }

    [Fact]
    public async Task BuildDiffReportsNoChangesForJsonbRoundTripAndTracksSameItemEnhancementAndRankChanges()
    {
        await SeedAsync();
        await using var context = postgres.CreateDbContext();
        var service = CreateService(context);
        var before = (await service.CaptureForTelegramAsync(123456, CancellationToken.None))!;
        var restored = (await service.GetAsync(before.BuildHash, CancellationToken.None))!;
        Assert.Contains("Изменений нет", CharacterBuildDiffFormatter.Format(before, restored));
        var after = before with
        {
            Equipment = [before.Equipment[0] with { EnhancementLevel = 5 }],
            Talents = [before.Talents[0] with { Rank = 3 }], BalanceVersion = "changed"
        };
        string report = CharacterBuildDiffFormatter.Format(before, after);
        Assert.Contains("EnhancementLevel: 0 → 5 (+5)", report);
        Assert.Contains("TEST_INTELLECT: 2 → 3", report);
        Assert.Contains("balance", report);
        Assert.DoesNotContain("Изменений нет", report);
    }

    [Fact]
    public async Task CaptureIncludesRawCapsRealEquipmentArtifactSetsAndSelectedTalentsWithoutMutatingCharacter()
    {
        var seeded = await SeedAsync();
        await using var context = postgres.CreateDbContext();
        var service = CreateService(context);
        var build = await service.CaptureForTelegramAsync(123456, CancellationToken.None);
        Assert.NotNull(build);
        Assert.Equal("MageTester", build.Name);
        Assert.Equal(seeded.CharacterId, build.CharacterId);
        Assert.Equal(118.4m, build.Stats["magicPenetration"].Raw);
        Assert.Equal(100, build.Stats["magicPenetration"].Effective);
        Assert.True(build.Stats["criticalChance"].Raw > 120);
        Assert.Equal(60, build.Stats["criticalChance"].Effective);
        Assert.Equal(seeded.ItemId, Assert.Single(build.Equipment).ItemInstanceId);
        Assert.Equal(10, build.Equipment[0].BaseDefinition.Stats.Intellect);
        Assert.Equal("TEST_BAG", build.SpatialArtifact!.BaseDefinition.Id);
        Assert.Single(build.Sets[0].ActiveBonuses);
        Assert.Equal(2, Assert.Single(build.Talents).Rank);
        Assert.Equal("TEST_INTELLECT", build.Talents[0].Definition.Id);
        Assert.Contains("TEST_SPELL", build.TalentAbilityIds);
        Assert.Contains("TEST_SPELL", build.AbilityPanel);
        Assert.Equal("TEST_SPELL", Assert.Single(build.Abilities).Id);
        var character = await context.Characters.AsNoTracking().SingleAsync();
        Assert.Equal(1, character.Level);
        Assert.Equal(0, character.Gold);
        Assert.Empty(await context.CharacterMutations.ToArrayAsync());
        Assert.Contains("Пробивание магии: 118.4% (эффективно 100%)", CharacterBuildSnapshotFormatter.Format(build));
    }

    [Fact]
    public async Task SameBuildAndJsonbRoundTripKeepHashesStableAndArchiveDeduplicates()
    {
        await SeedAsync();
        await using var context = postgres.CreateDbContext();
        var service = CreateService(context);
        var first = (await service.CaptureForTelegramAsync(123456, CancellationToken.None))!;
        var second = (await service.CaptureForTelegramAsync(123456, CancellationToken.None))!;
        var restored = (await service.GetAsync(first.BuildHash, CancellationToken.None))!;
        Assert.Equal(first.BuildHash, second.BuildHash);
        Assert.Equal(first.BuildHash, restored.BuildHash);
        Assert.Equal(first.EquipmentHash, restored.EquipmentHash);
        Assert.Equal(first.TalentHash, restored.TalentHash);
        Assert.Equal(1, await context.CharacterBuildArchives.CountAsync());
        Assert.Equal(first.BuildHash, (first with { Name = "Renamed" }).BuildHash);
        Assert.NotEqual(first.BuildHash, (first with { Level = 2 }).BuildHash);
        Assert.NotEqual(first.BuildHash, (first with { BalanceVersion = "new-balance" }).BuildHash);
        var json = JsonSerializer.Serialize(first, CharacterBuildSnapshot.JsonOptions);
        Assert.DoesNotContain("\"BuildHash\"", json);
    }

    [Fact]
    public async Task TrainingReferenceKeepsOriginalBuildAfterUnequipAndRejectsOtherAccounts()
    {
        var seeded = await SeedAsync();
        Guid sessionId = Guid.NewGuid();
        await using var context = postgres.CreateDbContext();
        var service = CreateService(context);
        var before = (await service.CaptureForTelegramAsync(123456, CancellationToken.None))!;
        await service.LinkTrainingAsync(seeded.AccountId, sessionId, before.BuildHash, CancellationToken.None);
        await context.CharacterEquipment.ExecuteDeleteAsync();
        var after = (await service.CaptureForTelegramAsync(123456, CancellationToken.None))!;
        await service.LinkTrainingAsync(seeded.AccountId, sessionId, after.BuildHash, CancellationToken.None);
        Assert.NotEqual(before.BuildHash, after.BuildHash);
        Assert.NotEqual(before.EquipmentHash, after.EquipmentHash);
        Assert.Equal(before.TalentHash, after.TalentHash);
        await using var freshContext = postgres.CreateDbContext();
        var freshService = CreateService(freshContext);
        var restored = await freshService.GetTrainingAsync(seeded.AccountId, sessionId, CancellationToken.None);
        Assert.Equal(before.BuildHash, restored!.BuildHash);
        Assert.Single(restored.Equipment);
        Assert.Null(await freshService.GetTrainingAsync(Guid.NewGuid(), sessionId, CancellationToken.None));
    }

    [Fact]
    public async Task UnknownTargetDoesNotCreateAnArchive()
    {
        await using var context = postgres.CreateDbContext();
        Assert.Null(await CreateService(context).CaptureForTelegramAsync(999, CancellationToken.None));
        Assert.Empty(await context.CharacterBuildArchives.ToArrayAsync());
    }

    [Fact]
    public async Task LargeDefenseReportsRawReductionSeparatelyFromProductionMitigationCap()
    {
        await SeedAsync();
        await using var context = postgres.CreateDbContext();
        var content = Content() with { Items = [Hat with { ArmorFlat = 90000 }, Artifact] };
        var provider = new StaticContentSnapshotProvider(content);
        var inventory = new InventoryEquipmentService(context, provider, TimeProvider.System);
        var service = new CharacterBuildSnapshotService(context, new(context, provider, inventory), provider, TimeProvider.System);
        var build = (await service.CaptureForTelegramAsync(123456, CancellationToken.None))!;
        Assert.True(build.Stats["armorDamageReductionPercent"].Raw > 99);
        Assert.Equal(60, build.Stats["armorDamageReductionPercent"].Effective);
    }

    [Fact]
    public async Task ConcurrentCaptureArchivesTheBuildOnce()
    {
        await SeedAsync();
        async Task<CharacterBuildSnapshot?> Capture()
        {
            await using var context = postgres.CreateDbContext();
            return await CreateService(context).CaptureForTelegramAsync(123456, CancellationToken.None);
        }
        var results = await Task.WhenAll(Capture(), Capture());
        Assert.Equal(results[0]!.BuildHash, results[1]!.BuildHash);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(1, await verify.CharacterBuildArchives.CountAsync());
    }

    [Fact]
    public async Task CapturePreservesEnhancementRandomAffixesAndAcceptedReforgeBeforeAndAfter()
    {
        var seeded = await SeedAsync();
        await using var context = postgres.CreateDbContext();
        var item = await context.CharacterItems.SingleAsync(item => item.Id == seeded.ItemId);
        var before = new GeneratedItemInstance(1,
            [new("AFFIX_1", "AFFIX_MAGIC", ItemStatIds.MagicPenetration, 3, 1, 5, 1, 1, false, true, 0)],
            1, 2, 3, 0.5m, 3, false, null, null, null, "Rolled Hat", 1);
        var after = before with { Affixes = [new("AFFIX_1", "AFFIX_INTELLECT", ItemStatIds.Intellect, 5, 1, 9, 1, 1, false, true, 0)] };
        item.ApplyGeneratedInstance(after, "TEST_HASH", "ADMIN", Guid.NewGuid(), "TEST_ITEM");
        item.ApplyEnhancement(1);
        var operation = new ItemReforgeOperation(Guid.NewGuid(), seeded.CharacterId, seeded.ItemId, "AFFIX_1",
            1, "ORE", 1, "STONE", 1, JsonSerializer.Serialize(before), JsonSerializer.Serialize(after), Now);
        operation.Decide(true, Now);
        context.ItemReforgeOperations.Add(operation);
        await context.SaveChangesAsync();
        var build = (await CreateService(context).CaptureForTelegramAsync(123456, CancellationToken.None))!;
        var gear = Assert.Single(build.Equipment);
        Assert.Equal(1, gear.EnhancementLevel);
        Assert.Equal(ItemStatIds.Intellect, Assert.Single(gear.Generated!.Affixes).StatId);
        var reforge = Assert.Single(gear.Reforges);
        Assert.Equal(ItemStatIds.MagicPenetration, reforge.Before!.StatId);
        Assert.Equal(3, reforge.Before.Value);
        Assert.Equal(ItemStatIds.Intellect, reforge.After!.StatId);
        Assert.Equal(5, reforge.After.Value);
        Assert.Contains("Перековка [AFFIX_1]: MAGIC_PENETRATION 3 → INTELLECT 5", CharacterBuildSnapshotFormatter.Format(build));
    }

    [Fact]
    public async Task ArchivedTrainingLogDeliversPinnedBuildAfterSessionCleanupAndRetriesFailedDocumentDelivery()
    {
        var seeded = await SeedAsync();
        await using var context = postgres.CreateDbContext();
        var service = CreateService(context);
        var build = (await service.CaptureForTelegramAsync(123456, CancellationToken.None))!;
        Guid sessionId = Guid.NewGuid();
        await service.LinkTrainingAsync(seeded.AccountId, sessionId, build.BuildHash, CancellationToken.None);
        CombatActorSnapshot player = new(seeded.CharacterId, CombatActorKind.Player, "MAGE", "MageTester",
            100, 100, "MANA", 100, 100, false, null, new Dictionary<string, DateTimeOffset>(), new HashSet<string>(), [], []);
        var enemy = player with { ActorId = Guid.NewGuid(), Kind = CombatActorKind.Monster, DefinitionId = "TRAINING_DUMMY" };
        var snapshot = new CombatSessionSnapshot(sessionId, 1, CombatSessionStatus.Cancelled, Now, player, enemy);
        var damage = new CombatEvent(CombatEventType.DamageDealt, Now, seeded.CharacterId, "TEST_SPELL", 100,
            Sequence: 1, SourceActorId: seeded.CharacterId, TargetActorId: enemy.ActorId);
        var sender = new BuildDocumentSender { FailJsonOnce = true };
        var first = await BossCombatLogArchive.SendAsync(seeded.AccountId, sessionId, snapshot, [damage],
            null, null, context, sender, NullLogger.Instance, Now, CancellationToken.None, service);
        Assert.False(first.Sent);
        Assert.Equal("combat_log_telegram_failed", first.ErrorCode);
        await context.CharacterEquipment.ExecuteDeleteAsync();
        var retry = await BossCombatLogArchive.SendAsync(seeded.AccountId, sessionId, null, null,
            null, null, context, sender, NullLogger.Instance, Now, CancellationToken.None, service);
        Assert.True(retry.Sent);
        Assert.Contains($"BuildHash: {build.BuildHash}", sender.Documents[^2]);
        using var json = JsonDocument.Parse(sender.Documents[^1]);
        Assert.Equal(build.BuildHash, json.RootElement.GetProperty("BuildHash").GetString());
        Assert.Single(json.RootElement.GetProperty("Snapshot").GetProperty("Equipment").EnumerateArray());
        int delivered = sender.Documents.Count;
        var duplicate = await BossCombatLogArchive.SendAsync(seeded.AccountId, sessionId, null, null,
            null, null, context, sender, NullLogger.Instance, Now, CancellationToken.None, service);
        Assert.True(duplicate.Sent);
        Assert.Equal(delivered, sender.Documents.Count);
    }

    private static GameContentPackage Content() => PhaseTwoTestContent.Create(Now, [], []) with
    {
        Items = [Hat, Artifact], EquipmentSets = [new("TEST_SET", "Test Set", [new(1, SpellPowerFlat: 15)])],
        Abilities = [new("TEST_SPELL", AbilityType.Instant, AbilityTargetType.SingleEnemy, 10,
            TimeSpan.FromSeconds(2), TimeSpan.Zero, false, GlobalCooldownCategory.None, true, "FIRE")],
        TalentTrees = [new("TEST_MAGE", "MAGE", 60, 1, [new("FIRE", "Flame", "Fire", 1)],
            [new("TEST_INTELLECT", "FIRE", 1, 0, "Mind", "Mind", 3, [], "Intellect", Modifiers:
                [new(TalentModifierType.StatModifier, "IntellectPercent", [5, 10, 15]),
                 new(TalentModifierType.AbilityModifier, TalentModifierKeys.UnlockAbility, [1, 1, 1], "TEST_SPELL")])])]
    };

    private static CharacterBuildSnapshotService CreateService(GameDbContext context)
    {
        var provider = new StaticContentSnapshotProvider(Content());
        var inventory = new InventoryEquipmentService(context, provider, TimeProvider.System);
        return new(context, new CharacterDerivedStateService(context, provider, inventory), provider, TimeProvider.System);
    }

    private async Task<(Guid AccountId, Guid CharacterId, Guid ItemId)> SeedAsync()
    {
        await using var context = postgres.CreateDbContext();
        Guid accountId = Guid.NewGuid(), characterId = Guid.NewGuid(), itemId = Guid.NewGuid(), artifactId = Guid.NewGuid();
        context.Accounts.Add(new Account(accountId, 123456, Now));
        context.Characters.Add(new Character(characterId, accountId, Guid.NewGuid(), "MageTester", "MAGETESTER", "HUMAN", "FEMALE", "MAGE", Now));
        context.CharacterItems.AddRange(new CharacterItem(itemId, characterId, Hat.Id, 1, Now),
            new CharacterItem(artifactId, characterId, Artifact.Id, 1, Now));
        context.CharacterEquipment.Add(new CharacterEquipment(characterId, EquipmentSlot.Head, itemId));
        context.CharacterSpatialArtifacts.Add(new CharacterSpatialArtifact(characterId, artifactId));
        var talents = new CharacterTalentState(characterId, "TEST_MAGE", 1, Now);
        talents.ReplaceRanks(TalentLoadoutIds.Loadout1, new Dictionary<string, int> { ["TEST_INTELLECT"] = 2 }, Now);
        context.CharacterTalentStates.Add(talents);
        await context.SaveChangesAsync();
        return (accountId, characterId, itemId);
    }

    private sealed class BuildDocumentSender : ITelegramMessageSender, ITelegramDocumentSender
    {
        public bool FailJsonOnce { get; set; }
        public List<string> Documents { get; } = [];
        public Task SendAsync(long chatId, string text, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SendDocumentAsync(long chatId, string fileName, string content, string? caption, CancellationToken cancellationToken)
        {
            Assert.Equal(123456, chatId);
            if (FailJsonOnce && fileName.EndsWith(".json", StringComparison.Ordinal))
            {
                FailJsonOnce = false;
                throw new HttpRequestException("Simulated Telegram delivery failure");
            }
            Documents.Add(content);
            return Task.CompletedTask;
        }
    }
}
