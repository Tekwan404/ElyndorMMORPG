using Elyndor.Core.Afk;
using Elyndor.Core.Characters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class AfkFarmSessionConfiguration : IEntityTypeConfiguration<AfkFarmSession>
{
    public void Configure(EntityTypeBuilder<AfkFarmSession> builder)
    {
        builder.ToTable(
            "afk_farm_sessions",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_afk_farm_sessions_positive_duration",
                    "\"EndsAtUtc\" > \"StartedAtUtc\"");
                table.HasCheckConstraint(
                    "ck_afk_farm_sessions_processing_window",
                    "\"LastProcessedAtUtc\" >= \"StartedAtUtc\" AND \"LastProcessedAtUtc\" <= \"EndsAtUtc\"");
            });
        builder.HasKey(session => session.Id)
            .HasName("pk_afk_farm_sessions");

        builder.Property(session => session.LocationId)
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(session => session.TargetMonsterId)
            .HasMaxLength(64);
        builder.Property(session => session.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(session => session.ContentVersion)
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(session => session.BalanceVersion)
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(session => session.CharacterSnapshotJson)
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(session => session.StopReason)
            .HasMaxLength(128);
        builder.Property(session => session.StartedAtUtc).IsRequired();
        builder.Property(session => session.EndsAtUtc).IsRequired();
        builder.Property(session => session.LastProcessedAtUtc).IsRequired();
        builder.Property(session => session.CreatedAtUtc).IsRequired();
        builder.Property(session => session.Version)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(session => session.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_afk_farm_sessions_characters_character_id");

        builder.HasIndex(session => new { session.CharacterId, session.CreatedAtUtc })
            .HasDatabaseName("ix_afk_farm_sessions_character_created_at_utc");
        builder.HasIndex(session => new { session.Status, session.EndsAtUtc })
            .HasDatabaseName("ix_afk_farm_sessions_status_ends_at_utc");
        builder.HasIndex(session => session.CharacterId)
            .IsUnique()
            .HasFilter("\"Status\" = 'Active'")
            .HasDatabaseName("uq_afk_farm_sessions_active_character_id");
    }
}
