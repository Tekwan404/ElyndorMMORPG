using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Talents;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Talents;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class TalentServiceIdempotencyTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 6, 0, 30, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task OldExactMutationReplaysAfterLaterTalentChangesAndPayloadReuseIsRejected()
    {
        GameContentPackage content = await LoadContentAsync();
        TalentDefinition talent = FindFirstLearnableTalent(content);
        (Guid accountId, Guid characterId) = await CreateCharacterAsync();

        await using GameDbContext context = postgres.CreateDbContext();
        TalentService service = new(context, content, new FixedTimeProvider(Now));

        TalentOperationResult initial =
            await service.GetAsync(accountId, CancellationToken.None);
        Assert.True(initial.IsSuccess);

        long initialVersion = initial.Snapshot!.State.StateVersion;
        string firstMutationId = Guid.CreateVersion7().ToString();

        TalentOperationResult learned = await service.LearnAsync(
            accountId,
            TalentLoadoutIds.Loadout1,
            talent.Id,
            initialVersion,
            firstMutationId,
            CancellationToken.None);
        Assert.True(learned.IsSuccess);

        TalentOperationResult payloadConflict = await service.LearnAsync(
            accountId,
            TalentLoadoutIds.Loadout2,
            talent.Id,
            initialVersion,
            firstMutationId,
            CancellationToken.None);
        Assert.False(payloadConflict.IsSuccess);
        Assert.Equal(TalentErrorCodes.MutationConflict, payloadConflict.ErrorCode);

        TalentOperationResult switched = await service.SwitchAsync(
            accountId,
            TalentLoadoutIds.Loadout2,
            learned.Snapshot!.State.StateVersion,
            Guid.CreateVersion7().ToString(),
            CancellationToken.None);
        Assert.True(switched.IsSuccess);

        TalentOperationResult oldReplay = await service.LearnAsync(
            accountId,
            TalentLoadoutIds.Loadout1,
            talent.Id,
            initialVersion,
            firstMutationId,
            CancellationToken.None);

        Assert.True(oldReplay.IsSuccess);
        Assert.Equal(
            switched.Snapshot!.State.StateVersion,
            oldReplay.Snapshot!.State.StateVersion);
        Assert.Equal(TalentLoadoutIds.Loadout2, oldReplay.Snapshot.State.ActiveLoadoutId);
        Assert.Equal(1, oldReplay.Snapshot.Loadout1Ranks[talent.Id]);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(
            2,
            await verify.CharacterMutations.CountAsync(
                mutation => mutation.CharacterId == characterId));
        CharacterTalentState persisted =
            await verify.CharacterTalentStates.SingleAsync(
                state => state.CharacterId == characterId);
        Assert.Equal(TalentLoadoutIds.Loadout2, persisted.ActiveLoadoutId);
        Assert.Equal(1, persisted.GetRanks(TalentLoadoutIds.Loadout1)[talent.Id]);
    }

    [Fact]
    public async Task ConcurrentExactMutationIsAppliedOnceAndBothCallersSucceed()
    {
        GameContentPackage content = await LoadContentAsync();
        TalentDefinition talent = FindFirstLearnableTalent(content);
        (Guid accountId, Guid characterId) = await CreateCharacterAsync();

        long initialVersion;
        await using (GameDbContext setupContext = postgres.CreateDbContext())
        {
            TalentService setupService =
                new(setupContext, content, new FixedTimeProvider(Now));
            TalentOperationResult initial =
                await setupService.GetAsync(accountId, CancellationToken.None);
            Assert.True(initial.IsSuccess);
            initialVersion = initial.Snapshot!.State.StateVersion;
        }

        string mutationId = Guid.CreateVersion7().ToString();
        await using GameDbContext firstContext = postgres.CreateDbContext();
        await using GameDbContext secondContext = postgres.CreateDbContext();
        TalentService first =
            new(firstContext, content, new FixedTimeProvider(Now));
        TalentService second =
            new(secondContext, content, new FixedTimeProvider(Now));

        TalentOperationResult[] results = await Task.WhenAll(
            first.LearnAsync(
                accountId,
                TalentLoadoutIds.Loadout1,
                talent.Id,
                initialVersion,
                mutationId,
                CancellationToken.None),
            second.LearnAsync(
                accountId,
                TalentLoadoutIds.Loadout1,
                talent.Id,
                initialVersion,
                mutationId,
                CancellationToken.None));

        Assert.All(results, result => Assert.True(result.IsSuccess));

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(
            1,
            await verify.CharacterMutations.CountAsync(
                mutation => mutation.CharacterId == characterId));
        CharacterTalentState persisted =
            await verify.CharacterTalentStates.SingleAsync(
                state => state.CharacterId == characterId);
        Assert.Equal(initialVersion + 1, persisted.StateVersion);
        Assert.Equal(1, persisted.GetRanks(TalentLoadoutIds.Loadout1)[talent.Id]);
    }

    private async Task<(Guid AccountId, Guid CharacterId)> CreateCharacterAsync()
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
            "TalentTester",
            $"TALENT{characterId:N}"[..16],
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now);
        character.SetLevel(60);

        context.Characters.Add(character);
        context.CharacterVitals.Add(new CharacterVitals(
            characterId,
            250,
            0,
            Now,
            Now));
        await context.SaveChangesAsync();

        return (accountId, characterId);
    }

    private static async Task<GameContentPackage> LoadContentAsync() =>
        await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

    private static TalentDefinition FindFirstLearnableTalent(
        GameContentPackage content) =>
        content.TalentTrees!
            .Single(tree => tree.ClassId == "WARRIOR")
            .Nodes.First(talent =>
                talent.RequiredSpentPoints == 0
                && talent.Prerequisites.Count == 0
                && (talent.RequiredLevel is null || talent.RequiredLevel <= 60));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
