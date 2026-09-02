using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Progression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elyndor.Infrastructure.Combat;

public interface ICombatSessionFinalizer
{
    Task<CombatRewardApplicationResult?> FinalizeAsync(
        Guid characterId,
        CombatSessionSnapshot snapshot,
        CancellationToken cancellationToken);
}

/// <summary>
/// Bridges in-memory combat runtime to permanent character state exactly once when a
/// CombatSession reaches a terminal state. Permanent progression stays outside CombatSession.
/// </summary>
public sealed class CombatSessionFinalizer(IServiceScopeFactory scopeFactory) : ICombatSessionFinalizer
{
    private const string StarterTownId = "STARTER_TOWN";

    public async Task<CombatRewardApplicationResult?> FinalizeAsync(
        Guid characterId,
        CombatSessionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (snapshot.Status == CombatSessionStatus.Active)
            return null;

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        GameDbContext dbContext = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        GameContentPackage content = scope.ServiceProvider.GetRequiredService<GameContentPackage>();
        Character? character = await dbContext.Characters
            .SingleOrDefaultAsync(candidate => candidate.Id == characterId, cancellationToken);
        CharacterVitals? vitals = await dbContext.CharacterVitals
            .SingleOrDefaultAsync(candidate => candidate.CharacterId == characterId, cancellationToken);
        CharacterLocation? location = await dbContext.CharacterLocations
            .SingleOrDefaultAsync(candidate => candidate.CharacterId == characterId, cancellationToken);

        if (vitals is not null)
        {
            DateTimeOffset checkpointAt = snapshot.ServerTimeUtc < vitals.CheckpointedAtUtc
                ? vitals.CheckpointedAtUtc
                : snapshot.ServerTimeUtc;

            if (snapshot.Status == CombatSessionStatus.Defeat && character is not null)
            {
                ClassProfile classProfile = (content.ClassProfiles
                    ?? throw new InvalidOperationException("Class profiles are required."))
                    .Single(profile => string.Equals(profile.Id, character.ClassId, StringComparison.Ordinal));
                ResourceProfile resourceProfile = (content.ResourceProfiles
                    ?? throw new InvalidOperationException("Resource profiles are required."))
                    .Single(profile => string.Equals(
                        profile.Id,
                        classProfile.ResourceProfileId,
                        StringComparison.Ordinal));

                if (location is not null)
                {
                    DateTimeOffset relocateAt = checkpointAt < location.UpdatedAtUtc
                        ? location.UpdatedAtUtc
                        : checkpointAt;
                    if (!string.Equals(location.LocationId, StarterTownId, StringComparison.Ordinal))
                        location.Relocate(StarterTownId, relocateAt);
                    checkpointAt = relocateAt;
                }

                // Prototype respawn: defeat has no XP/item penalty. The player returns to
                // the safe town immediately ready to play again.
                vitals.BeginContext(
                    snapshot.Player.MaxHp,
                    resourceProfile.RespawnValue,
                    checkpointAt);
            }
            else
            {
                vitals.BeginContext(
                    Math.Max(0m, snapshot.Player.Hp),
                    Math.Max(0m, snapshot.Player.Resource),
                    checkpointAt);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (snapshot.Status == CombatSessionStatus.Victory)
        {
            CombatRewardService rewards =
                scope.ServiceProvider.GetRequiredService<CombatRewardService>();
            return await rewards.ApplyVictoryAsync(characterId, snapshot, cancellationToken);
        }

        return null;
    }
}
