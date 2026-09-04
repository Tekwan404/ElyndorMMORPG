using Elyndor.Core.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class ContentReleaseConfiguration : IEntityTypeConfiguration<ContentRelease>
{
    public void Configure(EntityTypeBuilder<ContentRelease> builder)
    {
        builder.ToTable("content_releases");
        builder.HasKey(release => release.Id).HasName("pk_content_releases");

        builder.Property(release => release.PublishedAtUtc).IsRequired();
        builder.Property(release => release.PublishedBy).HasMaxLength(128).IsRequired();
        builder.Property(release => release.Note).HasMaxLength(1024);

        builder.HasOne<ContentRevision>()
            .WithMany()
            .HasForeignKey(release => release.RevisionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_content_releases_content_revisions_revision_id");

        builder.HasIndex(release => release.PublishedAtUtc)
            .HasDatabaseName("ix_content_releases_published_at");
        builder.HasIndex(release => new { release.RevisionId, release.PublishedAtUtc })
            .HasDatabaseName("ix_content_releases_revision_published_at");
    }
}
