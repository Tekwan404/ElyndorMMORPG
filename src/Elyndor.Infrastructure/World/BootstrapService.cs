using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.World;

public sealed record BootstrapCharacter(
    Guid Id,
    string Name,
    string RaceId,
    string GenderId,
    string ClassId,
    int Level);

public sealed record BootstrapLocation(
    string Id,
    string DisplayName,
    string DangerLevel,
    int RecommendedLevel);

public sealed record BootstrapWorld(
    BootstrapLocation CurrentLocation,
    long Version,
    IReadOnlyList<BootstrapLocation> OutgoingTransitions);

public sealed record BootstrapSnapshot(
    Guid AccountId,
    BootstrapCharacter? Character,
    BootstrapWorld? World,
    string ContentVersion,
    string BalanceVersion,
    DateTimeOffset ServerTimeUtc);

public sealed class BootstrapService(
    GameDbContext dbContext,
    GameContentPackage contentPackage,
    WorldMap worldMap,
    TimeProvider timeProvider)
{
    public async Task<BootstrapSnapshot> GetAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        Character? character = await dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.AccountId == accountId,
                cancellationToken);
        if (character is null)
        {
            return new BootstrapSnapshot(
                accountId,
                null,
                null,
                contentPackage.ContentVersion,
                contentPackage.BalanceVersion,
                timeProvider.GetUtcNow());
        }

        CharacterLocation location = await dbContext.CharacterLocations
            .AsNoTracking()
            .SingleAsync(
                candidate => candidate.CharacterId == character.Id,
                cancellationToken);
        LocationDefinition current = worldMap.GetRequired(location.LocationId);
        BootstrapLocation[] transitions = current.Transitions
            .Select(worldMap.GetRequired)
            .Select(ToLocation)
            .ToArray();

        return new BootstrapSnapshot(
            accountId,
            new BootstrapCharacter(
                character.Id,
                character.Name,
                character.RaceId,
                character.GenderId,
                character.ClassId,
                character.Level),
            new BootstrapWorld(ToLocation(current), location.Version, transitions),
            contentPackage.ContentVersion,
            contentPackage.BalanceVersion,
            timeProvider.GetUtcNow());
    }

    private static BootstrapLocation ToLocation(LocationDefinition location) =>
        new(
            location.Id,
            location.DisplayName,
            location.DangerLevel,
            location.RecommendedLevel);
}
