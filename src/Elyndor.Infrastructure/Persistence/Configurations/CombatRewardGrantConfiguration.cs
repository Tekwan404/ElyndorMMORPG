using Elyndor.Core.Characters;
using Elyndor.Core.Progression;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CombatRewardGrantConfiguration : IEntityTypeConfiguration<CombatRewardGrant>
{
    public void Configure(EntityTypeBuilder<CombatRewardGrant> builder)
    {
        builder.ToTable(
            "combat_reward_grants",
            table => table.HasCheckConstraint(
                "ck_combat_reward_grants_xp_non_negative",
                "\"XpEarned\" >= 0"));
        builder.HasKey(grant => grant.CombatSessionId).HasName("pk_combat_reward_grants");
        builder.Property(grant => grant.MonsterId).HasMaxLength(64).IsRequired();
        builder.Property(grant => grant.XpEarned).IsRequired();
        builder.Property(grant => grant.GrantedAtUtc).IsRequired();

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(grant => grant.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_combat_reward_grants_characters_character_id");

        builder.HasIndex(grant => grant.CharacterId)
            .HasDatabaseName("ix_combat_reward_grants_character_id");
    }
}
