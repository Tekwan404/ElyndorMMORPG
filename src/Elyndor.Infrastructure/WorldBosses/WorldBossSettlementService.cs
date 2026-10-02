using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Progression;
using Elyndor.Core.WorldBosses;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Elyndor.Infrastructure.WorldBosses;

public static class WorldBossSettlementErrorCodes
{
    public const string SpawnNotFound = "world_boss_spawn_not_found";
    public const string NotDefeated = "world_boss_not_defeated";
    public const string RewardContentMissing = "world_boss_reward_content_missing";
}

public sealed record WorldBossSettlementCharacterResult(
    Guid CharacterId,
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
    IReadOnlyList<WorldBossLootItemResult> Items,
    DateTimeOffset SettledAtUtc);

public sealed record WorldBossSettlementBatchResult(
    bool Succeeded,
    string? ErrorCode,
    Guid SpawnId,
    bool WasReplay,
    IReadOnlyList<WorldBossSettlementCharacterResult> Rewards)
{
    public static WorldBossSettlementBatchResult Failure(Guid spawnId, string errorCode) =>
        new(false, errorCode, spawnId, false, []);
}

public sealed class WorldBossSettlementService(
    GameDbContext db,
    IContentSnapshotProvider contentProvider,
    CharacterDerivedStateService derivedStateService,
    TimeProvider time,
    IWorldBossUpdatePublisher? updatePublisher = null,
    ILogger<WorldBossSettlementService>? logger = null)
{
    private static readonly Action<ILogger, Guid, Exception?> RealtimeSettlementDeliveryFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(4202, nameof(RealtimeSettlementDeliveryFailed)),
            "World boss {SpawnId} settled, but realtime reward delivery failed.");

    private static readonly Action<ILogger, Guid, int, int, Exception?> SettlementCompleted =
        LoggerMessage.Define<Guid, int, int>(
            LogLevel.Information,
            new EventId(4204, nameof(SettlementCompleted)),
            "World boss settlement completed: spawn {SpawnId}, rewards {RewardCount}, "
            + "eligibleParticipants {EligibleParticipants}.");

    private static readonly Action<ILogger, Guid, Guid, int, decimal, WorldBossRewardTier, string, Exception?>
        CharacterRewardSettled =
            LoggerMessage.Define<Guid, Guid, int, decimal, WorldBossRewardTier, string>(
                LogLevel.Information,
                new EventId(4205, nameof(CharacterRewardSettled)),
                "World boss reward settled: spawn {SpawnId}, character {CharacterId}, "
                + "rank {Rank}, percentile {Percentile}, tier {Tier}, {Details}.");

    public Task<WorldBossSettlementBatchResult> SettleAsync(
        Guid spawnId,
        CancellationToken cancellationToken)
    {
        if (spawnId == Guid.Empty)
            throw new ArgumentException("World boss spawn identifier cannot be empty.", nameof(spawnId));

        return db.Database.CreateExecutionStrategy().ExecuteAsync(
            () => SettleCoreAsync(spawnId, cancellationToken));
    }

    private async Task<WorldBossSettlementBatchResult> SettleCoreAsync(
        Guid spawnId,
        CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        await using IDbContextTransaction transaction =
            await db.Database.BeginTransactionAsync(cancellationToken);

        WorldBossSpawn? spawn = await db.WorldBossSpawns
            .FromSqlInterpolated(
                $"SELECT * FROM game.world_boss_spawns WHERE \"Id\" = {spawnId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (spawn is null)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return WorldBossSettlementBatchResult.Failure(
                spawnId,
                WorldBossSettlementErrorCodes.SpawnNotFound);
        }

        if (spawn.Status == WorldBossSpawnStatus.Settled)
        {
            WorldBossSettlementCharacterResult[] replay =
                await LoadExistingResultsAsync(spawnId, cancellationToken);
            await transaction.CommitAsync(CancellationToken.None);
            return new(
                true,
                null,
                spawnId,
                true,
                replay);
        }

        if (spawn.Status is not (WorldBossSpawnStatus.Defeated or WorldBossSpawnStatus.Settling))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return WorldBossSettlementBatchResult.Failure(
                spawnId,
                WorldBossSettlementErrorCodes.NotDefeated);
        }

        GameContentSnapshot content = contentProvider.GetCurrent();
        if (!content.Indexes.WorldBossesById.TryGetValue(
                spawn.BossDefinitionId,
                out WorldBossDefinition? definition)
            || !content.Indexes.WorldBossRewardProfilesById.TryGetValue(
                definition.RewardProfileId,
                out WorldBossRewardProfileDefinition? rewardProfile)
            || !content.Indexes.LootTablesById.TryGetValue(
                definition.LootTableId,
                out LootTableDefinition? chestTable)
            || content.Package.LevelProgression is null)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return WorldBossSettlementBatchResult.Failure(
                spawnId,
                WorldBossSettlementErrorCodes.RewardContentMissing);
        }

        if (spawn.Status == WorldBossSpawnStatus.Defeated)
            _ = spawn.TryBeginSettlement();

        DateTimeOffset now = time.GetUtcNow();
        WorldBossContribution[] contributions = await db.WorldBossContributions
            .AsNoTracking()
            .Where(contribution => contribution.SpawnId == spawnId)
            .OrderBy(contribution => contribution.CharacterId)
            .ToArrayAsync(cancellationToken);
        Guid[] participantCharacterIds = contributions
            .Select(contribution => contribution.CharacterId)
            .Distinct()
            .ToArray();
        Dictionary<Guid, Guid> accountByCharacterId = participantCharacterIds.Length == 0
            ? []
            : await db.Characters
                .AsNoTracking()
                .Where(character => participantCharacterIds.Contains(character.Id))
                .ToDictionaryAsync(
                    character => character.Id,
                    character => character.AccountId,
                    cancellationToken);
        WorldBossContribution[] eligible = contributions
            .Where(contribution => contribution.Damage >= rewardProfile.MinimumContribution)
            .OrderByDescending(contribution => contribution.Damage)
            .ThenBy(contribution => contribution.CharacterId)
            .ToArray();

        Dictionary<Guid, WorldBossRewardSettlement> existing =
            await db.WorldBossRewardSettlements
                .AsNoTracking()
                .Where(settlement => settlement.SpawnId == spawnId)
                .ToDictionaryAsync(
                    settlement => settlement.CharacterId,
                    cancellationToken);

        List<WorldBossSettlementCharacterResult> rewards = [];
        for (var eligibleIndex = 0; eligibleIndex < eligible.Length; eligibleIndex++)
        {
            WorldBossContribution contribution = eligible[eligibleIndex];
            if (existing.TryGetValue(
                    contribution.CharacterId,
                    out WorldBossRewardSettlement? alreadySettled))
            {
                rewards.Add(ToResult(alreadySettled));
                continue;
            }

            Character character = await db.Characters
                .FromSqlInterpolated(
                    $"SELECT * FROM game.characters WHERE \"Id\" = {contribution.CharacterId} FOR UPDATE")
                .SingleAsync(cancellationToken);

            WorldBossLeaderboardRewardResolution leaderboardReward =
                ResolveLeaderboardReward(
                    contribution.Damage,
                    eligibleIndex + 1,
                    eligible.Length,
                    rewardProfile);
            WorldBossRewardTier tier = leaderboardReward.Tier;
            Guid lootSeed = CreateLootSeed(spawnId, character.Id);
            var random = new SeededGameRandom(SeedToInt32(lootSeed));

            int totalChestCount = checked(
                leaderboardReward.ChestCount + leaderboardReward.EnhancedChestCount);
            int chestGold = 0;
            List<LootRoll> chestLoot = [];
            LootTableDefinition rewardChestTable = chestTable;
            if (totalChestCount > 0
                && !string.IsNullOrWhiteSpace(leaderboardReward.LootTableId))
            {
                if (!content.Indexes.LootTablesById.TryGetValue(
                        leaderboardReward.LootTableId,
                        out LootTableDefinition? configuredChestTable)
                    || configuredChestTable is null)
                {
                    throw new InvalidOperationException(
                        $"World boss reward chest '{leaderboardReward.LootTableId}' is missing from content.");
                }

                rewardChestTable = configuredChestTable;
            }

            for (var chestIndex = 0; chestIndex < totalChestCount; chestIndex++)
            {
                chestGold = checked(chestGold + RollInclusive(
                    rewardProfile.ChestGoldMin,
                    rewardProfile.ChestGoldMax,
                    random));
                LootRoll[] chestRolls = LootRoller.Roll(rewardChestTable, random)
                    .Select(roll => roll with { SourceQualityProfileId = "BOSS" })
                    .ToArray();
                if (chestRolls.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"World boss chest '{rewardChestTable.Id}' did not produce an item.");
                }

                chestLoot.AddRange(chestRolls);
            }

            int totalGold = checked(rewardProfile.BossGold + chestGold);

            CharacterProgressionResult progression =
                CharacterProgression.GrantExperience(
                    character,
                    rewardProfile.BossExperience,
                    content.Package.LevelProgression);
            character.AddGold(totalGold);

            if (progression.LeveledUp)
            {
                CharacterVitals vitals = await db.CharacterVitals.SingleAsync(
                    candidate => candidate.CharacterId == character.Id,
                    cancellationToken);
                CharacterDerivedState derived = await derivedStateService.ResolveAsync(
                    character.Id,
                    character.ClassId,
                    character.Level,
                    content,
                    cancellationToken);
                vitals.Checkpoint(
                    derived.Stats.MaxHp,
                    Math.Min(
                        vitals.CurrentResource,
                        derived.EffectiveResourceProfile.MaxValue),
                    now);
            }

            List<WorldBossLootItemResult> itemResults = [];
            for (var ordinal = 0; ordinal < chestLoot.Count; ordinal++)
            {
                LootRoll roll = chestLoot[ordinal];
                WorldBossGrantedItemSnapshot granted = await GrantItemAsync(
                    character.Id,
                    lootSeed,
                    roll,
                    ordinal,
                    now,
                    content,
                    cancellationToken);

                ItemDefinition item = content.Indexes.ItemsById[roll.ItemId];
                itemResults.Add(new(
                    item.Id,
                    granted.DisplayName,
                    item.Rarity,
                    roll.Quantity,
                    item.IconId,
                    granted.InstanceId,
                    granted.Pending,
                    granted.Stats,
                    granted.GeneratedItem));
            }

            WorldBossLootResult lootResult = new(
                rewardProfile.BossGold,
                chestGold,
                itemResults,
                leaderboardReward.Rank,
                leaderboardReward.EligibleParticipants,
                leaderboardReward.Percentile,
                leaderboardReward.ChestCount,
                leaderboardReward.EnhancedChestCount);
            var settlement = new WorldBossRewardSettlement(
                spawnId,
                character.Id,
                contribution.Damage,
                tier,
                totalGold,
                rewardProfile.BossExperience,
                tokens: 0,
                lootSeed,
                JsonSerializer.Serialize(lootResult),
                now);
            db.WorldBossRewardSettlements.Add(settlement);

            rewards.Add(new(
                character.Id,
                contribution.Damage,
                tier,
                leaderboardReward.Rank,
                leaderboardReward.EligibleParticipants,
                leaderboardReward.Percentile,
                leaderboardReward.ChestCount,
                leaderboardReward.EnhancedChestCount,
                rewardProfile.BossExperience,
                rewardProfile.BossGold,
                chestGold,
                itemResults,
                now));
        }

        if (!spawn.TryMarkSettled(now))
            throw new InvalidOperationException("World boss spawn could not complete settlement.");

        await db.SaveChangesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(CancellationToken.None);

        if (logger is not null)
        {
            SettlementCompleted(
                logger,
                spawnId,
                rewards.Count,
                eligible.Length,
                null);
            foreach (WorldBossSettlementCharacterResult reward in rewards)
            {
                string itemIds = reward.Items.Count == 0
                    ? "none"
                    : string.Join(
                        ',',
                        reward.Items.Select(item => item.ItemId));
                string details =
                    $"damage={reward.Contribution:0.##} "
                    + $"xp={reward.Experience} "
                    + $"gold={reward.BossGold + reward.ChestGold} "
                    + $"chests={reward.ChestCount} "
                    + $"enhancedChests={reward.EnhancedChestCount} "
                    + $"items={itemIds}";
                CharacterRewardSettled(
                    logger,
                    spawnId,
                    reward.CharacterId,
                    reward.Rank,
                    reward.Percentile,
                    reward.Tier,
                    details,
                    null);
            }
        }

        if (updatePublisher is not null)
        {
            Dictionary<Guid, WorldBossSettlementCharacterResult> rewardByCharacterId =
                rewards.ToDictionary(reward => reward.CharacterId);
            WorldBossRewardDelivery[] deliveries = contributions
                .Where(contribution =>
                    accountByCharacterId.ContainsKey(contribution.CharacterId))
                .Select(contribution => new WorldBossRewardDelivery(
                    accountByCharacterId[contribution.CharacterId],
                    contribution.CharacterId,
                    contribution.Damage,
                    rewardByCharacterId.GetValueOrDefault(contribution.CharacterId)))
                .ToArray();

            await PublishSettledSafelyAsync(
                spawnId,
                now,
                deliveries,
                cancellationToken);
        }

        return new(
            true,
            null,
            spawnId,
            false,
            rewards);
    }

    private async Task PublishSettledSafelyAsync(
        Guid spawnId,
        DateTimeOffset settledAtUtc,
        IReadOnlyCollection<WorldBossRewardDelivery> deliveries,
        CancellationToken cancellationToken)
    {
        try
        {
            await updatePublisher!.PublishSettledAsync(
                spawnId,
                settledAtUtc,
                deliveries,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Settlement is already durable. A reconnect performs an authoritative reward read.
        }
        catch (Exception exception)
        {
            if (logger is not null)
                RealtimeSettlementDeliveryFailed(logger, spawnId, exception);
        }
    }

    private async Task<WorldBossGrantedItemSnapshot> GrantItemAsync(
        Guid characterId,
        Guid rewardResolutionId,
        LootRoll roll,
        int ordinal,
        DateTimeOffset acquiredAtUtc,
        GameContentSnapshot content,
        CancellationToken cancellationToken)
    {
        if (!content.Indexes.ItemsById.TryGetValue(
                roll.ItemId,
                out ItemDefinition? definition))
        {
            throw new InvalidOperationException(
                $"World boss chest item '{roll.ItemId}' is missing from content.");
        }

        if (!definition.Stackable)
        {
            int availableSlots = await InventoryCapacity.FreeSlotsAsync(
                db,
                characterId,
                content,
                cancellationToken);
            WorldBossGrantedItemSnapshot? firstGranted = null;
            for (var index = 0; index < roll.Quantity; index++)
            {
                int generationOrdinal = checked(ordinal * 100 + index);
                if (availableSlots > 0)
                {
                    CharacterItem item = ItemInstancePersistenceFactory.CreateCharacterItem(
                        characterId,
                        definition,
                        rewardResolutionId,
                        "WORLD_BOSS_CHEST",
                        roll.ItemId,
                        generationOrdinal,
                        acquiredAtUtc,
                        content.Package,
                        roll.SourceQualityProfileId);
                    db.CharacterItems.Add(item);
                    firstGranted ??= ToGrantedSnapshot(item, definition, content);
                    availableSlots--;
                }
                else
                {
                    PendingLootItem pending = ItemInstancePersistenceFactory.CreatePendingLootItem(
                        characterId,
                        definition,
                        rewardResolutionId,
                        "WORLD_BOSS_CHEST",
                        roll.ItemId,
                        generationOrdinal,
                        acquiredAtUtc,
                        content.Package,
                        roll.SourceQualityProfileId);
                    db.PendingLootItems.Add(pending);
                    firstGranted ??= ToGrantedSnapshot(pending, definition);
                }
            }

            return firstGranted
                ?? throw new InvalidOperationException(
                    $"World boss chest item '{roll.ItemId}' produced no persisted instance.");
        }

        int remaining = roll.Quantity;
        CharacterItem[] stacks = await db.CharacterItems
            .Where(item => item.CharacterId == characterId
                && item.ItemDefinitionId == definition.Id
                && item.DefinitionVersion == definition.Version
                && item.Quantity < definition.MaxStack
                && item.TransactionLockId == null)
            .OrderBy(item => item.AcquiredAtUtc)
            .ToArrayAsync(cancellationToken);

        foreach (CharacterItem stack in stacks)
        {
            if (remaining <= 0)
                break;

            int available = definition.MaxStack - stack.Quantity;
            int toAdd = Math.Min(available, remaining);
            if (toAdd <= 0)
                continue;

            stack.AddQuantity(toAdd, definition.MaxStack);
            remaining -= toAdd;
        }

        int freeSlots = await InventoryCapacity.FreeSlotsAsync(
            db,
            characterId,
            content,
            cancellationToken);
        while (remaining > 0 && freeSlots > 0)
        {
            int quantity = Math.Min(definition.MaxStack, remaining);
            db.CharacterItems.Add(new CharacterItem(
                Guid.CreateVersion7(),
                characterId,
                definition.Id,
                quantity,
                acquiredAtUtc,
                definition.Version));
            remaining -= quantity;
            freeSlots--;
        }

        bool pendingOverflow = remaining > 0;
        if (pendingOverflow)
        {
            db.PendingLootItems.Add(new PendingLootItem(
                Guid.CreateVersion7(),
                characterId,
                rewardResolutionId,
                definition.Id,
                remaining,
                definition.Version,
                acquiredAtUtc));
        }

        return new WorldBossGrantedItemSnapshot(
            null,
            pendingOverflow,
            definition.Name,
            ToLootStats(definition.Stats, definition),
            null);
    }

    private static WorldBossGrantedItemSnapshot ToGrantedSnapshot(
        CharacterItem item,
        ItemDefinition definition,
        GameContentSnapshot content)
    {
        GeneratedItemInstance? generated = ItemInstancePersistenceFactory.ToGeneratedInstance(
            item,
            definition,
            content.Package.Itemization);
        ItemDefinition effective = generated is null
            ? definition
            : ItemInstanceGenerator.ApplyGeneratedAffixes(
                definition,
                generated.Affixes,
                generated.DisplayName);
        PrimaryStats stats = item.RolledPrimaryStats ?? effective.Stats;
        return new(
            item.Id,
            false,
            generated?.DisplayName ?? effective.Name,
            ToLootStats(stats, effective),
            generated);
    }

    private static WorldBossGrantedItemSnapshot ToGrantedSnapshot(
        PendingLootItem pending,
        ItemDefinition definition)
    {
        GeneratedItemInstance? generated = string.IsNullOrWhiteSpace(pending.GeneratedItemJson)
            ? null
            : JsonSerializer.Deserialize<GeneratedItemInstance>(pending.GeneratedItemJson);
        ItemDefinition effective = generated is null
            ? definition
            : ItemInstanceGenerator.ApplyGeneratedAffixes(
                definition,
                generated.Affixes,
                generated.DisplayName);
        PrimaryStats stats = pending.RolledPrimaryStats ?? effective.Stats;
        return new(
            pending.Id,
            true,
            generated?.DisplayName ?? effective.Name,
            ToLootStats(stats, effective),
            generated);
    }

    private static WorldBossLootItemStats ToLootStats(
        PrimaryStats stats,
        ItemDefinition definition) =>
        new(
            stats.Strength,
            stats.Agility,
            stats.Intellect,
            stats.Stamina,
            definition.MaxHpFlat,
            definition.AttackPowerFlat,
            definition.SpellPowerFlat,
            definition.CriticalChancePercent,
            definition.CriticalDamagePercent,
            definition.AccuracyPercent,
            definition.ArmorFlat,
            definition.MagicResistanceFlat,
            definition.DodgePercent,
            definition.ArmorPenetrationPercent,
            definition.MagicPenetrationPercent,
            definition.AttackSpeedPercent,
            definition.MaxResourceFlat,
            definition.WeaponDamageMin,
            definition.WeaponDamageMax,
            definition.BlockChancePercent,
            definition.BlockValueMin,
            definition.BlockValueMax);

    private sealed record WorldBossGrantedItemSnapshot(
        Guid? InstanceId,
        bool Pending,
        string DisplayName,
        WorldBossLootItemStats Stats,
        GeneratedItemInstance? GeneratedItem);

    private async Task<WorldBossSettlementCharacterResult[]> LoadExistingResultsAsync(
        Guid spawnId,
        CancellationToken cancellationToken)
    {
        WorldBossRewardSettlement[] settlements = await db.WorldBossRewardSettlements
            .AsNoTracking()
            .Where(settlement => settlement.SpawnId == spawnId)
            .OrderBy(settlement => settlement.CharacterId)
            .ToArrayAsync(cancellationToken);
        return settlements.Select(ToResult).ToArray();
    }

    private static WorldBossSettlementCharacterResult ToResult(
        WorldBossRewardSettlement settlement)
    {
        WorldBossLootResult loot = JsonSerializer.Deserialize<WorldBossLootResult>(
            settlement.LootResultJson)
            ?? new WorldBossLootResult(
                settlement.Gold,
                0,
                []);

        return new(
            settlement.CharacterId,
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
            loot.Items,
            settlement.SettledAtUtc);
    }

    private static WorldBossLeaderboardRewardResolution ResolveLeaderboardReward(
        decimal contribution,
        int rank,
        int eligibleParticipants,
        WorldBossRewardProfileDefinition profile)
    {
        if (profile.LeaderboardTiers is { Count: > 0 })
            return WorldBossLeaderboardRewardPolicy.Resolve(profile, rank, eligibleParticipants);

        return new(
            ResolveTier(contribution, profile),
            rank,
            eligibleParticipants,
            WorldBossLeaderboardRewardPolicy.CalculatePercentile(rank, eligibleParticipants),
            1,
            0,
            null);
    }

    private static WorldBossRewardTier ResolveTier(
        decimal contribution,
        WorldBossRewardProfileDefinition profile) =>
        profile.Tiers
            .Where(threshold => contribution >= threshold.MinimumContribution)
            .OrderByDescending(threshold => threshold.MinimumContribution)
            .Select(threshold => threshold.Tier)
            .FirstOrDefault(WorldBossRewardTier.Bronze);

    private static int RollInclusive(int min, int max, SeededGameRandom random)
    {
        if (max <= min)
            return min;

        int span = checked(max - min + 1);
        int offset = (int)decimal.Floor(random.NextUnit() * span);
        return min + Math.Min(offset, span - 1);
    }

    private static Guid CreateLootSeed(Guid spawnId, Guid characterId)
    {
        Span<byte> input = stackalloc byte[32];
        spawnId.TryWriteBytes(input);
        characterId.TryWriteBytes(input[16..]);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        return new Guid(hash[..16]);
    }

    private static int SeedToInt32(Guid seed)
    {
        Span<byte> bytes = stackalloc byte[16];
        seed.TryWriteBytes(bytes);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }
}
