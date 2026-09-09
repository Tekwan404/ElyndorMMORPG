using Elyndor.Core.Administration;
using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.World;
using Elyndor.Core.Talents;
using Elyndor.Core.Items;
using Elyndor.Core.Combat;
using Elyndor.Core.Progression;
using Elyndor.Core.Content;
using Elyndor.Core.Social;
using Elyndor.Core.Parties;
using Elyndor.Core.Dungeons;
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

    public DbSet<ItemRolledAffix> CharacterItemAffixes => Set<ItemRolledAffix>();

    public DbSet<ItemReforgeOperation> ItemReforgeOperations => Set<ItemReforgeOperation>();

    public DbSet<PendingLootItem> PendingLootItems => Set<PendingLootItem>();

    public DbSet<CharacterAbilityCooldown> CharacterAbilityCooldowns =>
        Set<CharacterAbilityCooldown>();

    public DbSet<ActiveCombatSession> ActiveCombatSessions =>
        Set<ActiveCombatSession>();
    public DbSet<CombatLootRoll> CombatLootRolls => Set<CombatLootRoll>();

    public DbSet<CombatSharedLootResolution> CombatSharedLootResolutions =>
        Set<CombatSharedLootResolution>();

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

    public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();

    public DbSet<Friendship> Friendships => Set<Friendship>();

    public DbSet<Party> Parties => Set<Party>();

    public DbSet<PartyMember> PartyMembers => Set<PartyMember>();

    public DbSet<PartyInvite> PartyInvites => Set<PartyInvite>();

    public DbSet<DungeonRun> DungeonRuns => Set<DungeonRun>();

    public DbSet<DungeonRunMember> DungeonRunMembers => Set<DungeonRunMember>();

    public DbSet<DungeonEncounter> DungeonEncounters => Set<DungeonEncounter>();

    public DbSet<DungeonEncounterMember> DungeonEncounterMembers => Set<DungeonEncounterMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("game");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameDbContext).Assembly);
    }
}
