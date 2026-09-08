using Elyndor.Core.Characters;
using Elyndor.Core.Social;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class FriendRequestConfiguration : IEntityTypeConfiguration<FriendRequest>
{
    public void Configure(EntityTypeBuilder<FriendRequest> builder)
    {
        builder.ToTable("friend_requests");
        builder.HasKey(request => request.Id).HasName("pk_friend_requests");
        builder.Property(request => request.PairKey).HasMaxLength(73).IsRequired();
        builder.Property(request => request.Status).HasConversion<int>().IsRequired();
        builder.Property(request => request.CreatedAtUtc).IsRequired();

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(request => request.RequesterCharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_friend_requests_requester_character");
        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(request => request.TargetCharacterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_friend_requests_target_character");

        builder.HasIndex(request => new { request.PairKey, request.Status })
            .IsUnique()
            .HasFilter("\"Status\" = 0")
            .HasDatabaseName("uq_friend_requests_pending_pair");
        builder.HasIndex(request => request.TargetCharacterId)
            .HasDatabaseName("ix_friend_requests_target_character_id");
    }
}

public sealed class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> builder)
    {
        builder.ToTable("friendships");
        builder.HasKey(friendship => friendship.PairKey).HasName("pk_friendships");
        builder.Property(friendship => friendship.PairKey).HasMaxLength(73).IsRequired();
        builder.Property(friendship => friendship.CreatedAtUtc).IsRequired();

        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(friendship => friendship.CharacterAId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_friendships_character_a");
        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(friendship => friendship.CharacterBId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_friendships_character_b");

        builder.HasIndex(friendship => friendship.CharacterAId)
            .HasDatabaseName("ix_friendships_character_a_id");
        builder.HasIndex(friendship => friendship.CharacterBId)
            .HasDatabaseName("ix_friendships_character_b_id");
    }
}
