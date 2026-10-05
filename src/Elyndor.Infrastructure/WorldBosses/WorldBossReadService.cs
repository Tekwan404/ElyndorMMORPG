using System.Text.Json;
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
    string? MonsterId,
    string? ArtId,
    decimal CurrentHealth,
    decimal MaxHealth,
    int CurrentPhase,
    string PhaseName,
    DateTimeOffset SpawnedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    int Participants,
    decimal PersonalDamage,
    decimal PersonalHealing,
    decimal PersonalContribution,
    decimal PartyDamage,
    decimal PartyHealing,
    decimal PartyContribution,
    int EligibleParticipants,
    int? PersonalRewardRank,
    decimal RewardPercentile,
    int RewardChestCount,
    int RewardEnhancedChestCount,
    decimal MinimumContribution,
    bool RewardEligible,
    string? RewardTier,
    string? NextRewardTier,
    decimal? NextRewardTierAtDamage,
    decimal DamageToNextRewardTier,
    string ContentVersion,
    string BalanceVersion);

public sealed record WorldBossActiveReadResult(
    bool CharacterFound,
    WorldBossActiveSnapshot? Active);

public sealed record WorldBossPersonalLeaderboardEntry(
    int Rank,
    Guid CharacterId,
    string Name,
    decimal Damage,
    decimal Healing,
    decimal Contribution);

public sealed record WorldBossPartyLeaderboardEntry(
    int Rank,
    Guid PartyId,
    string LeaderName,
    decimal Damage,
    decimal Healing,
    decimal Contribution);

public sealed record WorldBossLeaderboardReadResult(
    bool CharacterFound,
    bool SpawnFound,
    Guid SpawnId,
    IReadOnlyList<WorldBossPersonalLeaderboardEntry> Players,
    IReadOnlyList<WorldBossPartyLeaderboardEntry> Parties,
    int? PersonalRank,
    decimal PersonalDamage,
    decimal PersonalHealing,
    decimal PersonalContribution,
    Guid? PartyId,
    int? PartyRank,
    decimal PartyDamage,
    decimal PartyHealing,
    decimal PartyContribution);

public sealed record WorldBossRewardSnapshot(
    Guid SpawnId,
    decimal Contribution,
    WorldBossRewardTier Tier,
    int Rank,
    int EligibleParticipants,
    decimal Percentile,
    int ChestCount,
    int EnhancedChestCount,
    int Experience,
    int BossGold,
    int ChestGold,
    int TotalGold,
    IReadOnlyList<WorldBossLootItemResult> Items,
    DateTimeOffset SettledAtUtc);

