using Elyndor.Core.Characters;
using Elyndor.Core.Parties;
using Elyndor.Core.WorldBosses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class WorldBossSpawnConfiguration : IEntityTypeConfiguration<WorldBossSpawn>
{
    public void Configure(EntityTypeBuilder<WorldBossSpawn> builder)
    {
        builder.ToTable("world_boss_spawns", table =>
        {
            table.HasCheckConstraint("ck_world_boss_spawns_max_health_positive", "\"MaxHealth\" > 0");
            table.HasCheckConstraint("ck_world_boss_spawns_health_range", "\"CurrentHealth\" >= 0 AND \"CurrentHealth\" <= \"MaxHealth\"");
            table.HasCheckConstraint("ck_world_boss_spawns_phase_positive", "\"CurrentPhase\" > 0");
            table.HasCheckConstraint("ck_world_boss_spawns_expiry_after_spawn", "\"ExpiresAtUtc\" > \"SpawnedAtUtc\"");
        });

        builder.HasKey(x => x.Id).HasName("pk_world_boss_spawns");
        builder.Property(x => x.BossDefinitionId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.MaxHealth).HasPrecision(18, 3).IsRequired();
        builder.Property(x => x.CurrentHealth).HasPrecision(18, 3).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.ContentVersion).HasMaxLength(32).IsRequired();
        builder.Property(x => x.BalanceVersion).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken().IsRequired();

        builder.HasIndex(x => new { x.Status, x.ExpiresAtUtc })
            .HasDatabaseName("ix_world_boss_spawns_status_expires");
        builder.HasIndex(x => x.Status)
            .IsUnique()
            .HasFilter("\"Status\" = 'Active'")
            .HasDatabaseName("uq_world_boss_spawns_single_active");
        builder.HasIndex(x => new { x.BossDefinitionId, x.SpawnedAtUtc })
            .HasDatabaseName("ix_world_boss_spawns_definition_spawned");
    }
}

