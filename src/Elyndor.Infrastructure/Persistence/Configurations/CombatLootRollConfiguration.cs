using Elyndor.Core.Combat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CombatLootRollConfiguration : IEntityTypeConfiguration<CombatLootRoll>
{
    public void Configure(EntityTypeBuilder<CombatLootRoll> builder)
    {
        builder.ToTable(
            "combat_loot_rolls",
            table => table.HasCheckConstraint(
                "ck_combat_loot_rolls_quantity_positive",
                "\"Quantity\" > 0"));
        builder.HasKey(roll => roll.LootRollId).HasName("pk_combat_loot_rolls");
        builder.Property(roll => roll.ItemDefinitionId).HasMaxLength(64).IsRequired();
        builder.Property(roll => roll.EligibleCharacterIdsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(roll => roll.ChoicesJson).HasColumnType("jsonb").IsRequired();
        builder.Property(roll => roll.RollsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(roll => roll.Rarity).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(roll => roll.State).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(roll => roll.Quantity).IsRequired();
        builder.Property(roll => roll.EndsAtUtc).IsRequired();
        builder.Property(roll => roll.ItemInstanceSeed).IsRequired();

        builder.HasIndex(roll => new { roll.CombatSessionId, roll.ItemDefinitionId })
            .IsUnique()
            .HasDatabaseName("uq_combat_loot_rolls_session_item");
        builder.HasIndex(roll => new { roll.State, roll.EndsAtUtc })
            .HasDatabaseName("ix_combat_loot_rolls_state_ends_at_utc");
    }
}
