using Elyndor.Core.Characters;
using Elyndor.Core.Items;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class PendingLootItemConfiguration : IEntityTypeConfiguration<PendingLootItem>
{
    public void Configure(EntityTypeBuilder<PendingLootItem> builder)
    {
        builder.ToTable(
            "pending_loot_items",
            table => table.HasCheckConstraint(
                "ck_pending_loot_items_quantity_positive",
                "\"Quantity\" > 0"));

        builder.HasKey(item => item.Id).HasName("pk_pending_loot_items");
        builder.Property(item => item.ItemDefinitionId).HasMaxLength(64).IsRequired();
        builder.Property(item => item.DefinitionVersion).IsRequired();
        builder.Property(item => item.Quantity).IsRequired();
        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.RolledStrength).HasPrecision(18, 4);
        builder.Property(item => item.RolledAgility).HasPrecision(18, 4);
        builder.Property(item => item.RolledIntellect).HasPrecision(18, 4);
        builder.Property(item => item.RolledStamina).HasPrecision(18, 4);
        builder.Property(item => item.GeneratedItemJson).HasColumnType("jsonb");
        builder.Property(item => item.GenerationSeedHash).HasMaxLength(64);
        builder.Property(item => item.SourceType).HasMaxLength(32);
        builder.Property(item => item.SourceOperationId);
        builder.Property(item => item.SourceEntryId).HasMaxLength(128);
        builder.Ignore(item => item.RolledPrimaryStats);

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(item => item.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pending_loot_items_characters_character_id");

        builder.HasIndex(item => new { item.CharacterId, item.CreatedAtUtc })
            .HasDatabaseName("ix_pending_loot_items_character_created_at");
        builder.HasIndex(item => item.RewardResolutionId)
            .HasDatabaseName("ix_pending_loot_items_reward_resolution_id");
    }
}
