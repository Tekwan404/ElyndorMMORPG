using Elyndor.Core.Characters;
using Elyndor.Core.Parties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class PartyConfiguration : IEntityTypeConfiguration<Party>
{
    public void Configure(EntityTypeBuilder<Party> builder)
    {
        builder.ToTable("parties");
        builder.HasKey(party => party.Id).HasName("pk_parties");
        builder.Property(party => party.CreationRequestId).IsRequired();
        builder.Property(party => party.LeaderCharacterId).IsRequired();
        builder.Property(party => party.State).HasConversion<int>().IsRequired();
        builder.Property(party => party.Version).IsRequired();
        builder.Property(party => party.CreatedAtUtc).IsRequired();

        builder.HasIndex(party => party.CreationRequestId)
            .IsUnique()
            .HasDatabaseName("uq_parties_creation_request_id");
        builder.HasIndex(party => party.LeaderCharacterId)
            .HasDatabaseName("ix_parties_leader_character_id");
        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(party => party.LeaderCharacterId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_parties_leader_character");

        builder.HasMany(party => party.Members)
            .WithOne()
            .HasForeignKey(member => member.PartyId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_party_members_parties_party_id");
        builder.Navigation(party => party.Members)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class PartyMemberConfiguration : IEntityTypeConfiguration<PartyMember>
{
    public void Configure(EntityTypeBuilder<PartyMember> builder)
    {
        builder.ToTable("party_members");
        builder.HasKey(member => new { member.PartyId, member.CharacterId })
            .HasName("pk_party_members");
        builder.Property(member => member.JoinedAtUtc).IsRequired();
        builder.Property(member => member.State).HasConversion<int>().IsRequired();

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(member => member.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_party_members_character_id");
        builder.HasIndex(member => member.CharacterId)
            .IsUnique()
            .HasDatabaseName("uq_party_members_character_id");
    }
}

public sealed class PartyInviteConfiguration : IEntityTypeConfiguration<PartyInvite>
{
    public void Configure(EntityTypeBuilder<PartyInvite> builder)
    {
        builder.ToTable("party_invites");
        builder.HasKey(invite => invite.Id).HasName("pk_party_invites");
        builder.Property(invite => invite.Mode).HasConversion<int>().IsRequired();
        builder.Property(invite => invite.Status).HasConversion<int>().IsRequired();
        builder.Property(invite => invite.CreatedAtUtc).IsRequired();
        builder.Property(invite => invite.ExpiresAtUtc).IsRequired();

        builder.HasOne<Party>()
            .WithMany()
            .HasForeignKey(invite => invite.PartyId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_party_invites_party_id");
        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(invite => invite.InviterCharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_party_invites_inviter_character_id");
        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(invite => invite.TargetCharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_party_invites_target_character_id");

        builder.HasIndex(invite => new { invite.TargetCharacterId, invite.Status })
            .HasDatabaseName("ix_party_invites_target_status");
    }
}
