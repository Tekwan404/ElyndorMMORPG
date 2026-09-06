using Elyndor.Core.Characters;
using Elyndor.Core.Items;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CharacterItemConfiguration : IEntityTypeConfiguration<CharacterItem>
{
    public void Configure(EntityTypeBuilder<CharacterItem> builder)
    {
        builder.ToTable(
            "character_items",
            table => table.HasCheckConstraint(
                "ck_character_items_quantity_positive",
                "\"Quantity\" > 0"));
        builder.HasKey(item => item.Id).HasName("pk_character_items");
        builder.Property(item => item.ItemDefinitionId).HasMaxLength(64).IsRequired();
        builder.Property(item => item.DefinitionVersion).HasDefaultValue(1).IsRequired();
        builder.Property(item => item.RolledStrength).HasPrecision(18, 4);
        builder.Property(item => item.RolledAgility).HasPrecision(18, 4);
        builder.Property(item => item.RolledIntellect).HasPrecision(18, 4);
        builder.Property(item => item.RolledStamina).HasPrecision(18, 4);
        builder.Ignore(item => item.RolledPrimaryStats);
        builder.Property(item => item.Quantity).IsRequired();
        builder.Property(item => item.AcquiredAtUtc).IsRequired();
        builder.Property(item => item.IsLocked).HasDefaultValue(false).IsRequired();

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(item => item.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_character_items_characters_character_id");

        builder.HasIndex(item => new { item.CharacterId, item.ItemDefinitionId })
            .HasDatabaseName("ix_character_items_character_definition");
    }
}
