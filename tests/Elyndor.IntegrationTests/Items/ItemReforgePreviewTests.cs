using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;

namespace Elyndor.IntegrationTests.Items;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ItemReforgePreviewTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PreviewRejectsLockedItemBeforeInspectingGeneratedState()
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        Guid itemId = Guid.CreateVersion7();
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
            setup.Characters.Add(new Character(
                characterId, accountId, Guid.CreateVersion7(), "Forger", "FORGER0001", "HUMAN", "MALE", "WARRIOR", Now));
            CharacterItem item = new(itemId, characterId, "RECRUIT_IRON_SWORD", 1, Now);
            item.SetLocked(true);
            setup.CharacterItems.Add(item);
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        ItemReforgeService service = new(context, new StaticContentSnapshotProvider(content), new FixedTimeProvider(Now));

        ItemReforgePreviewResult result = await service.GetPreviewAsync(
            accountId, itemId, "suffix-1", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ItemReforgeErrorCodes.ItemLocked, result.ErrorCode);
    }

    [Fact]
    public async Task PreviewAllowsEquippedItemAndDifferentSlotAfterPreviousSelection()
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        Guid itemId = Guid.CreateVersion7();
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        ItemDefinition definition = content.Items!.Single(item => item.Id == "RECRUIT_IRON_SWORD");

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
            setup.Characters.Add(new Character(
                characterId, accountId, Guid.CreateVersion7(), "Forger", "FORGER0003", "HUMAN", "MALE", "WARRIOR", Now));

            CharacterItem item = new(itemId, characterId, definition.Id, 1, Now, definition.Version);
            item.ApplyGeneratedInstance(
                new GeneratedItemInstance(
                    60,
                    [
                        new GeneratedItemAffix(
                            "AFFIX_1", "STRENGTH", ItemStatIds.Strength,
                            12m, 5m, 20m, 1m, 1, false, false, 0),
                        new GeneratedItemAffix(
                            "AFFIX_2", "CRITICAL_CHANCE", ItemStatIds.CriticalChance,
                            4m, 2m, 8m, 0.1m, 1, false, false, 1)
                    ],
                    100m,
                    150m,
                    220m,
                    55.15m,
                    3,
                    false,
                    null,
                    null,
                    null,
                    definition.Name,
                    1),
                "TEST_HASH",
                "DROP",
                Guid.CreateVersion7(),
                "TEST_SOURCE");
            item.SelectReforgeSlot("AFFIX_1");

            setup.CharacterItems.Add(item);
            setup.CharacterEquipment.Add(new CharacterEquipment(characterId, EquipmentSlot.MainHand, itemId));
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        ItemReforgeService service = new(context, new StaticContentSnapshotProvider(content), new FixedTimeProvider(Now));

        ItemReforgePreviewResult result = await service.GetPreviewAsync(
            accountId, itemId, "AFFIX_2", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorCode);
        Assert.Equal("AFFIX_2", result.Current!.Affixes.Single(affix => affix.SlotKey == "AFFIX_2").SlotKey);
        Assert.NotNull(result.PossibleAffixes);
        Assert.NotEmpty(result.PossibleAffixes);
        Assert.All(result.PossibleAffixes, candidate =>
        {
            Assert.True(candidate.Max >= candidate.Min);
            Assert.True(candidate.Step > 0);
        });
    }

    [Fact]
    public async Task RollRejectsLockedItemWithoutCreatingAnOperation()
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        Guid itemId = Guid.CreateVersion7();
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
            setup.Characters.Add(new Character(
                characterId, accountId, Guid.CreateVersion7(), "Forger", "FORGER0002", "HUMAN", "MALE", "WARRIOR", Now));
            CharacterItem item = new(itemId, characterId, "RECRUIT_IRON_SWORD", 1, Now);
            item.SetLocked(true);
            setup.CharacterItems.Add(item);
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        ItemReforgeService service = new(context, new StaticContentSnapshotProvider(content), new FixedTimeProvider(Now));

        ItemReforgeOperationResult result = await service.RollAsync(
            accountId, itemId, "suffix-1", Guid.CreateVersion7(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ItemReforgeErrorCodes.ItemLocked, result.ErrorCode);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Empty(verify.ItemReforgeOperations);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
