using Elyndor.Core.Characters;
using Elyndor.Core.Items;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CharacterEquipmentConfiguration : IEntityTypeConfiguration<CharacterEquipment>
{
    public void Configure(EntityTypeBuilder<CharacterEquipment> builder)
    {
        builder.ToTable("character_equipment");
        builder.HasKey(equipment => new { equipment.CharacterId, equipment.Slot })
            .HasName("pk_character_equipment");
        builder.Property(equipment => equipment.Slot).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(equipment => equipment.CharacterItemId).IsRequired();

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(equipment => equipment.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_character_equipment_characters_character_id");

        builder.HasOne<CharacterItem>()
            .WithMany()
            .HasForeignKey(equipment => equipment.CharacterItemId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_character_equipment_character_items_item_id");

        builder.HasIndex(equipment => equipment.CharacterItemId)
            .IsUnique()
            .HasDatabaseName("uq_character_equipment_item_id");
    }
}
