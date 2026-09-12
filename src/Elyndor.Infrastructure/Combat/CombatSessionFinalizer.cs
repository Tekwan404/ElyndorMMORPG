using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Infrastructure.Characters;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Progression;
using Elyndor.Infrastructure.Dungeons;
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
        bool fled = snapshot.ParticipantRoster?.Any(participant =>
            participant.CharacterId == characterId
            && participant.Status == Elyndor.Core.Combat.Participants.CombatParticipantStatus.Fled) == true;
        if (snapshot.Status == CombatSessionStatus.Active && !fled)
            return null;

        // Training is a sandbox over the real combat runtime. It must never mutate durable
        // vitals, location, progression, currency or loot state. Check the authoritative enemy
        // collection instead of the compatibility-selected Enemy projection.
        IEnumerable<CombatActorSnapshot> enemies =
            snapshot.Enemies ?? [snapshot.Enemy];
        if (enemies.Any(enemy => string.Equals(
                enemy.DefinitionId,
                CombatSessionFactory.TrainingDummyId,
                StringComparison.Ordinal)))
        {
            return null;
        }

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        GameDbContext dbContext = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        CombatDurabilityService? durability =
            scope.ServiceProvider.GetService<CombatDurabilityService>();
        if (durability is not null && snapshot.Status != CombatSessionStatus.Active)
        {
            await durability.RecordTerminalSnapshotAsync(
                snapshot.SessionId,
                snapshot,
                cancellationToken);
        }

        DungeonService? dungeonService = scope.ServiceProvider.GetService<DungeonService>();

        if (snapshot.Status == CombatSessionStatus.Victory
            && snapshot.PlayerContributionEligible == false)
        {
            if (durability is not null)
            {
                await durability.CompleteParticipantAsync(
                    snapshot.SessionId,
                    characterId,
                    cancellationToken);
            }

            if (dungeonService is not null)
            {
                await dungeonService.HandleCombatFinishedAsync(
                    snapshot,
                    cancellationToken);
            }

            return null;
        }

        CharacterAbilityCooldownStore cooldownStore =
            scope.ServiceProvider.GetRequiredService<CharacterAbilityCooldownStore>();
        await cooldownStore.ReplaceAsync(
            characterId,
            snapshot.Player.Cooldowns,
            snapshot.ServerTimeUtc,
            cancellationToken);

        // A terminal victory may be observed again after reconnect/retry. Rewards are already
        // idempotent by CombatSessionId, but replaying the pre-reward combat vitals here would
        // overwrite authoritative post-reward state (for example a level-up full heal).
        if (snapshot.Status == CombatSessionStatus.Victory && !fled)
        {
            var existingReward = await dbContext.CombatRewardGrants
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    grant => grant.CombatSessionId == snapshot.SessionId
                        && grant.CharacterId == characterId,
                    cancellationToken);
            if (existingReward is not null)
            {
                if (durability is not null)
                {
                    await durability.CompleteParticipantAsync(
                        snapshot.SessionId,
                        characterId,
                        cancellationToken);
                }
                if (dungeonService is not null)
                {
                    await dungeonService.HandleCombatFinishedAsync(
                        snapshot,
                        cancellationToken);
                }
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

            if ((snapshot.Status == CombatSessionStatus.Defeat || snapshot.Player.Hp <= 0) && character is not null
                && !fled)
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

                string? dungeonId = await dbContext.DungeonEncounters
                    .Where(encounter => encounter.CombatSessionId == snapshot.SessionId)
                    .Select(encounter => encounter.Run!.DungeonId)
                    .SingleOrDefaultAsync(cancellationToken);

                if (location is not null)
                {
                    string respawnLocation = dungeonId is null
                        ? WorldLocationIds.StarterTown
                        : dungeonService?.GetDefinition(dungeonId)?.EntryLocationId ?? location.LocationId;
                    DateTimeOffset relocateAt = checkpointAt < location.UpdatedAtUtc
                        ? location.UpdatedAtUtc
                        : checkpointAt;
                    if (!string.Equals(location.LocationId, respawnLocation, StringComparison.Ordinal))
                        location.Relocate(respawnLocation, relocateAt);
                    checkpointAt = relocateAt;
                }

                // Ordinary defeats use the class respawn resource value. Dungeon wipes recover
                // at the entrance with half of the effective maximum resource so a retry is
                // immediately possible without granting a full-resource reset.
                decimal respawnResource = dungeonId is null
                    ? derived.EffectiveResourceProfile.RespawnValue
                    : derived.EffectiveResourceProfile.MaxValue / 2m;
                vitals.BeginContext(
                    derived.Stats.MaxHp,
                    respawnResource,
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

        CombatRewardApplicationResult? reward = null;
        if (snapshot.Status == CombatSessionStatus.Victory && !fled)
        {
            CombatRewardService rewards =
                scope.ServiceProvider.GetRequiredService<CombatRewardService>();
            reward = contentSnapshot is null
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

        if (durability is not null)
        {
            await durability.CompleteParticipantAsync(
                snapshot.SessionId,
                characterId,
                cancellationToken);
        }
        if (dungeonService is not null && snapshot.Status != CombatSessionStatus.Active)
        {
            await dungeonService.HandleCombatFinishedAsync(
                snapshot,
                cancellationToken);
        }
        return reward;
    }
}