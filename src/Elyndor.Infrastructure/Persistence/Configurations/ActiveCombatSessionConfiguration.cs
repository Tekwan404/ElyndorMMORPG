using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class ActiveCombatSessionConfiguration :
    IEntityTypeConfiguration<ActiveCombatSession>
{
    public void Configure(EntityTypeBuilder<ActiveCombatSession> builder)
    {
        builder.ToTable("active_combat_sessions");
        builder.HasKey(state => new { state.SessionId, state.CharacterId })
            .HasName("pk_active_combat_sessions");
        builder.Property(state => state.ContentVersion)
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(state => state.BalanceVersion)
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(state => state.TerminalSnapshotJson)
            .HasColumnType("jsonb");
        builder.Property(state => state.StartedAtUtc).IsRequired();

        builder.HasOne<Character>()
            .WithOne()
            .HasForeignKey<ActiveCombatSession>(state => state.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_active_combat_sessions_characters_character_id");

        builder.HasIndex(state => state.CharacterId)
            .IsUnique()
            .HasDatabaseName("uq_active_combat_sessions_character_id");
        builder.HasIndex(state => state.SessionId)
            .HasDatabaseName("ix_active_combat_sessions_session_id");
        builder.HasIndex(state => state.StartedAtUtc)
            .HasDatabaseName("ix_active_combat_sessions_started_at_utc");
    }
}
