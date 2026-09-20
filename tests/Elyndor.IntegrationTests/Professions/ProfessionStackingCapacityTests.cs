using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Core.Professions;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Professions;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Professions;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ProfessionStackingCapacityTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 7, 35, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SkinningFillsExistingStackWhenBackpackIsFull()
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        Guid combatSessionId = Guid.CreateVersion7();
        Guid enemyActorId = Guid.CreateVersion7();

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
            setup.Characters.Add(new Character(
                characterId,
                accountId,
                Guid.CreateVersion7(),
                "Stacker",
                $"STACKER{characterId:N}"[..16],
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now));
            setup.CharacterProfessions.Add(new CharacterProfession(characterId, ProfessionIds.Skinning, Now));
            setup.SkinnableCorpses.Add(new SkinnableCorpse(
                characterId,
                combatSessionId,
                enemyActorId,
                "FOREST_WOLF_L1",
                Now,
                Now.AddMinutes(30)));

            setup.CharacterItems.Add(new CharacterItem(
                Guid.CreateVersion7(),
                characterId,
                "ROUGH_HIDE",
                1,
                Now));
            for (int index = 0; index < 99; index++)
            {
                setup.CharacterItems.Add(new CharacterItem(
                    Guid.CreateVersion7(),
                    characterId,
                    "ARCHER_COMMON_WHISPER_TRACKER_HANDS",
                    1,
                    Now.AddTicks(index + 1)));
            }

            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        ProfessionService service = new(
            context,
            new StaticContentSnapshotProvider(content),
            new FixedTimeProvider(Now));

        ProfessionMutationResult result = await service.SkinAsync(
            accountId,
            combatSessionId,
            enemyActorId,
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorCode);
        Assert.Equal("ROUGH_HIDE", result.ItemId);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(
            100,
            await InventoryCapacity.CountUsedSlotsAsync(
                verify,
                characterId,
                CancellationToken.None));
        Assert.Equal(
            1 + result.Quantity,
            await verify.CharacterItems
                .Where(item => item.CharacterId == characterId && item.ItemDefinitionId == "ROUGH_HIDE")
                .SumAsync(item => item.Quantity));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
