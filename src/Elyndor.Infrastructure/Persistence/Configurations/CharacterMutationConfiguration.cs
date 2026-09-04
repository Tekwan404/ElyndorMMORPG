using Elyndor.Core.Characters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CharacterMutationConfiguration : IEntityTypeConfiguration<CharacterMutation>
{
    public void Configure(EntityTypeBuilder<CharacterMutation> builder)
    {
        builder.ToTable("character_mutations");
        builder.HasKey(mutation => new { mutation.CharacterId, mutation.MutationId })
            .HasName("pk_character_mutations");

        builder.Property(mutation => mutation.OperationType).HasMaxLength(32).IsRequired();
        builder.Property(mutation => mutation.RequestFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(mutation => mutation.CommittedAtUtc).IsRequired();

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(mutation => mutation.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_character_mutations_characters_character_id");

        builder.HasIndex(mutation => new { mutation.CharacterId, mutation.CommittedAtUtc })
            .HasDatabaseName("ix_character_mutations_character_committed_at");
    }
}
