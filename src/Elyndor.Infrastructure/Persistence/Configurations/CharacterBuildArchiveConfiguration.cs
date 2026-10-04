using Elyndor.Core.Characters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CharacterBuildArchiveConfiguration : IEntityTypeConfiguration<CharacterBuildArchive>
{
    public void Configure(EntityTypeBuilder<CharacterBuildArchive> builder)
    {
        builder.ToTable("character_build_archives");
        builder.HasKey(build => build.BuildHash).HasName("pk_character_build_archives");
        builder.Property(build => build.BuildHash).HasMaxLength(64);
        builder.Property(build => build.PayloadJson).HasColumnType("jsonb").IsRequired();
    }
}

public sealed class TrainingBuildReferenceConfiguration : IEntityTypeConfiguration<TrainingBuildReference>
{
    public void Configure(EntityTypeBuilder<TrainingBuildReference> builder)
    {
        builder.ToTable("training_build_references");
        builder.HasKey(reference => reference.SessionId);
        builder.Property(reference => reference.BuildHash).HasMaxLength(64).IsRequired();
        builder.HasOne<CharacterBuildArchive>().WithMany().HasForeignKey(reference => reference.BuildHash)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
