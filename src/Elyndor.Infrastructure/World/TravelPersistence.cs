using Elyndor.Core.World;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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
        IDbContextTransaction? ownedTransaction = null;
        if (dbContext.Database.CurrentTransaction is null)
        {
            ownedTransaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        try
        {
            CharacterTravelState? travel = await dbContext.CharacterTravelStates
                .FromSqlInterpolated(
                    $"SELECT * FROM game.character_travel_states WHERE \"CharacterId\" = {characterId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (travel is null || travel.EndsAtUtc > now)
            {
                if (ownedTransaction is not null)
                    await ownedTransaction.CommitAsync(cancellationToken);
                return false;
            }

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

            if (ownedTransaction is not null)
                await ownedTransaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            if (ownedTransaction is not null)
                await ownedTransaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (ownedTransaction is not null)
                await ownedTransaction.DisposeAsync();
        }
    }
}
