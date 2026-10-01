using Elyndor.Core.Characters;
using Elyndor.Core.Pvp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class ArenaInvitationConfiguration : IEntityTypeConfiguration<ArenaInvitation>
{
    public void Configure(EntityTypeBuilder<ArenaInvitation> builder)
    {
        builder.ToTable("arena_invitations", table =>
        {
            table.HasCheckConstraint("ck_arena_invitation_distinct", "\"InviterCharacterId\" <> \"TargetCharacterId\"");
            table.HasCheckConstraint("ck_arena_invitation_expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.InviterCharacterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.TargetCharacterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ArenaMatch>().WithMany().HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => new { x.InviterCharacterId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.TargetCharacterId, x.Status, x.ExpiresAtUtc });
        builder.HasIndex(x => x.MatchId).IsUnique();
    }
}
