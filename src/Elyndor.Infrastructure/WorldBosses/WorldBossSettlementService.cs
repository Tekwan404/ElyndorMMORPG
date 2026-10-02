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
    TimeProvider time)
{
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
        WorldBossContribution[] eligible = await db.WorldBossContributions
            .AsNoTracking()
            .Where(contribution =>
                contribution.SpawnId == spawnId
                && contribution.Damage >= rewardProfile.MinimumContribution)
            .OrderBy(contribution => contribution.CharacterId)
            .ToArrayAsync(cancellationToken);

        Dictionary<Guid, WorldBossRewardSettlement> existing =
            await db.WorldBossRewardSettlements
                .AsNoTracking()
                .Where(settlement => settlement.SpawnId == spawnId)
                .ToDictionaryAsync(
                    settlement => settlement.CharacterId,
                    cancellationToken);

        List<WorldBossSettlementCharacterResult> rewards = [];
        foreach (WorldBossContribution contribution in eligible)
        {
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

            WorldBossRewardTier tier = ResolveTier(
                contribution.Damage,
                rewardProfile);
            Guid lootSeed = CreateLootSeed(spawnId, character.Id);
            var random = new SeededGameRandom(SeedToInt32(lootSeed));
            int chestGold = RollInclusive(
                rewardProfile.ChestGoldMin,
                rewardProfile.ChestGoldMax,
                random);
            int totalGold = checked(rewardProfile.BossGold + chestGold);

            LootRoll[] chestLoot = LootRoller.Roll(chestTable, random)
                .Select(roll => roll with { SourceQualityProfileId = "BOSS" })
                .ToArray();
            if (chestLoot.Length == 0)
            {
                throw new InvalidOperationException(
                    $"World boss chest '{chestTable.Id}' did not produce an item.");
            }

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
            for (var ordinal = 0; ordinal < chestLoot.Length; ordinal++)
            {
                LootRoll roll = chestLoot[ordinal];
                await GrantItemAsync(
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
                    item.Name,
                    item.Rarity,
                    roll.Quantity,
                    item.IconId));
            }

            WorldBossLootResult lootResult = new(
                rewardProfile.BossGold,
                chestGold,
                itemResults);
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

        return new(
            true,
            null,
            spawnId,
            false,
            rewards);
    }

    private async Task GrantItemAsync(
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
            for (var index = 0; index < roll.Quantity; index++)
            {
                int generationOrdinal = checked(ordinal * 100 + index);
                if (availableSlots > 0)
                {
                    db.CharacterItems.Add(
                        ItemInstancePersistenceFactory.CreateCharacterItem(
                            characterId,
                            definition,
                            rewardResolutionId,
                            "WORLD_BOSS_CHEST",
                            roll.ItemId,
                            generationOrdinal,
                            acquiredAtUtc,
                            content.Package,
                            roll.SourceQualityProfileId));
                    availableSlots--;
                }
                else
                {
                    db.PendingLootItems.Add(
                        ItemInstancePersistenceFactory.CreatePendingLootItem(
                            characterId,
                            definition,
                            rewardResolutionId,
                            "WORLD_BOSS_CHEST",
                            roll.ItemId,
                            generationOrdinal,
                            acquiredAtUtc,
                            content.Package,
                            roll.SourceQualityProfileId));
                }
            }

            return;
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

        if (remaining > 0)
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
    }

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
            settlement.Experience,
            loot.BossGold,
            loot.ChestGold,
            loot.Items,
            settlement.SettledAtUtc);
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
