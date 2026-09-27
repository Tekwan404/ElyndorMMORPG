using Elyndor.Core.Characters;
using Elyndor.Core.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class PlayerCommerceConfiguration : IEntityTypeConfiguration<PlayerTrade>,
    IEntityTypeConfiguration<AuctionListing>, IEntityTypeConfiguration<CommerceMail>
{
    public void Configure(EntityTypeBuilder<PlayerTrade> builder)
    {
        builder.ToTable("player_trades", t => t.HasCheckConstraint("ck_trade_gold", "\"GoldA\" >= 0 AND \"GoldB\" >= 0 AND \"CharacterAId\" <> \"CharacterBId\""));
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.Ready);
        builder.Property(x => x.State).HasMaxLength(16);
        builder.Property(x => x.ConnectionA).HasMaxLength(128);
        builder.Property(x => x.ConnectionB).HasMaxLength(128);
        builder.HasIndex(x => new { x.State, x.ExpiresAt });
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterAId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterBId).OnDelete(DeleteBehavior.Cascade);
    }
    public void Configure(EntityTypeBuilder<AuctionListing> builder)
    {
        builder.ToTable("auction_listings", t => t.HasCheckConstraint("ck_auction_amounts", "\"Price\" > 0 AND \"Fee\" >= 0 AND \"Tax\" >= 0 AND \"Tax\" <= \"Price\""));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.State).HasMaxLength(16);
        builder.HasIndex(x => new { x.State, x.ExpiresAt });
        builder.HasIndex(x => x.ItemId).IsUnique().HasFilter("\"State\" = 'ACTIVE'");
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.SellerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.BuyerId).OnDelete(DeleteBehavior.Restrict);
    }
    public void Configure(EntityTypeBuilder<CommerceMail> builder)
    {
        builder.ToTable("commerce_mail");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.CharacterId, x.ClaimedAt });
        builder.HasIndex(x => x.ItemId).IsUnique().HasFilter("\"ClaimedAt\" IS NULL");
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
    }
}
