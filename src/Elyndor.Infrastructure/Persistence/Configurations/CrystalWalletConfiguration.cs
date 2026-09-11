using Elyndor.Core.Economy;
using Elyndor.Core.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CrystalWalletConfiguration : IEntityTypeConfiguration<CrystalWallet>
{
    public void Configure(EntityTypeBuilder<CrystalWallet> builder)
    {
        builder.ToTable("crystal_wallets", table => table.HasCheckConstraint(
            "ck_crystal_wallets_balance_non_negative", "\"Balance\" >= 0"));
        builder.HasKey(wallet => wallet.AccountId).HasName("pk_crystal_wallets");
        builder.Property(wallet => wallet.Balance).HasDefaultValue(0L).IsRequired();
        builder.HasOne<Account>().WithMany().HasForeignKey(wallet => wallet.AccountId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_crystal_wallets_accounts_account_id");
    }
}

public sealed class CrystalLedgerEntryConfiguration : IEntityTypeConfiguration<CrystalLedgerEntry>
{
    public void Configure(EntityTypeBuilder<CrystalLedgerEntry> builder)
    {
        builder.ToTable("crystal_ledger_entries", table =>
        {
            table.HasCheckConstraint("ck_crystal_ledger_entries_delta_non_zero", "\"Delta\" <> 0");
            table.HasCheckConstraint("ck_crystal_ledger_entries_balance_after_non_negative", "\"BalanceAfter\" >= 0");
        });
        builder.HasKey(entry => entry.Id).HasName("pk_crystal_ledger_entries");
        builder.Property(entry => entry.EntryType).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(entry => entry.Reference).HasMaxLength(128).IsRequired();
        builder.Property(entry => entry.RequestFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(entry => entry.CreatedAtUtc).IsRequired();
        builder.HasOne<Account>().WithMany().HasForeignKey(entry => entry.AccountId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_crystal_ledger_entries_accounts_account_id");
        builder.HasIndex(entry => new { entry.AccountId, entry.OperationId }).IsUnique()
            .HasDatabaseName("uq_crystal_ledger_entries_account_operation");
        builder.HasIndex(entry => new { entry.AccountId, entry.CreatedAtUtc })
            .HasDatabaseName("ix_crystal_ledger_entries_account_created_at");
    }
}
