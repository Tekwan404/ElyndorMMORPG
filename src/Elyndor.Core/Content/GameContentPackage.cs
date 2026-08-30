using Elyndor.Core.World;

namespace Elyndor.Core.Content;

public sealed record GameContentPackage(
    string ContentVersion,
    string BalanceVersion,
    DateTimeOffset PublishedAtUtc,
    IReadOnlyList<GameContentDefinition> Definitions,
    IReadOnlyList<LocationDefinition> Locations);

public sealed record GameContentDefinition(
    string Type,
    string Id,
    IReadOnlyList<GameContentReference> References);

public sealed record GameContentReference(string Type, string Id);

public sealed record ContentValidationError(string Code, string Path, string Message);
