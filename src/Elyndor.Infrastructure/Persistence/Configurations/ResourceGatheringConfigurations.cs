using Elyndor.Core.Characters;
using Elyndor.Core.Professions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class ResourceNodeStateConfiguration : IEntityTypeConfiguration<ResourceNodeState>
{
    public void Configure(EntityTypeBuilder<ResourceNodeState> builder)
    {
        builder.ToTable("resource_nodes");
        builder.HasKey(node => node.NodeId);
        builder.Property(node => node.NodeId).HasColumnName("node_id").HasMaxLength(128);
        builder.Property(node => node.AvailableAtUtc).HasColumnName("available_at_utc");
    }
}

public sealed class ResourceGatheringReceiptConfiguration : IEntityTypeConfiguration<ResourceGatheringReceipt>
{
    public void Configure(EntityTypeBuilder<ResourceGatheringReceipt> builder)
    {
        builder.ToTable("resource_gathering_receipts", table =>
            table.HasCheckConstraint("ck_resource_gathering_quantity", "\"Quantity\" > 0"));
        builder.HasKey(receipt => new { receipt.CharacterId, receipt.MutationId });
        builder.Property(receipt => receipt.NodeId).HasMaxLength(128);
        builder.Property(receipt => receipt.ItemId).HasMaxLength(128);
        builder.HasOne<Character>().WithMany().HasForeignKey(receipt => receipt.CharacterId).OnDelete(DeleteBehavior.Cascade);
    }
}
