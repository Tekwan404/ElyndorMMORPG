using Elyndor.Core.Characters;
using Elyndor.Core.Pvp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elyndor.Infrastructure.Persistence.Configurations;

public sealed class ArenaQueueEntryConfiguration : IEntityTypeConfiguration<ArenaQueueEntry>
{
    public void Configure(EntityTypeBuilder<ArenaQueueEntry> builder)
    {
        builder.ToTable("arena_queue_entries", table =>
        {
            table.HasCheckConstraint("ck_arena_queue_level_positive", "\"Level\" > 0");
            table.HasCheckConstraint("ck_arena_queue_rating_non_negative", "\"RatingSnapshot\" >= 0");
        });
        builder.HasKey(x => x.CharacterId).HasName("pk_arena_queue_entries");
        builder.Property(x => x.Mode).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.Mode, x.JoinedAtUtc })
            .HasDatabaseName("ix_arena_queue_mode_joined");
        builder.HasIndex(x => new { x.Mode, x.RatingSnapshot, x.JoinedAtUtc })
            .HasDatabaseName("ix_arena_queue_mode_rating_joined");
    }
}

public sealed class ArenaMatchConfiguration : IEntityTypeConfiguration<ArenaMatch>
{
    public void Configure(EntityTypeBuilder<ArenaMatch> builder)
    {
        builder.ToTable("arena_matches", table => table.HasCheckConstraint(
            "ck_arena_matches_distinct_characters", "\"CharacterAId\" <> \"CharacterBId\""));
        builder.HasKey(x => x.Id).HasName("pk_arena_matches");
        builder.Property(x => x.SeasonId).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Mode).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterAId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterBId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.CharacterAId).HasDatabaseName("ix_arena_matches_character_a");
        builder.HasIndex(x => x.CharacterBId).HasDatabaseName("ix_arena_matches_character_b");
        builder.HasIndex(x => new { x.Outcome, x.CharacterAId })
            .HasDatabaseName("ix_arena_matches_active_character_a");
        builder.HasIndex(x => new { x.Outcome, x.CharacterBId })
            .HasDatabaseName("ix_arena_matches_active_character_b");
    }
}

public sealed class ArenaStandingConfiguration : IEntityTypeConfiguration<ArenaStanding>
{
    public void Configure(EntityTypeBuilder<ArenaStanding> builder)
    {
        builder.ToTable("arena_standings", table =>
        {
            table.HasCheckConstraint("ck_arena_standings_rating_non_negative", "\"Rating\" >= 0");
            table.HasCheckConstraint("ck_arena_standings_counts_non_negative", "\"Wins\" >= 0 AND \"Losses\" >= 0 AND \"Draws\" >= 0");
        });
        builder.HasKey(x => new { x.CharacterId, x.SeasonId }).HasName("pk_arena_standings");
        builder.Property(x => x.SeasonId).HasMaxLength(16).IsRequired();
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.SeasonId, x.Rating, x.Wins })
            .IsDescending(false, true, true).HasDatabaseName("ix_arena_standings_leaderboard");
    }
}

public sealed class ArenaHonorWalletConfiguration : IEntityTypeConfiguration<ArenaHonorWallet>
{
    public void Configure(EntityTypeBuilder<ArenaHonorWallet> builder)
    {
        builder.ToTable("arena_honor_wallets", table => table.HasCheckConstraint(
            "ck_arena_honor_wallets_balance_non_negative", "\"Balance\" >= 0"));
        builder.HasKey(x => x.CharacterId).HasName("pk_arena_honor_wallets");
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ArenaHonorLedgerEntryConfiguration : IEntityTypeConfiguration<ArenaHonorLedgerEntry>
{
    public void Configure(EntityTypeBuilder<ArenaHonorLedgerEntry> builder)
    {
        builder.ToTable("arena_honor_ledger_entries", table =>
        {
            table.HasCheckConstraint("ck_arena_honor_ledger_delta_positive", "\"Delta\" > 0");
            table.HasCheckConstraint("ck_arena_honor_ledger_balance_valid", "\"BalanceAfter\" >= \"Delta\"");
        });
        builder.HasKey(x => new { x.MatchId, x.CharacterId }).HasName("pk_arena_honor_ledger_entries");
        builder.HasOne<ArenaMatch>().WithMany().HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.CharacterId, x.CreatedAtUtc })
            .HasDatabaseName("ix_arena_honor_ledger_character_created");
    }
}