using Elyndor.Core.Characters;
using Elyndor.Core.Quests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CharacterQuestStateConfiguration :
    IEntityTypeConfiguration<CharacterQuestState>
{
    public void Configure(EntityTypeBuilder<CharacterQuestState> builder)
    {
        builder.ToTable(
            "character_quest_states",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_character_quest_states_status",
                    "\"Status\" IN ('ACTIVE','READY_TO_CLAIM','COMPLETED')");
                table.HasCheckConstraint(
                    "ck_character_quest_states_progress_json",
                    "jsonb_typeof(\"ProgressJson\") = 'object'");
                table.HasCheckConstraint(
                    "ck_character_quest_states_version_positive",
                    "\"StateVersion\" > 0");
            });

        builder.HasKey(state => new { state.CharacterId, state.QuestId })
            .HasName("pk_character_quest_states");
        builder.Property(state => state.QuestId).HasMaxLength(64).IsRequired();
        builder.Property(state => state.Status).HasMaxLength(24).IsRequired();
        builder.Property(state => state.ProgressJson).HasColumnType("jsonb").IsRequired();
        builder.Property(state => state.AcceptedAtUtc).IsRequired();
        builder.Property(state => state.StateVersion).IsRequired();

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(state => state.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_character_quest_states_characters_character_id");

        builder.HasIndex(state => new { state.CharacterId, state.Status })
            .HasDatabaseName("ix_character_quest_states_character_status");
    }
}