public sealed record WorldBossRewardReadResult(
    bool CharacterFound,
    bool SpawnFound,
    WorldBossRewardSnapshot? Reward);

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
        var personalContributionRow = await db.WorldBossContributions.AsNoTracking()
            .Where(contribution => contribution.SpawnId == spawn.Id
                && contribution.CharacterId == characterId.Value)
            .Select(contribution => new
            {
                contribution.Damage,
                contribution.Healing
            })
            .SingleOrDefaultAsync(cancellationToken);
        decimal personalDamage = personalContributionRow?.Damage ?? 0m;
        decimal personalHealing = personalContributionRow?.Healing ?? 0m;
        decimal personalContribution = checked(personalDamage + personalHealing);

        Guid? partyId = await db.PartyMembers.AsNoTracking()
            .Where(member => member.CharacterId == characterId.Value)
            .Select(member => (Guid?)member.PartyId)
            .SingleOrDefaultAsync(cancellationToken);
        var partyContributionRow = partyId is null
            ? null
            : await db.WorldBossPartyContributions.AsNoTracking()
                .Where(contribution => contribution.SpawnId == spawn.Id
                    && contribution.PartyId == partyId.Value)
                .Select(contribution => new
                {
                    contribution.Damage,
                    contribution.Healing
                })
                .SingleOrDefaultAsync(cancellationToken);
        decimal partyDamage = partyContributionRow?.Damage ?? 0m;
        decimal partyHealing = partyContributionRow?.Healing ?? 0m;
        decimal partyContribution = checked(partyDamage + partyHealing);

        string? monsterId = null;
        string? artId = null;
        if (definition is not null
            && content.Indexes.EncountersById.TryGetValue(
                definition.EncounterProfileId,
                out var encounter))
        {
            monsterId = encounter.MonsterId;
            if (content.Indexes.MonstersById.TryGetValue(
                    encounter.MonsterId,
                    out var monster))
            {
                artId = monster.ArtId;
            }
        }

        WorldBossPhaseDefinition? phase = definition?.Phases
            .SingleOrDefault(item => item.Phase == spawn.CurrentPhase);
        WorldBossRewardProgress rewardProgress = await ResolveRewardProgressAsync(
            spawn.Id,
            characterId.Value,
            content,
            definition,
            personalContribution,
            cancellationToken);

        return new WorldBossActiveReadResult(
            true,
            new WorldBossActiveSnapshot(
                spawn.Id,
                spawn.BossDefinitionId,
                definition?.Name ?? spawn.BossDefinitionId,
                definition?.Level ?? 0,
                monsterId,
                artId,
                spawn.CurrentHealth,
                spawn.MaxHealth,
                spawn.CurrentPhase,
                phase?.Name ?? $"Phase {spawn.CurrentPhase}",
                spawn.SpawnedAtUtc,
                spawn.ExpiresAtUtc,
                participants,
                personalDamage,
                personalHealing,
                personalContribution,
                partyDamage,
                partyHealing,
                partyContribution,
                rewardProgress.EligibleParticipants,
                rewardProgress.PersonalRank,
                rewardProgress.Percentile,
                rewardProgress.ChestCount,
                rewardProgress.EnhancedChestCount,
                rewardProgress.MinimumContribution,
                rewardProgress.Eligible,
                rewardProgress.CurrentTier,
                rewardProgress.NextTier,
                rewardProgress.NextTierAtDamage,
                rewardProgress.DamageToNextTier,
                spawn.ContentVersion,
                spawn.BalanceVersion));
    }

    public async Task<WorldBossRewardReadResult> GetRewardAsync(
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
            return new WorldBossRewardReadResult(false, false, null);

        bool spawnFound = await db.WorldBossSpawns.AsNoTracking()
            .AnyAsync(spawn => spawn.Id == spawnId, cancellationToken);
        if (!spawnFound)
            return new WorldBossRewardReadResult(true, false, null);

        WorldBossRewardSettlement? settlement = await db.WorldBossRewardSettlements
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.SpawnId == spawnId
                    && candidate.CharacterId == characterId.Value,
                cancellationToken);
        if (settlement is null)
            return new WorldBossRewardReadResult(true, true, null);

        WorldBossLootResult loot = JsonSerializer.Deserialize<WorldBossLootResult>(
            settlement.LootResultJson)
            ?? new WorldBossLootResult(settlement.Gold, 0, []);

        return new WorldBossRewardReadResult(
            true,
            true,
            new WorldBossRewardSnapshot(
                spawnId,
                settlement.ContributionScore,
                settlement.RewardTier,
                loot.Rank,
                loot.EligibleParticipants,
                loot.Percentile,
                loot.ChestCount,
                loot.EnhancedChestCount,
                settlement.Experience,
                loot.BossGold,
                loot.ChestGold,
                settlement.Gold,
                loot.Items,
                settlement.SettledAtUtc));
    }

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
                false, false, spawnId, [], [], null, 0, 0, 0, null, null, 0, 0, 0);
        }

        bool spawnExists = await db.WorldBossSpawns.AsNoTracking()
            .AnyAsync(spawn => spawn.Id == spawnId, cancellationToken);
        if (!spawnExists)
        {
            return new WorldBossLeaderboardReadResult(
                true, false, spawnId, [], [], null, 0, 0, 0, null, null, 0, 0, 0);
        }

        var playerRows = await (
            from contribution in db.WorldBossContributions.AsNoTracking()
            join character in db.Characters.AsNoTracking()
                on contribution.CharacterId equals character.Id
            where contribution.SpawnId == spawnId
            orderby contribution.Damage + contribution.Healing descending, contribution.CharacterId
            select new
            {
                contribution.CharacterId,
                character.Name,
                contribution.Damage,
                contribution.Healing,
                Contribution = contribution.Damage + contribution.Healing
            })
            .Take(50)
            .ToListAsync(cancellationToken);

        List<WorldBossPersonalLeaderboardEntry> players = [];
        for (int index = 0; index < playerRows.Count; index++)
        {
            var row = playerRows[index];
            players.Add(new(
                index + 1,
                row.CharacterId,
                row.Name,
                row.Damage,
                row.Healing,
                row.Contribution));
        }

        var personalContribution = await db.WorldBossContributions.AsNoTracking()
            .Where(contribution => contribution.SpawnId == spawnId
                && contribution.CharacterId == characterId.Value)
            .Select(contribution => new
            {
                contribution.CharacterId,
                contribution.Damage,
                contribution.Healing,
                Contribution = contribution.Damage + contribution.Healing
            })
            .SingleOrDefaultAsync(cancellationToken);
        int? personalRank = null;
        if (personalContribution is not null)
        {
            int higherScores = await db.WorldBossContributions.AsNoTracking()
                .CountAsync(
                    contribution => contribution.SpawnId == spawnId
                        && contribution.Damage + contribution.Healing
                            > personalContribution.Contribution,
                    cancellationToken);
            Guid[] tiedCharacterIds = await db.WorldBossContributions.AsNoTracking()
                .Where(contribution => contribution.SpawnId == spawnId
                    && contribution.Damage + contribution.Healing
                        == personalContribution.Contribution)
                .OrderBy(contribution => contribution.CharacterId)
                .Select(contribution => contribution.CharacterId)
                .ToArrayAsync(cancellationToken);
            int tieIndex = Array.IndexOf(tiedCharacterIds, personalContribution.CharacterId);
            personalRank = tieIndex < 0 ? null : higherScores + tieIndex + 1;
        }
        decimal? personalDamageValue = personalContribution?.Damage;
        decimal? personalHealingValue = personalContribution?.Healing;
        decimal? personalContributionValue = personalContribution?.Contribution;

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
            orderby contribution.Damage + contribution.Healing descending, contribution.PartyId
            select new
            {
                contribution.PartyId,
                LeaderName = leader.Name,
                contribution.Damage,
                contribution.Healing,
                Contribution = contribution.Damage + contribution.Healing
            })
            .Take(25)
            .ToListAsync(cancellationToken);

        List<WorldBossPartyLeaderboardEntry> parties = [];
        decimal? previousPartyContribution = null;
        var partyRank = 0;
        for (int index = 0; index < partyRows.Count; index++)
        {
            var row = partyRows[index];
            if (previousPartyContribution != row.Contribution)
                partyRank = index + 1;
            parties.Add(new(
                partyRank,
                row.PartyId,
                row.LeaderName,
                row.Damage,
                row.Healing,
                row.Contribution));
            previousPartyContribution = row.Contribution;
        }

        var currentPartyContribution = partyId is null
            ? null
            : await db.WorldBossPartyContributions.AsNoTracking()
                .Where(contribution => contribution.SpawnId == spawnId
                    && contribution.PartyId == partyId.Value)
                .Select(contribution => new
                {
                    contribution.Damage,
                    contribution.Healing,
                    Contribution = contribution.Damage + contribution.Healing
                })
                .SingleOrDefaultAsync(cancellationToken);
        decimal? partyDamageValue = currentPartyContribution?.Damage;
        decimal? partyHealingValue = currentPartyContribution?.Healing;
        decimal? partyContributionValue = currentPartyContribution?.Contribution;
        int? currentPartyRank = partyContributionValue is null
            ? null
            : 1 + await db.WorldBossPartyContributions.AsNoTracking()
                .CountAsync(
                    contribution => contribution.SpawnId == spawnId
                        && contribution.Damage + contribution.Healing > partyContributionValue.Value,
                    cancellationToken);

        return new WorldBossLeaderboardReadResult(
            true,
            true,
            spawnId,
            players,
            parties,
            personalRank,
            personalDamageValue ?? 0m,
            personalHealingValue ?? 0m,
            personalContributionValue ?? 0m,
            partyId,
            currentPartyRank,
            partyDamageValue ?? 0m,
            partyHealingValue ?? 0m,
            partyContributionValue ?? 0m);
    }

    private async Task<WorldBossRewardProgress> ResolveRewardProgressAsync(
        Guid spawnId,
        Guid characterId,
        GameContentSnapshot content,
        WorldBossDefinition? definition,
        decimal personalContribution,
        CancellationToken cancellationToken)
    {
        if (definition is null
            || !content.Indexes.WorldBossRewardProfilesById.TryGetValue(
                definition.RewardProfileId,
                out WorldBossRewardProfileDefinition? profile))
        {
            return new(false, null, null, null, 0m, 0, null, 0m, 0, 0, 0m);
        }

        if (profile.LeaderboardTiers is not { Count: > 0 })
        {
            WorldBossRewardTierThresholdDefinition[] orderedTiers = profile.Tiers
                .OrderBy(tier => tier.MinimumContribution)
                .ToArray();
            WorldBossRewardTierThresholdDefinition? current = orderedTiers
                .LastOrDefault(tier => personalContribution >= tier.MinimumContribution);
            WorldBossRewardTierThresholdDefinition? next = orderedTiers
                .FirstOrDefault(tier => personalContribution < tier.MinimumContribution);

            return new(
                personalContribution >= profile.MinimumContribution,
                current?.Tier.ToString(),
                next?.Tier.ToString(),
                next?.MinimumContribution,
                next is null
                    ? 0m
                    : Math.Max(0m, next.MinimumContribution - personalContribution),
                0,
                null,
                0m,
                0,
                0,
                profile.MinimumContribution);
        }

        int eligibleParticipants = await db.WorldBossContributions.AsNoTracking()
            .CountAsync(
                contribution => contribution.SpawnId == spawnId
                    && contribution.Damage + contribution.Healing >= profile.MinimumContribution,
                cancellationToken);
        if (personalContribution < profile.MinimumContribution
            || eligibleParticipants == 0)
        {
            return new(
                false,
                null,
                "Top50",
                null,
                0m,
                eligibleParticipants,
                null,
                0m,
                0,
                0,
                profile.MinimumContribution);
        }

        int higherScores = await db.WorldBossContributions.AsNoTracking()
            .CountAsync(
                contribution => contribution.SpawnId == spawnId
                    && contribution.Damage + contribution.Healing > personalContribution,
                cancellationToken);
        Guid[] tiedCharacterIds = await db.WorldBossContributions.AsNoTracking()
            .Where(contribution => contribution.SpawnId == spawnId
                && contribution.Damage + contribution.Healing == personalContribution)
            .OrderBy(contribution => contribution.CharacterId)
            .Select(contribution => contribution.CharacterId)
            .ToArrayAsync(cancellationToken);
        int tieIndex = Array.IndexOf(tiedCharacterIds, characterId);
        if (tieIndex < 0)
        {
            return new(
                false,
                null,
                "Top50",
                null,
                0m,
                eligibleParticipants,
                null,
                0m,
                0,
                0,
                profile.MinimumContribution);
        }

        int personalRank = higherScores + tieIndex + 1;
        WorldBossLeaderboardRewardResolution reward =
            WorldBossLeaderboardRewardPolicy.Resolve(
                profile,
                personalRank,
                eligibleParticipants);

        return new(
            true,
            reward.Tier.ToString(),
            null,
            null,
            0m,
            eligibleParticipants,
            reward.Rank,
            reward.Percentile,
            reward.ChestCount,
            reward.EnhancedChestCount,
            profile.MinimumContribution);
    }

    private sealed record WorldBossRewardProgress(
        bool Eligible,
        string? CurrentTier,
        string? NextTier,
        decimal? NextTierAtDamage,
        decimal DamageToNextTier,
        int EligibleParticipants,
        int? PersonalRank,
        decimal Percentile,
        int ChestCount,
        int EnhancedChestCount,
        decimal MinimumContribution);

}
