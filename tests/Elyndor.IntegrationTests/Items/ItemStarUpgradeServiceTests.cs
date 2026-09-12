using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Items;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ItemStarUpgradeServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);
    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UpgradeRejectsLockedItemWithoutCreatingMutation()
    {
        Guid accountId = Guid.CreateVersion7(); Guid characterId = Guid.CreateVersion7(); Guid itemId = Guid.CreateVersion7();
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
            setup.Characters.Add(new Character(characterId, accountId, Guid.CreateVersion7(), "Forger", "FORGER0003", "HUMAN", "MALE", "WARRIOR", Now));
            CharacterItem item = new(itemId, characterId, "RECRUIT_IRON_SWORD", 1, Now); item.SetLocked(true);
            setup.CharacterItems.Add(item); await setup.SaveChangesAsync();
        }
        await using GameDbContext context = postgres.CreateDbContext();
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        ItemStarUpgradeService service = new(context, new StaticContentSnapshotProvider(content), new FixedTimeProvider(Now));
        ItemStarUpgradeResult result = await service.UpgradeAsync(accountId, itemId, Guid.CreateVersion7(), CancellationToken.None);
        Assert.False(result.Succeeded); Assert.Equal(ItemStarUpgradeErrorCodes.ItemLocked, result.ErrorCode);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Empty(verify.CharacterMutations);
    }

    [Fact]
    public async Task UpgradeAdvancesSparseBlackConstellationBootsFromTwoToThreeStars()
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        ItemDefinition definition = Assert.Single(content.Items!, item =>
            item.Id == "ARCHER_LEGENDARY_BLACK_CONSTELLATION_FEET");
        ItemizationDefinition itemization = Assert.IsType<ItemizationDefinition>(content.Itemization);

        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        Guid itemId = Guid.CreateVersion7();
        Guid sourceOperationId = Guid.CreateVersion7();
        Guid mutationId = Guid.CreateVersion7();

        GeneratedItemInstance generated = new(
            ItemLevel: 23,
            Affixes:
            [
                new("AFFIX_1", "AGILITY", "AGILITY", 7m, 5m, 12m, 1m, 4, true, false, 0),
                new("AFFIX_2", "STAMINA", "STAMINA", 7m, 5m, 12m, 1m, 4, true, false, 1),
                new("AFFIX_3", "CRITICAL_CHANCE", "CRITICAL_CHANCE", 3.2m, 2m, 5m, 0.1m, 4, true, false, 2),
                new("AFFIX_4", "ACCURACY", "ACCURACY", 4.4m, 2.3m, 5.9m, 0.1m, 4, false, true, 3),
            ],
            MinimumTemplateItemPower: 200m,
            ActualItemPower: 290.20m,
            MaxTemplateItemPower: 494.91m,
            RollQuality: 42m,
            Stars: 2,
            IsPerfect: false,
            PerfectOrigin: null,
            GeneratedPrefixId: null,
            GeneratedSuffixId: null,
            DisplayName: "Сапоги Чёрного Созвездия",
            GenerationVersion: definition.GenerationVersion);

        GeneratedItemAffix[] strengthened = ItemStarUpgradeCalculator.IncreaseToTargetStar(generated.Affixes, 3);
        GeneratedItemInstance naturalRecalculation = ItemInstanceGenerator.Recalculate(
            definition,
            itemization,
            generated.ItemLevel,
            strengthened,
            perfectOrigin: "STAR_UPGRADE");
        Assert.NotEqual(3, naturalRecalculation.Stars);

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
            Character character = new(
                characterId,
                accountId,
                Guid.CreateVersion7(),
                "Starforger",
                "STARFORGER",
                "HUMAN",
                "MALE",
                "ARCHER",
                Now);
            character.AddGold(1_000);
            setup.Characters.Add(character);

            CharacterItem boots = new(itemId, characterId, definition.Id, 1, Now);
            boots.ApplyGeneratedInstance(
                generated,
                "TEST-SEED-BLACK-CONSTELLATION",
                "TEST",
                sourceOperationId,
                "BLACK_CONSTELLATION_BOOTS");
            setup.CharacterItems.Add(boots);
            setup.CharacterItems.Add(new CharacterItem(
                Guid.CreateVersion7(),
                characterId,
                "REFORGE_STONE",
                10,
                Now.AddSeconds(1)));
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        ItemStarUpgradeService service = new(
            context,
            new StaticContentSnapshotProvider(content),
            new FixedTimeProvider(Now));

        ItemStarUpgradeResult result = await service.UpgradeAsync(
            accountId,
            itemId,
            mutationId,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Null(result.ErrorCode);
        Assert.NotNull(result.Item);
        Assert.Equal(3, result.Item.Stars);
        Assert.Equal(
            generated.Affixes.Select(affix => (affix.SlotKey, affix.StatId)),
            result.Item.Affixes.Select(affix => (affix.SlotKey, affix.StatId)));
        Assert.All(result.Item.Affixes, upgradedAffix =>
        {
            GeneratedItemAffix original = generated.Affixes.Single(affix => affix.SlotKey == upgradedAffix.SlotKey);
            Assert.True(upgradedAffix.Value >= original.Value);
        });

        await using GameDbContext verify = postgres.CreateDbContext();
        Character savedCharacter = await verify.Characters.SingleAsync(character => character.Id == characterId);
        CharacterItem savedBoots = await verify.CharacterItems.Include(item => item.Affixes).SingleAsync(item => item.Id == itemId);
        CharacterItem stoneStack = await verify.CharacterItems.SingleAsync(item =>
            item.CharacterId == characterId && item.ItemDefinitionId == "REFORGE_STONE");
        Assert.Equal(900, savedCharacter.Gold);
        Assert.Equal(6, stoneStack.Quantity);
        Assert.Equal(3, savedBoots.Stars);
        Assert.Equal(1, savedBoots.EnhancementLevel);
        Assert.Single(verify.CharacterMutations, mutation =>
            mutation.CharacterId == characterId && mutation.MutationId == mutationId);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
