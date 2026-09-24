using Elyndor.Core.Characters;
using Elyndor.Core.Economy;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Economy;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;

namespace Elyndor.IntegrationTests.Economy;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class CharacterSkinServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 0, 0, 0, TimeSpan.Zero);
    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PurchaseReplayAndEquipDoNotChargeTwice()
    {
        Guid accountId = await CreateAccountAsync("FEMALE", "MAGE");
        await using GameDbContext db = postgres.CreateDbContext();
        var provider = new StaticContentSnapshotProvider(await GameContentPackageLoader.LoadAsync(RepositoryContentPath()));
        var wallet = new CrystalWalletService(db, new FixedTimeProvider());
        await wallet.GrantAsync(accountId, Guid.CreateVersion7(), CrystalLedgerEntryType.AdminGrant, 1000, "test", CancellationToken.None);
        var skins = new CharacterSkinService(db, provider, new FixedTimeProvider());
        Guid operation = Guid.CreateVersion7();

        CharacterSkinMutationResult first = await skins.PurchaseAsync(accountId, "MAGE_FEMALE_FIRE", operation, CancellationToken.None);
        CharacterSkinMutationResult replay = await skins.PurchaseAsync(accountId, "MAGE_FEMALE_FIRE", operation, CancellationToken.None);
        CharacterSkinMutationResult duplicate = await skins.PurchaseAsync(accountId, "MAGE_FEMALE_FIRE", Guid.CreateVersion7(), CancellationToken.None);
        CharacterSkinMutationResult equip = await skins.EquipAsync(accountId, "MAGE_FEMALE_FIRE", CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(350, first.CrystalBalance);
        Assert.Equal(first.CrystalBalance, replay.CrystalBalance);
        Assert.Equal("skin_already_owned", duplicate.ErrorCode);
        Assert.True(equip.Succeeded);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Single(verify.CharacterSkinOwnerships);
        Assert.Equal(350, Assert.Single(verify.CrystalWallets).Balance);
        Assert.Equal("MAGE_FEMALE_FIRE", Assert.Single(verify.Characters).ActiveSkinId);
    }

    [Fact]
    public async Task WrongGenderClassAndUnownedSkinCannotBePurchasedOrEquipped()
    {
        Guid accountId = await CreateAccountAsync("MALE", "WARRIOR");
        await using GameDbContext db = postgres.CreateDbContext();
        var skins = new CharacterSkinService(db,
            new StaticContentSnapshotProvider(await GameContentPackageLoader.LoadAsync(RepositoryContentPath())),
            new FixedTimeProvider());

        Assert.Equal("skin_incompatible", (await skins.PurchaseAsync(accountId, "WARRIOR_FEMALE_FURY", Guid.CreateVersion7(), CancellationToken.None)).ErrorCode);
        Assert.Equal("skin_incompatible", (await skins.EquipAsync(accountId, "WARRIOR_FEMALE_FURY", CancellationToken.None)).ErrorCode);
        Assert.Null(Assert.Single(db.Characters).ActiveSkinId);
    }

    [Fact]
    public async Task FemaleCharacterCannotEquipUnownedOrOtherClassSkin()
    {
        Guid accountId = await CreateAccountAsync("FEMALE", "MAGE");
        await using GameDbContext db = postgres.CreateDbContext();
        var skins = new CharacterSkinService(db,
            new StaticContentSnapshotProvider(await GameContentPackageLoader.LoadAsync(RepositoryContentPath())),
            new FixedTimeProvider());

        Assert.Equal("skin_not_owned", (await skins.EquipAsync(accountId, "MAGE_FEMALE_FIRE", CancellationToken.None)).ErrorCode);
        Assert.Equal("skin_incompatible", (await skins.EquipAsync(accountId, "ARCHER_FEMALE_DEFAULT", CancellationToken.None)).ErrorCode);
        Assert.True((await skins.EquipAsync(accountId, "MAGE_FEMALE_DEFAULT", CancellationToken.None)).Succeeded);
    }

    [Fact]
    public async Task CatalogContainsOnlySkinsCompatibleWithTheCharacter()
    {
        Guid accountId = await CreateAccountAsync("FEMALE", "MAGE");
        await using GameDbContext db = postgres.CreateDbContext();
        var skins = new CharacterSkinService(db,
            new StaticContentSnapshotProvider(await GameContentPackageLoader.LoadAsync(RepositoryContentPath())),
            new FixedTimeProvider());

        CharacterSkinStoreSnapshot catalog = (await skins.GetAsync(accountId, CancellationToken.None))!;

        Assert.NotEmpty(catalog.Skins);
        Assert.All(catalog.Skins, skin =>
        {
            Assert.Equal("MAGE", skin.Definition.ClassId);
            Assert.Equal("FEMALE", skin.Definition.GenderId);
            Assert.True(skin.Eligible);
        });
    }

    private async Task<Guid> CreateAccountAsync(string gender, string classId)
    {
        Guid accountId = Guid.CreateVersion7();
        await using GameDbContext db = postgres.CreateDbContext();
        db.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
        db.Characters.Add(new Character(Guid.CreateVersion7(), accountId, Guid.CreateVersion7(), "Buyer", "BUYER00000000001", "HUMAN", gender, classId, Now));
        await db.SaveChangesAsync();
        return accountId;
    }

    private sealed class FixedTimeProvider : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }

    private static string RepositoryContentPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "content", "package.json");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository content package was not found.");
    }
}
