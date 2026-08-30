namespace Elyndor.Contracts.World;

public sealed record WorldLocationResponse(
    string Id,
    string DisplayName,
    string DangerLevel,
    int RecommendedLevel);

public sealed record BootstrapCharacterResponse(
    Guid Id,
    string Name,
    string RaceId,
    string GenderId,
    string ClassId,
    int Level);

public sealed record BootstrapWorldResponse(
    WorldLocationResponse CurrentLocation,
    long Version,
    IReadOnlyList<WorldLocationResponse> OutgoingTransitions);

public sealed record BootstrapResponse(
    Guid AccountId,
    BootstrapCharacterResponse? Character,
    BootstrapWorldResponse? World,
    string ContentVersion,
    string BalanceVersion,
    DateTimeOffset ServerTimeUtc);

public sealed record TravelRequest(Guid RequestId, string TargetLocationId);

public sealed record TravelResponse(string LocationId, long Version);
