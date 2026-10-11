using Elyndor.Core.Professions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CharacterRecipeKnowledgeConfiguration : IEntityTypeConfiguration<CharacterRecipeKnowledge>
{
    public void Configure(EntityTypeBuilder<CharacterRecipeKnowledge> builder)
    {
        builder.ToTable("character_recipe_knowledge");
        builder.HasKey(item => new { item.CharacterId, item.ProfessionId, item.RecipeId });
        builder.Property(item => item.ProfessionId).HasMaxLength(64);
        builder.Property(item => item.RecipeId).HasMaxLength(128);
        builder.HasOne<CharacterProfession>().WithMany()
            .HasForeignKey(item => new { item.CharacterId, item.ProfessionId }).OnDelete(DeleteBehavior.Cascade);
    }
}
