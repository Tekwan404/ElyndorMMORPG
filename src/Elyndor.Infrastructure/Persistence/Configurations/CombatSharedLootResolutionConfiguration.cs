using Elyndor.Core.Combat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CombatSharedLootResolutionConfiguration
    : IEntityTypeConfiguration<CombatSharedLootResolution>
{
    public void Configure(EntityTypeBuilder<CombatSharedLootResolution> builder)
    {
        builder.ToTable("combat_shared_loot_resolutions");
        builder.HasKey(resolution => resolution.CombatSessionId)
            .HasName("pk_combat_shared_loot_resolutions");
        builder.Property(resolution => resolution.GroupLootJson)
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(resolution => resolution.ResolvedAtUtc).IsRequired();
    }
}
