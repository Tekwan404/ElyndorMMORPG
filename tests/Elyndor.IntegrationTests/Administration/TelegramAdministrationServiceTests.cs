using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Administration;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class TelegramAdministrationServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RepeatedUpdateDoesNotApplyRelocationTwice()
    {
        await SeedCharacterAsync(732_707_324);

        AdministrationResult first = await ExecuteAsync(
            9001,
            new AdministrationOperation(
                AdministrationOperationType.SetLocation,
                732_707_324,
                "WHISPERING_FOREST"));
        AdministrationResult retry = await ExecuteAsync(
            9001,
            new AdministrationOperation(
                AdministrationOperationType.SetLocation,
                732_707_324,
                "WHISPERING_FOREST"));

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsDuplicate);
        await using GameDbContext context = postgres.CreateDbContext();
        CharacterLocation location = await context.CharacterLocations.SingleAsync();
        Assert.Equal("WHISPERING_FOREST", location.LocationId);
        Assert.Equal(2, location.Version);
        Assert.Equal(1, await context.AdminCommandAudits.CountAsync());
    }

    [Fact]
    public async Task DeleteRemovesCharacterStateButPreservesAccount()
    {
        await SeedCharacterAsync(732_707_324);

        AdministrationResult result = await ExecuteAsync(
            9002,
            new AdministrationOperation(
                AdministrationOperationType.Delete,
                732_707_324,
                "Arthas"));

        Assert.True(result.IsSuccess);
        await using GameDbContext context = postgres.CreateDbContext();
        Assert.Equal(1, await context.Accounts.CountAsync());
        Assert.Empty(await context.Characters.ToListAsync());
        Assert.Empty(await context.CharacterVitals.ToListAsync());
        Assert.Empty(await context.CharacterLocations.ToListAsync());
    }

    private async Task<AdministrationResult> ExecuteAsync(
        long updateId,
        AdministrationOperation operation)
    {
        await using GameDbContext context = postgres.CreateDbContext();
        TelegramAdministrationService service = new(
            context,
            new FixedTimeProvider(Now),
            CreateContent(),
            null);
        return await service.ExecuteAsync(
            updateId,
            732_707_324,
            operation,
            CancellationToken.None);
    }

    private async Task SeedCharacterAsync(long telegramUserId)
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(accountId, telegramUserId, Now));
        context.Characters.Add(new Character(
            characterId,
            accountId,
            Guid.CreateVersion7(),
            "Arthas",
            "ARTHAS",
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now));
        context.CharacterVitals.Add(new CharacterVitals(characterId, 150, 0, Now, Now));
        context.CharacterLocations.Add(new CharacterLocation(characterId, "STARTER_TOWN", 1, Now));
        await context.SaveChangesAsync();
    }

    private static GameContentPackage CreateContent() => PhaseTwoTestContent.Create(
        Now,
        [
            new("RACE", "HUMAN", []),
            new("RACE", "UNDEAD", []),
            new("GENDER", "MALE", []),
            new("CLASS", "WARRIOR", []),
            new("CLASS", "ARCHER", []),
            new("CLASS", "MAGE", [])
        ],
        [
            new("STARTER_TOWN", "Starter Town", "SAFE", 1, ["WHISPERING_FOREST"]),
            new("WHISPERING_FOREST", "Whispering Forest", "LOW", 2, ["STARTER_TOWN"])
        ]);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
