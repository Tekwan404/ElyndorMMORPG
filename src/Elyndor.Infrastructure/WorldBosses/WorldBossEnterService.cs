using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Parties;
using Elyndor.Core.WorldBosses;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Pvp;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.WorldBosses;

public static class WorldBossEnterErrorCodes
{
    public const string CharacterNotFound = "character_not_found";
    public const string SpawnNotFound = "world_boss_spawn_not_found";
    public const string NotActive = "world_boss_not_active";
    public const string Expired = "world_boss_expired";
    public const string EncounterNotConfigured = "world_boss_encounter_not_configured";
    public const string ContentVersionMismatch = "world_boss_content_version_mismatch";
}

public sealed record WorldBossEnterResult(
    bool Succeeded,
    string? ErrorCode,
    CombatOperationResult? Combat)
{
    public static WorldBossEnterResult Failure(string errorCode) =>
        new(false, errorCode, null);

    public static WorldBossEnterResult Success(CombatOperationResult combat) =>
        new(true, null, combat);
}

public sealed class WorldBossEnterService(
    GameDbContext db,
    IContentSnapshotProvider contentProvider,
    CombatSessionFactory factory,
    CombatSessionRegistry registry,
    CombatDurabilityService durability,
    CharacterOperationGuard operationGuard,
    PartyService partyService,
    ArenaMatchRuntime arenaRuntime,
    TimeProvider time)
{
    public async Task<WorldBossEnterResult> EnterAsync(
        Guid accountId,
        Guid spawnId,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account identifier cannot be empty.", nameof(accountId));
        if (spawnId == Guid.Empty)
            throw new ArgumentException("World boss spawn identifier cannot be empty.", nameof(spawnId));

        registry.ClearFinished(accountId);
        if (registry.Resume(accountId).Succeeded || arenaRuntime.IsInMatch(accountId))
            return WorldBossEnterResult.Failure(CombatErrorCodes.AlreadyActive);

        Guid? characterId = await db.Characters.AsNoTracking()
            .Where(character => character.AccountId == accountId)
            .Select(character => (Guid?)character.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (characterId is null)
            return WorldBossEnterResult.Failure(WorldBossEnterErrorCodes.CharacterNotFound);

        string? locationId = await db.CharacterLocations.AsNoTracking()
            .Where(location => location.CharacterId == characterId.Value)
            .Select(location => location.LocationId)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(locationId))
            return WorldBossEnterResult.Failure(CombatErrorCodes.InvalidLocation);

        WorldBossSpawn? spawn = await db.WorldBossSpawns.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == spawnId, cancellationToken);
        if (spawn is null)
            return WorldBossEnterResult.Failure(WorldBossEnterErrorCodes.SpawnNotFound);

        DateTimeOffset now = time.GetUtcNow();
        if (spawn.Status == WorldBossSpawnStatus.Active && now >= spawn.ExpiresAtUtc)
            return WorldBossEnterResult.Failure(WorldBossEnterErrorCodes.Expired);
        if (spawn.Status != WorldBossSpawnStatus.Active || spawn.CurrentHealth <= 0)
            return WorldBossEnterResult.Failure(WorldBossEnterErrorCodes.NotActive);

        GameContentSnapshot content = contentProvider.GetCurrent();
        if (!string.Equals(spawn.ContentVersion, content.ContentVersion, StringComparison.Ordinal)
            || !string.Equals(spawn.BalanceVersion, content.BalanceVersion, StringComparison.Ordinal))
        {
            return WorldBossEnterResult.Failure(
                WorldBossEnterErrorCodes.ContentVersionMismatch);
        }

        if (!content.Indexes.WorldBossesById.TryGetValue(
                spawn.BossDefinitionId,
                out WorldBossDefinition? definition)
            || !content.Indexes.EncountersById.TryGetValue(
                definition.EncounterProfileId,
                out var encounter)
            || !content.Indexes.MonstersById.ContainsKey(encounter.MonsterId))
        {
            return WorldBossEnterResult.Failure(
                WorldBossEnterErrorCodes.EncounterNotConfigured);
        }

        CombatSessionCreationResult created = await factory.CreateAsync(
            accountId,
            encounter.MonsterId,
            locationId,
            cancellationToken,
            partyMembersOverride: null,
            allowUnlistedEncounter: true,
            enemyMaxHealthOverride: spawn.MaxHealth,
            enemyCurrentHealthOverride: spawn.CurrentHealth);
        if (!created.Succeeded || created.Session is null)
            return WorldBossEnterResult.Failure(
                created.ErrorCode ?? CombatErrorCodes.CommandRejected);

        CombatSessionParticipant[] participants = created.Participants?.ToArray()
            ?? [new CombatSessionParticipant(accountId, created.CharacterId)];

        using IDisposable admissionLease = await operationGuard.AcquireManyAsync(
            participants.Select(participant => participant.AccountId),
            cancellationToken);

        if (!await IsPartyRosterCurrentAsync(
                accountId,
                participants,
                cancellationToken))
        {
            return WorldBossEnterResult.Failure(CombatErrorCodes.CommandRejected);
        }

        foreach (CombatSessionParticipant participant in participants)
            registry.ClearFinished(participant.AccountId);

        if (participants.Any(participant =>
                arenaRuntime.IsInMatch(participant.AccountId)
                || registry.HasActiveCombat(participant.AccountId)))
        {
            return WorldBossEnterResult.Failure(CombatErrorCodes.AlreadyActive);
        }

        HashSet<Guid> initiallyAttachedCharacterIds = created.Session
            .Snapshot()
            .ParticipantRoster?
            .Where(participant => participant.Status == CombatParticipantStatus.Active)
            .Select(participant => participant.CharacterId)
            .ToHashSet()
            ?? [created.CharacterId];

        foreach (CombatSessionParticipant participant in participants
                     .Where(participant =>
                         initiallyAttachedCharacterIds.Contains(participant.CharacterId)))
        {
            if (await durability.BeginAsync(
                    participant.CharacterId,
                    created.Session.Snapshot(participant.CharacterId),
                    cancellationToken))
            {
                continue;
            }

            await durability.CompleteAsync(created.Session.SessionId, CancellationToken.None);
            return WorldBossEnterResult.Failure(CombatErrorCodes.AlreadyActive);
        }

        bool bindingCreated = false;
        try
        {
            WorldBossBindingResult binding = await BindIfActiveAsync(
                spawnId,
                created.Session.SessionId,
                created.Session.Snapshot().Enemy.ActorId,
                cancellationToken);
            if (!binding.Succeeded)
            {
                await durability.CompleteAsync(
                    created.Session.SessionId,
                    CancellationToken.None);
                return WorldBossEnterResult.Failure(binding.ErrorCode!);
            }

            bindingCreated = true;
            created.Session.SynchronizePrimaryEnemyHealthBeforeRegistration(
                binding.CurrentHealth);
            created.Session.EnableExternalSynchronization();

            CombatParticipantBinding[] additionalParticipants = participants
                .Where(participant => participant.CharacterId != created.CharacterId)
                .Select(participant => new CombatParticipantBinding(
                    participant.AccountId,
                    participant.CharacterId))
                .ToArray();

            if (!registry.TryAdd(
                    accountId,
                    created.CharacterId,
                    created.Session,
                    created.ContentSnapshot,
                    additionalParticipants,
                    created.LocationId ?? locationId))
            {
                await CleanupBindingAsync(created.Session.SessionId);
                bindingCreated = false;
                await durability.CompleteAsync(
                    created.Session.SessionId,
                    CancellationToken.None);
                return WorldBossEnterResult.Failure(CombatErrorCodes.AlreadyActive);
            }

            CombatOperationResult combat = CombatOperationResult.FromSnapshot(
                created.Session.Snapshot(),
                created.ContentSnapshot) with
            {
                Events = created.Session.GetEventsAfter(0)
            };
            return WorldBossEnterResult.Success(combat);
        }
        catch
        {
            if (bindingCreated)
                await CleanupBindingAsync(created.Session.SessionId);
            await durability.CompleteAsync(
                created.Session.SessionId,
                CancellationToken.None);
            throw;
        }
    }

    private async Task<WorldBossBindingResult> BindIfActiveAsync(
        Guid spawnId,
        Guid sessionId,
        Guid bossActorId,
        CancellationToken cancellationToken) =>
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction =
                await db.Database.BeginTransactionAsync(cancellationToken);

            WorldBossSpawn? spawn = await db.WorldBossSpawns
                .FromSqlInterpolated(
                    $"SELECT * FROM game.world_boss_spawns WHERE \"Id\" = {spawnId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (spawn is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return WorldBossBindingResult.Failure(
                    WorldBossEnterErrorCodes.SpawnNotFound);
            }

            DateTimeOffset now = time.GetUtcNow();
            if (spawn.Status == WorldBossSpawnStatus.Active && now >= spawn.ExpiresAtUtc)
            {
                spawn.TryExpire(now);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(CancellationToken.None);
                return WorldBossBindingResult.Failure(
                    WorldBossEnterErrorCodes.Expired);
            }

            if (spawn.Status != WorldBossSpawnStatus.Active || spawn.CurrentHealth <= 0)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return WorldBossBindingResult.Failure(
                    WorldBossEnterErrorCodes.NotActive);
            }

            db.WorldBossCombatSessions.Add(new WorldBossCombatSessionBinding(
                sessionId,
                spawnId,
                bossActorId,
                partyId: null,
                boundAtUtc: now));
            await db.SaveChangesAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await transaction.CommitAsync(CancellationToken.None);
            return WorldBossBindingResult.Success(spawn.CurrentHealth);
        });

    private async Task CleanupBindingAsync(Guid sessionId)
    {
        db.ChangeTracker.Clear();
        await db.WorldBossCombatSessions
            .Where(binding => binding.CombatSessionId == sessionId)
            .ExecuteDeleteAsync(CancellationToken.None);
    }

    private async Task<bool> IsPartyRosterCurrentAsync(
        Guid accountId,
        CombatSessionParticipant[] participants,
        CancellationToken cancellationToken)
    {
        PartyCombatMember[] current = (await partyService.GetCombatMembersAsync(
                accountId,
                cancellationToken))
            .ToArray();
        if (current.Length != participants.Length)
            return false;

        (Guid AccountId, Guid CharacterId)[] expected = participants
            .Select(participant => (participant.AccountId, participant.CharacterId))
            .OrderBy(participant => participant.AccountId)
            .ThenBy(participant => participant.CharacterId)
            .ToArray();
        (Guid AccountId, Guid CharacterId)[] actual = current
            .Select(participant => (participant.AccountId, participant.CharacterId))
            .OrderBy(participant => participant.AccountId)
            .ThenBy(participant => participant.CharacterId)
            .ToArray();
        return expected.SequenceEqual(actual);
    }

    private sealed record WorldBossBindingResult(
        bool Succeeded,
        string? ErrorCode,
        decimal CurrentHealth)
    {
        public static WorldBossBindingResult Failure(string errorCode) =>
            new(false, errorCode, 0);

        public static WorldBossBindingResult Success(decimal currentHealth) =>
            new(true, null, currentHealth);
    }
}
