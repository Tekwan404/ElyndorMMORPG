using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Core.Professions;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Professions;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Professions;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ResourceGatheringTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 6, 0, 0, TimeSpan.Zero);
    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task HarvestReplayAndRestartReturnExactlyOneStoredReward()
    {
        var (accountId, characterId) = await CreateCharacterAsync();
        Guid mutation = Guid.NewGuid();
        var first = await Gather(accountId, "FOREST_MINT_1", mutation);
        Assert.True(first.IsSuccess, first.ErrorCode);
        var replay = await Gather(accountId, "FOREST_MINT_1", mutation, Now.AddHours(1));
        Assert.True(replay.Replayed);
        Assert.Equal(first.Quantity, replay.Quantity);
        Assert.Equal(first.SkillIncreased, replay.SkillIncreased);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(first.Quantity, await verify.CharacterItems.Where(item => item.CharacterId == characterId && item.ItemDefinitionId == "FOREST_MINT").SumAsync(item => item.Quantity));
        Assert.Single(await verify.ResourceGatheringReceipts.ToArrayAsync());
        Assert.Equal(2, (await verify.CharacterProfessions.SingleAsync()).Skill);
    }

    [Fact]
    public async Task TwoPlayersCannotClaimOneSharedSpawn()
    {
        var one = await CreateCharacterAsync();
        var two = await CreateCharacterAsync();
        var results = await Task.WhenAll(Gather(one.AccountId, "FOREST_MINT_1", Guid.NewGuid()), Gather(two.AccountId, "FOREST_MINT_1", Guid.NewGuid()));
        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => result.ErrorCode == "resource_unavailable");
        Assert.True((await Gather(two.AccountId, "FOREST_MINT_1", Guid.NewGuid(), Now.AddSeconds(30))).IsSuccess);
    }

    [Fact]
    public async Task FullInventoryDoesNotConsumeNodeOrAdvanceSkill()
    {
        var (accountId, characterId) = await CreateCharacterAsync();
        await using (var setup = postgres.CreateDbContext())
        {
            var content = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
            for (int index = 0; index < content.InventoryProfile!.DefaultCapacity; index++)
                setup.CharacterItems.Add(new CharacterItem(Guid.NewGuid(), characterId, "ROUGH_HIDE", 99, Now));
            await setup.SaveChangesAsync();
        }
        var result = await Gather(accountId, "FOREST_MINT_1", Guid.NewGuid());
        Assert.Equal(ProfessionErrorCodes.InventoryFull, result.ErrorCode);
        await using var verify = postgres.CreateDbContext();
        Assert.Empty(await verify.ResourceGatheringReceipts.ToArrayAsync());
        Assert.Equal(1, (await verify.CharacterProfessions.SingleAsync()).Skill);
        Assert.Equal(DateTimeOffset.UnixEpoch, (await verify.ResourceNodes.SingleAsync()).AvailableAtUtc);
    }

    [Fact]
    public async Task PartialStackOverflowRollsBackTheEntireHarvest()
    {
        var (accountId, characterId) = await CreateCharacterAsync();
        await using (var setup = postgres.CreateDbContext())
        {
            var content = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
            setup.CharacterItems.Add(new CharacterItem(Guid.NewGuid(), characterId, "FOREST_MINT", 98, Now));
            for (int index = 1; index < content.InventoryProfile!.DefaultCapacity; index++)
                setup.CharacterItems.Add(new CharacterItem(Guid.NewGuid(), characterId, "ROUGH_HIDE", 99, Now));
            await setup.SaveChangesAsync();
        }
        await using (var context = postgres.CreateDbContext())
        {
            var content = new StaticContentSnapshotProvider(await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json")));
            var clock = new Clock(Now);
            var result = await new ResourceGatheringService(context, content, new ProfessionService(context, content, clock), new MaximumYieldFactory(), clock)
                .GatherAsync(accountId, "FOREST_MINT_1", Guid.NewGuid(), CancellationToken.None);
            Assert.Equal(ProfessionErrorCodes.InventoryFull, result.ErrorCode);
        }
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(98, (await verify.CharacterItems.SingleAsync(item => item.ItemDefinitionId == "FOREST_MINT")).Quantity);
        Assert.Equal(1, (await verify.CharacterProfessions.SingleAsync()).Skill);
        Assert.Empty(await verify.ResourceGatheringReceipts.ToArrayAsync());
        Assert.Equal(DateTimeOffset.UnixEpoch, (await verify.ResourceNodes.SingleAsync()).AvailableAtUtc);
    }

    [Fact]
    public async Task ReusedMutationForDifferentNodeIsRejected()
    {
        var character = await CreateCharacterAsync();
        var mutation = Guid.NewGuid();
        Assert.True((await Gather(character.AccountId, "FOREST_MINT_1", mutation)).IsSuccess);
        Assert.Equal(ProfessionErrorCodes.IdempotencyConflict, (await Gather(character.AccountId, "SILVERLEAF_1", mutation)).ErrorCode);
    }

    [Theory]
    [InlineData("DEWY_BLOSSOM_1", "profession_skill_too_low")]
    [InlineData("MISSING", "resource_not_found")]
    public async Task InvalidTargetCannotGrantMaterial(string nodeId, string expected)
    {
        var character = await CreateCharacterAsync();
        Assert.Equal(expected, (await Gather(character.AccountId, nodeId, Guid.NewGuid())).ErrorCode);
        await using var verify = postgres.CreateDbContext();
        Assert.Empty(await verify.CharacterItems.ToArrayAsync());
    }

    [Fact]
    public async Task GatheredHerbsBecomeOnePotionAndCraftReplayPreservesProgress()
    {
        var (accountId, characterId) = await CreateCharacterAsync();
        foreach (string node in new[] { "FOREST_MINT_1", "FOREST_MINT_2", "SILVERLEAF_1", "SILVERLEAF_2" })
            Assert.True((await Gather(accountId, node, Guid.NewGuid())).IsSuccess);

        await using (var setup = postgres.CreateDbContext())
        {
            setup.CharacterProfessions.Add(new CharacterProfession(characterId, ProfessionIds.Alchemy, Now));
            setup.CharacterLocations.Remove(await setup.CharacterLocations.SingleAsync());
            setup.CharacterLocations.Add(new CharacterLocation(characterId, "STARTER_TOWN", 1, Now));
            await setup.SaveChangesAsync();
        }

        Guid mutation = Guid.NewGuid();
        var content = new StaticContentSnapshotProvider(await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json")));
        string recipe = content.GetCurrent().Package.ProfessionRecipes!.First(recipe => recipe.ProfessionId == ProfessionIds.Alchemy && recipe.RequiredSkill == 1).Id;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            await using var context = postgres.CreateDbContext();
            var result = await new ProfessionService(context, content, new Clock(Now), new FixedRandomFactory())
                .CraftAsync(accountId, recipe, mutation, CancellationToken.None);
            Assert.True(result.IsSuccess, result.ErrorCode);
            Assert.Equal(attempt == 1, result.Replayed);
        }
        await using var verify = postgres.CreateDbContext();
        var potion = Assert.Single(await verify.CharacterItems.ToArrayAsync());
        Assert.Equal(content.GetCurrent().Package.ProfessionRecipes!.First(item => item.Id == recipe).OutputItemId, potion.ItemDefinitionId);
        Assert.Equal(1, potion.Quantity);
        Assert.Equal(2, (await verify.CharacterProfessions.SingleAsync(profession => profession.ProfessionId == ProfessionIds.Alchemy)).Skill);
        Assert.Equal(5, (await verify.CharacterProfessions.SingleAsync(profession => profession.ProfessionId == ProfessionIds.Herbalism)).Skill);
    }

    [Fact]
    public async Task GatheringInAnotherLocationCannotChangeProgress()
    {
        var character = await CreateCharacterAsync();
        await using (var setup = postgres.CreateDbContext())
        {
            setup.CharacterLocations.Remove(await setup.CharacterLocations.SingleAsync());
            setup.CharacterLocations.Add(new CharacterLocation(character.CharacterId, "STARTER_TOWN", 1, Now));
            await setup.SaveChangesAsync();
        }
        Assert.Equal("resource_wrong_location", (await Gather(character.AccountId, "FOREST_MINT_1", Guid.NewGuid())).ErrorCode);
        await using var verify = postgres.CreateDbContext();
        Assert.Empty(await verify.CharacterItems.ToArrayAsync());
        Assert.Empty(await verify.ResourceNodes.ToArrayAsync());
        Assert.Equal(1, (await verify.CharacterProfessions.SingleAsync()).Skill);
    }

    [Fact]
    public async Task ForgettingResetsSkillAndOldRetryCannotEraseRelearnedProfession()
    {
        var (accountId, characterId) = await CreateCharacterAsync();
        Assert.True((await Gather(accountId, "FOREST_MINT_1", Guid.NewGuid())).IsSuccess);
        await using (var setup = postgres.CreateDbContext())
        {
            setup.CharacterLocations.Remove(await setup.CharacterLocations.SingleAsync());
            setup.CharacterLocations.Add(new CharacterLocation(characterId, "STARTER_TOWN", 1, Now));
            await setup.SaveChangesAsync();
        }
        var content = new StaticContentSnapshotProvider(await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json")));
        Guid forgetId = Guid.NewGuid();
        await using var context = postgres.CreateDbContext();
        var service = new ProfessionService(context, content, new Clock(Now));
        Assert.True((await service.ForgetAsync(accountId, ProfessionIds.Herbalism, forgetId, CancellationToken.None)).IsSuccess);
        Assert.True((await service.LearnAsync(accountId, ProfessionIds.Herbalism, Guid.NewGuid(), CancellationToken.None)).IsSuccess);
        Assert.True((await service.ForgetAsync(accountId, ProfessionIds.Herbalism, forgetId, CancellationToken.None)).Replayed);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(1, (await verify.CharacterProfessions.SingleAsync()).Skill);
        Assert.Single(await verify.CharacterItems.ToArrayAsync());
        Assert.Single(await verify.ResourceGatheringReceipts.ToArrayAsync());
    }

    private async Task<ProfessionMutationResult> Gather(Guid accountId, string nodeId, Guid mutationId, DateTimeOffset? now = null)
    {
        await using var db = postgres.CreateDbContext();
        var content = new StaticContentSnapshotProvider(await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json")));
        var clock = new Clock(now ?? Now);
        return await new ResourceGatheringService(db, content, new ProfessionService(db, content, clock), new FixedRandomFactory(), clock)
            .GatherAsync(accountId, nodeId, mutationId, CancellationToken.None);
    }
    private async Task<(Guid AccountId, Guid CharacterId)> CreateCharacterAsync()
    {
        var accountId = Guid.NewGuid(); var characterId = Guid.NewGuid();
        await using var db = postgres.CreateDbContext();
        db.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
        db.Characters.Add(new Character(characterId, accountId, Guid.NewGuid(), "Herbalist", $"HERB{characterId:N}"[..16], "HUMAN", "MALE", "WARRIOR", Now));
        db.CharacterLocations.Add(new CharacterLocation(characterId, "WHISPERING_FOREST", 1, Now));
        db.CharacterProfessions.Add(new CharacterProfession(characterId, ProfessionIds.Herbalism, Now));
        await db.SaveChangesAsync();
        return (accountId, characterId);
    }
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class FixedRandomFactory : IGameRandomFactory { public IGameRandom Create() => new SequenceGameRandom(0m, 0m); }
    private sealed class MaximumYieldFactory : IGameRandomFactory { public IGameRandom Create() => new SequenceGameRandom(0.99m, 0m); }
}
