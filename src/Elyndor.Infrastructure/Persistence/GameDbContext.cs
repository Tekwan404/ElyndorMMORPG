using Elyndor.Core.Administration;
using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.World;
using Elyndor.Core.Talents;
using Elyndor.Core.Items;
using Elyndor.Core.Combat;
using Elyndor.Core.Progression;
using Elyndor.Core.Content;
using Elyndor.Core.Quests;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Persistence;

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Character> Characters => Set<Character>();

    public DbSet<CharacterVitals> CharacterVitals => Set<CharacterVitals>();

    public DbSet<CharacterMutation> CharacterMutations => Set<CharacterMutation>();

    public DbSet<CharacterLocation> CharacterLocations => Set<CharacterLocation>();

    public DbSet<TravelOperation> TravelOperations => Set<TravelOperation>();

    public DbSet<CharacterTravelState> CharacterTravelStates => Set<CharacterTravelState>();

    public DbSet<AdminCommandAudit> AdminCommandAudits => Set<AdminCommandAudit>();

    public DbSet<CharacterTalentState> CharacterTalentStates => Set<CharacterTalentState>();

    public DbSet<CharacterItem> CharacterItems => Set<CharacterItem>();

    public DbSet<PendingLootItem> PendingLootItems => Set<PendingLootItem>();

    public DbSet<CharacterAbilityCooldown> CharacterAbilityCooldowns =>
        Set<CharacterAbilityCooldown>();

    public DbSet<ActiveCombatSession> ActiveCombatSessions =>
        Set<ActiveCombatSession>();

    public DbSet<CombatConsumableUse> CombatConsumableUses =>
        Set<CombatConsumableUse>();

    public DbSet<CharacterEquipment> CharacterEquipment => Set<CharacterEquipment>();

    public DbSet<CombatRewardGrant> CombatRewardGrants => Set<CombatRewardGrant>();

    public DbSet<CharacterContractAcceptance> CharacterContractAcceptances =>
        Set<CharacterContractAcceptance>();

    public DbSet<CharacterContractCompletion> CharacterContractCompletions =>
        Set<CharacterContractCompletion>();

    public DbSet<CharacterQuestState> CharacterQuestStates =>
        Set<CharacterQuestState>();

    public DbSet<QuestRewardGrant> QuestRewardGrants =>
        Set<QuestRewardGrant>();

    public DbSet<ContentRevision> ContentRevisions => Set<ContentRevision>();

    public DbSet<ContentRelease> ContentReleases => Set<ContentRelease>();

    public DbSet<ContentAuditEntry> ContentAuditEntries => Set<ContentAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("game");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameDbContext).Assembly);
    }
}
