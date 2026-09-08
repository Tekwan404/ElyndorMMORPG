using Elyndor.Core.Dungeons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class DungeonRunConfiguration : IEntityTypeConfiguration<DungeonRun>
{
    public void Configure(EntityTypeBuilder<DungeonRun> builder)
    {
        builder.ToTable("dungeon_runs");
        builder.HasKey(run => run.Id).HasName("pk_dungeon_runs");
        builder.Property(run => run.CreationRequestId).IsRequired();
        builder.Property(run => run.DungeonId).HasMaxLength(64).IsRequired();
        builder.Property(run => run.State).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(run => run.CurrentEncounterIndex).IsRequired();
        builder.HasIndex(run => new { run.PartyId, run.State })
            .HasDatabaseName("ix_dungeon_runs_party_state");
        builder.HasIndex(run => run.CreationRequestId)
            .IsUnique()
            .HasDatabaseName("uq_dungeon_runs_creation_request_id");
        builder.HasMany(run => run.Members)
            .WithOne()
            .HasForeignKey(member => member.RunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(run => run.Encounters)
            .WithOne(encounter => encounter.Run)
            .HasForeignKey(encounter => encounter.RunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DungeonRunMemberConfiguration : IEntityTypeConfiguration<DungeonRunMember>
{
    public void Configure(EntityTypeBuilder<DungeonRunMember> builder)
    {
        builder.ToTable("dungeon_run_members");
        builder.HasKey(member => new { member.RunId, member.CharacterId })
            .HasName("pk_dungeon_run_members");
        builder.Property(member => member.State).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(member => member.CharacterId)
            .HasDatabaseName("ix_dungeon_run_members_character_id");
    }
}

public sealed class DungeonEncounterConfiguration : IEntityTypeConfiguration<DungeonEncounter>
{
    public void Configure(EntityTypeBuilder<DungeonEncounter> builder)
    {
        builder.ToTable("dungeon_encounters");
        builder.HasKey(encounter => encounter.Id).HasName("pk_dungeon_encounters");
        builder.Property(encounter => encounter.MonsterId).HasMaxLength(64).IsRequired();
        builder.Property(encounter => encounter.State).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(encounter => new { encounter.RunId, encounter.EncounterIndex })
            .IsUnique()
            .HasDatabaseName("uq_dungeon_encounters_run_index");
        builder.HasIndex(encounter => encounter.CombatSessionId)
            .HasDatabaseName("ix_dungeon_encounters_combat_session_id");
        builder.HasMany(encounter => encounter.Members)
            .WithOne()
            .HasForeignKey(member => member.EncounterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DungeonEncounterMemberConfiguration : IEntityTypeConfiguration<DungeonEncounterMember>
{
    public void Configure(EntityTypeBuilder<DungeonEncounterMember> builder)
    {
        builder.ToTable("dungeon_encounter_members");
        builder.HasKey(member => new { member.EncounterId, member.CharacterId })
            .HasName("pk_dungeon_encounter_members");
        builder.HasIndex(member => member.CharacterId)
            .HasDatabaseName("ix_dungeon_encounter_members_character_id");
    }
}
