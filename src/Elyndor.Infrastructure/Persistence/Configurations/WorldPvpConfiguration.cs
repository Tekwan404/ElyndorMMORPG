using Elyndor.Core.Characters;
using Elyndor.Core.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class WorldPvpStandingConfiguration : IEntityTypeConfiguration<WorldPvpStanding>
{
    public void Configure(EntityTypeBuilder<WorldPvpStanding> builder)
    {
        builder.ToTable("world_pvp_standings", t => t.HasCheckConstraint("ck_world_pvp_standing_nonnegative",
            "\"Rating\" >= 0 AND \"Victories\" >= 0 AND \"Assists\" >= 0 AND \"Deaths\" >= 0 AND \"Season\" > 0"));
        builder.HasKey(x => new { x.CharacterId, x.Season });
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.Season, x.Rating, x.CharacterId }).IsDescending(false, true, false);
    }
}
public sealed class WorldPvpWalletConfiguration : IEntityTypeConfiguration<WorldPvpWallet>
{
    public void Configure(EntityTypeBuilder<WorldPvpWallet> builder)
    {
        builder.ToTable("world_pvp_wallets", t => t.HasCheckConstraint("ck_world_pvp_wallet_nonnegative",
            "\"FieldHonor\" >= 0 AND \"BloodyTokens\" >= 0"));
        builder.HasKey(x => x.CharacterId);
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
    }
}
public sealed class WorldPvpDeathReceiptConfiguration : IEntityTypeConfiguration<WorldPvpDeathReceipt>
{
    public void Configure(EntityTypeBuilder<WorldPvpDeathReceipt> builder)
    {
        builder.ToTable("world_pvp_death_receipts");
        builder.HasKey(x => new { x.SessionId, x.VictimId });
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.VictimId).OnDelete(DeleteBehavior.Cascade);
    }
}
public sealed class WorldPvpRewardReceiptConfiguration : IEntityTypeConfiguration<WorldPvpRewardReceipt>
{
    public void Configure(EntityTypeBuilder<WorldPvpRewardReceipt> builder)
    {
        builder.ToTable("world_pvp_reward_receipts", t => t.HasCheckConstraint("ck_world_pvp_receipt_positive",
            "\"Rating\" > 0 AND \"FieldHonor\" > 0 AND \"BloodyTokens\" >= 0"));
        builder.HasKey(x => new { x.SessionId, x.VictimId, x.CharacterId });
        builder.Property(x => x.GoldEarned).HasDefaultValue(0L);
        builder.ToTable(t => t.HasCheckConstraint("ck_world_pvp_gold_nonnegative", "\"GoldEarned\" >= 0"));
        builder.HasOne<WorldPvpDeathReceipt>().WithMany().HasForeignKey(x => new { x.SessionId, x.VictimId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.CharacterId, x.VictimId, x.AtUtc });
        builder.HasIndex(x => new { x.CharacterId, x.AtUtc });
    }
}
