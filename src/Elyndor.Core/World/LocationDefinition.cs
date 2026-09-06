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
    IReadOnlyList<LocationEncounterDefinition>? Encounters = null,
    int MinimumLevel = 1,
    int MaximumLevel = 60,
    string? RequiredContractId = null,
    string? ArtId = null,
    string Description = "");
