namespace Elyndor.Core.World;

public sealed record LocationEncounterDefinition(
    string MonsterId,
    decimal Weight = 1);

public sealed record LocationDefinition(
    string Id,
    string DisplayName,
    string DangerLevel,
    int RecommendedLevel,
    IReadOnlyList<string> Transitions,
    IReadOnlyList<LocationEncounterDefinition>? Encounters = null);
