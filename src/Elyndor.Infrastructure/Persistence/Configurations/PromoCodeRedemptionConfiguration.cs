using Elyndor.Core.Characters;
using Elyndor.Core.Economy;
using Elyndor.Core.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class PromoCodeRedemptionConfiguration : IEntityTypeConfiguration<PromoCodeRedemption>
{
    public void Configure(EntityTypeBuilder<PromoCodeRedemption> builder)
    {
        builder.ToTable("promo_code_redemptions");
        builder.HasKey(redemption => redemption.OperationId).HasName("pk_promo_code_redemptions");
        builder.Property(redemption => redemption.Code).HasMaxLength(64).IsRequired();
        builder.Property(redemption => redemption.RequestFingerprint).HasMaxLength(64).IsRequired();
        builder.HasOne<Account>().WithMany().HasForeignKey(redemption => redemption.AccountId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_promo_code_redemptions_accounts_account_id");
        builder.HasOne<Character>().WithMany().HasForeignKey(redemption => redemption.CharacterId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_promo_code_redemptions_characters_character_id");
        builder.HasIndex(redemption => new { redemption.AccountId, redemption.Code }).HasDatabaseName("ix_promo_code_redemptions_account_code");
        builder.HasIndex(redemption => redemption.Code).HasDatabaseName("ix_promo_code_redemptions_code");
    }
}
