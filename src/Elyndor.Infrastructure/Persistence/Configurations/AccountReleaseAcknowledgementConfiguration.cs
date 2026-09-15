using Elyndor.Core.Identity;
using Elyndor.Core.Releases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class AccountReleaseAcknowledgementConfiguration
    : IEntityTypeConfiguration<AccountReleaseAcknowledgement>
{
    public void Configure(EntityTypeBuilder<AccountReleaseAcknowledgement> builder)
    {
        builder.ToTable("account_release_acknowledgements");
        builder.HasKey(acknowledgement => new { acknowledgement.AccountId, acknowledgement.ReleaseId })
            .HasName("pk_account_release_acknowledgements");
        builder.Property(acknowledgement => acknowledgement.ReleaseId).HasMaxLength(64).IsRequired();
        builder.Property(acknowledgement => acknowledgement.AcknowledgedAtUtc).IsRequired();
        builder.HasOne<Account>().WithMany().HasForeignKey(acknowledgement => acknowledgement.AccountId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_account_release_acknowledgements_accounts_account_id");
        builder.HasIndex(acknowledgement => acknowledgement.AcknowledgedAtUtc)
            .HasDatabaseName("ix_account_release_acknowledgements_acknowledged_at_utc");
    }
}
