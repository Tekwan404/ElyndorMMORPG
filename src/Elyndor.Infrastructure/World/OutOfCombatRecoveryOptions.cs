namespace Elyndor.Infrastructure.World;

public sealed class OutOfCombatRecoveryOptions
{
    public const string SectionName = "Gameplay:Recovery";

    // Percent of MaxHP restored per second while out of combat.
    public decimal TownHpPercentPerSecond { get; init; } = 15m;

    // Field recovery is intentionally slower than resting in a safe town.
    public decimal FieldHpPercentPerSecond { get; init; } = 10m;

    public bool IsValid() =>
        TownHpPercentPerSecond >= 0m
        && TownHpPercentPerSecond <= 100m
        && FieldHpPercentPerSecond >= 0m
        && FieldHpPercentPerSecond <= 100m;
}
