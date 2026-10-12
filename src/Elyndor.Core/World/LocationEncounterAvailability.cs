namespace Elyndor.Core.World;

public static class LocationEncounterAvailability
{
    public static bool IsAvailable(LocationEncounterDefinition encounter, DateTimeOffset now)
    {
        if (encounter.Availability is not { } schedule)
            return true;
        Validate(schedule);
        return Phase(schedule, now) < schedule.DurationSeconds;
    }

    public static DateTimeOffset? NextChange(LocationEncounterDefinition encounter, DateTimeOffset now)
    {
        if (encounter.Availability is not { } schedule)
            return null;
        Validate(schedule);
        double phase = Phase(schedule, now);
        double seconds = phase < schedule.DurationSeconds
            ? schedule.DurationSeconds - phase
            : schedule.PeriodSeconds - phase;
        return now.AddSeconds(seconds);
    }

    private static double Phase(EncounterAvailabilityDefinition schedule, DateTimeOffset now)
    {
        double seconds = (now - DateTimeOffset.UnixEpoch).TotalSeconds - schedule.OffsetSeconds;
        return (seconds % schedule.PeriodSeconds + schedule.PeriodSeconds) % schedule.PeriodSeconds;
    }

    private static void Validate(EncounterAvailabilityDefinition schedule)
    {
        if (!schedule.IsValid)
            throw new ArgumentException("Invalid encounter availability schedule.", nameof(schedule));
    }
}
