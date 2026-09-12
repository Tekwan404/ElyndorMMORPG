using Elyndor.Core.Afk;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class AfkFarmIntervalGrantConfiguration : IEntityTypeConfiguration<AfkFarmIntervalGrant>
{
    public void Configure(EntityTypeBuilder<AfkFarmIntervalGrant> builder)
    {
        builder.ToTable(
            "afk_farm_interval_grants",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_afk_farm_interval_grants_positive_window",
                    "\"EndedAtUtc\" > \"StartedAtUtc\"");
                table.HasCheckConstraint(
                    "ck_afk_farm_interval_grants_non_negative_rewards",
                    "\"Kills\" >= 0 AND \"XpEarned\" >= 0 AND \"GoldEarned\" >= 0");
                table.HasCheckConstraint(
                    "ck_afk_farm_interval_grants_loot_json",
                    "jsonb_typeof(\"LootJson\") = 'array'");
            });
        builder.HasKey(grant => new { grant.SessionId, grant.IntervalIndex })
            .HasName("pk_afk_farm_interval_grants");
        builder.Property(grant => grant.LootJson)
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(grant => grant.StartedAtUtc).IsRequired();
        builder.Property(grant => grant.EndedAtUtc).IsRequired();
        builder.Property(grant => grant.GrantedAtUtc).IsRequired();

        builder.HasOne<AfkFarmSession>()
            .WithMany()
            .HasForeignKey(grant => grant.SessionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_afk_farm_interval_grants_sessions_session_id");
    }
}
