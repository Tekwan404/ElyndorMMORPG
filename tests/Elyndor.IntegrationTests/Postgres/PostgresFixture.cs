using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Elyndor.IntegrationTests.Postgres;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18.4")
        .WithDatabase("elyndor_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using GameDbContext context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public GameDbContext CreateDbContext()
    {
        DbContextOptions<GameDbContext> options = new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql(ConnectionString, options => options.EnableRetryOnFailure())
            .Options;

        return new GameDbContext(options);
    }

    public async Task ResetAsync()
    {
        await using GameDbContext context = CreateDbContext();
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE game.item_reforge_operations, game.character_item_affixes, game.combat_shared_loot_resolutions, game.combat_loot_rolls, game.combat_reward_grants, game.pending_loot_items, game.combat_consumable_uses, game.active_combat_sessions, game.dungeon_encounter_members, game.dungeon_encounters, game.dungeon_run_members, game.dungeon_runs, game.party_invites, game.party_members, game.parties, game.character_contract_completions, game.character_contract_acceptances, game.character_equipment, game.character_items, game.character_ability_cooldowns, game.character_talent_states, game.character_vitals, game.character_mutations, game.character_travel_states, game.content_audit_entries, game.content_releases, game.content_revisions, game.admin_command_audits, game.friend_requests, game.friendships, game.travel_operations, game.character_locations, game.characters, game.crystal_ledger_entries, game.crystal_wallets, game.accounts CASCADE");
    }
}
