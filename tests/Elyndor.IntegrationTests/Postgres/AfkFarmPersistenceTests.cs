using Elyndor.Core.Afk;
using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Elyndor.IntegrationTests.Postgres;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class AfkFarmPersistenceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SessionPersistsAcrossDbContextRecreation()
    {
        (Guid accountId, Character character) = await SeedCharacterAsync();
        Guid sessionId = Guid.CreateVersion7();

        await using (GameDbContext writeContext = postgres.CreateDbContext())
        {
            writeContext.AfkFarmSessions.Add(CreateSession(sessionId, character.Id));
            await writeContext.SaveChangesAsync();
        }

        await using GameDbContext readContext = postgres.CreateDbContext();
        AfkFarmSession stored = await readContext.AfkFarmSessions.SingleAsync();

        Assert.Equal(sessionId, stored.Id);
        Assert.Equal(character.Id, stored.CharacterId);
        Assert.Equal(AfkFarmStatus.Active, stored.Status);
        Assert.Equal(Now, stored.LastProcessedAtUtc);
        Assert.NotEqual(Guid.Empty, accountId);
    }

    [Fact]
    public async Task DatabaseRejectsSecondActiveSessionForSameCharacter()
    {
        (_, Character character) = await SeedCharacterAsync();

        await using (GameDbContext firstContext = postgres.CreateDbContext())
        {
            firstContext.AfkFarmSessions.Add(CreateSession(Guid.CreateVersion7(), character.Id));
            await firstContext.SaveChangesAsync();
        }

        await using GameDbContext secondContext = postgres.CreateDbContext();
        secondContext.AfkFarmSessions.Add(CreateSession(Guid.CreateVersion7(), character.Id));

        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => secondContext.SaveChangesAsync());
        PostgresException postgresException = Assert.IsType<PostgresException>(exception.InnerException);

        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
        Assert.Equal("uq_afk_farm_sessions_active_character_id", postgresException.ConstraintName);
    }

    [Fact]
    public async Task CancelledSessionDoesNotBlockNewActiveSession()
    {
        (_, Character character) = await SeedCharacterAsync();

        await using (GameDbContext firstContext = postgres.CreateDbContext())
        {
            AfkFarmSession cancelled = CreateSession(Guid.CreateVersion7(), character.Id);
            cancelled.Cancel(Now.AddMinutes(10));
            firstContext.AfkFarmSessions.Add(cancelled);
            await firstContext.SaveChangesAsync();
        }

        await using (GameDbContext secondContext = postgres.CreateDbContext())
        {
            secondContext.AfkFarmSessions.Add(CreateSession(Guid.CreateVersion7(), character.Id));
            await secondContext.SaveChangesAsync();
        }

        await using GameDbContext readContext = postgres.CreateDbContext();
        Assert.Equal(2, await readContext.AfkFarmSessions.CountAsync());
        Assert.Equal(1, await readContext.AfkFarmSessions.CountAsync(
            session => session.Status == AfkFarmStatus.Active));
    }

    private async Task<(Guid AccountId, Character Character)> SeedCharacterAsync()
    {
        Guid accountId = Guid.CreateVersion7();
        Character character = new(
            Guid.CreateVersion7(),
            accountId,
            Guid.CreateVersion7(),
            "AfkTester",
            "AFKTESTER",
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now);

        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
        context.Characters.Add(character);
        await context.SaveChangesAsync();
        return (accountId, character);
    }

    private static AfkFarmSession CreateSession(Guid sessionId, Guid characterId) => new(
        sessionId,
        characterId,
        "WHISPERING_FOREST",
        AfkFarmMode.Safe,
        Now,
        Now.AddHours(1),
        "content-v1",
        "balance-v1",
        "{}",
        Now);
}
