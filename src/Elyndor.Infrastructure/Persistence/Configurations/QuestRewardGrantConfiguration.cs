using Elyndor.Core.Characters;
using Elyndor.Core.Quests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class QuestRewardGrantConfiguration :
    IEntityTypeConfiguration<QuestRewardGrant>
{
    public void Configure(EntityTypeBuilder<QuestRewardGrant> builder)
    {
        builder.ToTable(
            "quest_reward_grants",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_quest_reward_grants_xp_non_negative",
                    "\"XpEarned\" >= 0");
                table.HasCheckConstraint(
                    "ck_quest_reward_grants_gold_non_negative",
                    "\"GoldEarned\" >= 0");
                table.HasCheckConstraint(
                    "ck_quest_reward_grants_items_json",
                    "jsonb_typeof(\"RewardItemsJson\") = 'array'");
            });

        builder.HasKey(grant => new { grant.CharacterId, grant.QuestId })
            .HasName("pk_quest_reward_grants");
        builder.Property(grant => grant.QuestId).HasMaxLength(64).IsRequired();
        builder.Property(grant => grant.ClaimMutationId).IsRequired();
        builder.Property(grant => grant.RewardItemsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(grant => grant.GrantedAtUtc).IsRequired();

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(grant => grant.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_quest_reward_grants_characters_character_id");

        builder.HasIndex(grant => grant.ClaimMutationId)
            .IsUnique()
            .HasDatabaseName("uq_quest_reward_grants_claim_mutation_id");
    }
}
