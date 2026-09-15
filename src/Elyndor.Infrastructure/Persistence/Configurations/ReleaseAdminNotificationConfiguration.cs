using Elyndor.Core.Releases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class ReleaseAdminNotificationConfiguration
    : IEntityTypeConfiguration<ReleaseAdminNotification>
{
    public void Configure(EntityTypeBuilder<ReleaseAdminNotification> builder)
    {
        builder.ToTable("release_admin_notifications");
        builder.HasKey(notification => new { notification.ReleaseId, notification.TelegramUserId })
            .HasName("pk_release_admin_notifications");
        builder.Property(notification => notification.ReleaseId).HasMaxLength(64).IsRequired();
        builder.Property(notification => notification.SentAtUtc).IsRequired();
        builder.HasIndex(notification => notification.SentAtUtc)
            .HasDatabaseName("ix_release_admin_notifications_sent_at_utc");
    }
}
