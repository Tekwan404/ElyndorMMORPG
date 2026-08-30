using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CharacterConfiguration : IEntityTypeConfiguration<Character>
{
    public void Configure(EntityTypeBuilder<Character> builder)
    {
        builder.ToTable("characters");
        builder.HasKey(character => character.Id).HasName("pk_characters");

        builder.Property(character => character.Name).HasMaxLength(16).IsRequired();
        builder.Property(character => character.NormalizedName).HasMaxLength(16).IsRequired();
        builder.Property(character => character.RaceId).HasMaxLength(16).IsRequired();
        builder.Property(character => character.GenderId).HasMaxLength(16).IsRequired();
        builder.Property(character => character.ClassId).HasMaxLength(16).IsRequired();
        builder.Property(character => character.Level).IsRequired();
        builder.Property(character => character.CreatedAtUtc).IsRequired();

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(character => character.AccountId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_characters_accounts_account_id");

        builder.HasIndex(character => character.AccountId)
            .IsUnique()
            .HasDatabaseName("uq_characters_account_id");
        builder.HasIndex(character => character.CreationRequestId)
            .IsUnique()
            .HasDatabaseName("uq_characters_creation_request_id");
        builder.HasIndex(character => character.NormalizedName)
            .IsUnique()
            .HasDatabaseName("uq_characters_normalized_name");
    }
}
