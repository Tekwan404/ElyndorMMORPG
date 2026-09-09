using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Characters;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class CharacterCreationServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 30, 10, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ExactRequestRetryReturnsSameCharacter()
    {
        Guid accountId = await CreateAccountAsync(100);
        Guid requestId = Guid.CreateVersion7();
        CreateCharacterCommand command = CreateCommand(requestId, "Arthas");

        CharacterCreationResult first = await CreateAsync(accountId, command);
        CharacterCreationResult retry = await CreateAsync(accountId, command);

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(first.Character!.Id, retry.Character!.Id);

        await using GameDbContext context = postgres.CreateDbContext();
        Assert.Equal(1, await context.Characters.CountAsync());
        Assert.Equal(1, await context.CharacterLocations.CountAsync());
    }

    [Fact]
    public async Task ReusedRequestWithDifferentPayloadIsRejected()
    {
        Guid accountId = await CreateAccountAsync(200);
        Guid requestId = Guid.CreateVersion7();
        await CreateAsync(accountId, CreateCommand(requestId, "Jaina"));

        CharacterCreationResult result = await CreateAsync(
            accountId,
            CreateCommand(requestId, "Sylvanas", classId: "ARCHER"));

        Assert.Equal(CharacterCreationErrorCodes.IdempotencyConflict, result.ErrorCode);
    }

    [Fact]
    public async Task ConcurrentNormalizedNameRaceHasOneWinner()
    {
        Guid firstAccountId = await CreateAccountAsync(300);
        Guid secondAccountId = await CreateAccountAsync(400);

        Task<CharacterCreationResult>[] attempts =
        [
            CreateAsync(firstAccountId, CreateCommand(Guid.CreateVersion7(), "Артас")),
            CreateAsync(secondAccountId, CreateCommand(Guid.CreateVersion7(), "артас"))
        ];

        CharacterCreationResult[] results = await Task.WhenAll(attempts);

        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(
            results,
            result => result.ErrorCode == CharacterCreationErrorCodes.NameTaken);
    }

    [Fact]
    public async Task ConcurrentDifferentRequestsForOneAccountHaveOneWinner()
    {
        Guid accountId = await CreateAccountAsync(500);

        Task<CharacterCreationResult>[] attempts =
        [
            CreateAsync(accountId, CreateCommand(Guid.CreateVersion7(), "Arthas")),
            CreateAsync(accountId, CreateCommand(Guid.CreateVersion7(), "Jaina"))
        ];

        CharacterCreationResult[] results = await Task.WhenAll(attempts);

        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(
            results,
            result => result.ErrorCode == CharacterCreationErrorCodes.AlreadyExists);
    }

    [Theory]
    [InlineData("WARRIOR", "RECRUIT_IRON_SWORD")]
    [InlineData("ARCHER", "HUNTER_SHORTBOW")]
    [InlineData("MAGE", "APPRENTICE_STAFF")]
    public async Task CreationGrantsAndEquipsStartingWeapon(
        string classId,
        string expectedItemId)
    {
        Guid accountId = await CreateAccountAsync(525 + classId.Length);

        CharacterCreationResult result = await CreateAsync(
            accountId,
            CreateCommand(
                Guid.CreateVersion7(),
                $"Start{classId[..3]}",
                classId: classId));

        Assert.True(result.IsSuccess, result.ErrorCode);

        await using GameDbContext context = postgres.CreateDbContext();
        CharacterItem item = await context.CharacterItems.AsNoTracking().SingleAsync();
        CharacterEquipment equipment = await context.CharacterEquipment.AsNoTracking().SingleAsync();

        Assert.Equal(expectedItemId, item.ItemDefinitionId);
        Assert.Equal(result.Character!.Id, item.CharacterId);
        Assert.Equal(EquipmentSlot.MainHand, equipment.Slot);
        Assert.Equal(item.Id, equipment.CharacterItemId);
    }

    [Fact]
    public async Task MageStartsWithIntellectScaledMana()
    {
        Guid accountId = await CreateAccountAsync(550);

        CharacterCreationResult result = await CreateAsync(
            accountId,
            CreateCommand(Guid.CreateVersion7(), "Jaina", classId: "MAGE"));

        Assert.True(result.IsSuccess);
        await using GameDbContext context = postgres.CreateDbContext();
        CharacterVitals vitals = await context.CharacterVitals.AsNoTracking().SingleAsync();
        Assert.Equal(155, vitals.CurrentResource);
    }

    [Theory]
    [InlineData("ORC", "MALE", "WARRIOR")]
    [InlineData("HUMAN", "UNKNOWN", "WARRIOR")]
    [InlineData("HUMAN", "MALE", "PRIEST")]
    public async Task InvalidRosterValueLeavesNoPartialState(
        string raceId,
        string genderId,
        string classId)
    {
        Guid accountId = await CreateAccountAsync(600);

        CharacterCreationResult result = await CreateAsync(
            accountId,
            CreateCommand(
                Guid.CreateVersion7(),
                "Arthas",
                raceId,
                genderId,
                classId));

        Assert.Equal(CharacterCreationErrorCodes.InvalidRoster, result.ErrorCode);
        await using GameDbContext context = postgres.CreateDbContext();
        Assert.Empty(await context.Characters.ToListAsync());
        Assert.Empty(await context.CharacterLocations.ToListAsync());
    }

    private async Task<Guid> CreateAccountAsync(long telegramUserId)
    {
        Guid accountId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(accountId, telegramUserId, Now));
        await context.SaveChangesAsync();
        return accountId;
    }

    private async Task<CharacterCreationResult> CreateAsync(
        Guid accountId,
        CreateCharacterCommand command)
    {
        await using GameDbContext context = postgres.CreateDbContext();
        GameContentPackage content = CreateContentPackage();
        TimeProvider timeProvider = new FixedTimeProvider(Now);
        InventoryEquipmentService inventory = new(context, content, timeProvider);
        CharacterDerivedStateService derived = new(context, content, inventory);
        CharacterCreationService service = new(
            context,
            timeProvider,
            content,
            derived);
        return await service.CreateAsync(accountId, command, CancellationToken.None);
    }

    private static CreateCharacterCommand CreateCommand(
        Guid requestId,
        string name,
        string raceId = "HUMAN",
        string genderId = "MALE",
        string classId = "WARRIOR") =>
        new(requestId, name, raceId, genderId, classId);

    private static GameContentPackage CreateContentPackage()
    {
        GameContentPackage content = PhaseTwoTestContent.Create(
            Now,
            [
                new GameContentDefinition("RACE", "HUMAN", []),
                new GameContentDefinition("RACE", "UNDEAD", []),
                new GameContentDefinition("GENDER", "MALE", []),
                new GameContentDefinition("GENDER", "FEMALE", []),
                new GameContentDefinition("CLASS", "WARRIOR", []),
                new GameContentDefinition("CLASS", "ARCHER", []),
                new GameContentDefinition("CLASS", "MAGE", [])
            ],
            []);

        return content with
        {
            ResourceScaling = new ResourceScalingProfile(100, 5),
            ClassProfiles = content.ClassProfiles!.Select(profile => profile with
            {
                StartingEquipmentItemIds = profile.Id switch
                {
                    "WARRIOR" => ["RECRUIT_IRON_SWORD"],
                    "ARCHER" => ["HUNTER_SHORTBOW"],
                    "MAGE" => ["APPRENTICE_STAFF"],
                    _ => []
                }
            }).ToArray(),
            Items =
            [
                StarterWeapon(
                    "RECRUIT_IRON_SWORD",
                    "WARRIOR",
                    EquipmentCategoryIds.OneHandSword),
                StarterWeapon(
                    "HUNTER_SHORTBOW",
                    "ARCHER",
                    EquipmentCategoryIds.Bow),
                StarterWeapon(
                    "APPRENTICE_STAFF",
                    "MAGE",
                    EquipmentCategoryIds.Staff)
            ]
        };
    }

    private static ItemDefinition StarterWeapon(
        string id,
        string classId,
        string weaponCategory) =>
        new(
            id,
            id,
            ItemType.Equipment,
            ItemRarity.Common,
            1,
            false,
            1,
            EquipmentSlot.MainHand,
            new PrimaryStats(0, 0, 0, 0),
            "Starter weapon",
            WeaponBaseAttackIntervalSeconds: 2,
            WeaponCategory: weaponCategory,
            AllowedClassIds: [classId],
            WeaponDamageMin: 5,
            WeaponDamageMax: 8);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
