using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Characters;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class CharacterDerivedStateServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 4, 9, 30, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task MageLevel60ManaUsesFinalIntellectIncludingEquipment()
    {
        GameContentPackage content = await LoadContentAsync();
        ItemDefinition staff = new(
            "TEST_MAGE_STAFF",
            "Test Mage Staff",
            ItemType.Equipment,
            ItemRarity.Common,
            1,
            false,
            1,
            EquipmentSlot.Weapon,
            new PrimaryStats(0, 0, 10, 0),
            "Derived-state integration test staff.",
            WeaponCategory: EquipmentCategoryIds.Staff,
            AllowedClassIds: ["MAGE"]);
        content = content with
        {
            Items = (content.Items ?? []).Concat([staff]).ToArray()
        };

        (Guid characterId, _) = await CreateCharacterAsync("MAGE", 60);
        Guid itemId = Guid.CreateVersion7();
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.CharacterItems.Add(new CharacterItem(
                itemId, characterId, staff.Id, 1, Now));
            setup.CharacterEquipment.Add(new CharacterEquipment(
                characterId, EquipmentSlot.Weapon, itemId));
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        CharacterDerivedStateService service = CreateService(context, content);
        CharacterDerivedState state = await service.ResolveAsync(
            characterId, "MAGE", 60, CancellationToken.None);

        Assert.Equal(198, state.Stats.Intellect);
        Assert.Equal(1090, state.EffectiveResourceProfile.MaxValue);
        Assert.Equal("MANA", state.BaseResourceProfile.Id);
        Assert.Contains("MAGE_FIREBALL", state.KnownAbilityIds);
    }

    [Fact]
    public async Task TalentStateFromPreviousClassIsIgnored()
    {
        GameContentPackage content = await LoadContentAsync();
        (Guid characterId, _) = await CreateCharacterAsync("MAGE", 60);
        TalentTreeDefinition warriorTree = content.TalentTrees!.Single(tree => tree.Id == "WARRIOR_TREE");

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            CharacterTalentState stale = new(
                characterId,
                warriorTree.Id,
                warriorTree.Version,
                Now);
            stale.ReplaceRanks(
                TalentLoadoutIds.Loadout1,
                new Dictionary<string, int> { ["B-1-1"] = 1 },
                Now);
            setup.CharacterTalentStates.Add(stale);
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        CharacterDerivedStateService service = CreateService(context, content);
        CharacterDerivedState state = await service.ResolveAsync(
            characterId, "MAGE", 60, CancellationToken.None);

        Assert.Empty(state.ActiveTalentRanks);
        Assert.Equal(1040, state.EffectiveResourceProfile.MaxValue);
    }

    private async Task<(Guid CharacterId, Guid AccountId)> CreateCharacterAsync(
        string classId,
        int level)
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
            "Derived",
            $"DERIVED{characterId:N}"[..16],
            "HUMAN",
            "MALE",
            classId,
            Now);
        character.SetLevel(level);
        context.Characters.Add(character);
        await context.SaveChangesAsync();
        return (characterId, accountId);
    }

    private static CharacterDerivedStateService CreateService(
        GameDbContext context,
        GameContentPackage content)
    {
        TimeProvider timeProvider = new FixedTimeProvider(Now);
        InventoryEquipmentService inventory = new(context, content, timeProvider);
        return new CharacterDerivedStateService(context, content, inventory);
    }

    private static Task<GameContentPackage> LoadContentAsync() =>
        GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
