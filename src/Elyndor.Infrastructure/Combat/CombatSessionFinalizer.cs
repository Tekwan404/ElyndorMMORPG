using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Infrastructure.Characters;
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

    Task<CombatRewardApplicationResult?> FinalizeAsync(
        Guid characterId,
        CombatSessionSnapshot snapshot,
        GameContentSnapshot? contentSnapshot,
        CancellationToken cancellationToken) =>
        FinalizeAsync(characterId, snapshot, cancellationToken);
}

/// <summary>
/// Bridges in-memory combat runtime to permanent character state exactly once when a
/// CombatSession reaches a terminal state. Permanent progression stays outside CombatSession.
/// </summary>
public sealed class CombatSessionFinalizer(IServiceScopeFactory scopeFactory) : ICombatSessionFinalizer
{
    private const string StarterTownId = "STARTER_TOWN";

    public Task<CombatRewardApplicationResult?> FinalizeAsync(
        Guid characterId,
        CombatSessionSnapshot snapshot,
        CancellationToken cancellationToken) =>
        FinalizeAsync(characterId, snapshot, null, cancellationToken);

    public async Task<CombatRewardApplicationResult?> FinalizeAsync(
        Guid characterId,
        CombatSessionSnapshot snapshot,
        GameContentSnapshot? contentSnapshot,
        CancellationToken cancellationToken)
    {
        if (snapshot.Status == CombatSessionStatus.Active)
            return null;

        // Training is a sandbox over the real combat runtime. It must never mutate durable
        // vitals, location, progression, currency or loot state.
        if (string.Equals(
                snapshot.Enemy.DefinitionId,
                CombatSessionFactory.TrainingDummyId,
                StringComparison.Ordinal))
            return null;

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        GameDbContext dbContext = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        // A terminal victory may be observed again after reconnect/retry. Rewards are already
        // idempotent by CombatSessionId, but replaying the pre-reward combat vitals here would
        // overwrite authoritative post-reward state (for example a level-up full heal).
        if (snapshot.Status == CombatSessionStatus.Victory)
        {
            var existingReward = await dbContext.CombatRewardGrants
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    grant => grant.CombatSessionId == snapshot.SessionId,
                    cancellationToken);
            if (existingReward is not null)
            {
                return new CombatRewardApplicationResult(
                    false,
                    existingReward.XpEarned,
                    existingReward.GoldEarned,
                    null,
                    []);
            }
        }

        CharacterDerivedStateService derivedStateService =
            scope.ServiceProvider.GetRequiredService<CharacterDerivedStateService>();
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
                CharacterDerivedState derived = contentSnapshot is null
                    ? await derivedStateService.ResolveAsync(
                        character.Id,
                        character.ClassId,
                        character.Level,
                        cancellationToken)
                    : await derivedStateService.ResolveAsync(
                        character.Id,
                        character.ClassId,
                        character.Level,
                        contentSnapshot,
                        cancellationToken);

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
                    derived.Stats.MaxHp,
                    derived.EffectiveResourceProfile.RespawnValue,
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
            return contentSnapshot is null
                ? await rewards.ApplyVictoryAsync(
                    characterId,
                    snapshot,
                    cancellationToken)
                : await rewards.ApplyVictoryAsync(
                    characterId,
                    snapshot,
                    contentSnapshot,
                    cancellationToken);
        }

        return null;
    }
}
