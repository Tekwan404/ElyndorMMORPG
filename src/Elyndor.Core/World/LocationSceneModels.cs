namespace Elyndor.Core.World;

// A shared UTC schedule, not a per-character encounter roll or a consumable spawn.
public sealed record EncounterAvailabilityDefinition(
    int PeriodSeconds,
    int DurationSeconds,
    int OffsetSeconds = 0)
{
    public bool IsValid => PeriodSeconds > 0
        && DurationSeconds > 0 && DurationSeconds < PeriodSeconds
        && OffsetSeconds >= 0 && OffsetSeconds < PeriodSeconds;
}

public sealed record LocationPointDefinition(
    string Id,
    string DisplayName,
    string Description);

public enum LocationWorldState
{
    Calm,
    Invasion,
    Corruption
}
