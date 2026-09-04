using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Talents;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Characters;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class TalentDerivedVitalsLifecycleTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 4, 12, 30, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task LearnAndResetActiveTalentRescaleMana()
    {
        GameContentPackage content = await CreateContentAsync();
        (Guid accountId, Guid characterId) = await CreateMageAsync(currentResource: 85);

        await using GameDbContext context = postgres.CreateDbContext();
        TalentService service = new(context, content, new FixedTimeProvider(Now));

        TalentOperationResult learned = await service.LearnAsync(
            accountId,
            TalentLoadoutIds.Loadout1,
            "TEST_MANA_WELL",
            expectedStateVersion: 1,
            mutationId: "learn-mana-well",
            CancellationToken.None);

        Assert.True(learned.IsSuccess);
        await AssertResourceAsync(characterId, 135);

        TalentOperationResult reset = await service.ResetAsync(
            accountId,
            TalentLoadoutIds.Loadout1,
            learned.Snapshot!.State.StateVersion,
            "reset-mana-well",
            CancellationToken.None);

        Assert.True(reset.IsSuccess);
        await AssertResourceAsync(characterId, 85);
    }

    [Fact]
    public async Task SwitchingToResourceTalentLoadoutRescalesMana()
    {
        GameContentPackage content = await CreateContentAsync();
        (Guid accountId, Guid characterId) = await CreateMageAsync(currentResource: 85);
        TalentTreeDefinition tree = content.TalentTrees!.Single(item => item.ClassId == "MAGE");

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            CharacterTalentState state = new(
                characterId,
                tree.Id,
                tree.Version,
                Now);
            state.ReplaceRanks(
                TalentLoadoutIds.Loadout2,
                new Dictionary<string, int> { ["TEST_MANA_WELL"] = 1 },
                Now);
            setup.CharacterTalentStates.Add(state);
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        TalentService service = new(context, content, new FixedTimeProvider(Now));

        TalentOperationResult switched = await service.SwitchAsync(
            accountId,
            TalentLoadoutIds.Loadout2,
            expectedStateVersion: 2,
            mutationId: "switch-mana-loadout",
            CancellationToken.None);

        Assert.True(switched.IsSuccess);
        await AssertResourceAsync(characterId, 135);
    }

    private async Task<(Guid AccountId, Guid CharacterId)> CreateMageAsync(decimal currentResource)
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
            "ManaLifecycle",
            $"MANA{characterId:N}"[..16],
            "HUMAN",
            "FEMALE",
            "MAGE",
            Now);
        character.SetLevel(2);
        context.Characters.Add(character);
        context.CharacterVitals.Add(new CharacterVitals(
            characterId,
            100,
            currentResource,
            Now,
            Now));
        await context.SaveChangesAsync();
        return (accountId, characterId);
    }

    private async Task AssertResourceAsync(Guid characterId, decimal expected)
    {
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(expected, await verify.CharacterVitals
            .AsNoTracking()
            .Where(vitals => vitals.CharacterId == characterId)
            .Select(vitals => vitals.CurrentResource)
            .SingleAsync());
    }

    private static async Task<GameContentPackage> CreateContentAsync()
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        TalentTreeDefinition tree = new(
            "TEST_MAGE_D2_TREE",
            "MAGE",
            1,
            1,
            [new TalentBranchDefinition("ARCANE", "Arcane", "D2 lifecycle test branch.", 1)],
            [
                new TalentDefinition(
                    "TEST_MANA_WELL",
                    "ARCANE",
                    1,
                    0,
                    "Mana Well",
                    "Mana Well",
                    1,
                    [],
                    "Increases maximum Mana for D2 lifecycle verification.",
                    Modifiers:
                    [
                        new TalentModifierDefinition(
                            TalentModifierType.ResourceModifier,
                            TalentModifierKeys.MaxResourceFlat,
                            [100])
                    ])
            ]);

        return content with
        {
            TalentTrees = (content.TalentTrees ?? [])
                .Where(item => !string.Equals(item.ClassId, "MAGE", StringComparison.Ordinal))
                .Append(tree)
                .ToArray()
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
