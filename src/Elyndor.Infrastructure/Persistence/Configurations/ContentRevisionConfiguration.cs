using Elyndor.Core.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class ContentRevisionConfiguration : IEntityTypeConfiguration<ContentRevision>
{
    public void Configure(EntityTypeBuilder<ContentRevision> builder)
    {
        builder.ToTable("content_revisions");
        builder.HasKey(revision => revision.Id).HasName("pk_content_revisions");

        builder.Property(revision => revision.ContentVersion).HasMaxLength(64).IsRequired();
        builder.Property(revision => revision.BalanceVersion).HasMaxLength(64).IsRequired();
        builder.Property(revision => revision.SourcePublishedAtUtc).IsRequired();
        builder.Property(revision => revision.PayloadJson).HasColumnType("text").IsRequired();
        builder.Property(revision => revision.PayloadSha256).HasMaxLength(64).IsRequired();
        builder.Property(revision => revision.CreatedAtUtc).IsRequired();
        builder.Property(revision => revision.CreatedBy).HasMaxLength(128).IsRequired();
        builder.Property(revision => revision.Note).HasMaxLength(1024);

        builder.HasIndex(revision => new { revision.ContentVersion, revision.BalanceVersion })
            .HasDatabaseName("ix_content_revisions_versions");
        builder.HasIndex(revision => revision.PayloadSha256)
            .HasDatabaseName("ix_content_revisions_payload_sha256");
        builder.HasIndex(revision => revision.CreatedAtUtc)
            .HasDatabaseName("ix_content_revisions_created_at");
    }
}
