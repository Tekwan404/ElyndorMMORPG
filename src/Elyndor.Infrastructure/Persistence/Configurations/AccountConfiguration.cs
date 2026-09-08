using Elyndor.Core.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");
        builder.HasKey(account => account.Id).HasName("pk_accounts");
        builder.Property(account => account.TelegramUserId).IsRequired();
        builder.Property(account => account.TelegramUsername).HasMaxLength(33);
        builder.Property(account => account.NormalizedTelegramUsername).HasMaxLength(32);
        builder.Property(account => account.CreatedAtUtc).IsRequired();
        builder.Property(account => account.LastSeenAtUtc).IsRequired();
        builder.HasIndex(account => account.TelegramUserId)
            .IsUnique()
            .HasDatabaseName("uq_accounts_telegram_user_id");
        builder.HasIndex(account => account.NormalizedTelegramUsername)
            .IsUnique()
            .HasDatabaseName("uq_accounts_normalized_telegram_username");
    }
}
