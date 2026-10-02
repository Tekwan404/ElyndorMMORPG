using Elyndor.Core.Content;
using Elyndor.Core.WorldBosses;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.WorldBosses;

public sealed record WorldBossActiveSnapshot(
    Guid SpawnId,
    string BossDefinitionId,
    string Name,
    int Level,
    decimal CurrentHealth,
    decimal MaxHealth,
    int CurrentPhase,
    string PhaseName,
    DateTimeOffset SpawnedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    int Participants,
    decimal PersonalDamage,
    decimal PartyDamage,
    string ContentVersion,
    string BalanceVersion);

public sealed record WorldBossActiveReadResult(
    bool CharacterFound,
    WorldBossActiveSnapshot? Active);

public sealed class WorldBossReadService(
    GameDbContext db,
    IContentSnapshotProvider contentProvider,
    TimeProvider time)
{
    public async Task<WorldBossActiveReadResult> GetActiveAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account identifier cannot be empty.", nameof(accountId));

        Guid? characterId = await db.Characters.AsNoTracking()
            .Where(character => character.AccountId == accountId)
            .Select(character => (Guid?)character.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (characterId is null)
            return new WorldBossActiveReadResult(false, null);

        DateTimeOffset now = time.GetUtcNow();
        WorldBossSpawn? spawn = await db.WorldBossSpawns.AsNoTracking()
            .SingleOrDefaultAsync(candidate =>
                candidate.Status == WorldBossSpawnStatus.Active
                && candidate.ExpiresAtUtc > now,
                cancellationToken);
        if (spawn is null)
            return new WorldBossActiveReadResult(true, null);

        GameContentSnapshot content = contentProvider.GetCurrent();
        content.Indexes.WorldBossesById.TryGetValue(
            spawn.BossDefinitionId,
            out WorldBossDefinition? definition);

        int participants = await db.WorldBossContributions.AsNoTracking()
            .CountAsync(contribution => contribution.SpawnId == spawn.Id, cancellationToken);
        decimal personalDamage = await db.WorldBossContributions.AsNoTracking()
            .Where(contribution => contribution.SpawnId == spawn.Id
                && contribution.CharacterId == characterId.Value)
            .Select(contribution => (decimal?)contribution.Damage)
            .SingleOrDefaultAsync(cancellationToken) ?? 0m;

        Guid? partyId = await db.PartyMembers.AsNoTracking()
            .Where(member => member.CharacterId == characterId.Value)
            .Select(member => (Guid?)member.PartyId)
            .SingleOrDefaultAsync(cancellationToken);
        decimal partyDamage = partyId is null
            ? 0m
            : await db.WorldBossPartyContributions.AsNoTracking()
                .Where(contribution => contribution.SpawnId == spawn.Id
                    && contribution.PartyId == partyId.Value)
                .Select(contribution => (decimal?)contribution.Damage)
                .SingleOrDefaultAsync(cancellationToken) ?? 0m;

        WorldBossPhaseDefinition? phase = definition?.Phases
            .SingleOrDefault(item => item.Phase == spawn.CurrentPhase);

        return new WorldBossActiveReadResult(
            true,
            new WorldBossActiveSnapshot(
                spawn.Id,
                spawn.BossDefinitionId,
                definition?.Name ?? spawn.BossDefinitionId,
                definition?.Level ?? 0,
                spawn.CurrentHealth,
                spawn.MaxHealth,
                spawn.CurrentPhase,
                phase?.Name ?? $"Phase {spawn.CurrentPhase}",
                spawn.SpawnedAtUtc,
                spawn.ExpiresAtUtc,
                participants,
                personalDamage,
                partyDamage,
                spawn.ContentVersion,
                spawn.BalanceVersion));
    }
}
