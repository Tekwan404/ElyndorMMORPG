using Elyndor.Core.Items;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class ItemReforgeOperationConfiguration
    : IEntityTypeConfiguration<ItemReforgeOperation>
{
    public void Configure(EntityTypeBuilder<ItemReforgeOperation> builder)
    {
        builder.ToTable(
            "item_reforge_operations",
            table =>
            {
                table.HasCheckConstraint("ck_item_reforge_operations_cost_gold", "\"CostGold\" >= 0");
                table.HasCheckConstraint("ck_item_reforge_operations_material_quantity", "\"MaterialQuantity\" >= 0");
                table.HasCheckConstraint("ck_item_reforge_operations_catalyst_quantity", "\"CatalystQuantity\" >= 0");
            });
        builder.HasKey(operation => operation.OperationId)
            .HasName("pk_item_reforge_operations");
        builder.Property(operation => operation.SlotKey).HasMaxLength(64).IsRequired();
        builder.Property(operation => operation.MaterialItemId).HasMaxLength(64).IsRequired();
        builder.Property(operation => operation.CatalystItemId).HasMaxLength(64).IsRequired();
        builder.Property(operation => operation.CurrentItemJson).HasColumnType("jsonb").IsRequired();
        builder.Property(operation => operation.ProposedItemJson).HasColumnType("jsonb").IsRequired();
        builder.Property(operation => operation.State)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(operation => operation.CreatedAtUtc).IsRequired();
        builder.Property(operation => operation.DecidedAtUtc);

        builder.HasIndex(operation => new { operation.CharacterId, operation.ItemInstanceId, operation.State })
            .HasDatabaseName("ix_item_reforge_operations_character_item_state");
        builder.HasIndex(operation => operation.ItemInstanceId)
            .HasDatabaseName("ix_item_reforge_operations_item_id");
    }
}
