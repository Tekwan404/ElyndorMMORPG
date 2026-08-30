using Elyndor.Core.Characters;
using Elyndor.Core.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CharacterLocationConfiguration : IEntityTypeConfiguration<CharacterLocation>
{
    public void Configure(EntityTypeBuilder<CharacterLocation> builder)
    {
        builder.ToTable("character_locations");
        builder.HasKey(location => location.CharacterId).HasName("pk_character_locations");
        builder.Property(location => location.LocationId).HasMaxLength(64).IsRequired();
        builder.Property(location => location.Version).IsConcurrencyToken().IsRequired();
        builder.Property(location => location.UpdatedAtUtc).IsRequired();

        builder.HasOne<Character>()
            .WithOne()
            .HasForeignKey<CharacterLocation>(location => location.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_character_locations_characters_character_id");
    }
}
