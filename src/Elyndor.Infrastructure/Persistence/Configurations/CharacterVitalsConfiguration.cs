using Elyndor.Core.Characters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CharacterVitalsConfiguration : IEntityTypeConfiguration<CharacterVitals>
{
    public void Configure(EntityTypeBuilder<CharacterVitals> builder)
    {
        builder.ToTable(
            "character_vitals",
            table =>
            {
                table.HasCheckConstraint("ck_character_vitals_hp_non_negative", "\"CurrentHp\" >= 0");
                table.HasCheckConstraint(
                    "ck_character_vitals_resource_non_negative",
                    "\"CurrentResource\" >= 0");
            });
        builder.HasKey(vitals => vitals.CharacterId).HasName("pk_character_vitals");
        builder.Property(vitals => vitals.CurrentHp).HasPrecision(12, 3).IsRequired();
        builder.Property(vitals => vitals.CurrentResource).HasPrecision(12, 3).IsRequired();
        builder.Property(vitals => vitals.CheckpointedAtUtc).IsRequired();
        builder.Property(vitals => vitals.ContextStartedAtUtc).IsRequired();
        builder.HasOne<Character>()
            .WithOne()
            .HasForeignKey<CharacterVitals>(vitals => vitals.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_character_vitals_characters_character_id");
    }
}
