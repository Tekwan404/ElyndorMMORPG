using Elyndor.Core.Administration;
using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.World;
using Elyndor.Core.Talents;
using Elyndor.Core.Items;
using Elyndor.Core.Progression;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Persistence;

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Character> Characters => Set<Character>();

    public DbSet<CharacterVitals> CharacterVitals => Set<CharacterVitals>();

    public DbSet<CharacterLocation> CharacterLocations => Set<CharacterLocation>();

    public DbSet<TravelOperation> TravelOperations => Set<TravelOperation>();

    public DbSet<AdminCommandAudit> AdminCommandAudits => Set<AdminCommandAudit>();

    public DbSet<CharacterTalentState> CharacterTalentStates => Set<CharacterTalentState>();

    public DbSet<CharacterItem> CharacterItems => Set<CharacterItem>();

    public DbSet<CharacterEquipment> CharacterEquipment => Set<CharacterEquipment>();

    public DbSet<CombatRewardGrant> CombatRewardGrants => Set<CombatRewardGrant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("game");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameDbContext).Assembly);
    }
}
