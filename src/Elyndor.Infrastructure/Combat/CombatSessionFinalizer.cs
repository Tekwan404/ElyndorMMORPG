using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Characters;
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
    public async Task<CombatRewardApplicationResult?> FinalizeAsync(
        Guid characterId,
        CombatSessionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (snapshot.Status == CombatSessionStatus.Active)
            return null;

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        GameDbContext dbContext = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        CharacterVitals? vitals = await dbContext.CharacterVitals
            .SingleOrDefaultAsync(
                candidate => candidate.CharacterId == characterId,
                cancellationToken);
        if (vitals is not null)
        {
            DateTimeOffset checkpointAt = snapshot.ServerTimeUtc < vitals.CheckpointedAtUtc
                ? vitals.CheckpointedAtUtc
                : snapshot.ServerTimeUtc;
            vitals.BeginContext(
                Math.Max(0m, snapshot.Player.Hp),
                Math.Max(0m, snapshot.Player.Resource),
                checkpointAt);
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
