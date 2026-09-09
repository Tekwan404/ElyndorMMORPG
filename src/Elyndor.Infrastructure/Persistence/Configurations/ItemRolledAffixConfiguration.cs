using Elyndor.Core.Items;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class ItemRolledAffixConfiguration : IEntityTypeConfiguration<ItemRolledAffix>
{
    public void Configure(EntityTypeBuilder<ItemRolledAffix> builder)
    {
        builder.ToTable(
            "character_item_affixes",
            table =>
            {
                table.HasCheckConstraint("ck_character_item_affixes_step_positive", "\"StepAtGeneration\" > 0");
                table.HasCheckConstraint("ck_character_item_affixes_range", "\"MaxAtGeneration\" >= \"MinAtGeneration\"");
                table.HasCheckConstraint("ck_character_item_affixes_value_range", "\"Value\" >= \"MinAtGeneration\" AND \"Value\" <= \"MaxAtGeneration\"");
            });
        builder.HasKey(affix => new { affix.ItemInstanceId, affix.SlotKey })
            .HasName("pk_character_item_affixes");
        builder.Property(affix => affix.SlotKey).HasMaxLength(64).IsRequired();
        builder.Property(affix => affix.AffixDefinitionId).HasMaxLength(64).IsRequired();
        builder.Property(affix => affix.StatId).HasMaxLength(64).IsRequired();
        builder.Property(affix => affix.Value).HasPrecision(18, 4).IsRequired();
        builder.Property(affix => affix.MinAtGeneration).HasPrecision(18, 4).IsRequired();
        builder.Property(affix => affix.MaxAtGeneration).HasPrecision(18, 4).IsRequired();
        builder.Property(affix => affix.StepAtGeneration).HasPrecision(18, 4).IsRequired();
        builder.Property(affix => affix.AffixTier).IsRequired();
        builder.Property(affix => affix.IsGuaranteed).IsRequired();
        builder.Property(affix => affix.IsReforgeSlot).IsRequired();
        builder.Property(affix => affix.GenerationOrdinal).IsRequired();

        builder.HasIndex(affix => affix.ItemInstanceId)
            .HasDatabaseName("ix_character_item_affixes_item_id");
        builder.HasIndex(affix => affix.StatId)
            .HasDatabaseName("ix_character_item_affixes_stat_id");
    }
}
