using Elyndor.Core.Professions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CharacterProfessionConfiguration : IEntityTypeConfiguration<CharacterProfession>
{
    public void Configure(EntityTypeBuilder<CharacterProfession> builder)
    {
        builder.ToTable("character_professions");
        builder.HasKey(item => new { item.CharacterId, item.ProfessionId });
        builder.Property(item => item.CharacterId).HasColumnName("character_id");
        builder.Property(item => item.ProfessionId).HasColumnName("profession_id").HasMaxLength(64);
        builder.Property(item => item.Skill).HasColumnName("skill");
        builder.Property(item => item.LearnedAtUtc).HasColumnName("learned_at_utc");
        builder.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.HasIndex(item => item.CharacterId);
        builder.HasOne<Elyndor.Core.Characters.Character>()
            .WithMany()
            .HasForeignKey(item => item.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SkinnableCorpseConfiguration : IEntityTypeConfiguration<SkinnableCorpse>
{
    public void Configure(EntityTypeBuilder<SkinnableCorpse> builder)
    {
        builder.ToTable("character_skinnable_corpses");
        builder.HasKey(item => new { item.CharacterId, item.CombatSessionId, item.EnemyActorId });
        builder.Property(item => item.CharacterId).HasColumnName("character_id");
        builder.Property(item => item.CombatSessionId).HasColumnName("combat_session_id");
        builder.Property(item => item.EnemyActorId).HasColumnName("enemy_actor_id");
        builder.Property(item => item.MonsterDefinitionId).HasColumnName("monster_definition_id").HasMaxLength(128);
        builder.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(item => item.ExpiresAtUtc).HasColumnName("expires_at_utc");
        builder.Property(item => item.SkinnedAtUtc).HasColumnName("skinned_at_utc");
        builder.Property(item => item.SkinningMutationId).HasColumnName("skinning_mutation_id");
        builder.Property(item => item.YieldItemId).HasColumnName("yield_item_id").HasMaxLength(128);
        builder.Property(item => item.YieldQuantity).HasColumnName("yield_quantity");
        builder.Property(item => item.SkillIncreased).HasColumnName("skill_increased");
        builder.HasIndex(item => new { item.CharacterId, item.ExpiresAtUtc });
        builder.HasOne<Elyndor.Core.Characters.Character>()
            .WithMany()
            .HasForeignKey(item => item.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
