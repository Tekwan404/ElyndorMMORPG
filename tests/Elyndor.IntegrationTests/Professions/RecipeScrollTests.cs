using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Core.Professions;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Professions;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Professions;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class RecipeScrollTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 8, 0, 0, TimeSpan.Zero);
    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("LEATHERWORKING")]
    [InlineData("ALCHEMY")]
    public async Task StudyConsumesOnceAndUnlocksCraftAcrossRestart(string professionId)
    {
        var (account, character, content, recipe) = await Setup(professionId);
        Guid mutation = Guid.NewGuid();
        Assert.True((await Study(account, recipe.Id, mutation, content)).IsSuccess);
        Assert.True((await Study(account, recipe.Id, mutation, content)).Replayed);
        await using var verify = postgres.CreateDbContext();
        Assert.Single(await verify.CharacterRecipeKnowledge.ToArrayAsync());
        Assert.Equal(1, (await verify.CharacterItems.SingleAsync(item => item.ItemDefinitionId == recipe.LearningScrollItemId)).Quantity);
        foreach (var ingredient in recipe.Ingredients)
            verify.CharacterItems.Add(new CharacterItem(Guid.NewGuid(), character, ingredient.ItemId, ingredient.Quantity, Now));
        await verify.SaveChangesAsync();
        var crafted = await new ProfessionService(verify, content, new Clock()).CraftAsync(account, recipe.Id, Guid.NewGuid(), CancellationToken.None);
        Assert.True(crafted.IsSuccess, crafted.ErrorCode);
        Assert.Equal(recipe.OutputItemId, crafted.ItemId);
    }

    [Fact]
    public async Task CraftCannotBypassUnlearnedRecipe()
    {
        var (account, _, content, recipe) = await Setup();
        await using var db = postgres.CreateDbContext();
        var result = await new ProfessionService(db, content, new Clock()).CraftAsync(account, recipe.Id, Guid.NewGuid(), CancellationToken.None);
        Assert.Equal("profession_recipe_not_learned", result.ErrorCode);
        Assert.Empty(await db.CharacterRecipeKnowledge.ToArrayAsync());
    }

    [Fact]
    public async Task ConcurrentStudyDoesNotConsumeTwoScrolls()
    {
        var (account, _, content, recipe) = await Setup();
        var results = await Task.WhenAll(Study(account, recipe.Id, Guid.NewGuid(), content), Study(account, recipe.Id, Guid.NewGuid(), content));
        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => result.ErrorCode == "profession_recipe_already_learned");
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(1, (await verify.CharacterItems.SingleAsync()).Quantity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LockedScrollIsNotConsumed(bool transactionLocked)
    {
        var (account, _, content, recipe) = await Setup();
        await using (var setup = postgres.CreateDbContext())
        {
            var scroll = await setup.CharacterItems.SingleAsync();
            if (transactionLocked) scroll.AcquireTransactionLock(Guid.NewGuid());
            else scroll.SetLocked(true);
            await setup.SaveChangesAsync();
        }
        Assert.Equal("profession_recipe_scroll_missing", (await Study(account, recipe.Id, Guid.NewGuid(), content)).ErrorCode);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(2, (await verify.CharacterItems.IgnoreQueryFilters().SingleAsync()).Quantity);
        Assert.Empty(await verify.CharacterRecipeKnowledge.ToArrayAsync());
    }

    [Fact]
    public async Task ScrollOutsideInventoryIsNotConsumed()
    {
        var (account, character, content, recipe) = await Setup();
        await using (var setup = postgres.CreateDbContext())
        {
            var scroll = await setup.CharacterItems.SingleAsync();
            Guid custody = Guid.NewGuid();
            scroll.AcquireTransactionLock(custody);
            scroll.Transfer(custody, character, "MAILBOX");
            scroll.ReleaseTransactionLock(custody);
            await setup.SaveChangesAsync();
        }
        Assert.Equal("profession_recipe_scroll_missing", (await Study(account, recipe.Id, Guid.NewGuid(), content)).ErrorCode);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(2, (await verify.CharacterItems.IgnoreQueryFilters().SingleAsync()).Quantity);
        Assert.Empty(await verify.CharacterRecipeKnowledge.ToArrayAsync());
    }

    [Fact]
    public async Task StudyRejectsMutationReuseForAnotherRecipe()
    {
        var (account, _, content, recipe) = await Setup();
        Guid mutation = Guid.NewGuid();
        Assert.True((await Study(account, recipe.Id, mutation, content)).IsSuccess);
        Assert.Equal(ProfessionErrorCodes.IdempotencyConflict,
            (await Study(account, "ANOTHER_RECIPE", mutation, content)).ErrorCode);
        await using var verify = postgres.CreateDbContext();
        Assert.Single(await verify.CharacterRecipeKnowledge.ToArrayAsync());
        Assert.Equal(1, (await verify.CharacterItems.SingleAsync()).Quantity);
    }

    [Fact]
    public async Task StudyOutsideWorkshopPreservesScrollAndProgress()
    {
        var (account, character, content, recipe) = await Setup();
        await using (var setup = postgres.CreateDbContext())
        {
            await setup.CharacterLocations.Where(item => item.CharacterId == character).ExecuteDeleteAsync();
            setup.CharacterLocations.Add(new CharacterLocation(character, "WHISPERING_FOREST", 1, Now));
            await setup.SaveChangesAsync();
        }
        Assert.Equal(ProfessionErrorCodes.WrongWorkshop, (await Study(account, recipe.Id, Guid.NewGuid(), content)).ErrorCode);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(2, (await verify.CharacterItems.SingleAsync()).Quantity);
        Assert.Empty(await verify.CharacterRecipeKnowledge.ToArrayAsync());
    }

    [Fact]
    public async Task StudyRequiresAuthoredSkillAndProfession()
    {
        var (account, _, content, recipe) = await Setup();
        var advanced = content.GetCurrent().Package.ProfessionRecipes!.First(item => item.ProfessionId == recipe.ProfessionId && item.LearningScrollItemId is not null && item.RequiredSkill > 1);
        Assert.Equal(ProfessionErrorCodes.SkillTooLow, (await Study(account, advanced.Id, Guid.NewGuid(), content)).ErrorCode);
        var other = content.GetCurrent().Package.ProfessionRecipes!.First(item => item.ProfessionId != recipe.ProfessionId && item.LearningScrollItemId is not null);
        Assert.Equal(ProfessionErrorCodes.ProfessionNotLearned, (await Study(account, other.Id, Guid.NewGuid(), content)).ErrorCode);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(2, (await verify.CharacterItems.SingleAsync()).Quantity);
        Assert.Empty(await verify.CharacterRecipeKnowledge.ToArrayAsync());
    }

    [Fact]
    public async Task ForgettingRemovesKnowledgeAndStudyReplayCannotRestoreIt()
    {
        var (account, _, content, recipe) = await Setup();
        Guid mutation = Guid.NewGuid();
        Assert.True((await Study(account, recipe.Id, mutation, content)).IsSuccess);
        await using (var db = postgres.CreateDbContext())
        {
            var service = new ProfessionService(db, content, new Clock());
            Assert.True((await service.ForgetAsync(account, recipe.ProfessionId, Guid.NewGuid(), CancellationToken.None)).IsSuccess);
            Assert.True((await service.LearnAsync(account, recipe.ProfessionId, Guid.NewGuid(), CancellationToken.None)).IsSuccess);
        }
        Assert.True((await Study(account, recipe.Id, mutation, content)).Replayed);
        await using var verify = postgres.CreateDbContext();
        Assert.Empty(await verify.CharacterRecipeKnowledge.ToArrayAsync());
        Assert.Equal(1, (await verify.CharacterItems.SingleAsync()).Quantity);
    }

    private async Task<ProfessionMutationResult> Study(Guid account, string recipe, Guid mutation, StaticContentSnapshotProvider content)
    {
        await using var db = postgres.CreateDbContext();
        return await new ProfessionRecipeLearningService(db, content, new ProfessionService(db, content, new Clock()), new Clock())
            .LearnAsync(account, recipe, mutation, CancellationToken.None);
    }

    private async Task<(Guid Account, Guid Character, StaticContentSnapshotProvider Content, ProfessionRecipeDefinition Recipe)> Setup(string? professionId = null)
    {
        var content = new StaticContentSnapshotProvider(await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json")));
        var recipe = content.GetCurrent().Package.ProfessionRecipes!.First(recipe => recipe.LearningScrollItemId is not null && recipe.RequiredSkill == 1 && (professionId is null || recipe.ProfessionId == professionId));
        Guid account = Guid.NewGuid(), character = Guid.NewGuid();
        await using var db = postgres.CreateDbContext();
        db.Accounts.Add(new Account(account, Random.Shared.NextInt64(1, long.MaxValue), Now));
        db.Characters.Add(new Character(character, account, Guid.NewGuid(), "Artisan", $"ART{character:N}"[..16], "HUMAN", "MALE", "ARCHER", Now));
        db.CharacterLocations.Add(new CharacterLocation(character, "STARTER_TOWN", 1, Now));
        db.CharacterProfessions.Add(new CharacterProfession(character, recipe.ProfessionId, Now));
        db.CharacterItems.Add(new CharacterItem(Guid.NewGuid(), character, recipe.LearningScrollItemId!, 2, Now));
        await db.SaveChangesAsync();
        return (account, character, content, recipe);
    }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
