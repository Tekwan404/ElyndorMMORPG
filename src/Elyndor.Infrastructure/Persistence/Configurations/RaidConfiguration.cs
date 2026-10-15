using Elyndor.Core.Characters;
using Elyndor.Core.Raids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class RaidGroupConfiguration : IEntityTypeConfiguration<RaidGroup>
{
    public void Configure(EntityTypeBuilder<RaidGroup> builder)
    {
        builder.ToTable("raids");
        builder.HasKey(raid => raid.Id).HasName("pk_raids");
        builder.Property(raid => raid.CreationRequestId).IsRequired();
        builder.Property(raid => raid.LeaderCharacterId).IsRequired();
        builder.Property(raid => raid.MaximumMembers).IsRequired();
        builder.Property(raid => raid.State).HasConversion<int>().IsRequired();
        builder.Property(raid => raid.Version).IsRequired();
        builder.Property(raid => raid.CreatedAtUtc).IsRequired();

        builder.HasIndex(raid => raid.CreationRequestId)
            .IsUnique()
            .HasDatabaseName("uq_raids_creation_request_id");
        builder.HasIndex(raid => raid.LeaderCharacterId)
            .HasDatabaseName("ix_raids_leader_character_id");
        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(raid => raid.LeaderCharacterId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_raids_leader_character");

        builder.HasMany(raid => raid.Members)
            .WithOne()
            .HasForeignKey(member => member.RaidId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_raid_members_raids_raid_id");
        builder.Navigation(raid => raid.Members)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class RaidMemberConfiguration : IEntityTypeConfiguration<RaidMember>
{
    public void Configure(EntityTypeBuilder<RaidMember> builder)
    {
        builder.ToTable("raid_members");
        builder.HasKey(member => new { member.RaidId, member.CharacterId })
            .HasName("pk_raid_members");
        builder.Property(member => member.Role).HasConversion<int>().IsRequired();
        builder.Property(member => member.State).HasConversion<int>().IsRequired();
        builder.Property(member => member.ReadyState).HasConversion<int>().IsRequired();
        builder.Property(member => member.JoinedAtUtc).IsRequired();

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(member => member.CharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_raid_members_character_id");
        builder.HasIndex(member => member.CharacterId)
            .IsUnique()
            .HasDatabaseName("uq_raid_members_character_id");
    }
}

public sealed class RaidInviteConfiguration : IEntityTypeConfiguration<RaidInvite>
{
    public void Configure(EntityTypeBuilder<RaidInvite> builder)
    {
        builder.ToTable("raid_invites");
        builder.HasKey(invite => invite.Id).HasName("pk_raid_invites");
        builder.Property(invite => invite.Status).HasConversion<int>().IsRequired();
        builder.Property(invite => invite.CreatedAtUtc).IsRequired();
        builder.Property(invite => invite.ExpiresAtUtc).IsRequired();

        builder.HasOne<RaidGroup>()
            .WithMany()
            .HasForeignKey(invite => invite.RaidId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_raid_invites_raid_id");
        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(invite => invite.InviterCharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_raid_invites_inviter_character_id");
        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(invite => invite.TargetCharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_raid_invites_target_character_id");
        builder.HasIndex(invite => new { invite.TargetCharacterId, invite.Status })
            .HasDatabaseName("ix_raid_invites_target_status");
    }
}

public sealed class RaidReadyCheckConfiguration : IEntityTypeConfiguration<RaidReadyCheck>
{
    public void Configure(EntityTypeBuilder<RaidReadyCheck> builder)
    {
        builder.ToTable("raid_ready_checks");
        builder.HasKey(check => check.Id).HasName("pk_raid_ready_checks");
        builder.Property(check => check.State).HasConversion<int>().IsRequired();
        builder.Property(check => check.StartedAtUtc).IsRequired();
        builder.Property(check => check.ExpiresAtUtc).IsRequired();

        builder.HasOne<RaidGroup>()
            .WithMany()
            .HasForeignKey(check => check.RaidId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_raid_ready_checks_raid_id");
        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(check => check.StartedByCharacterId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_raid_ready_checks_started_by_character_id");
        builder.HasIndex(check => new { check.RaidId, check.State })
            .HasDatabaseName("ix_raid_ready_checks_raid_state");
    }
}
