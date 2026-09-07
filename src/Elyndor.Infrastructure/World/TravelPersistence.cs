using Elyndor.Core.World;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.World;

public static class TravelPersistence
{
    public static async Task<CharacterTravelState?> GetActiveAsync(
        GameDbContext dbContext,
        Guid characterId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await CompleteDueAsync(
            dbContext,
            characterId,
            now,
            cancellationToken);
        return await dbContext.CharacterTravelStates
            .AsNoTracking()
            .SingleOrDefaultAsync(
                state => state.CharacterId == characterId,
                cancellationToken);
    }

    public static async Task<bool> IsTravellingAsync(
        GameDbContext dbContext,
        Guid characterId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await GetActiveAsync(
            dbContext,
            characterId,
            now,
            cancellationToken) is not null;

    public static async Task<bool> CompleteDueAsync(
        GameDbContext dbContext,
        Guid characterId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        CharacterTravelState? travel = await dbContext.CharacterTravelStates
            .SingleOrDefaultAsync(
                state => state.CharacterId == characterId,
                cancellationToken);
        if (travel is null || travel.EndsAtUtc > now)
            return false;

        CharacterLocation location = await dbContext.CharacterLocations
            .SingleAsync(
                state => state.CharacterId == characterId,
                cancellationToken);
        if (!string.Equals(
                location.LocationId,
                travel.FromLocationId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Travel state for character '{characterId}' no longer matches its source location.");
        }

        DateTimeOffset completedAtUtc = travel.EndsAtUtc < location.UpdatedAtUtc
            ? location.UpdatedAtUtc
            : travel.EndsAtUtc;
        location.Relocate(travel.TargetLocationId, completedAtUtc);
        dbContext.TravelOperations.Add(new TravelOperation(
            characterId,
            travel.RequestId,
            travel.TargetLocationId,
            travel.TargetLocationId,
            location.Version,
            completedAtUtc));
        dbContext.CharacterTravelStates.Remove(travel);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
