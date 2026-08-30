using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Elyndor.IntegrationTests.Postgres;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class PhaseOneSchemaTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 8, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AccountTelegramUserIdIsUnique()
    {
        await using GameDbContext firstContext = postgres.CreateDbContext();
        firstContext.Accounts.Add(new Account(Guid.CreateVersion7(), 42, Now));
        await firstContext.SaveChangesAsync();

        await using GameDbContext secondContext = postgres.CreateDbContext();
        secondContext.Accounts.Add(new Account(Guid.CreateVersion7(), 42, Now));

        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => secondContext.SaveChangesAsync());

        Assert.Equal(PostgresErrorCodes.UniqueViolation, GetPostgresException(exception).SqlState);
        Assert.Equal("uq_accounts_telegram_user_id", GetPostgresException(exception).ConstraintName);
    }

    [Fact]
    public async Task CharacterAccountAndNormalizedNameAreUnique()
    {
        Guid firstAccountId = Guid.CreateVersion7();
        Guid secondAccountId = Guid.CreateVersion7();

        await using (GameDbContext setupContext = postgres.CreateDbContext())
        {
            setupContext.Accounts.AddRange(
                new Account(firstAccountId, 100, Now),
                new Account(secondAccountId, 200, Now));
            setupContext.Characters.Add(
                CreateCharacter(firstAccountId, "Артас", "АРТАС"));
            await setupContext.SaveChangesAsync();
        }

        await using GameDbContext duplicateNameContext = postgres.CreateDbContext();
        duplicateNameContext.Characters.Add(
            CreateCharacter(secondAccountId, "артас", "АРТАС"));

        DbUpdateException duplicateName = await Assert.ThrowsAsync<DbUpdateException>(
            () => duplicateNameContext.SaveChangesAsync());

        Assert.Equal("uq_characters_normalized_name", GetPostgresException(duplicateName).ConstraintName);

        await using GameDbContext duplicateAccountContext = postgres.CreateDbContext();
        duplicateAccountContext.Characters.Add(
            CreateCharacter(firstAccountId, "Тралл", "ТРАЛЛ"));

        DbUpdateException duplicateAccount = await Assert.ThrowsAsync<DbUpdateException>(
            () => duplicateAccountContext.SaveChangesAsync());

        Assert.Equal("uq_characters_account_id", GetPostgresException(duplicateAccount).ConstraintName);
    }

    [Fact]
    public async Task CharacterLocationVersionIsPersisted()
    {
        Guid accountId = Guid.CreateVersion7();
        Character character = CreateCharacter(accountId, "Jaina", "JAINA");

        await using (GameDbContext writeContext = postgres.CreateDbContext())
        {
            writeContext.Accounts.Add(new Account(accountId, 300, Now));
            writeContext.Characters.Add(character);
            writeContext.CharacterLocations.Add(
                new CharacterLocation(character.Id, "STARTER_TOWN", 7, Now));
            await writeContext.SaveChangesAsync();
        }

        await using GameDbContext readContext = postgres.CreateDbContext();
        CharacterLocation stored = await readContext.CharacterLocations.SingleAsync();

        Assert.Equal("STARTER_TOWN", stored.LocationId);
        Assert.Equal(7, stored.Version);
        Assert.Equal(Now, stored.UpdatedAtUtc);
    }

    private static Character CreateCharacter(Guid accountId, string name, string normalizedName) =>
        new(
            Guid.CreateVersion7(),
            accountId,
            Guid.CreateVersion7(),
            name,
            normalizedName,
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now);

    private static PostgresException GetPostgresException(DbUpdateException exception) =>
        Assert.IsType<PostgresException>(exception.InnerException);
}
