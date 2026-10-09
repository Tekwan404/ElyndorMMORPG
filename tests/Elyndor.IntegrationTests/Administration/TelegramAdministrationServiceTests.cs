using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Core.Talents;
using Elyndor.Core.World;
using Elyndor.Core.WorldBosses;
using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.WorldBosses;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Administration;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class TelegramAdministrationServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RepeatedUpdateDoesNotApplyRelocationTwice()
    {
        await SeedCharacterAsync(732_707_324);

        AdministrationResult first = await ExecuteAsync(
            9001,
            new AdministrationOperation(
                AdministrationOperationType.SetLocation,
                732_707_324,
                "WHISPERING_FOREST"));
        AdministrationResult retry = await ExecuteAsync(
            9001,
            new AdministrationOperation(
                AdministrationOperationType.SetLocation,
                732_707_324,
                "WHISPERING_FOREST"));

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsDuplicate);
        await using GameDbContext context = postgres.CreateDbContext();
        CharacterLocation location = await context.CharacterLocations.SingleAsync();
        Assert.Equal("WHISPERING_FOREST", location.LocationId);
        Assert.Equal(2, location.Version);
        Assert.Equal(1, await context.AdminCommandAudits.CountAsync());
    }

    [Fact]
    public async Task DeleteRemovesCharacterStateButPreservesAccount()
    {
        await SeedCharacterAsync(732_707_324);

        AdministrationResult result = await ExecuteAsync(
            9002,
            new AdministrationOperation(
                AdministrationOperationType.Delete,
                732_707_324,
                "Arthas"));

        Assert.True(result.IsSuccess);
        await using GameDbContext context = postgres.CreateDbContext();
        Assert.Equal(1, await context.Accounts.CountAsync());
        Assert.Empty(await context.Characters.ToListAsync());
        Assert.Empty(await context.CharacterVitals.ToListAsync());
        Assert.Empty(await context.CharacterLocations.ToListAsync());
    }

    [Fact]
    public async Task RestoreUsesScaledManaForLevel60Mage()
    {
        await SeedCharacterAsync(
            732_707_324,
            classId: "MAGE",
            level: 60,
            currentHp: 1,
            currentResource: 1);
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        AdministrationResult result = await ExecuteAsync(
            9003,
            new AdministrationOperation(
                AdministrationOperationType.Restore,
                732_707_324),
            content);

        Assert.True(result.IsSuccess);
        await using GameDbContext context = postgres.CreateDbContext();
        CharacterVitals vitals = await context.CharacterVitals.AsNoTracking().SingleAsync();
        Assert.Equal(1040, vitals.CurrentResource);
    }

    [Fact]
    public async Task SetLevelScalesManaUsingDerivedMaximums()
    {
        await SeedCharacterAsync(
            732_707_324,
            classId: "MAGE",
            level: 1,
            currentHp: 50,
            currentResource: 50);
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        AdministrationResult result = await ExecuteAsync(
            9004,
            new AdministrationOperation(
                AdministrationOperationType.SetLevel,
                732_707_324,
                NumericValue: 60),
            content);

        Assert.True(result.IsSuccess);
        await using GameDbContext context = postgres.CreateDbContext();
        CharacterVitals vitals = await context.CharacterVitals.AsNoTracking().SingleAsync();
        Assert.Equal(335.484m, vitals.CurrentResource);
    }

    [Fact]
    public async Task ClassChangeResetsTalentsAndUnequipsOldGear()
    {
        Guid characterId = await SeedCharacterAsync(732_707_324);
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        TalentTreeDefinition warriorTree = content.TalentTrees!.Single(tree => tree.Id == "WARRIOR_TREE");

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            Guid itemId = Guid.CreateVersion7();
            setup.CharacterItems.Add(new CharacterItem(
                itemId, characterId, "RANGER_FANG_BLADE", 1, Now));
            setup.CharacterEquipment.Add(new CharacterEquipment(
                characterId, EquipmentSlot.MainHand, itemId));

            CharacterTalentState talents = new(
                characterId,
                warriorTree.Id,
                warriorTree.Version,
                Now);
            talents.ReplaceRanks(
                TalentLoadoutIds.Loadout1,
                new Dictionary<string, int> { ["B-1-1"] = 1 },
                Now);
            setup.CharacterTalentStates.Add(talents);
            await setup.SaveChangesAsync();
        }

        AdministrationResult result = await ExecuteAsync(
            9005,
            new AdministrationOperation(
                AdministrationOperationType.SetClass,
                732_707_324,
                "MAGE"),
            content);

        Assert.True(result.IsSuccess);
        await using GameDbContext context = postgres.CreateDbContext();
        Character character = await context.Characters.AsNoTracking().SingleAsync();
        CharacterTalentState talentState = await context.CharacterTalentStates.AsNoTracking().SingleAsync();
        Assert.Equal("MAGE", character.ClassId);
        Assert.Empty(await context.CharacterEquipment.AsNoTracking().ToArrayAsync());
        Assert.Equal("MAGE_TREE", talentState.TalentTreeId);
        Assert.Empty(talentState.GetRanks(TalentLoadoutIds.Loadout1));
        Assert.Empty(talentState.GetRanks(TalentLoadoutIds.Loadout2));
    }

    [Fact]
    public async Task SpawnWorldBossUsesLifecycleAndIsIdempotentPerTelegramUpdate()
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        await using GameDbContext context = postgres.CreateDbContext();
        TimeProvider timeProvider = new FixedTimeProvider(Now);
        var provider = new StaticContentSnapshotProvider(content);
        var derived = new CharacterDerivedStateService(context, provider, inventoryService: null);
        var lifecycle = new WorldBossLifecycleService(context, provider, timeProvider);
        var service = new TelegramAdministrationService(
            context,
            timeProvider,
            provider,
            derived,
            contentAdministrationService: null,
            messageSender: null,
            worldBossLifecycleService: lifecycle);
        AdministrationOperation operation = new(
            AdministrationOperationType.SpawnWorldBoss,
            Value: "WORLD_BOSS_ASH_ARCHON");

        AdministrationResult first = await service.ExecuteAsync(
            9010,
            732_707_324,
            operation,
            CancellationToken.None);
        AdministrationResult replay = await service.ExecuteAsync(
            9010,
            732_707_324,
            operation,
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal("admin_world_boss_spawned", first.Code);
        Assert.True(replay.IsSuccess);
        Assert.True(replay.IsDuplicate);

        WorldBossSpawn spawn = await context.WorldBossSpawns.AsNoTracking().SingleAsync();
        Assert.Equal("WORLD_BOSS_ASH_ARCHON", spawn.BossDefinitionId);
        Assert.Equal(WorldBossSpawnStatus.Active, spawn.Status);
        Assert.Equal(1_000_000m, spawn.CurrentHealth);
        Assert.Equal(1, await context.AdminCommandAudits.CountAsync());
    }

    [Fact]
    public async Task GiveItemGeneratesProceduralEquipmentAndIsIdempotentPerUpdate()
    {
        await SeedCharacterAsync(732_707_324, level: 60);
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        AdministrationOperation operation = new(
            AdministrationOperationType.GiveItem,
            732_707_324,
            "UNIQUE_WARRIOR_BLACKHEART 1 BOSS");

        AdministrationResult first = await ExecuteAsync(9006, operation, content);
        AdministrationResult retry = await ExecuteAsync(9006, operation, content);

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.True(retry.IsDuplicate);

        await using GameDbContext context = postgres.CreateDbContext();
        CharacterItem item = await context.CharacterItems
            .Include(candidate => candidate.Affixes)
            .SingleAsync(candidate => candidate.ItemDefinitionId == "UNIQUE_WARRIOR_BLACKHEART");
        Assert.Equal(60, item.ItemLevel);
        Assert.Equal(2, item.GenerationVersion);
        Assert.Equal("ADMIN_GRANT", item.SourceType);
        Assert.Equal(5, item.Affixes.Count);
        Assert.Equal(3, item.Affixes.Count(affix => affix.IsGuaranteed));
        Assert.Equal(1, await context.CharacterItems.CountAsync(candidate =>
            candidate.ItemDefinitionId == "UNIQUE_WARRIOR_BLACKHEART"));
    }

    [Fact]
    public async Task GmForgeGrantsPerfectExtremeSwordAsBoundTestEquipmentOnce()
    {
        await SeedCharacterAsync(732_707_324, level: 60);
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        AdministrationOperation operation = new(
            AdministrationOperationType.GmForge, 732_707_324,
            "UNIQUE_WARRIOR_BLACKHEART quality=PERFECT stars=5 enhance=5 WEAPON_DAMAGE=1500 CRITICAL_DAMAGE=150");

        AdministrationResult first = await ExecuteAsync(9050, operation, content);
        AdministrationResult replay = await ExecuteAsync(9050, operation, content);

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(replay.IsDuplicate);
        await using GameDbContext context = postgres.CreateDbContext();
        CharacterItem item = await context.CharacterItems.Include(x => x.Affixes).SingleAsync();
        Assert.Equal(GmItemForge.SourceType, item.SourceType);
        Assert.Equal(ItemBindStates.Bound, item.BindState);
        Assert.True(item.IsLocked);
        Assert.Equal(5, item.Stars);
        Assert.Equal(5, item.EnhancementLevel);
        Assert.Equal(100m, item.RollQuality);
        Assert.True(item.IsPerfect);
        Assert.Equal(GmItemForge.SourceType, item.PerfectOrigin);
        Assert.Equal(1500m, item.Affixes.Single(x => x.StatId == ItemStatIds.WeaponDamage).Value);
        Assert.Equal(150m, item.Affixes.Single(x => x.StatId == ItemStatIds.CriticalDamage).Value);
        Assert.Equal(1, await context.AdminCommandAudits.CountAsync());
    }

    [Fact]
    public async Task GmForgeClonePreservesOriginalAndOverridesOnlyTheCopy()
    {
        await SeedCharacterAsync(732_707_324, level: 60);
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        AdministrationResult originalGrant = await ExecuteAsync(
            9051, new AdministrationOperation(AdministrationOperationType.GiveItem,
                732_707_324, "UNIQUE_WARRIOR_BLACKHEART 1 NORMAL"), content);
        Assert.True(originalGrant.IsSuccess);

        Guid originalId;
        await using (GameDbContext context = postgres.CreateDbContext())
        {
            originalId = await context.CharacterItems.AsNoTracking().Select(x => x.Id).SingleAsync();
        }

        AdministrationResult cloned = await ExecuteAsync(9052,
            new AdministrationOperation(AdministrationOperationType.GmForge, 732_707_324,
                $"clone:{originalId:D} stars=5 CRITICAL_DAMAGE=150"), content);
        Assert.True(cloned.IsSuccess, cloned.Message);

        await using GameDbContext verify = postgres.CreateDbContext();
        CharacterItem[] items = await verify.CharacterItems.Include(x => x.Affixes).ToArrayAsync();
        Assert.Equal(2, items.Length);
        CharacterItem original = items.Single(x => x.Id == originalId);
        CharacterItem clone = items.Single(x => x.Id != originalId);
        Assert.Equal("ADMIN_GRANT", original.SourceType);
        Assert.Equal(ItemBindStates.Unbound, original.BindState);
        Assert.Equal(GmItemForge.SourceType, clone.SourceType);
        Assert.Equal(ItemBindStates.Bound, clone.BindState);
        Assert.True(clone.IsLocked);
        Assert.Equal(5, clone.Stars);
        Assert.Equal(150m, clone.Affixes.Single(x => x.StatId == ItemStatIds.CriticalDamage).Value);
        Assert.DoesNotContain(original.Affixes,
            x => x.StatId == ItemStatIds.CriticalDamage && x.Value == 150m);
    }

    private async Task<AdministrationResult> ExecuteAsync(
        long updateId,
        AdministrationOperation operation,
        GameContentPackage? content = null)
    {
        await using GameDbContext context = postgres.CreateDbContext();
        GameContentPackage resolvedContent = content ?? CreateContent();
        TimeProvider timeProvider = new FixedTimeProvider(Now);
        InventoryEquipmentService inventory = new(context, resolvedContent, timeProvider);
        CharacterDerivedStateService derived = new(context, resolvedContent, inventory);
        TelegramAdministrationService service = new(
            context,
            timeProvider,
            resolvedContent,
            derived,
            null);
        return await service.ExecuteAsync(
            updateId,
            732_707_324,
            operation,
            CancellationToken.None);
    }

    private async Task<Guid> SeedCharacterAsync(
        long telegramUserId,
        string classId = "WARRIOR",
        int level = 1,
        decimal currentHp = 150,
        decimal currentResource = 0)
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(accountId, telegramUserId, Now));
        Character character = new(
            characterId,
            accountId,
            Guid.CreateVersion7(),
            "Arthas",
            "ARTHAS",
            "HUMAN",
            "MALE",
            classId,
            Now);
        character.SetLevel(level);
        context.Characters.Add(character);
        context.CharacterVitals.Add(new CharacterVitals(
            characterId, currentHp, currentResource, Now, Now));
        context.CharacterLocations.Add(new CharacterLocation(characterId, "STARTER_TOWN", 1, Now));
        await context.SaveChangesAsync();
        return characterId;
    }

    private static GameContentPackage CreateContent() => PhaseTwoTestContent.Create(
        Now,
        [
            new("RACE", "HUMAN", []),
            new("RACE", "UNDEAD", []),
            new("GENDER", "MALE", []),
            new("CLASS", "WARRIOR", []),
            new("CLASS", "ARCHER", []),
            new("CLASS", "MAGE", [])
        ],
        [
            new("STARTER_TOWN", "Starter Town", "SAFE", 1, ["WHISPERING_FOREST"]),
            new("WHISPERING_FOREST", "Whispering Forest", "LOW", 2, ["STARTER_TOWN"])
        ]);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
