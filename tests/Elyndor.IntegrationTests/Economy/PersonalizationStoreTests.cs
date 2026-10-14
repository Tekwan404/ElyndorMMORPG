using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Pvp;
using Microsoft.Extensions.Options;
using Elyndor.Core.Characters;
using Elyndor.Core.Economy;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Economy;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Elyndor.IntegrationTests.Economy;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class PersonalizationStoreTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 0, 0, 0, TimeSpan.Zero);
    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task OwnedFrameSurvivesReloadAndReplayNeverChargesOrEquipsAgain()
    {
        Guid account = await SeedAsync("FrameHero", 1000);
        Guid operation = Guid.NewGuid();
        await using (GameDbContext db = postgres.CreateDbContext())
        {
            var service = await ServiceAsync(db);
            Assert.True((await service.PurchaseAsync(account, "FRAME_ASH", operation, CancellationToken.None)).Succeeded);
            Assert.True((await service.EquipAsync(account, "Frame", "FRAME_ASH", CancellationToken.None)).Succeeded);
            Assert.True((await service.EquipAsync(account, "Frame", null, CancellationToken.None)).Succeeded);
            Assert.True((await service.PurchaseAsync(account, "FRAME_ASH", operation, CancellationToken.None)).Succeeded);
            Assert.Equal("cosmetic_already_owned", (await service.PurchaseAsync(account, "FRAME_ASH", Guid.NewGuid(), CancellationToken.None)).ErrorCode);
        }
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Null((await verify.Characters.SingleAsync()).ActiveProfileFrameId);
        Assert.Single(await verify.CharacterCosmeticOwnerships.ToArrayAsync());
        Assert.Equal(880, (await verify.CrystalWallets.SingleAsync()).Balance);
        Assert.Equal(2, await verify.CrystalLedgerEntries.CountAsync());
        var loaded = await (await ServiceAsync(verify)).GetAsync(account, CancellationToken.None);
        Assert.True(loaded!.Cosmetics.Single(item => item.Definition.Id == "FRAME_ASH").Owned);
    }

    [Fact]
    public async Task InvalidUnownedOrWrongSlotCannotBeEquippedAndInsufficientFundsDoNotCreateOwnership()
    {
        Guid account = await SeedAsync("PoorHero", 10);
        await using GameDbContext db = postgres.CreateDbContext();
        var service = await ServiceAsync(db);
        Assert.Equal("cosmetic_not_owned", (await service.EquipAsync(account, "Frame", "FRAME_ASH", CancellationToken.None)).ErrorCode);
        Assert.Equal("cosmetic_invalid_slot", (await service.EquipAsync(account, "Title", "FRAME_ASH", CancellationToken.None)).ErrorCode);
        Assert.Equal("cosmetic_insufficient_crystals", (await service.PurchaseAsync(account, "FRAME_ASH", Guid.NewGuid(), CancellationToken.None)).ErrorCode);
        Assert.Empty(await db.CharacterCosmeticOwnerships.AsNoTracking().ToArrayAsync());
        Assert.Equal(10, (await db.CrystalWallets.AsNoTracking().SingleAsync()).Balance);
    }

    [Fact]
    public async Task RenameRejectsInvalidTakenAndSameNameWithoutChargingAndReplaysWithoutReverting()
    {
        Guid account = await SeedAsync("FirstHero", 1000);
        await SeedAsync("TakenHero", 1000);
        await using GameDbContext db = postgres.CreateDbContext();
        var service = await ServiceAsync(db);
        Assert.False((await service.RenameAsync(account, "a1", Guid.NewGuid(), CancellationToken.None)).Succeeded);
        Assert.Equal("rename_name_taken", (await service.RenameAsync(account, "TakenHero", Guid.NewGuid(), CancellationToken.None)).ErrorCode);
        Assert.Equal("rename_same_name", (await service.RenameAsync(account, "FirstHero", Guid.NewGuid(), CancellationToken.None)).ErrorCode);
        Guid operation = Guid.NewGuid();
        Assert.True((await service.RenameAsync(account, "SecondHero99", operation, CancellationToken.None)).Succeeded);
        Assert.Equal("personalization_operation_conflict", (await service.RenameAsync(account, "OtherHero", operation, CancellationToken.None)).ErrorCode);
        Assert.True((await service.RenameAsync(account, "ThirdHero", Guid.NewGuid(), CancellationToken.None)).Succeeded);
        Assert.True((await service.RenameAsync(account, "SecondHero99", operation, CancellationToken.None)).Succeeded);
        db.ChangeTracker.Clear();
        Assert.Equal("ThirdHero", (await db.Characters.SingleAsync(item => item.AccountId == account)).Name);
        Assert.Equal(700, (await db.CrystalWallets.SingleAsync(item => item.AccountId == account)).Balance);
    }

    [Fact]
    public async Task ConcurrentPurchasesCannotDoubleSpendOrBuyTheSameCosmeticTwice()
    {
        Guid account = await SeedAsync("RaceHero", 120);
        await using GameDbContext first = postgres.CreateDbContext();
        await using GameDbContext second = postgres.CreateDbContext();
        var firstService = await ServiceAsync(first);
        var secondService = await ServiceAsync(second);
        var results = await Task.WhenAll(
            firstService.PurchaseAsync(account, "FRAME_ASH", Guid.NewGuid(), CancellationToken.None),
            secondService.PurchaseAsync(account, "FRAME_ASH", Guid.NewGuid(), CancellationToken.None));
        Assert.Single(results, result => result.Succeeded);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(0, (await verify.CrystalWallets.SingleAsync()).Balance);
        Assert.Single(await verify.CharacterCosmeticOwnerships.ToArrayAsync());
    }

    [Fact]
    public async Task FrameAndTitleAreIndependentAndEquippingNeverCharges()
    {
        Guid account = await SeedAsync("StyleHero", 500);
        await using (GameDbContext db = postgres.CreateDbContext())
        {
            var service = await ServiceAsync(db);
            Assert.True((await service.PurchaseAsync(account, "FRAME_ETHER", Guid.NewGuid(), CancellationToken.None)).Succeeded);
            Assert.True((await service.PurchaseAsync(account, "TITLE_WANDERER", Guid.NewGuid(), CancellationToken.None)).Succeeded);
            Assert.True((await service.EquipAsync(account, "Frame", "FRAME_ETHER", CancellationToken.None)).Succeeded);
            Assert.True((await service.EquipAsync(account, "Title", "TITLE_WANDERER", CancellationToken.None)).Succeeded);
        }
        await using GameDbContext verify = postgres.CreateDbContext();
        var snapshot = await (await ServiceAsync(verify)).GetAsync(account, CancellationToken.None);
        Assert.Equal("FRAME_ETHER", snapshot!.ActiveFrameId);
        Assert.Equal("TITLE_WANDERER", snapshot.ActiveTitleId);
        Assert.Equal(240, snapshot.CrystalBalance);
        Assert.Equal(3, await verify.CrystalLedgerEntries.CountAsync());
    }

    [Fact]
    public async Task ConcurrentRenamesToOneNameOnlyChargeTheWinner()
    {
        Guid firstAccount = await SeedAsync("FirstOwner", 150);
        Guid secondAccount = await SeedAsync("SecondOwner", 150);
        await using GameDbContext first = postgres.CreateDbContext();
        await using GameDbContext second = postgres.CreateDbContext();
        var firstService = await ServiceAsync(first);
        var secondService = await ServiceAsync(second);
        var results = await Task.WhenAll(
            firstService.RenameAsync(firstAccount, "UniqueHero", Guid.NewGuid(), CancellationToken.None),
            secondService.RenameAsync(secondAccount, "UniqueHero", Guid.NewGuid(), CancellationToken.None));
        Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result => result.ErrorCode == "rename_name_taken");
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(150, await verify.CrystalWallets.SumAsync(item => item.Balance));
        Assert.Equal(1, await verify.Characters.CountAsync(item => item.Name == "UniqueHero"));
        Assert.Equal(3, await verify.CrystalLedgerEntries.CountAsync());
    }

    [Fact]
    public async Task ConcurrentRenameAndCosmeticCannotSpendTheSameBalance()
    {
        Guid account = await SeedAsync("BudgetHero", 150);
        await using GameDbContext first = postgres.CreateDbContext();
        await using GameDbContext second = postgres.CreateDbContext();
        var firstService = await ServiceAsync(first);
        var secondService = await ServiceAsync(second);
        var results = await Task.WhenAll(
            firstService.RenameAsync(account, "RenamedHero", Guid.NewGuid(), CancellationToken.None),
            secondService.PurchaseAsync(account, "FRAME_ASH", Guid.NewGuid(), CancellationToken.None));
        Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result => result.ErrorCode == "cosmetic_insufficient_crystals");
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.InRange((await verify.CrystalWallets.SingleAsync()).Balance, 0, 30);
        Assert.Equal(2, await verify.CrystalLedgerEntries.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RenameDuringHostedOrTestArenaDoesNotCharge(bool hosted)
    {
        Guid account = await SeedAsync("ArenaHero", 500);
        await using GameDbContext db = postgres.CreateDbContext();
        Guid characterId = (await db.Characters.SingleAsync()).Id;
        var time = new FixedTime();
        var registry = new ArenaTestRegistry(time, () => new SeededGameRandom(42));
        var runtime = new ArenaMatchRuntime(time, new SystemGameRandomFactory(), new ArenaPresenceTracker(time), Options.Create(new ArenaOptions()));
        ArenaTestEntrant first = Entrant(account, characterId);
        if (hosted) runtime.Register(Guid.NewGuid(), first, Entrant(Guid.NewGuid(), Guid.NewGuid()));
        else registry.Join(first);
        var service = new PersonalizationStoreService(db,
            new StaticContentSnapshotProvider(await GameContentPackageLoader.LoadAsync(ContentPath())), time, runtime, registry);
        var result = await service.RenameAsync(account, "NewArenaHero", Guid.NewGuid(), CancellationToken.None);
        Assert.Equal("personalization_in_combat", result.ErrorCode);
        db.ChangeTracker.Clear();
        Assert.Equal("ArenaHero", (await db.Characters.SingleAsync()).Name);
        Assert.Equal(500, (await db.CrystalWallets.SingleAsync()).Balance);
        Assert.Single(await db.CrystalLedgerEntries.ToArrayAsync());
    }

    [Fact]
    public async Task PersonalizationMigrationCanRollBackAndReapplyOnIsolatedDatabase()
    {
        await using GameDbContext db = postgres.CreateDbContext();
        string[] migrations = db.Database.GetMigrations().ToArray();
        Assert.EndsWith("_StorePersonalization", migrations[^1], StringComparison.Ordinal);
        IMigrator migrator = db.GetService<IMigrator>();
        try { await migrator.MigrateAsync(migrations[^2]); }
        finally { await migrator.MigrateAsync(migrations[^1]); }
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.CharacterCosmeticOwnerships.AsNoTracking().ToArrayAsync());
    }

    private static ArenaTestEntrant Entrant(Guid accountId, Guid characterId) => new(
        new ArenaFighter(accountId, characterId,
            new CombatActorState(characterId, 100, 100, 100, 100, CombatStats.Default),
            new Dictionary<string, AbilityDefinition>(), new AutoAttackProfile(TimeSpan.FromHours(1), 1, 0, 0)),
        30, "ArenaHero", "WARRIOR", "MALE", null);

    private static async Task<PersonalizationStoreService> ServiceAsync(GameDbContext db) => new(db,
        new StaticContentSnapshotProvider(await GameContentPackageLoader.LoadAsync(ContentPath())), new FixedTime());
    private async Task<Guid> SeedAsync(string name, long crystals)
    {
        Guid account = Guid.NewGuid();
        await using GameDbContext db = postgres.CreateDbContext();
        db.Accounts.Add(new Account(account, Random.Shared.NextInt64(100, long.MaxValue), Now));
        db.Characters.Add(new Character(Guid.NewGuid(), account, Guid.NewGuid(), name, name.ToUpperInvariant(), "HUMAN", "MALE", "WARRIOR", Now));
        await db.SaveChangesAsync();
        await new CrystalWalletService(db, new FixedTime()).GrantAsync(account, Guid.NewGuid(), CrystalLedgerEntryType.AdminGrant, crystals, "test", CancellationToken.None);
        return account;
    }
    private static string ContentPath()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "content", "package.json"))) root = root.Parent;
        return Path.Combine(root!.FullName, "content", "package.json");
    }
    private sealed class FixedTime : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
