using Elyndor.Core.Content;

namespace Elyndor.Infrastructure.World;

public sealed class OutOfCombatRecoveryOptions
{
    private const string ManaResourceId = "MANA";

    public const string SectionName = "Gameplay:Recovery";

    // Percent of MaxHP restored per second while out of combat.
    public decimal TownHpPercentPerSecond { get; init; } = 15m;

    // Field recovery is intentionally slower than resting in a safe town.
    public decimal FieldHpPercentPerSecond { get; init; } = 10m;

    // Mana scales with Intellect, so its recovery must scale with MaxMana as well.
    public decimal TownManaPercentPerSecond { get; init; } = 12m;

    public decimal FieldManaPercentPerSecond { get; init; } = 8m;

    public decimal ResolveResourceRegenPerSecond(
        ResourceProfile profile,
        bool isTown)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!string.Equals(profile.Id, ManaResourceId, StringComparison.Ordinal))
            return Math.Max(0m, profile.OutOfCombatRegenPerSecond);

        decimal percentPerSecond = isTown
            ? TownManaPercentPerSecond
            : FieldManaPercentPerSecond;
        return profile.MaxValue * percentPerSecond / 100m;
    }

    public bool IsValid() =>
        IsPercent(TownHpPercentPerSecond)
        && IsPercent(FieldHpPercentPerSecond)
        && IsPercent(TownManaPercentPerSecond)
        && IsPercent(FieldManaPercentPerSecond);

    private static bool IsPercent(decimal value) => value >= 0m && value <= 100m;
}
