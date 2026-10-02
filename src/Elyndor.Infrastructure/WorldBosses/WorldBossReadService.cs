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

public sealed record WorldBossPersonalLeaderboardEntry(
    int Rank,
    Guid CharacterId,
    string Name,
    decimal Damage);

public sealed record WorldBossPartyLeaderboardEntry(
    int Rank,
    Guid PartyId,
    string LeaderName,
    decimal Damage);

public sealed record WorldBossLeaderboardReadResult(
    bool CharacterFound,
    bool SpawnFound,
    Guid SpawnId,
    IReadOnlyList<WorldBossPersonalLeaderboardEntry> Players,
    IReadOnlyList<WorldBossPartyLeaderboardEntry> Parties,
    int? PersonalRank,
    decimal PersonalDamage,
    Guid? PartyId,
    int? PartyRank,
    decimal PartyDamage);

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

    public async Task<WorldBossLeaderboardReadResult> GetLeaderboardAsync(
        Guid accountId,
        Guid spawnId,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account identifier cannot be empty.", nameof(accountId));
        if (spawnId == Guid.Empty)
            throw new ArgumentException("World boss spawn identifier cannot be empty.", nameof(spawnId));

        Guid? characterId = await db.Characters.AsNoTracking()
            .Where(character => character.AccountId == accountId)
            .Select(character => (Guid?)character.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (characterId is null)
        {
            return new WorldBossLeaderboardReadResult(
                false, false, spawnId, [], [], null, 0, null, null, 0);
        }

        bool spawnExists = await db.WorldBossSpawns.AsNoTracking()
            .AnyAsync(spawn => spawn.Id == spawnId, cancellationToken);
        if (!spawnExists)
        {
            return new WorldBossLeaderboardReadResult(
                true, false, spawnId, [], [], null, 0, null, null, 0);
        }

        var playerRows = await (
            from contribution in db.WorldBossContributions.AsNoTracking()
            join character in db.Characters.AsNoTracking()
                on contribution.CharacterId equals character.Id
            where contribution.SpawnId == spawnId
            orderby contribution.Damage descending, contribution.CharacterId
            select new
            {
                contribution.CharacterId,
                character.Name,
                contribution.Damage
            })
            .Take(50)
            .ToListAsync(cancellationToken);

        List<WorldBossPersonalLeaderboardEntry> players = [];
        decimal? previousPlayerDamage = null;
        var playerRank = 0;
        for (int index = 0; index < playerRows.Count; index++)
        {
            var row = playerRows[index];
            if (previousPlayerDamage != row.Damage)
                playerRank = index + 1;
            players.Add(new(
                playerRank,
                row.CharacterId,
                row.Name,
                row.Damage));
            previousPlayerDamage = row.Damage;
        }

        decimal? personalDamageValue = await db.WorldBossContributions.AsNoTracking()
            .Where(contribution => contribution.SpawnId == spawnId
                && contribution.CharacterId == characterId.Value)
            .Select(contribution => (decimal?)contribution.Damage)
            .SingleOrDefaultAsync(cancellationToken);
        int? personalRank = personalDamageValue is null
            ? null
            : 1 + await db.WorldBossContributions.AsNoTracking()
                .CountAsync(
                    contribution => contribution.SpawnId == spawnId
                        && contribution.Damage > personalDamageValue.Value,
                    cancellationToken);

        Guid? partyId = await db.PartyMembers.AsNoTracking()
            .Where(member => member.CharacterId == characterId.Value)
            .Select(member => (Guid?)member.PartyId)
            .SingleOrDefaultAsync(cancellationToken);

        var partyRows = await (
            from contribution in db.WorldBossPartyContributions.AsNoTracking()
            join party in db.Parties.AsNoTracking()
                on contribution.PartyId equals party.Id
            join leader in db.Characters.AsNoTracking()
                on party.LeaderCharacterId equals leader.Id
            where contribution.SpawnId == spawnId
            orderby contribution.Damage descending, contribution.PartyId
            select new
            {
                contribution.PartyId,
                LeaderName = leader.Name,
                contribution.Damage
            })
            .Take(25)
            .ToListAsync(cancellationToken);

        List<WorldBossPartyLeaderboardEntry> parties = [];
        decimal? previousPartyDamage = null;
        var partyRank = 0;
        for (int index = 0; index < partyRows.Count; index++)
        {
            var row = partyRows[index];
            if (previousPartyDamage != row.Damage)
                partyRank = index + 1;
            parties.Add(new(
                partyRank,
                row.PartyId,
                row.LeaderName,
                row.Damage));
            previousPartyDamage = row.Damage;
        }

        decimal? partyDamageValue = partyId is null
            ? null
            : await db.WorldBossPartyContributions.AsNoTracking()
                .Where(contribution => contribution.SpawnId == spawnId
                    && contribution.PartyId == partyId.Value)
                .Select(contribution => (decimal?)contribution.Damage)
                .SingleOrDefaultAsync(cancellationToken);
        int? currentPartyRank = partyDamageValue is null
            ? null
            : 1 + await db.WorldBossPartyContributions.AsNoTracking()
                .CountAsync(
                    contribution => contribution.SpawnId == spawnId
                        && contribution.Damage > partyDamageValue.Value,
                    cancellationToken);

        return new WorldBossLeaderboardReadResult(
            true,
            true,
            spawnId,
            players,
            parties,
            personalRank,
            personalDamageValue ?? 0m,
            partyId,
            currentPartyRank,
            partyDamageValue ?? 0m);
    }
    }
}
