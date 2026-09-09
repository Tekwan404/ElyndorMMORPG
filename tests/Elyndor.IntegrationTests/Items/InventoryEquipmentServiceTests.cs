using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Talents;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Items;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class InventoryEquipmentServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 8, 45, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ItemLockMutationIsDurableReplaySafeAndPayloadBound()
    {
        (Guid accountId, Guid characterId) =
            await CreateCharacterAsync(currentHp: 100);
        Guid itemId = await AddItemAsync(characterId, "WOLF_HIDE", 1);
        Guid lockMutationId = Guid.CreateVersion7();

        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = await CreateServiceAsync(context);

        InventoryOperationResult locked = await service.SetItemLockAsync(
            accountId,
            itemId,
            true,
            lockMutationId,
            CancellationToken.None);
        InventoryOperationResult exactReplay = await service.SetItemLockAsync(
            accountId,
            itemId,
            true,
            lockMutationId,
            CancellationToken.None);
        InventoryOperationResult mismatchedReplay = await service.SetItemLockAsync(
            accountId,
            itemId,
            false,
            lockMutationId,
            CancellationToken.None);

        Assert.True(locked.IsSuccess);
        Assert.True(exactReplay.IsSuccess);
        Assert.True(exactReplay.Snapshot!.Items.Single(item => item.Id == itemId).IsLocked);
        Assert.False(mismatchedReplay.IsSuccess);
        Assert.Equal(
            InventoryErrorCodes.MutationConflict,
            mismatchedReplay.ErrorCode);

        InventoryOperationResult unlocked = await service.SetItemLockAsync(
            accountId,
            itemId,
            false,
            Guid.CreateVersion7(),
            CancellationToken.None);
        Assert.True(unlocked.IsSuccess);
        Assert.False(unlocked.Snapshot!.Items.Single(item => item.Id == itemId).IsLocked);

        InventoryOperationResult oldReplay = await service.SetItemLockAsync(
            accountId,
            itemId,
            true,
            lockMutationId,
            CancellationToken.None);
        Assert.True(oldReplay.IsSuccess);
        Assert.False(oldReplay.Snapshot!.Items.Single(item => item.Id == itemId).IsLocked);

        await using GameDbContext verify = postgres.CreateDbContext();
        CharacterItem persisted = await verify.CharacterItems
            .AsNoTracking()
            .SingleAsync(item => item.Id == itemId);
        Assert.False(persisted.IsLocked);
        Assert.Equal(
            2,
            await verify.CharacterMutations.CountAsync(
                mutation => mutation.CharacterId == characterId));
    }

    [Fact]
    public async Task SameConsumableMutationIsAppliedOnlyOnce()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(currentHp: 25);
        Guid itemId = await AddItemAsync(characterId, "SMALL_HEALING_POTION", 2);
        Guid mutationId = Guid.CreateVersion7();

        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = await CreateServiceAsync(context);

        InventoryOperationResult first = await service.UseConsumableOutOfCombatAsync(
            accountId, itemId, mutationId, 200, "RAGE", 100, Now, CancellationToken.None);
        InventoryOperationResult replay = await service.UseConsumableOutOfCombatAsync(
            accountId, itemId, mutationId, 200, "RAGE", 100, Now, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(replay.IsSuccess);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(75, await verify.CharacterVitals
            .Where(v => v.CharacterId == characterId)
            .Select(v => v.CurrentHp)
            .SingleAsync());
        Assert.Equal(1, await verify.CharacterItems
            .Where(i => i.Id == itemId)
            .Select(i => i.Quantity)
            .SingleAsync());
        Assert.Equal(1, await verify.CharacterMutations.CountAsync());
    }

    [Fact]
    public async Task ResourceConsumableRestoresMatchingDurableResource()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(
            currentHp: 200,
            classId: "WARRIOR",
            currentResource: 10);
        Guid itemId = await AddItemAsync(characterId, "SMALL_RAGE_POTION", 1);

        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = await CreateServiceAsync(context);

        InventoryOperationResult result = await service.UseConsumableOutOfCombatAsync(
            accountId,
            itemId,
            Guid.CreateVersion7(),
            200,
            "RAGE",
            100,
            Now,
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using GameDbContext verify = postgres.CreateDbContext();
        CharacterVitals vitals = await verify.CharacterVitals
            .AsNoTracking()
            .SingleAsync(candidate => candidate.CharacterId == characterId);
        Assert.Equal(40, vitals.CurrentResource);
        Assert.Empty(await verify.CharacterItems
            .Where(item => item.Id == itemId)
            .ToArrayAsync());
    }

    [Theory]
    [InlineData("MINOR_BATTLE_TONIC")]
    [InlineData("MINOR_ANTIDOTE")]
    public async Task CombatOnlyConsumableIsNotSpentOutsideCombat(string definitionId)
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(currentHp: 100);
        Guid itemId = await AddItemAsync(characterId, definitionId, 1);

        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = await CreateServiceAsync(context);

        InventoryOperationResult result = await service.UseConsumableOutOfCombatAsync(
            accountId,
            itemId,
            Guid.CreateVersion7(),
            200,
            "RAGE",
            100,
            Now,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(InventoryErrorCodes.ConsumableUnavailable, result.ErrorCode);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(1, await verify.CharacterItems
            .Where(item => item.Id == itemId)
            .Select(item => item.Quantity)
            .SingleAsync());
    }

    [Fact]
    public async Task ReusingInventoryMutationIdForDifferentOperationIsRejected()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(currentHp: 25);
        Guid itemId = await AddItemAsync(characterId, "RANGER_FANG_BLADE", 1);
        Guid mutationId = Guid.CreateVersion7();

        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = await CreateServiceAsync(context);

        Assert.True((await service.EquipAsync(
            accountId, itemId, mutationId, CancellationToken.None)).IsSuccess);

        InventoryOperationResult conflict = await service.UnequipAsync(
            accountId, EquipmentSlot.Weapon, mutationId, CancellationToken.None);

        Assert.False(conflict.IsSuccess);
        Assert.Equal(InventoryErrorCodes.MutationConflict, conflict.ErrorCode);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Single(await verify.CharacterEquipment
            .Where(e => e.CharacterId == characterId)
            .ToArrayAsync());
    }

    [Fact]
    public async Task ConcurrentConsumableUsesPreserveStackQuantity()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(currentHp: 25);
        Guid itemId = await AddItemAsync(characterId, "SMALL_HEALING_POTION", 2);

        await using GameDbContext firstContext = postgres.CreateDbContext();
        await using GameDbContext secondContext = postgres.CreateDbContext();
        InventoryEquipmentService first = await CreateServiceAsync(firstContext);
        InventoryEquipmentService second = await CreateServiceAsync(secondContext);

        InventoryOperationResult[] results = await Task.WhenAll(
            first.UseConsumableOutOfCombatAsync(
                accountId, itemId, Guid.CreateVersion7(), 200, "RAGE", 100, Now, CancellationToken.None),
            second.UseConsumableOutOfCombatAsync(
                accountId, itemId, Guid.CreateVersion7(), 200, "RAGE", 100, Now.AddSeconds(1), CancellationToken.None));

        Assert.All(results, result => Assert.True(result.IsSuccess));

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(125, await verify.CharacterVitals
            .Where(v => v.CharacterId == characterId)
            .Select(v => v.CurrentHp)
            .SingleAsync());
        Assert.Empty(await verify.CharacterItems.Where(i => i.Id == itemId).ToArrayAsync());
        Assert.Equal(2, await verify.CharacterMutations.CountAsync());
    }

    [Fact]
    public async Task ConcurrentUseOfSingleConsumableSucceedsOnlyOnce()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(currentHp: 25);
        Guid itemId = await AddItemAsync(characterId, "SMALL_HEALING_POTION", 1);

        await using GameDbContext firstContext = postgres.CreateDbContext();
        await using GameDbContext secondContext = postgres.CreateDbContext();
        InventoryEquipmentService first = await CreateServiceAsync(firstContext);
        InventoryEquipmentService second = await CreateServiceAsync(secondContext);

        InventoryOperationResult[] results = await Task.WhenAll(
            first.UseConsumableOutOfCombatAsync(
                accountId, itemId, Guid.CreateVersion7(), 200, "RAGE", 100, Now, CancellationToken.None),
            second.UseConsumableOutOfCombatAsync(
                accountId, itemId, Guid.CreateVersion7(), 200, "RAGE", 100, Now.AddSeconds(1), CancellationToken.None));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => result.ErrorCode == InventoryErrorCodes.ItemNotFound);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(75, await verify.CharacterVitals
            .Where(v => v.CharacterId == characterId)
            .Select(v => v.CurrentHp)
            .SingleAsync());
        Assert.Empty(await verify.CharacterItems.Where(i => i.Id == itemId).ToArrayAsync());
        Assert.Equal(1, await verify.CharacterMutations.CountAsync());
    }

    [Fact]
    public async Task ConcurrentEquipWritesLeaveOneAuthoritativeSlot()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(currentHp: 100);
        Guid firstItemId = await AddItemAsync(characterId, "RANGER_FANG_BLADE", 1);
        Guid secondItemId = await AddItemAsync(characterId, "RANGER_FANG_BLADE", 1);

        await using GameDbContext firstContext = postgres.CreateDbContext();
        await using GameDbContext secondContext = postgres.CreateDbContext();
        InventoryEquipmentService first = await CreateServiceAsync(firstContext);
        InventoryEquipmentService second = await CreateServiceAsync(secondContext);

        InventoryOperationResult[] results = await Task.WhenAll(
            first.EquipAsync(accountId, firstItemId, Guid.CreateVersion7(), CancellationToken.None),
            second.EquipAsync(accountId, secondItemId, Guid.CreateVersion7(), CancellationToken.None));

        Assert.All(results, result => Assert.True(result.IsSuccess));

        await using GameDbContext verify = postgres.CreateDbContext();
        CharacterEquipment equipment = await verify.CharacterEquipment
            .SingleAsync(e => e.CharacterId == characterId && e.Slot == EquipmentSlot.MainHand);
        Assert.Contains(equipment.CharacterItemId, new[] { firstItemId, secondItemId });
        Assert.Equal(2, await verify.CharacterMutations.CountAsync());
    }

    [Fact]
    public async Task EquipAndUnequipRescaleMageManaThroughDerivedState()
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        ItemDefinition staff = new(
            "TEST_MAGE_LIFECYCLE_STAFF",
            "Test Mage Lifecycle Staff",
            ItemType.Equipment,
            ItemRarity.Common,
            1,
            false,
            1,
            EquipmentSlot.Weapon,
            new PrimaryStats(0, 0, 10, 0),
            "D2 equipment lifecycle integration test staff.",
            WeaponCategory: EquipmentCategoryIds.Staff,
            AllowedClassIds: ["MAGE"]);
        content = content with
        {
            Items = (content.Items ?? []).Concat([staff]).ToArray()
        };

        (Guid accountId, Guid characterId) = await CreateCharacterAsync(
            currentHp: 100,
            classId: "MAGE",
            currentResource: 520,
            level: 60);
        Guid itemId = await AddItemAsync(characterId, staff.Id, 1);

        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = new(context, content, new FixedTimeProvider(Now));

        InventoryOperationResult equip = await service.EquipAsync(
            accountId,
            itemId,
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.True(equip.IsSuccess);
        await using (GameDbContext verifyEquip = postgres.CreateDbContext())
        {
            Assert.Equal(545, await verifyEquip.CharacterVitals
                .Where(vitals => vitals.CharacterId == characterId)
                .Select(vitals => vitals.CurrentResource)
                .SingleAsync());
        }

        InventoryOperationResult unequip = await service.UnequipAsync(
            accountId,
            EquipmentSlot.Weapon,
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.True(unequip.IsSuccess);
        await using GameDbContext verifyUnequip = postgres.CreateDbContext();
        Assert.Equal(520, await verifyUnequip.CharacterVitals
            .Where(vitals => vitals.CharacterId == characterId)
            .Select(vitals => vitals.CurrentResource)
            .SingleAsync());
    }

    [Fact]
    public async Task MageCannotEquipOneHandSword()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(100, "MAGE");
        Guid itemId = await AddItemAsync(characterId, "RANGER_FANG_BLADE", 1);
        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = await CreateServiceAsync(context);

        InventoryOperationResult result = await service.EquipAsync(
            accountId, itemId, Guid.CreateVersion7(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(InventoryErrorCodes.WeaponCategoryRestricted, result.ErrorCode);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Empty(await verify.CharacterEquipment.Where(e => e.CharacterId == characterId).ToArrayAsync());
        Assert.Equal(0, await verify.CharacterMutations.CountAsync());
    }

    [Fact]
    public async Task MageCannotEquipLeatherArmor()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(100, "MAGE");
        Guid itemId = await AddItemAsync(characterId, "RANGER_HIDE_VEST", 1);
        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = await CreateServiceAsync(context);

        InventoryOperationResult result = await service.EquipAsync(
            accountId, itemId, Guid.CreateVersion7(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(InventoryErrorCodes.ArmorCategoryRestricted, result.ErrorCode);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Empty(await verify.CharacterEquipment.Where(e => e.CharacterId == characterId).ToArrayAsync());
    }

    [Fact]
    public async Task ExplicitAllowedClassRestrictionIsEnforced()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(100, "WARRIOR");
        Guid itemId = await AddItemAsync(characterId, "RANGER_FANG_BLADE", 1);
        await using GameDbContext context = postgres.CreateDbContext();

        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        content = content with
        {
            Items = content.Items!.Select(item =>
                item.Id == "RANGER_FANG_BLADE"
                    ? item with { AllowedClassIds = ["ARCHER"] }
                    : item).ToArray()
        };
        InventoryEquipmentService service =
            new(context, content, new FixedTimeProvider(Now));

        InventoryOperationResult result = await service.EquipAsync(
            accountId, itemId, Guid.CreateVersion7(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(InventoryErrorCodes.ClassRestricted, result.ErrorCode);
    }

    [Fact]
    public async Task ArcherCanEquipAllowedWeaponAndArmorCategories()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(100, "ARCHER");
        Guid weaponId = await AddItemAsync(characterId, "HUNTER_SHORTBOW", 1);
        Guid chestId = await AddItemAsync(characterId, "HUNTER_LEATHER_VEST", 1);
        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = await CreateServiceAsync(context);

        Assert.True((await service.EquipAsync(
            accountId, weaponId, Guid.CreateVersion7(), CancellationToken.None)).IsSuccess);
        Assert.True((await service.EquipAsync(
            accountId, chestId, Guid.CreateVersion7(), CancellationToken.None)).IsSuccess);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(2, await verify.CharacterEquipment.CountAsync(e => e.CharacterId == characterId));
    }


    [Fact]
    public async Task ArcherCannotEquipOneHandSword()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(100, "ARCHER");
        Guid itemId = await AddItemAsync(characterId, "RANGER_FANG_BLADE", 1);
        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = await CreateServiceAsync(context);

        InventoryOperationResult result = await service.EquipAsync(
            accountId, itemId, Guid.CreateVersion7(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(InventoryErrorCodes.WeaponCategoryRestricted, result.ErrorCode);
    }

    [Fact]
    public async Task WarriorCanEquipShieldButMageCannot()
    {
        (Guid warriorAccountId, Guid warriorCharacterId) =
            await CreateCharacterAsync(100, "WARRIOR");
        (Guid mageAccountId, Guid mageCharacterId) =
            await CreateCharacterAsync(100, "MAGE");
        Guid warriorShieldId =
            await AddItemAsync(warriorCharacterId, "RECRUIT_WOODEN_SHIELD", 1);
        Guid mageShieldId =
            await AddItemAsync(mageCharacterId, "RECRUIT_WOODEN_SHIELD", 1);

        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = await CreateServiceAsync(context);

        InventoryOperationResult warriorResult = await service.EquipAsync(
            warriorAccountId,
            warriorShieldId,
            Guid.CreateVersion7(),
            CancellationToken.None);
        InventoryOperationResult mageResult = await service.EquipAsync(
            mageAccountId,
            mageShieldId,
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.True(warriorResult.IsSuccess);
        Assert.False(mageResult.IsSuccess);
        Assert.Equal(InventoryErrorCodes.ClassRestricted, mageResult.ErrorCode);
        Assert.Contains(
            EquipmentSlot.OffHand,
            warriorResult.Snapshot!.Equipped.Keys);
    }

    [Fact]
    public async Task WarriorCannotEquipOneHandWeaponInOffHandWithoutPermission()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(100, "WARRIOR");
        Guid swordId = await AddItemAsync(characterId, "RANGER_FANG_BLADE", 1);
        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = await CreateServiceAsync(context);

        InventoryOperationResult result = await service.EquipAsync(
            accountId,
            swordId,
            EquipmentSlot.OffHand,
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(InventoryErrorCodes.DualWieldPermissionRequired, result.ErrorCode);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Empty(await verify.CharacterEquipment
            .Where(e => e.CharacterId == characterId)
            .ToArrayAsync());
    }

    [Fact]
    public async Task BerserkerDoubleStrikePermissionAllowsTwoOneHandWeapons()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(100, "WARRIOR", level: 30);
        Guid mainHandId = await AddItemAsync(characterId, "RANGER_FANG_BLADE", 1);
        Guid offHandId = await AddItemAsync(characterId, "RANGER_FANG_BLADE", 1);
        await GrantDualWieldPermissionAsync(characterId);

        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = await CreateServiceAsync(context);

        Assert.True((await service.EquipAsync(
            accountId,
            mainHandId,
            EquipmentSlot.MainHand,
            Guid.CreateVersion7(),
            CancellationToken.None)).IsSuccess);
        InventoryOperationResult offHand = await service.EquipAsync(
            accountId,
            offHandId,
            EquipmentSlot.OffHand,
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.True(offHand.IsSuccess);
        Assert.Equal(mainHandId, offHand.Snapshot!.Equipped[EquipmentSlot.MainHand].Id);
        Assert.Equal(offHandId, offHand.Snapshot.Equipped[EquipmentSlot.OffHand].Id);
    }

    [Fact]
    public async Task TwoHandedWeaponStillConflictsWithDualWieldOffHand()
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        ItemDefinition greatsword = new(
            "TEST_DUAL_WIELD_GREATSWORD",
            "Test Dual Wield Greatsword",
            ItemType.Equipment,
            ItemRarity.Common,
            1,
            false,
            1,
            EquipmentSlot.MainHand,
            new PrimaryStats(1, 0, 0, 0),
            "Test",
            WeaponCategory: EquipmentCategoryIds.TwoHandSword,
            WeaponDamageMin: 8,
            WeaponDamageMax: 12);
        content = content with { Items = content.Items!.Concat([greatsword]).ToArray() };

        (Guid accountId, Guid characterId) = await CreateCharacterAsync(100, "WARRIOR", level: 30);
        Guid greatswordId = await AddItemAsync(characterId, greatsword.Id, 1);
        Guid swordId = await AddItemAsync(characterId, "RANGER_FANG_BLADE", 1);
        await GrantDualWieldPermissionAsync(characterId);

        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = new(context, content, new FixedTimeProvider(Now));

        Assert.True((await service.EquipAsync(
            accountId,
            greatswordId,
            Guid.CreateVersion7(),
            CancellationToken.None)).IsSuccess);
        InventoryOperationResult result = await service.EquipAsync(
            accountId,
            swordId,
            EquipmentSlot.OffHand,
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(InventoryErrorCodes.TwoHandedConflict, result.ErrorCode);
    }

    [Fact]
    public async Task SwitchingAwayFromDualWieldLoadoutUnequipsOffHandWeapon()
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        (Guid accountId, Guid characterId) =
            await CreateCharacterAsync(100, "WARRIOR", level: 30);
        Guid mainHandId = await AddItemAsync(characterId, "RANGER_FANG_BLADE", 1);
        Guid offHandId = await AddItemAsync(characterId, "RANGER_FANG_BLADE", 1);
        long stateVersion = await GrantDualWieldPermissionAsync(characterId);

        await using (GameDbContext equipmentContext = postgres.CreateDbContext())
        {
            InventoryEquipmentService equipment =
                new(equipmentContext, content, new FixedTimeProvider(Now));
            Assert.True((await equipment.EquipAsync(
                accountId,
                mainHandId,
                EquipmentSlot.MainHand,
                Guid.CreateVersion7(),
                CancellationToken.None)).IsSuccess);
            Assert.True((await equipment.EquipAsync(
                accountId,
                offHandId,
                EquipmentSlot.OffHand,
                Guid.CreateVersion7(),
                CancellationToken.None)).IsSuccess);
        }

        await using (GameDbContext talentContext = postgres.CreateDbContext())
        {
            TalentService talents = new(talentContext, content, new FixedTimeProvider(Now));
            TalentOperationResult switched = await talents.SwitchAsync(
                accountId,
                TalentLoadoutIds.Loadout2,
                stateVersion,
                Guid.CreateVersion7().ToString(),
                CancellationToken.None);
            Assert.True(switched.IsSuccess);
        }

        await using GameDbContext verify = postgres.CreateDbContext();
        CharacterEquipment[] equipped = await verify.CharacterEquipment
            .Where(candidate => candidate.CharacterId == characterId)
            .ToArrayAsync();
        CharacterEquipment remaining = Assert.Single(equipped);
        Assert.Equal(EquipmentSlot.MainHand, remaining.Slot);
        Assert.Equal(mainHandId, remaining.CharacterItemId);
        Assert.Equal(
            2,
            await verify.CharacterItems.CountAsync(item => item.CharacterId == characterId));
    }

    [Fact]
    public async Task ResettingActiveDualWieldLoadoutUnequipsOffHandWeapon()
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        (Guid accountId, Guid characterId) =
            await CreateCharacterAsync(100, "WARRIOR", level: 30);
        Guid mainHandId = await AddItemAsync(characterId, "RANGER_FANG_BLADE", 1);
        Guid offHandId = await AddItemAsync(characterId, "RANGER_FANG_BLADE", 1);
        long stateVersion = await GrantDualWieldPermissionAsync(characterId);

        await using (GameDbContext equipmentContext = postgres.CreateDbContext())
        {
            InventoryEquipmentService equipment =
                new(equipmentContext, content, new FixedTimeProvider(Now));
            Assert.True((await equipment.EquipAsync(
                accountId,
                mainHandId,
                EquipmentSlot.MainHand,
                Guid.CreateVersion7(),
                CancellationToken.None)).IsSuccess);
            Assert.True((await equipment.EquipAsync(
                accountId,
                offHandId,
                EquipmentSlot.OffHand,
                Guid.CreateVersion7(),
                CancellationToken.None)).IsSuccess);
        }

        await using (GameDbContext talentContext = postgres.CreateDbContext())
        {
            TalentService talents = new(talentContext, content, new FixedTimeProvider(Now));
            TalentOperationResult reset = await talents.ResetAsync(
                accountId,
                TalentLoadoutIds.Loadout1,
                stateVersion,
                Guid.CreateVersion7().ToString(),
                CancellationToken.None);
            Assert.True(reset.IsSuccess);
        }

        await using GameDbContext verify = postgres.CreateDbContext();
        CharacterEquipment[] equipped = await verify.CharacterEquipment
            .Where(candidate => candidate.CharacterId == characterId)
            .ToArrayAsync();
        CharacterEquipment remaining = Assert.Single(equipped);
        Assert.Equal(EquipmentSlot.MainHand, remaining.Slot);
        Assert.Equal(mainHandId, remaining.CharacterItemId);
        Assert.Equal(
            2,
            await verify.CharacterItems.CountAsync(item => item.CharacterId == characterId));
    }

    [Fact]
    public async Task EquippingTwoHandedWeaponUnequipsExistingShield()
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        ItemDefinition greatsword = new(
            "TEST_GREATSWORD",
            "Test Greatsword",
            ItemType.Equipment,
            ItemRarity.Common,
            1,
            false,
            1,
            EquipmentSlot.MainHand,
            new PrimaryStats(1, 0, 0, 0),
            "Test",
            WeaponCategory: EquipmentCategoryIds.TwoHandSword,
            WeaponDamageMin: 8,
            WeaponDamageMax: 12);
        content = content with
        {
            Items = content.Items!.Concat([greatsword]).ToArray()
        };

        (Guid accountId, Guid characterId) =
            await CreateCharacterAsync(100, "WARRIOR");
        Guid shieldId = await AddItemAsync(
            characterId, "RECRUIT_WOODEN_SHIELD", 1);
        Guid greatswordId = await AddItemAsync(
            characterId, greatsword.Id, 1);

        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service =
            new(context, content, new FixedTimeProvider(Now));

        Assert.True((await service.EquipAsync(
            accountId,
            shieldId,
            Guid.CreateVersion7(),
            CancellationToken.None)).IsSuccess);
        InventoryOperationResult result = await service.EquipAsync(
            accountId,
            greatswordId,
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(EquipmentSlot.MainHand, result.Snapshot!.Equipped.Keys);
        Assert.DoesNotContain(EquipmentSlot.OffHand, result.Snapshot.Equipped.Keys);
    }

    [Fact]
    public async Task TwoHandedSwapCannotOverflowFullInventory()
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        ItemDefinition greatsword = new(
            "TEST_FULL_INVENTORY_GREATSWORD",
            "Test Full Inventory Greatsword",
            ItemType.Equipment,
            ItemRarity.Common,
            1,
            false,
            1,
            EquipmentSlot.MainHand,
            new PrimaryStats(1, 0, 0, 0),
            "Capacity projection test.",
            WeaponCategory: EquipmentCategoryIds.TwoHandSword,
            WeaponDamageMin: 8,
            WeaponDamageMax: 12);
        content = content with
        {
            Items = content.Items!.Concat([greatsword]).ToArray()
        };

        (Guid accountId, Guid characterId) =
            await CreateCharacterAsync(100, "WARRIOR");
        Guid mainHandId = await AddItemAsync(
            characterId,
            "RECRUIT_IRON_SWORD",
            1);
        Guid shieldId = await AddItemAsync(
            characterId,
            "RECRUIT_WOODEN_SHIELD",
            1);

        await using (GameDbContext equipSetup = postgres.CreateDbContext())
        {
            InventoryEquipmentService equipment =
                new(equipSetup, content, new FixedTimeProvider(Now));
            Assert.True((await equipment.EquipAsync(
                accountId,
                mainHandId,
                EquipmentSlot.MainHand,
                Guid.CreateVersion7(),
                CancellationToken.None)).IsSuccess);
            Assert.True((await equipment.EquipAsync(
                accountId,
                shieldId,
                EquipmentSlot.OffHand,
                Guid.CreateVersion7(),
                CancellationToken.None)).IsSuccess);
        }

        Guid greatswordId = await AddItemAsync(
            characterId,
            greatsword.Id,
            1);
        await using (GameDbContext fill = postgres.CreateDbContext())
        {
            for (var index = 0; index < 39; index++)
            {
                fill.CharacterItems.Add(new CharacterItem(
                    Guid.CreateVersion7(),
                    characterId,
                    "RECRUIT_IRON_SWORD",
                    1,
                    Now));
            }
            await fill.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service =
            new(context, content, new FixedTimeProvider(Now));
        InventoryOperationResult result = await service.EquipAsync(
            accountId,
            greatswordId,
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(InventoryErrorCodes.InventoryFull, result.ErrorCode);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(
            40,
            await InventoryCapacity.CountUsedSlotsAsync(
                verify,
                characterId,
                CancellationToken.None));
        Assert.Contains(
            await verify.CharacterEquipment
                .Where(item => item.CharacterId == characterId)
                .ToArrayAsync(),
            item => item.CharacterItemId == mainHandId);
        Assert.Contains(
            await verify.CharacterEquipment
                .Where(item => item.CharacterId == characterId)
                .ToArrayAsync(),
            item => item.CharacterItemId == shieldId);
    }

    [Fact]
    public async Task CannotEquipShieldWhileTwoHandedWeaponIsEquipped()
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        ItemDefinition greatsword = new(
            "TEST_GREATSWORD_BLOCK",
            "Test Greatsword",
            ItemType.Equipment,
            ItemRarity.Common,
            1,
            false,
            1,
            EquipmentSlot.MainHand,
            new PrimaryStats(1, 0, 0, 0),
            "Test",
            WeaponCategory: EquipmentCategoryIds.TwoHandSword,
            WeaponDamageMin: 8,
            WeaponDamageMax: 12);
        content = content with
        {
            Items = content.Items!.Concat([greatsword]).ToArray()
        };

        (Guid accountId, Guid characterId) =
            await CreateCharacterAsync(100, "WARRIOR");
        Guid greatswordId = await AddItemAsync(characterId, greatsword.Id, 1);
        Guid shieldId = await AddItemAsync(
            characterId, "RECRUIT_WOODEN_SHIELD", 1);

        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service =
            new(context, content, new FixedTimeProvider(Now));

        Assert.True((await service.EquipAsync(
            accountId,
            greatswordId,
            Guid.CreateVersion7(),
            CancellationToken.None)).IsSuccess);
        InventoryOperationResult result = await service.EquipAsync(
            accountId,
            shieldId,
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(InventoryErrorCodes.TwoHandedConflict, result.ErrorCode);
    }

    [Fact]
    public async Task EquippingCanonicalMainHandReplacesLegacyWeaponAlias()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(100, "WARRIOR");
        Guid legacyWeaponId = await AddItemAsync(characterId, "RANGER_FANG_BLADE", 1);
        Guid canonicalWeaponId = await AddItemAsync(characterId, "RANGER_FANG_BLADE", 1);

        await using (GameDbContext seed = postgres.CreateDbContext())
        {
            seed.CharacterEquipment.Add(new CharacterEquipment(
                characterId,
                EquipmentSlot.Weapon,
                legacyWeaponId));
            await seed.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        GameContentPackage content =
            await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        ItemDefinition source = content.Items!.Single(item => item.Id == "RANGER_FANG_BLADE");
        content = content with
        {
            Items = content.Items!.Select(item =>
                item.Id == source.Id
                    ? item with { Slot = EquipmentSlot.MainHand }
                    : item).ToArray()
        };
        InventoryEquipmentService service =
            new(context, content, new FixedTimeProvider(Now));

        InventoryOperationResult result = await service.EquipAsync(
            accountId,
            canonicalWeaponId,
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using GameDbContext verify = postgres.CreateDbContext();
        CharacterEquipment[] equipped = await verify.CharacterEquipment
            .Where(e => e.CharacterId == characterId)
            .ToArrayAsync();

        CharacterEquipment entry = Assert.Single(equipped);
        Assert.Equal(EquipmentSlot.MainHand, entry.Slot);
        Assert.Equal(canonicalWeaponId, entry.CharacterItemId);
    }

    [Fact]
    public async Task SameRingTemplateCanFillBothCanonicalRingSlots()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(
            currentHp: 100,
            classId: "WARRIOR",
            level: 25);
        Guid firstRingId = await AddItemAsync(characterId, "ACC_COMMON_2_RING", 1);
        Guid secondRingId = await AddItemAsync(characterId, "ACC_COMMON_2_RING", 1);

        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = await CreateServiceAsync(context);

        Assert.True((await service.EquipAsync(
            accountId,
            firstRingId,
            Guid.CreateVersion7(),
            CancellationToken.None)).IsSuccess);
        Assert.True((await service.EquipAsync(
            accountId,
            secondRingId,
            EquipmentSlot.Ring2,
            Guid.CreateVersion7(),
            CancellationToken.None)).IsSuccess);

        await using GameDbContext verify = postgres.CreateDbContext();
        CharacterEquipment[] equipped = await verify.CharacterEquipment
            .AsNoTracking()
            .Where(item => item.CharacterId == characterId
                && (item.Slot == EquipmentSlot.Ring1 || item.Slot == EquipmentSlot.Ring2))
            .OrderBy(item => item.Slot)
            .ToArrayAsync();

        Assert.Equal(2, equipped.Length);
        Assert.Equal(firstRingId, Assert.Single(equipped, item => item.Slot == EquipmentSlot.Ring1).CharacterItemId);
        Assert.Equal(secondRingId, Assert.Single(equipped, item => item.Slot == EquipmentSlot.Ring2).CharacterItemId);
    }

    [Fact]
    public async Task ArcherBowPreservesCompatibleQuiverOffHand()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(
            currentHp: 100,
            classId: "ARCHER",
            level: 25);
        Guid bowId = await AddItemAsync(characterId, "ARCHER_COMMON_WHISPER_TRACKER_BOW", 1);
        Guid quiverId = await AddItemAsync(characterId, "ARCHER_COMMON_WHISPER_TRACKER_QUIVER", 1);

        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = await CreateServiceAsync(context);

        Assert.True((await service.EquipAsync(
            accountId,
            quiverId,
            Guid.CreateVersion7(),
            CancellationToken.None)).IsSuccess);
        Assert.True((await service.EquipAsync(
            accountId,
            bowId,
            Guid.CreateVersion7(),
            CancellationToken.None)).IsSuccess);

        await using GameDbContext verify = postgres.CreateDbContext();
        CharacterEquipment mainHand = await verify.CharacterEquipment
            .AsNoTracking()
            .SingleAsync(item => item.CharacterId == characterId
                && item.Slot == EquipmentSlot.MainHand);
        CharacterEquipment offHand = await verify.CharacterEquipment
            .AsNoTracking()
            .SingleAsync(item => item.CharacterId == characterId
                && item.Slot == EquipmentSlot.OffHand);

        Assert.Equal(bowId, mainHand.CharacterItemId);
        Assert.Equal(quiverId, offHand.CharacterItemId);
    }

    [Fact]
    public async Task MissingItemDefinitionIsExposedAsSafeLegacyPlaceholder()
    {
        (Guid accountId, Guid characterId) =
            await CreateCharacterAsync(100, "WARRIOR");
        Guid itemId = await AddItemAsync(
            characterId,
            "REMOVED_LEGACY_ITEM",
            1);

        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = await CreateServiceAsync(context);
        InventoryOperationResult result = await service.GetAsync(
            accountId,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        InventoryItemSnapshot item = Assert.Single(
            result.Snapshot!.Items,
            candidate => candidate.Id == itemId);
        Assert.Equal("REMOVED_LEGACY_ITEM", item.Definition.Id);
        Assert.Equal(ItemType.Material, item.Definition.Type);
        Assert.Equal(new PrimaryStats(0, 0, 0, 0), item.EffectiveStats);
        Assert.Contains("Устаревший предмет", item.Definition.Name);
    }

    private async Task<long> GrantDualWieldPermissionAsync(Guid characterId)
    {
        await using GameDbContext context = postgres.CreateDbContext();
        CharacterTalentState state = new(characterId, "WARRIOR_TREE", 1, Now);
        state.ReplaceRanks(
            TalentLoadoutIds.Loadout1,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["B-4-1"] = 1
            },
            Now);
        context.CharacterTalentStates.Add(state);
        await context.SaveChangesAsync();
        return state.StateVersion;
    }

    private async Task<(Guid AccountId, Guid CharacterId)> CreateCharacterAsync(
        decimal currentHp,
        string classId = "WARRIOR",
        decimal currentResource = 0,
        int level = 1)
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();

        context.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
        Character character = new(
            characterId, accountId, Guid.CreateVersion7(), "Inventory", $"INV{Guid.NewGuid():N}"[..16],
            "HUMAN", "MALE", classId, Now);
        character.SetLevel(level);
        context.Characters.Add(character);
        context.CharacterVitals.Add(new CharacterVitals(
            characterId, currentHp, currentResource, Now, Now));
        await context.SaveChangesAsync();
        return (accountId, characterId);
    }

    private async Task<Guid> AddItemAsync(Guid characterId, string definitionId, int quantity)
    {
        Guid itemId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.CharacterItems.Add(new CharacterItem(itemId, characterId, definitionId, quantity, Now));
        await context.SaveChangesAsync();
        return itemId;
    }

    private static async Task<InventoryEquipmentService> CreateServiceAsync(GameDbContext context)
    {
        var content = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        return new InventoryEquipmentService(context, content, new FixedTimeProvider(Now));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
