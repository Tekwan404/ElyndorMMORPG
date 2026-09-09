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
        builder.Ignore(item => item.IsProcedurallyGenerated);
        builder.Property(item => item.ItemLevel);
        builder.Property(item => item.MinimumTemplateItemPower).HasPrecision(18, 4);
        builder.Property(item => item.ActualItemPower).HasPrecision(18, 4);
        builder.Property(item => item.MaxTemplateItemPower).HasPrecision(18, 4);
        builder.Property(item => item.RollQuality).HasPrecision(7, 2);
        builder.Property(item => item.Stars);
        builder.Property(item => item.IsPerfect).HasDefaultValue(false).IsRequired();
        builder.Property(item => item.PerfectOrigin).HasMaxLength(16);
        builder.Property(item => item.GenerationVersion).HasDefaultValue(0).IsRequired();
        builder.Property(item => item.GenerationSeedHash).HasMaxLength(64);
        builder.Property(item => item.GeneratedPrefixId).HasMaxLength(128);
        builder.Property(item => item.GeneratedSuffixId).HasMaxLength(128);
        builder.Property(item => item.GeneratedDisplayName).HasMaxLength(256);
        builder.Property(item => item.ReforgeSlotKey).HasMaxLength(64);
        builder.Property(item => item.ReforgeCount).HasDefaultValue(0).IsRequired();
        builder.Property(item => item.EnhancementLevel).HasDefaultValue(0).IsRequired();
        builder.Property(item => item.BindState).HasMaxLength(16).HasDefaultValue(ItemBindStates.Unbound).IsRequired();
        builder.Property(item => item.TransactionLockId);
        builder.Property(item => item.SourceType).HasMaxLength(32);
        builder.Property(item => item.SourceOperationId);
        builder.Property(item => item.SourceEntryId).HasMaxLength(128);
        builder.Property(item => item.Quantity).IsRequired();
        builder.Property(item => item.AcquiredAtUtc).IsRequired();
        builder.Property(item => item.IsLocked).HasDefaultValue(false).IsRequired();

        builder.HasMany(item => item.Affixes)
            .WithOne()
            .HasForeignKey(affix => affix.ItemInstanceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_character_item_affixes_character_items_item_id");

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(item => item.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_character_items_characters_character_id");

        builder.HasIndex(item => new { item.CharacterId, item.ItemDefinitionId })
            .HasDatabaseName("ix_character_items_character_definition");
        builder.HasIndex(item => item.SourceOperationId)
            .HasDatabaseName("ix_character_items_source_operation");
        builder.HasIndex(item => item.TransactionLockId)
            .HasDatabaseName("ix_character_items_transaction_lock");
    }
}
