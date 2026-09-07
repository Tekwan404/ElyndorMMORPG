using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CharacterAbilityCooldownConfiguration
    : IEntityTypeConfiguration<CharacterAbilityCooldown>
{
    public void Configure(EntityTypeBuilder<CharacterAbilityCooldown> builder)
    {
        builder.ToTable("character_ability_cooldowns");
        builder.HasKey(state => new { state.CharacterId, state.AbilityId })
            .HasName("pk_character_ability_cooldowns");
        builder.Property(state => state.AbilityId).HasMaxLength(64).IsRequired();
        builder.Property(state => state.ReadyAtUtc).IsRequired();

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(state => state.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_character_ability_cooldowns_characters_character_id");

        builder.HasIndex(state => state.ReadyAtUtc)
            .HasDatabaseName("ix_character_ability_cooldowns_ready_at_utc");
    }
}
