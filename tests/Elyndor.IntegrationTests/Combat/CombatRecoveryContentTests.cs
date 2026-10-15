using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Identity;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Dungeons;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Progression;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elyndor.IntegrationTests.Combat;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class CombatRecoveryContentTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 0, 0, 0, TimeSpan.Zero);
    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RecoveryUsesBattleContentAfterRewardChangesOrMonsterRemoval(bool removeMonster, bool defeat)
    {
        GameContentPackage original = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        GameContentPackage oldContent = original with
        {
            ContentVersion = "99.1.0",
            Monsters = original.Monsters!.Select(monster => monster.Id == "FOREST_WOLF_L1"
                ? monster with { GoldRewardMin = 7, GoldRewardMax = 7, LootTableId = null, GrantsXp = false }
                : monster).ToArray()
        };
        DungeonDefinition dungeon = oldContent.Dungeons![0] with
        {
            Encounters = [oldContent.Dungeons![0].Encounters[0] with { MonsterId = "FOREST_WOLF_L1" }]
        };
        oldContent = oldContent with { Dungeons = [dungeon] };
        GameContentPackage newContent = oldContent with
        {
            ContentVersion = "99.2.0",
            Dungeons = [],
            Monsters = oldContent.Monsters!.Where(monster => !removeMonster || monster.Id != "FOREST_WOLF_L1")
                .Select(monster => monster.Id == "FOREST_WOLF_L1"
                    ? monster with { GoldRewardMin = 777, GoldRewardMax = 777 } : monster).ToArray()
        };
        Guid characterId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Guid sessionId = Guid.NewGuid();
        await using (GameDbContext seed = postgres.CreateDbContext())
        {
            seed.Accounts.Add(new Account(accountId, 42, Now));
            seed.Characters.Add(new Character(characterId, accountId, Guid.NewGuid(), "AuditHero", "AUDITHERO", "HUMAN", "MALE", "WARRIOR", Now));
            seed.CharacterVitals.Add(new CharacterVitals(characterId, 100, 0, Now, Now));
            seed.CharacterLocations.Add(new CharacterLocation(characterId, "WHISPERING_FOREST", 1, Now));
            seed.ActiveCombatSessions.Add(new ActiveCombatSession(sessionId, characterId, Now, oldContent.ContentVersion, oldContent.BalanceVersion));
            DungeonRun run = DungeonRun.Create(Guid.NewGuid(), Guid.NewGuid(), characterId, dungeon.Id, Now);
            run.AddMember(characterId, Now);
            DungeonEncounter encounter = DungeonEncounter.Create(Guid.NewGuid(), run.Id, 0, "FOREST_WOLF_L1", Now);
            encounter.Activate(sessionId);
            run.Encounters.Add(encounter);
            seed.DungeonRuns.Add(run);
            await seed.SaveChangesAsync();
        }
        CombatSessionSnapshot terminal = new(sessionId, 1, defeat ? CombatSessionStatus.Defeat : CombatSessionStatus.Victory, Now,
            Actor(characterId, CombatActorKind.Player, "WARRIOR", defeat ? 0 : 100),
            Actor(Guid.NewGuid(), CombatActorKind.Monster, "FOREST_WOLF_L1", defeat ? 100 : 0),
            oldContent.ContentVersion, oldContent.BalanceVersion);
        await using (ServiceProvider failed = BuildProvider(oldContent, interrupt: true))
        {
            ICombatSessionFinalizer finalizer = failed.GetRequiredService<ICombatSessionFinalizer>();
            await Assert.ThrowsAsync<IOException>(() => finalizer.FinalizeAsync(characterId,
                terminal, GameContentSnapshot.Create(oldContent), CancellationToken.None));
        }
        await using (GameDbContext journal = postgres.CreateDbContext())
        {
            Assert.NotNull((await journal.ActiveCombatSessions.SingleAsync()).TerminalSnapshotJson);
            Assert.Empty(await journal.CombatRewardGrants.ToArrayAsync());
        }
        await using ServiceProvider healthy = BuildProvider(newContent, interrupt: false);
        using IServiceScope scope = healthy.CreateScope();
        CombatDurabilityService recovery = scope.ServiceProvider.GetRequiredService<CombatDurabilityService>();
        Assert.Equal(1, await recovery.RecoverInterruptedAsync(CancellationToken.None));
        Assert.Equal(0, await recovery.RecoverInterruptedAsync(CancellationToken.None));
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(defeat ? 0 : 7, (await verify.Characters.SingleAsync()).Gold);
        if (defeat)
        {
            Assert.Empty(await verify.CombatRewardGrants.ToArrayAsync());
            Assert.Equal(dungeon.EntryLocationId, (await verify.CharacterLocations.SingleAsync()).LocationId);
        }
        else Assert.Equal(7, (await verify.CombatRewardGrants.SingleAsync()).GoldEarned);
        Assert.Empty(await verify.ActiveCombatSessions.ToArrayAsync());
        Assert.Equal(defeat ? DungeonRunState.Active : DungeonRunState.Completed, (await verify.DungeonRuns.SingleAsync()).State);
        Assert.Equal(defeat ? DungeonEncounterState.Wiped : DungeonEncounterState.Completed, (await verify.DungeonEncounters.SingleAsync()).State);
    }

    private ServiceProvider BuildProvider(GameContentPackage content, bool interrupt)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddScoped<GameDbContext>(_ => postgres.CreateDbContext());
        services.AddSingleton<IContentSnapshotProvider>(new StaticContentSnapshotProvider(content));
        services.AddSingleton<TimeProvider>(new FixedTime());
        services.AddSingleton<IGameRandomFactory, SystemGameRandomFactory>();
        services.AddScoped<InventoryEquipmentService>();
        services.AddScoped<CharacterDerivedStateService>();
        if (interrupt)
            services.AddScoped<CharacterAbilityCooldownStore>(_ => throw new IOException("Injected failure after terminal journal."));
        else
            services.AddScoped<CharacterAbilityCooldownStore>();
        services.AddScoped<CombatRewardService>();
        services.AddScoped<PartyService>();
        services.AddScoped<DungeonService>();
        services.AddScoped<CombatDurabilityService>();
        services.AddSingleton<ICombatSessionFinalizer, CombatSessionFinalizer>();
        return services.BuildServiceProvider();
    }

    private static CombatActorSnapshot Actor(Guid id, CombatActorKind kind, string definition, decimal hp) =>
        new(id, kind, definition, definition, hp, 100, "NONE", 0, 100, false, null,
            new Dictionary<string, DateTimeOffset>(), new HashSet<string>(), [], []);

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
