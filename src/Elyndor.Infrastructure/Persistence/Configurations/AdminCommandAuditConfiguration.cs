using Elyndor.Core.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class AdminCommandAuditConfiguration : IEntityTypeConfiguration<AdminCommandAudit>
{
    public void Configure(EntityTypeBuilder<AdminCommandAudit> builder)
    {
        builder.ToTable("admin_command_audits");
        builder.HasKey(audit => audit.UpdateId).HasName("pk_admin_command_audits");
        builder.Property(audit => audit.UpdateId).ValueGeneratedNever();
        builder.Property(audit => audit.CommandName).HasMaxLength(32).IsRequired();
        builder.Property(audit => audit.ResultCode).HasMaxLength(64).IsRequired();
        builder.Property(audit => audit.ResultSummary).HasMaxLength(1024).IsRequired();
        builder.Property(audit => audit.ReceivedAtUtc).IsRequired();
        builder.HasIndex(audit => new { audit.AdministratorTelegramUserId, audit.ReceivedAtUtc })
            .HasDatabaseName("ix_admin_command_audits_administrator_received_at");
        builder.HasIndex(audit => new { audit.TargetTelegramUserId, audit.ReceivedAtUtc })
            .HasDatabaseName("ix_admin_command_audits_target_received_at");
    }
}
