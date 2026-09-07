using Elyndor.Core.Characters;
using Elyndor.Core.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CharacterTravelStateConfiguration :
    IEntityTypeConfiguration<CharacterTravelState>
{
    public void Configure(EntityTypeBuilder<CharacterTravelState> builder)
    {
        builder.ToTable(
            "character_travel_states",
            table => table.HasCheckConstraint(
                "ck_character_travel_states_duration",
                "\"EndsAtUtc\" > \"StartedAtUtc\""));

        builder.HasKey(state => state.CharacterId)
            .HasName("pk_character_travel_states");
        builder.Property(state => state.RequestId).IsRequired();
        builder.Property(state => state.FromLocationId).HasMaxLength(64).IsRequired();
        builder.Property(state => state.TargetLocationId).HasMaxLength(64).IsRequired();
        builder.Property(state => state.StartedAtUtc).IsRequired();
        builder.Property(state => state.EndsAtUtc).IsRequired();

        builder.HasOne<Character>()
            .WithOne()
            .HasForeignKey<CharacterTravelState>(state => state.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_character_travel_states_characters_character_id");

        builder.HasIndex(state => state.EndsAtUtc)
            .HasDatabaseName("ix_character_travel_states_ends_at_utc");
        builder.HasIndex(state => new { state.CharacterId, state.RequestId })
            .IsUnique()
            .HasDatabaseName("uq_character_travel_states_character_request");
    }
}
