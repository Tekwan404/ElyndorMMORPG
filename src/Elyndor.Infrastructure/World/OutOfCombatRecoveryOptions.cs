namespace Elyndor.Infrastructure.World;

public sealed class OutOfCombatRecoveryOptions
{
    public const string SectionName = "Gameplay:Recovery";

    public decimal TownHpPercentPerSecond { get; init; } = 15m;

    public decimal FieldHpPercentPerSecond { get; init; } = 10m;

    public bool IsValid() =>
        TownHpPercentPerSecond >= 0m
        && TownHpPercentPerSecond <= 100m
        && FieldHpPercentPerSecond >= 0m
        && FieldHpPercentPerSecond <= 100m;
}
