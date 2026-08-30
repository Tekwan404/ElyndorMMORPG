using Elyndor.Core.Characters;
using Elyndor.Core.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class TravelOperationConfiguration : IEntityTypeConfiguration<TravelOperation>
{
    public void Configure(EntityTypeBuilder<TravelOperation> builder)
    {
        builder.ToTable("travel_operations");
        builder.HasKey(operation => new { operation.CharacterId, operation.RequestId })
            .HasName("pk_travel_operations");
        builder.Property(operation => operation.TargetLocationId).HasMaxLength(64).IsRequired();
        builder.Property(operation => operation.ResultLocationId).HasMaxLength(64).IsRequired();
        builder.Property(operation => operation.ResultVersion).IsRequired();
        builder.Property(operation => operation.CompletedAtUtc).IsRequired();

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(operation => operation.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_travel_operations_characters_character_id");
    }
}
