using Elyndor.Core.Characters;
using Elyndor.Core.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CharacterContractCompletionConfiguration
    : IEntityTypeConfiguration<CharacterContractCompletion>
{
    public void Configure(EntityTypeBuilder<CharacterContractCompletion> builder)
    {
        builder.ToTable("character_contract_completions");
        builder.HasKey(state => new { state.CharacterId, state.ContractId })
            .HasName("pk_character_contract_completions");
        builder.Property(state => state.ContractId).HasMaxLength(64).IsRequired();
        builder.Property(state => state.TargetMonsterId).HasMaxLength(64).IsRequired();
        builder.Property(state => state.CombatSessionId).IsRequired();
        builder.Property(state => state.CompletedAtUtc).IsRequired();

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(state => state.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_character_contract_completions_characters_character_id");

        builder.HasIndex(state => state.CombatSessionId)
            .HasDatabaseName("ix_character_contract_completions_combat_session_id");
    }
}
