using Elyndor.Core.Characters;
using Elyndor.Core.Items;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CharacterSpatialArtifactConfiguration : IEntityTypeConfiguration<CharacterSpatialArtifact>
{
    public void Configure(EntityTypeBuilder<CharacterSpatialArtifact> builder)
    {
        builder.ToTable("character_spatial_artifacts");
        builder.HasKey(artifact => artifact.CharacterId)
            .HasName("pk_character_spatial_artifacts");
        builder.Property(artifact => artifact.CharacterItemId).IsRequired();

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(artifact => artifact.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_character_spatial_artifacts_characters_character_id");

        builder.HasOne<CharacterItem>()
            .WithMany()
            .HasForeignKey(artifact => artifact.CharacterItemId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_character_spatial_artifacts_character_items_item_id");

        builder.HasIndex(artifact => artifact.CharacterItemId)
            .IsUnique()
            .HasDatabaseName("uq_character_spatial_artifacts_item_id");
    }
}
