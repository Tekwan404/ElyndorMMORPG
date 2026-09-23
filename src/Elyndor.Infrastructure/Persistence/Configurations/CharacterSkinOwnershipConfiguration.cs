using Elyndor.Core.Characters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CharacterSkinOwnershipConfiguration : IEntityTypeConfiguration<CharacterSkinOwnership>
{
    public void Configure(EntityTypeBuilder<CharacterSkinOwnership> builder)
    {
        builder.ToTable("character_skin_ownerships", table => table.HasCheckConstraint(
            "ck_character_skin_ownerships_price_positive", "\"CrystalPrice\" > 0"));
        builder.HasKey(item => new { item.CharacterId, item.SkinId }).HasName("pk_character_skin_ownerships");
        builder.Property(item => item.SkinId).HasMaxLength(64).IsRequired();
        builder.HasIndex(item => item.OperationId).IsUnique().HasDatabaseName("uq_character_skin_ownerships_operation_id");
        builder.HasOne<Character>().WithMany().HasForeignKey(item => item.CharacterId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_character_skin_ownerships_characters_character_id");
    }
}
