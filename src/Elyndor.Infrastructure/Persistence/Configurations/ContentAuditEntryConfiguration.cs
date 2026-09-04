using Elyndor.Core.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class ContentAuditEntryConfiguration : IEntityTypeConfiguration<ContentAuditEntry>
{
    public void Configure(EntityTypeBuilder<ContentAuditEntry> builder)
    {
        builder.ToTable("content_audit_entries");
        builder.HasKey(entry => entry.Id).HasName("pk_content_audit_entries");

        builder.Property(entry => entry.Action).HasMaxLength(32).IsRequired();
        builder.Property(entry => entry.Actor).HasMaxLength(128).IsRequired();
        builder.Property(entry => entry.OccurredAtUtc).IsRequired();
        builder.Property(entry => entry.DetailsJson).HasColumnType("jsonb").IsRequired();

        builder.HasOne<ContentRevision>()
            .WithMany()
            .HasForeignKey(entry => entry.RevisionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_content_audit_entries_content_revisions_revision_id");

        builder.HasOne<ContentRelease>()
            .WithMany()
            .HasForeignKey(entry => entry.ReleaseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_content_audit_entries_content_releases_release_id");

        builder.HasIndex(entry => entry.OccurredAtUtc)
            .HasDatabaseName("ix_content_audit_entries_occurred_at");
        builder.HasIndex(entry => entry.RevisionId)
            .HasDatabaseName("ix_content_audit_entries_revision_id");
        builder.HasIndex(entry => entry.ReleaseId)
            .HasDatabaseName("ix_content_audit_entries_release_id");
    }
}
