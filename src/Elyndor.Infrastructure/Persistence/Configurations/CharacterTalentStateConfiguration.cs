using Elyndor.Core.Characters;
using Elyndor.Core.Talents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CharacterTalentStateConfiguration : IEntityTypeConfiguration<CharacterTalentState>
{
    public void Configure(EntityTypeBuilder<CharacterTalentState> builder)
    {
        builder.ToTable("character_talent_states", table =>
        {
            table.HasCheckConstraint("ck_character_talent_states_active_loadout",
                "\"ActiveLoadoutId\" IN ('LOADOUT_1', 'LOADOUT_2')");
            table.HasCheckConstraint("ck_character_talent_states_state_version",
                "\"StateVersion\" > 0");
            table.HasCheckConstraint("ck_character_talent_states_loadout_1_json",
                "jsonb_typeof(\"Loadout1RanksJson\") = 'object'");
            table.HasCheckConstraint("ck_character_talent_states_loadout_2_json",
                "jsonb_typeof(\"Loadout2RanksJson\") = 'object'");
        });
        builder.HasKey(state => state.CharacterId).HasName("pk_character_talent_states");
        builder.Property(state => state.TalentTreeId).HasMaxLength(64).IsRequired();
        builder.Property(state => state.ActiveLoadoutId).HasMaxLength(16).IsRequired();
        builder.Property(state => state.Loadout1RanksJson).HasColumnType("jsonb").IsRequired();
        builder.Property(state => state.Loadout2RanksJson).HasColumnType("jsonb").IsRequired();
        builder.Property(state => state.TalentVersion).IsRequired();
        builder.Property(state => state.StateVersion).IsConcurrencyToken().IsRequired();
        builder.Property(state => state.LastChangedAtUtc).IsRequired();
        builder.Property(state => state.LastMutationId).HasMaxLength(64);
        builder.HasOne<Character>().WithOne().HasForeignKey<CharacterTalentState>(state => state.CharacterId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_character_talent_states_characters_character_id");
    }
}
