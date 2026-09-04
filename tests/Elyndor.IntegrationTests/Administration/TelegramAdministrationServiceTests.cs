using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Core.Talents;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
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
                characterId, EquipmentSlot.Weapon, itemId));

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
