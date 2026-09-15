using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Professions;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Professions;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Professions;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ProfessionServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 11, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SkinningCompletesWithNpgsqlRetryStrategy()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync();
        Guid combatSessionId = Guid.CreateVersion7();
        Guid enemyActorId = Guid.CreateVersion7();
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.CharacterProfessions.Add(new CharacterProfession(characterId, ProfessionIds.Skinning, Now));
            setup.SkinnableCorpses.Add(new SkinnableCorpse(
                characterId,
                combatSessionId,
                enemyActorId,
                "FOREST_WOLF_L1",
                Now,
                Now.AddMinutes(30)));
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        ProfessionService service = await CreateServiceAsync(context);

        Guid mutationId = Guid.CreateVersion7();
        ProfessionMutationResult result = await service.SkinAsync(
            accountId,
            combatSessionId,
            enemyActorId,
            mutationId,
            CancellationToken.None);
        ProfessionMutationResult replay = await service.SkinAsync(
            accountId,
            combatSessionId,
            enemyActorId,
            mutationId,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ROUGH_LEATHER", result.ItemId);
        Assert.InRange(result.Quantity, 1, 2);
        Assert.True(replay.IsSuccess);
        Assert.True(replay.Replayed);
        Assert.Equal(result.ItemId, replay.ItemId);
        Assert.Equal(result.Quantity, replay.Quantity);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.True(await verify.SkinnableCorpses
            .AnyAsync(corpse => corpse.CharacterId == characterId && corpse.SkinnedAtUtc != null));
    }

    private async Task<(Guid AccountId, Guid CharacterId)> CreateCharacterAsync()
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
        context.Characters.Add(new Character(
            characterId,
            accountId,
            Guid.CreateVersion7(),
            "Skinner",
            $"SKINNER{characterId:N}"[..16],
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now));
        await context.SaveChangesAsync();
        return (accountId, characterId);
    }

    private static async Task<ProfessionService> CreateServiceAsync(GameDbContext context)
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        return new ProfessionService(
            context,
            new StaticContentSnapshotProvider(content),
            new FixedTimeProvider(Now));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
