using Elyndor.Core.Characters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CharacterCosmeticOwnershipConfiguration : IEntityTypeConfiguration<CharacterCosmeticOwnership>
{
    public void Configure(EntityTypeBuilder<CharacterCosmeticOwnership> builder)
    {
        builder.ToTable("character_cosmetic_ownerships", table => table.HasCheckConstraint(
            "ck_character_cosmetic_ownerships_price_positive", "\"CrystalPrice\" > 0"));
        builder.HasKey(item => new { item.CharacterId, item.CosmeticId }).HasName("pk_character_cosmetic_ownerships");
        builder.Property(item => item.CosmeticId).HasMaxLength(64).IsRequired();
        builder.HasOne<Character>().WithMany().HasForeignKey(item => item.CharacterId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_character_cosmetic_ownerships_character");
    }
}
