using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elyndor.IntegrationTests.Combat;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class CombatDurabilityServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 7, 9, 30, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task InterruptedCombatRefundsJournaledConsumable()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync();
        Guid sessionId = Guid.CreateVersion7();

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.ActiveCombatSessions.Add(new ActiveCombatSession(
                sessionId,
                characterId,
                Now,
                "0.13.3",
                "0.11.0"));
            setup.CharacterItems.Add(new CharacterItem(
                Guid.CreateVersion7(),
                characterId,
                "SMALL_HEALING_POTION",
                2,
                Now));
            await setup.SaveChangesAsync();
        }

        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        GameContentSnapshot snapshot = GameContentSnapshot.Create(package);

        await using GameDbContext context = postgres.CreateDbContext();
        CombatDurabilityService service = new(
            context,
            NullLogger<CombatDurabilityService>.Instance);

        string? reserveError = await service.ReserveConsumableAsync(
            accountId,
            sessionId,
            "combat-potion-1",
            "SMALL_HEALING_POTION",
            snapshot,
            Now,
            CancellationToken.None);

        Assert.Null(reserveError);
        Assert.Equal(
            1,
            await context.CharacterItems
                .Where(item =>
                    item.CharacterId == characterId
                    && item.ItemDefinitionId == "SMALL_HEALING_POTION")
                .SumAsync(item => item.Quantity));
        Assert.Single(await context.CombatConsumableUses.ToArrayAsync());

        int recovered = await service.RecoverInterruptedAsync(
            CancellationToken.None);

        Assert.Equal(1, recovered);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(
            2,
            await verify.CharacterItems
                .Where(item =>
                    item.CharacterId == characterId
                    && item.ItemDefinitionId == "SMALL_HEALING_POTION")
                .SumAsync(item => item.Quantity));
        Assert.Empty(await verify.ActiveCombatSessions.ToArrayAsync());
        Assert.Empty(await verify.CombatConsumableUses.ToArrayAsync());
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
            "Recovery",
            $"RECOVER{characterId:N}"[..16],
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now));
        await context.SaveChangesAsync();
        return (accountId, characterId);
    }
}
