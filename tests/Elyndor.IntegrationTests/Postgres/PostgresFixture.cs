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
        DbContextOptions<GameDbContext> options =
            new DbContextOptionsBuilder<GameDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;

        return new GameDbContext(options);
    }

    public async Task ResetAsync()
    {
        await using GameDbContext context = CreateDbContext();
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE game.travel_operations, game.character_locations, game.characters, game.accounts CASCADE");
    }
}
