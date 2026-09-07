using Elyndor.Core.Characters;
using Elyndor.Core.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class CharacterContractAcceptanceConfiguration
    : IEntityTypeConfiguration<CharacterContractAcceptance>
{
    public void Configure(EntityTypeBuilder<CharacterContractAcceptance> builder)
    {
        builder.ToTable("character_contract_acceptances");
        builder.HasKey(state => new { state.CharacterId, state.ContractId })
            .HasName("pk_character_contract_acceptances");
        builder.Property(state => state.ContractId).HasMaxLength(64).IsRequired();
        builder.Property(state => state.AcceptedAtUtc).IsRequired();

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(state => state.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_character_contract_acceptances_characters_character_id");
    }
}
