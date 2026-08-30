namespace Elyndor.Contracts.Characters;

public sealed record AccountResponse(Guid AccountId);

public sealed record CreateCharacterRequest(
    Guid RequestId,
    string Name,
    string RaceId,
    string GenderId,
    string ClassId);

public sealed record CharacterResponse(
    Guid Id,
    string Name,
    string RaceId,
    string GenderId,
    string ClassId,
    int Level,
    DateTimeOffset CreatedAtUtc);
