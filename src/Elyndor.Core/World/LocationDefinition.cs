namespace Elyndor.Core.World;

public sealed record LocationDefinition(
    string Id,
    string DisplayName,
    string DangerLevel,
    int RecommendedLevel,
    IReadOnlyList<string> Transitions);
