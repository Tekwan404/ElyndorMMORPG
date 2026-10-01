using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Items;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class CharacterItemIdentityTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 27, 18, 30, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DuplicateItemUuidIsRejectedByDatabasePrimaryKey()
    {
        Guid characterId = await CreateCharacterAsync();
        Guid itemId = Guid.CreateVersion7();

        await using (GameDbContext firstContext = postgres.CreateDbContext())
        {
            firstContext.CharacterItems.Add(new CharacterItem(
                itemId,
                characterId,
                "RECRUIT_IRON_SWORD",
                1,
                Now));
            await firstContext.SaveChangesAsync();
        }

        await using (GameDbContext duplicateContext = postgres.CreateDbContext())
        {
            duplicateContext.CharacterItems.Add(new CharacterItem(
                itemId,
                characterId,
                "ROUGH_HIDE",
                1,
                Now.AddSeconds(1)));
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                duplicateContext.SaveChangesAsync());
        }

        await using GameDbContext verify = postgres.CreateDbContext();
        CharacterItem persisted = await verify.CharacterItems.AsNoTracking()
            .SingleAsync(item => item.Id == itemId);
        Assert.Equal("RECRUIT_IRON_SWORD", persisted.ItemDefinitionId);
        Assert.Equal(1, await verify.CharacterItems.AsNoTracking()
            .CountAsync(item => item.Id == itemId));
    }

    private async Task<Guid> CreateCharacterAsync()
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
            "Identity",
            $"IDENTITY{characterId:N}"[..16],
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now));
        await context.SaveChangesAsync();
        return characterId;
    }
}