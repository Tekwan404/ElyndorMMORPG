using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.World;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Persistence;

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Character> Characters => Set<Character>();

    public DbSet<CharacterVitals> CharacterVitals => Set<CharacterVitals>();

    public DbSet<CharacterLocation> CharacterLocations => Set<CharacterLocation>();

    public DbSet<TravelOperation> TravelOperations => Set<TravelOperation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("game");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameDbContext).Assembly);
    }
}
