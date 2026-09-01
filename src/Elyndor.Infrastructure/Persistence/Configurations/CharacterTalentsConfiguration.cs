using Elyndor.Core.Talents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CharacterTalentsConfiguration : IEntityTypeConfiguration<CharacterTalents>
{
    public void Configure(EntityTypeBuilder<CharacterTalents> builder)
    {
        builder.ToTable("character_talents");
        builder.HasKey(ct => ct.CharacterId).HasName("pk_character_talents");

        builder.Property(ct => ct.TalentTreeId).HasMaxLength(32).IsRequired();
        builder.Property(ct => ct.AllocatedPoints).IsRequired();
        builder.Property(ct => ct.TotalSpentPoints).IsRequired();
        builder.Property(ct => ct.TotalAvailablePoints).IsRequired();
        builder.Property(ct => ct.LastModifiedUtc).IsRequired();

        // AllocatedPoints stored as JSONB in PostgreSQL
        builder.Property(ct => ct.AllocatedPoints)
            .HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, new System.Text.Json.JsonSerializerOptions()),
                v => System.Text.Json.JsonSerializer.Deserialize<IReadOnlyDictionary<string, int>>(v, new System.Text.Json.JsonSerializerOptions()) ?? new Dictionary<string, int>().AsReadOnly());

        builder.HasOne<Elyndor.Core.Characters.Character>()
            .WithOne()
            .HasForeignKey<CharacterTalents>(ct => ct.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_character_talents_characters_character_id");

        builder.HasIndex(ct => ct.TalentTreeId)
            .HasDatabaseName("ix_character_talents_tree_id");
    }
}
