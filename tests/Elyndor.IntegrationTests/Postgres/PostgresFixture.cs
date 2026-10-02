using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Npgsql;

namespace Elyndor.IntegrationTests.Postgres;

public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _connectionString;

    public string ConnectionString => _connectionString ?? throw new InvalidOperationException("Fixture not initialized.");

    public async Task InitializeAsync()
    {
        string? external = Environment.GetEnvironmentVariable("ELYNDOR_TEST_POSTGRES");
        if (!string.IsNullOrWhiteSpace(external))
        {
            var settings = new NpgsqlConnectionStringBuilder(external);
            if (settings.Host is not ("127.0.0.1" or "localhost") || settings.Database?.EndsWith("_tests", StringComparison.Ordinal) != true)
                throw new InvalidOperationException("External test database must be local and end in _tests; tests truncate its data.");
            _connectionString = external;
        }
        else
        {
            _container = new PostgreSqlBuilder("postgres:18.4").WithDatabase("elyndor_tests")
                .WithUsername("postgres").WithPassword("postgres").Build();
            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }

        await using GameDbContext context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

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
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE game.release_admin_notifications");
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE game.world_boss_reward_settlements, game.world_boss_damage_mutations, game.world_boss_party_contributions, game.world_boss_contributions, game.world_boss_spawns, game.promo_code_redemptions, game.item_reforge_operations, game.character_item_affixes, game.combat_shared_loot_resolutions, game.combat_loot_rolls, game.combat_reward_grants, game.pending_loot_items, game.combat_consumable_uses, game.active_combat_sessions, game.dungeon_encounter_members, game.dungeon_encounters, game.dungeon_run_members, game.dungeon_runs, game.raid_ready_checks, game.raid_invites, game.raid_members, game.raids, game.party_invites, game.party_members, game.parties, game.character_contract_completions, game.character_contract_acceptances, game.character_equipment, game.character_items, game.character_ability_cooldowns, game.character_talent_states, game.character_vitals, game.character_mutations, game.character_travel_states, game.content_audit_entries, game.content_releases, game.content_revisions, game.admin_command_audits, game.friend_requests, game.friendships, game.travel_operations, game.character_locations, game.characters, game.crystal_ledger_entries, game.crystal_wallets, game.accounts CASCADE");
    }
}
