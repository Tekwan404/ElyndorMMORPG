using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elyndor.IntegrationTests.Combat;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class CombatDurabilityDungeonRecoveryTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 16, 8, 5, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task InterruptedGroupDungeonCombatMarksEncounterWipedAndClearsAllParticipantJournals()
    {
        Guid firstAccountId = Guid.CreateVersion7();
        Guid secondAccountId = Guid.CreateVersion7();
        Guid firstCharacterId = Guid.CreateVersion7();
        Guid secondCharacterId = Guid.CreateVersion7();
        Guid runId = Guid.CreateVersion7();
        Guid encounterId = Guid.CreateVersion7();
        Guid sessionId = Guid.CreateVersion7();

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.Accounts.AddRange(
                new Account(firstAccountId, 91601, Now),
                new Account(secondAccountId, 91602, Now));
            setup.Characters.AddRange(
                new Character(
                    firstCharacterId,
                    firstAccountId,
                    Guid.CreateVersion7(),
                    "RecoveryOne",
                    "RECOVERYONE",
                    "HUMAN",
                    "MALE",
                    "WARRIOR",
                    Now),
                new Character(
                    secondCharacterId,
                    secondAccountId,
                    Guid.CreateVersion7(),
                    "RecoveryTwo",
                    "RECOVERYTWO",
                    "HUMAN",
                    "FEMALE",
                    "MAGE",
                    Now));

            DungeonRun run = DungeonRun.Create(
                runId,
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                "SHATTERED_ORDER",
                Now);
            run.AddMember(firstCharacterId, Now);
            run.AddMember(secondCharacterId, Now);

            DungeonEncounter encounter = DungeonEncounter.Create(
                encounterId,
                runId,
                0,
                "SHATTERED_ORDER_MIRROR_CASTELLAN_L25",
                Now);
            encounter.Activate(sessionId);
            run.Encounters.Add(encounter);
            setup.DungeonRuns.Add(run);

            setup.ActiveCombatSessions.AddRange(
                new ActiveCombatSession(
                    sessionId,
                    firstCharacterId,
                    Now,
                    "0.22.0",
                    "0.18.0"),
                new ActiveCombatSession(
                    sessionId,
                    secondCharacterId,
                    Now,
                    "0.22.0",
                    "0.18.0"));
            await setup.SaveChangesAsync();
        }

        await using GameDbContext recoveryContext = postgres.CreateDbContext();
        CombatDurabilityService durability = new(
            recoveryContext,
            NullLogger<CombatDurabilityService>.Instance);

        int recovered = await durability.RecoverInterruptedAsync(
            CancellationToken.None);

        Assert.Equal(2, recovered);

        await using GameDbContext verify = postgres.CreateDbContext();
        DungeonRun persistedRun = await verify.DungeonRuns
            .Include(run => run.Encounters)
            .SingleAsync(run => run.Id == runId);
        DungeonEncounter persistedEncounter = persistedRun.Encounters.Single();

        Assert.Equal(DungeonRunState.Active, persistedRun.State);
        Assert.Equal(DungeonEncounterState.Wiped, persistedEncounter.State);
        Assert.Equal(1, persistedEncounter.WipeCount);
        Assert.Equal(sessionId, persistedEncounter.CombatSessionId);
        Assert.Null(persistedEncounter.CompletedAtUtc);
        Assert.Empty(await verify.ActiveCombatSessions
            .Where(state => state.SessionId == sessionId)
            .ToArrayAsync());
    }
}
