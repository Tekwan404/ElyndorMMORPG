using Elyndor.Core.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class PremiumStorePurchaseConfiguration : IEntityTypeConfiguration<PremiumStorePurchase>
{
    public void Configure(EntityTypeBuilder<PremiumStorePurchase> builder)
    {
        builder.ToTable("premium_store_purchases", table => { table.HasCheckConstraint("ck_premium_store_purchases_quantity_positive", "\"Quantity\" > 0"); table.HasCheckConstraint("ck_premium_store_purchases_price_positive", "\"CrystalPrice\" > 0"); });
        builder.HasKey(purchase => purchase.OperationId).HasName("pk_premium_store_purchases");
        builder.Property(purchase => purchase.Sku).HasMaxLength(64).IsRequired();
        builder.Property(purchase => purchase.ItemDefinitionId).HasMaxLength(64).IsRequired();
        builder.HasIndex(purchase => new { purchase.AccountId, purchase.Sku }).HasDatabaseName("ix_premium_store_purchases_account_sku");
    }
}
