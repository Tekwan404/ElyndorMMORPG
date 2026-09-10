using Elyndor.Core.Items;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class ItemSalvageOperationConfiguration : IEntityTypeConfiguration<ItemSalvageOperation>
{
    public void Configure(EntityTypeBuilder<ItemSalvageOperation> builder)
    {
        builder.ToTable("item_salvage_operations", table =>
        {
            table.HasCheckConstraint("ck_item_salvage_operations_stones", "\"ReforgeStoneQuantity\" > 0");
            table.HasCheckConstraint("ck_item_salvage_operations_material", "\"MaterialQuantity\" >= 0");
        });
        builder.HasKey(operation => operation.OperationId).HasName("pk_item_salvage_operations");
        builder.Property(operation => operation.ItemDefinitionId).HasMaxLength(64).IsRequired();
        builder.Property(operation => operation.ReforgeStoneItemId).HasMaxLength(64).IsRequired();
        builder.Property(operation => operation.MaterialItemId).HasMaxLength(64).IsRequired();
        builder.Property(operation => operation.CreatedAtUtc).IsRequired();
        builder.HasIndex(operation => new { operation.CharacterId, operation.ItemInstanceId })
            .HasDatabaseName("ix_item_salvage_operations_character_item");
    }
}
