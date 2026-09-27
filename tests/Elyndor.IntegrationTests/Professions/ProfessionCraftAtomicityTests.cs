using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Core.Professions;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Professions;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Professions;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ProfessionCraftAtomicityTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 27, 18, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CraftRollsBackConsumptionAndResultWhenFailureOccursBeforeCommitThenRetriesOnce()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync();
        GameContentPackage content = await CreateContentAsync();
        Guid mutationId = Guid.CreateVersion7();

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.CharacterProfessions.Add(new CharacterProfession(
                characterId,
                ProfessionIds.Skinning,
                Now));
            setup.CharacterItems.Add(new CharacterItem(
                Guid.CreateVersion7(),
                characterId,
                "ROUGH_HIDE",
                2,
                Now));
            await setup.SaveChangesAsync();
        }

        await using (GameDbContext faultingContext = CreateFaultingContext())
        {
            ProfessionService service = CreateService(faultingContext, content);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CraftAsync(
                accountId,
                "TEST_ATOMIC_CRAFT",
                mutationId,
                CancellationToken.None));
        }

        await using (GameDbContext afterFailure = postgres.CreateDbContext())
        {
            CharacterItem material = await afterFailure.CharacterItems.AsNoTracking()
                .SingleAsync(item =>
                    item.CharacterId == characterId
                    && item.ItemDefinitionId == "ROUGH_HIDE");
            Assert.Equal(2, material.Quantity);
            Assert.False(await afterFailure.CharacterItems.AsNoTracking()
                .AnyAsync(item =>
                    item.CharacterId == characterId
                    && item.ItemDefinitionId == "RECRUIT_IRON_SWORD"));
            Assert.False(await afterFailure.CharacterMutations.AsNoTracking()
                .AnyAsync(item =>
                    item.CharacterId == characterId
                    && item.MutationId == mutationId));
        }

        await using (GameDbContext retryContext = postgres.CreateDbContext())
        {
            ProfessionService service = CreateService(retryContext, content);
            ProfessionMutationResult retry = await service.CraftAsync(
                accountId,
                "TEST_ATOMIC_CRAFT",
                mutationId,
                CancellationToken.None);
            ProfessionMutationResult replay = await service.CraftAsync(
                accountId,
                "TEST_ATOMIC_CRAFT",
                mutationId,
                CancellationToken.None);

            Assert.True(retry.IsSuccess);
            Assert.False(retry.Replayed);
            Assert.True(replay.IsSuccess);
            Assert.True(replay.Replayed);
        }

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.False(await verify.CharacterItems.AsNoTracking()
            .AnyAsync(item =>
                item.CharacterId == characterId
                && item.ItemDefinitionId == "ROUGH_HIDE"));
        Assert.Equal(1, await verify.CharacterItems.AsNoTracking()
            .CountAsync(item =>
                item.CharacterId == characterId
                && item.ItemDefinitionId == "RECRUIT_IRON_SWORD"));
        Assert.Equal(1, await verify.CharacterMutations.AsNoTracking()
            .CountAsync(item =>
                item.CharacterId == characterId
                && item.MutationId == mutationId));
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
        context.Characters.Add(new Character(
            characterId,
            accountId,
            Guid.CreateVersion7(),
            "Crafter",
            $"CRAFTER{characterId:N}"[..16],
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now));
        await context.SaveChangesAsync();
        return (accountId, characterId);
    }

    private static async Task<GameContentPackage> CreateContentAsync()
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        ProfessionRecipeDefinition recipe = new(
            "TEST_ATOMIC_CRAFT",
            ProfessionIds.Skinning,
            "Atomic craft",
            1,
            300,
            "RECRUIT_IRON_SWORD",
            1,
            [new ProfessionRecipeIngredient("ROUGH_HIDE", 2)]);
        return content with
        {
            ProfessionRecipes = (content.ProfessionRecipes ?? [])
                .Concat([recipe])
                .ToArray()
        };
    }

    private ProfessionService CreateService(
        GameDbContext context,
        GameContentPackage content) =>
        new(
            context,
            new StaticContentSnapshotProvider(content),
            new FixedTimeProvider(Now));

    private GameDbContext CreateFaultingContext()
    {
        DbContextOptions<GameDbContext> options = new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql(postgres.ConnectionString, builder => builder.EnableRetryOnFailure())
            .AddInterceptors(new ThrowOnceAfterSaveChangesInterceptor())
            .Options;
        return new GameDbContext(options);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}