using Elyndor.Core.Pvp;

namespace Elyndor.Infrastructure.Pvp;

/// <summary>Server-side arena configuration (section "Arena"). Feature is off by default.</summary>
public sealed class ArenaOptions
{
    public bool Enabled { get; set; }
    public TimeSpan MatchDuration { get; set; } = ArenaCombatSession.DefaultDuration;
    public TimeSpan ReconnectGrace { get; set; } = TimeSpan.FromSeconds(20);
    public TimeSpan QueueOfflineGrace { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan CompletedMatchRetention { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan RematchCooldown { get; set; } = TimeSpan.FromMinutes(3);
    public TimeSpan HonorFarmWindow { get; set; } = TimeSpan.FromHours(1);
    public int MaxHonorWinsPerOpponentInWindow { get; set; } = 3;
    public int RatingChangeFactor { get; set; } = ArenaProgressionRules.RatingChangeFactor;
    public long VictoryHonor { get; set; } = ArenaProgressionRules.VictoryHonor;
}