public sealed class WorldBossContributionConfiguration : IEntityTypeConfiguration<WorldBossContribution>
{
    public void Configure(EntityTypeBuilder<WorldBossContribution> builder)
    {
        builder.ToTable("world_boss_contributions", table =>
        {
            table.HasCheckConstraint("ck_world_boss_contributions_damage_non_negative", "\"Damage\" >= 0");
            table.HasCheckConstraint("ck_world_boss_contributions_healing_non_negative", "\"Healing\" >= 0");
            table.HasCheckConstraint("ck_world_boss_contributions_activity_order", "\"LastActivityAtUtc\" >= \"FirstActivityAtUtc\"");
        });

        builder.HasKey(x => new { x.SpawnId, x.CharacterId }).HasName("pk_world_boss_contributions");
        builder.Property(x => x.Damage).HasPrecision(18, 3).IsRequired();
        builder.Property(x => x.Healing).HasPrecision(18, 3).IsRequired();

        builder.HasOne<WorldBossSpawn>().WithMany().HasForeignKey(x => x.SpawnId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_world_boss_contributions_spawn");
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_world_boss_contributions_character");

        builder.HasIndex(x => new { x.SpawnId, x.Damage, x.Healing })
            .IsDescending(false, true, true)
            .HasDatabaseName("ix_world_boss_contributions_leaderboard");
        builder.HasIndex(x => new { x.CharacterId, x.LastActivityAtUtc })
            .HasDatabaseName("ix_world_boss_contributions_character_activity");
    }
}

public sealed class WorldBossPartyContributionConfiguration : IEntityTypeConfiguration<WorldBossPartyContribution>
{
    public void Configure(EntityTypeBuilder<WorldBossPartyContribution> builder)
    {
        builder.ToTable("world_boss_party_contributions", table =>
        {
            table.HasCheckConstraint("ck_world_boss_party_contributions_damage_non_negative", "\"Damage\" >= 0");
            table.HasCheckConstraint("ck_world_boss_party_contributions_healing_non_negative", "\"Healing\" >= 0");
        });

        builder.HasKey(x => new { x.SpawnId, x.PartyId }).HasName("pk_world_boss_party_contributions");
        builder.Property(x => x.Damage).HasPrecision(18, 3).IsRequired();
        builder.Property(x => x.Healing).HasPrecision(18, 3).IsRequired();

        builder.HasOne<WorldBossSpawn>().WithMany().HasForeignKey(x => x.SpawnId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_world_boss_party_contributions_spawn");
        builder.HasOne<Party>().WithMany().HasForeignKey(x => x.PartyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_world_boss_party_contributions_party");

        builder.HasIndex(x => new { x.SpawnId, x.Damage })
            .IsDescending(false, true)
            .HasDatabaseName("ix_world_boss_party_contributions_leaderboard");
    }
}

public sealed class WorldBossHealingMutationConfiguration : IEntityTypeConfiguration<WorldBossHealingMutation>
{
    public void Configure(EntityTypeBuilder<WorldBossHealingMutation> builder)
    {
        builder.ToTable("world_boss_healing_mutations", table =>
            table.HasCheckConstraint("ck_world_boss_healing_mutations_amount_positive", "\"EffectiveHealing\" > 0"));

        builder.HasKey(x => new { x.SpawnId, x.MutationId })
            .HasName("pk_world_boss_healing_mutations");
        builder.Property(x => x.EffectiveHealing).HasPrecision(18, 3).IsRequired();

        builder.HasOne<WorldBossSpawn>().WithMany().HasForeignKey(x => x.SpawnId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_world_boss_healing_mutations_spawn");
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_world_boss_healing_mutations_character");
        builder.HasOne<Party>().WithMany().HasForeignKey(x => x.PartyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_world_boss_healing_mutations_party");

        builder.HasIndex(x => x.CombatSessionId)
            .HasDatabaseName("ix_world_boss_healing_mutations_combat_session");
    }
}

public sealed class WorldBossDamageMutationConfiguration : IEntityTypeConfiguration<WorldBossDamageMutation>
{
    public void Configure(EntityTypeBuilder<WorldBossDamageMutation> builder)
    {
        builder.ToTable("world_boss_damage_mutations", table =>
        {
            table.HasCheckConstraint("ck_world_boss_damage_mutations_requested_non_negative", "\"RequestedDamage\" >= 0");
            table.HasCheckConstraint("ck_world_boss_damage_mutations_applied_range", "\"AppliedDamage\" >= 0 AND \"AppliedDamage\" <= \"RequestedDamage\"");
        });

        builder.HasKey(x => new { x.SpawnId, x.MutationId }).HasName("pk_world_boss_damage_mutations");
        builder.Property(x => x.RequestedDamage).HasPrecision(18, 3).IsRequired();
        builder.Property(x => x.AppliedDamage).HasPrecision(18, 3).IsRequired();

        builder.HasOne<WorldBossSpawn>().WithMany().HasForeignKey(x => x.SpawnId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_world_boss_damage_mutations_spawn");
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_world_boss_damage_mutations_character");
        builder.HasOne<Party>().WithMany().HasForeignKey(x => x.PartyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_world_boss_damage_mutations_party");

        builder.HasIndex(x => new { x.CharacterId, x.CommittedAtUtc })
            .HasDatabaseName("ix_world_boss_damage_mutations_character_committed");
        builder.HasIndex(x => x.CombatSessionId)
            .HasDatabaseName("ix_world_boss_damage_mutations_combat_session");
    }
}

public sealed class WorldBossRewardSettlementConfiguration : IEntityTypeConfiguration<WorldBossRewardSettlement>
{
    public void Configure(EntityTypeBuilder<WorldBossRewardSettlement> builder)
    {
        builder.ToTable("world_boss_reward_settlements", table =>
        {
            table.HasCheckConstraint("ck_world_boss_reward_settlements_score_non_negative", "\"ContributionScore\" >= 0");
            table.HasCheckConstraint("ck_world_boss_reward_settlements_rewards_non_negative", "\"Gold\" >= 0 AND \"Experience\" >= 0 AND \"Tokens\" >= 0");
        });

        builder.HasKey(x => new { x.SpawnId, x.CharacterId }).HasName("pk_world_boss_reward_settlements");
        builder.Property(x => x.ContributionScore).HasPrecision(18, 3).IsRequired();
        builder.Property(x => x.RewardTier).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.LootResultJson).HasColumnType("jsonb").IsRequired();

        builder.HasOne<WorldBossSpawn>().WithMany().HasForeignKey(x => x.SpawnId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_world_boss_reward_settlements_spawn");
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_world_boss_reward_settlements_character");

        builder.HasIndex(x => new { x.CharacterId, x.SettledAtUtc })
            .HasDatabaseName("ix_world_boss_reward_settlements_character_settled");
    }
}

public sealed class WorldBossCombatSessionBindingConfiguration
    : IEntityTypeConfiguration<WorldBossCombatSessionBinding>
{
    public void Configure(EntityTypeBuilder<WorldBossCombatSessionBinding> builder)
    {
        builder.ToTable("world_boss_combat_sessions");
        builder.HasKey(x => x.CombatSessionId).HasName("pk_world_boss_combat_sessions");
        builder.Property(x => x.CombatSessionId).ValueGeneratedNever();

        builder.HasOne<WorldBossSpawn>().WithMany().HasForeignKey(x => x.SpawnId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_world_boss_combat_sessions_spawn");
        builder.HasOne<Party>().WithMany().HasForeignKey(x => x.PartyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_world_boss_combat_sessions_party");

        builder.HasIndex(x => x.SpawnId)
            .HasDatabaseName("ix_world_boss_combat_sessions_spawn");
        builder.HasIndex(x => x.PartyId)
            .HasDatabaseName("ix_world_boss_combat_sessions_party");
    }
}
