using Elyndor.Core.Combat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CombatConsumableUseConfiguration :
    IEntityTypeConfiguration<CombatConsumableUse>
{
    public void Configure(EntityTypeBuilder<CombatConsumableUse> builder)
    {
        builder.ToTable(
            "combat_consumable_uses",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_combat_consumable_uses_definition_version",
                    "\"DefinitionVersion\" > 0");
                table.HasCheckConstraint(
                    "ck_combat_consumable_uses_max_stack",
                    "\"MaxStack\" >= 2");
            });
        builder.HasKey(state => new { state.SessionId, state.CommandId })
            .HasName("pk_combat_consumable_uses");
        builder.Property(state => state.CommandId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(state => state.ItemDefinitionId)
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(state => state.DefinitionVersion).IsRequired();
        builder.Property(state => state.MaxStack).IsRequired();
        builder.Property(state => state.UsedAtUtc).IsRequired();

        builder.HasOne<ActiveCombatSession>()
            .WithMany()
            .HasForeignKey(state => new { state.SessionId, state.CharacterId })
            .HasPrincipalKey(state => new { state.SessionId, state.CharacterId })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_combat_consumable_uses_active_combat_session");

        builder.HasIndex(state => state.CharacterId)
            .HasDatabaseName("ix_combat_consumable_uses_character_id");
    }
}
