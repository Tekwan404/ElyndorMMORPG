using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Core.Quests;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Quests;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Quests;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class QuestInventoryCapacityTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 20, 8, 20, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TurnInRewardUsesSlotReleasedBeforeSave()
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        int wolfHideVersion = content.Items!
            .Single(item => item.Id == "WOLF_HIDE")
            .Version;

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.Accounts.Add(new Account(
                accountId,
                Random.Shared.NextInt64(1, long.MaxValue),
                Now));
            Character character = new(
                characterId,
                accountId,
                Guid.CreateVersion7(),
                "QuestCapacity",
                $"QCAP{characterId:N}"[..16],
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now);
            character.SetLevel(2);
            setup.Characters.Add(character);
            setup.CharacterVitals.Add(new CharacterVitals(
                characterId,
                250,
                0,
                Now,
                Now));
            setup.CharacterLocations.Add(new CharacterLocation(
                characterId,
                "WHISPERING_FOREST",
                1,
                Now));
            setup.QuestRewardGrants.Add(new QuestRewardGrant(
                characterId,
                "QUEST_01_FIRST_HUNT",
                Guid.CreateVersion7(),
                0,
                0,
                "[]",
                Now));
            setup.CharacterItems.Add(new CharacterItem(
                Guid.CreateVersion7(),
                characterId,
                "WOLF_HIDE",
                4,
                Now,
                wolfHideVersion));
            for (var index = 0; index < 99; index++)
            {
                setup.CharacterItems.Add(new CharacterItem(
                    Guid.CreateVersion7(),
                    characterId,
                    "RECRUIT_IRON_SWORD",
                    1,
                    Now.AddTicks(index + 1)));
            }
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        StaticContentSnapshotProvider provider = new(content);
        CharacterDerivedStateService derivedState = new(context, content, null);
        QuestService service = new(
            context,
            provider,
            derivedState,
            new SystemGameRandomFactory(),
            new FixedTimeProvider(Now));

        QuestMutationResult accepted = await service.AcceptAsync(
            accountId,
            "QUEST_02_WOLF_HIDES",
            CancellationToken.None);
        Assert.True(accepted.IsSuccess);

        QuestClaimResult claim = await service.ClaimAsync(
            accountId,
            "QUEST_02_WOLF_HIDES",
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.True(claim.IsSuccess);
        Assert.True(claim.Granted);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(
            100,
            await InventoryCapacity.CountUsedSlotsAsync(
                verify,
                characterId,
                CancellationToken.None));
        Assert.False(await verify.CharacterItems
            .AsNoTracking()
            .AnyAsync(item =>
                item.CharacterId == characterId
                && item.ItemDefinitionId == "WOLF_HIDE"));
        Assert.Equal(
            2,
            await verify.CharacterItems
                .AsNoTracking()
                .Where(item =>
                    item.CharacterId == characterId
                    && item.ItemDefinitionId == "SMALL_HEALING_POTION")
                .SumAsync(item => item.Quantity));
        Assert.False(await verify.PendingLootItems
            .AsNoTracking()
            .AnyAsync(item => item.CharacterId == characterId));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
