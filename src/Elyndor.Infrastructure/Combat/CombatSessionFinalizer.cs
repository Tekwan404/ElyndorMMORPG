using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Characters;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elyndor.Infrastructure.Combat;

public interface ICombatSessionFinalizer
{
    Task FinalizeAsync(
        Guid characterId,
        CombatSessionSnapshot snapshot,
        CancellationToken cancellationToken);
}

/// <summary>
/// Bridges in-memory combat runtime to permanent character state exactly once when a
/// CombatSession reaches a terminal state. Permanent rewards can be composed here later
/// without moving EF Core writes into CombatSession itself.
/// </summary>
public sealed class CombatSessionFinalizer(IServiceScopeFactory scopeFactory) : ICombatSessionFinalizer
{
    public async Task FinalizeAsync(
        Guid characterId,
        CombatSessionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (snapshot.Status == CombatSessionStatus.Active)
            return;

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        GameDbContext dbContext = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        CharacterVitals? vitals = await dbContext.CharacterVitals
            .SingleOrDefaultAsync(
                candidate => candidate.CharacterId == characterId,
                cancellationToken);
        if (vitals is null)
            return;

        DateTimeOffset checkpointAt = snapshot.ServerTimeUtc < vitals.CheckpointedAtUtc
            ? vitals.CheckpointedAtUtc
            : snapshot.ServerTimeUtc;
        vitals.Checkpoint(
            Math.Max(0m, snapshot.Player.Hp),
            Math.Max(0m, snapshot.Player.Resource),
            checkpointAt);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
