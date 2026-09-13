using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elyndor.Core.Afk;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Progression;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Afk;

public sealed record AfkFarmProgressResult(
    bool Processed,
    AfkFarmSession? Session,
    AfkFarmIntervalGrant? Grant,
    string? ErrorCode = null);

public sealed class AfkFarmProgressService(
    GameDbContext dbContext,
    IContentSnapshotProvider contentProvider,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<AfkFarmProgressResult> ProcessAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => ProcessCoreAsync(accountId, cancellationToken));

    private async Task<AfkFarmProgressResult> ProcessCoreAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        Character? character = await dbContext.Characters.FromSqlInterpolated(
                $"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (character is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(false, null, null, AfkFarmErrorCodes.CharacterNotFound);
        }

        AfkFarmSession? session = await dbContext.AfkFarmSessions
            .Where(candidate => candidate.CharacterId == character.Id
                && candidate.Status == AfkFarmStatus.Active)
            .SingleOrDefaultAsync(cancellationToken);
        if (session is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(false, null, null, AfkFarmErrorCodes.NoSession);
        }

        GameContentSnapshot content = contentProvider.GetCurrent();
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!string.Equals(session.ContentVersion, content.ContentVersion, StringComparison.Ordinal)
            || !string.Equals(session.BalanceVersion, content.BalanceVersion, StringComparison.Ordinal))
        {
            session.Invalidate(now, "content_version_changed");
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(false, session, null, "afk_content_version_changed");
        }

        AfkFarmRewardProfile profile = content.Package.AfkFarm
            ?? throw new InvalidOperationException("AFK reward profile is required in content.");
        ValidateProfile(profile);
        DateTimeOffset intervalStart = session.LastProcessedAtUtc;
        DateTimeOffset intervalEnd = Min(
            session.EndsAtUtc,
            intervalStart.AddSeconds(profile.ProcessingIntervalSeconds));
        if (now < intervalEnd)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(false, session, null);
        }

        int intervalIndex = checked((int)((intervalStart - session.StartedAtUtc).TotalSeconds
            / profile.ProcessingIntervalSeconds));
        AfkFarmIntervalGrant? replay = await dbContext.AfkFarmIntervalGrants
            .SingleOrDefaultAsync(grant => grant.SessionId == session.Id
                && grant.IntervalIndex == intervalIndex, cancellationToken);
        if (replay is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(false, session, replay);
        }

        if (!content.Indexes.LocationsById.TryGetValue(session.LocationId, out var location))
            throw new InvalidOperationException("AFK location is missing from content.");
        AfkCharacterSnapshot snapshot = JsonSerializer.Deserialize<AfkCharacterSnapshot>(
            session.CharacterSnapshotJson,
            JsonOptions) ?? throw new InvalidOperationException("AFK character snapshot is invalid.");
        AfkFarmSimulationResult simulation = AfkFarmSimulator.Simulate(new(
            session.Id,
            intervalIndex,
            snapshot,
            location,
            content.Indexes.MonstersById,
            intervalStart,
            intervalEnd,
            session.ContentVersion,
            session.TargetMonsterId));

        int xp = Scale(simulation.XpCandidate, profile.XpMultiplier);
        int gold = Scale(simulation.GoldCandidate, profile.GoldMultiplier);
        LootRoll[] loot = RollLoot(simulation.LootCandidates, content, profile.LootMultiplier,
            session.Id, intervalIndex);
        CharacterProgression.GrantExperience(
            character,
            xp,
            content.Package.LevelProgression
                ?? throw new InvalidOperationException("Level progression content is required."));
        character.AddGold(gold);

        bool inventoryFull = await AddLootAsync(
            character.Id, session.Id, intervalIndex, loot, now, content, cancellationToken);
        AfkFarmIntervalGrant grant = new(
            session.Id, intervalIndex, intervalStart, intervalEnd,
            simulation.Kills, xp, gold, JsonSerializer.Serialize(loot, JsonOptions), now);
        dbContext.AfkFarmIntervalGrants.Add(grant);
        session.CompleteInterval(intervalEnd);
        if (inventoryFull)
            session.StopForInventoryFull(intervalEnd);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, session, grant);
    }

    private async Task<bool> AddLootAsync(Guid characterId, Guid sessionId, int intervalIndex,
        IReadOnlyList<LootRoll> loot, DateTimeOffset now, GameContentSnapshot content,
        CancellationToken cancellationToken)
    {
        bool overflow = false;
        foreach (LootRoll roll in loot)
        {
            if (!content.Indexes.ItemsById.TryGetValue(roll.ItemId, out ItemDefinition? definition))
                throw new InvalidOperationException($"AFK loot item '{roll.ItemId}' is missing from content.");
            for (var ordinal = 0; ordinal < roll.Quantity; ordinal++)
            {
                if (!definition.Stackable && await InventoryCapacity.FreeSlotsAsync(
                        dbContext, characterId, content, cancellationToken) <= 0)
                {
                    dbContext.PendingLootItems.Add(ItemInstancePersistenceFactory.CreatePendingLootItem(
                        characterId, definition, sessionId, "AFK", $"{intervalIndex}:{roll.ItemId}", ordinal,
                        now, content.Package));
                    overflow = true;
                    continue;
                }

                dbContext.CharacterItems.Add(ItemInstancePersistenceFactory.CreateCharacterItem(
                    characterId, definition, sessionId, "AFK", $"{intervalIndex}:{roll.ItemId}", ordinal,
                    now, content.Package));
            }
        }
        return overflow;
    }

    private static LootRoll[] RollLoot(IReadOnlyList<AfkFarmLootCandidate> candidates,
        GameContentSnapshot content, decimal multiplier, Guid sessionId, int intervalIndex)
    {
        if (multiplier is < 0 or > 1)
            throw new InvalidOperationException("AFK loot multiplier must be between zero and one.");
        List<LootRoll> result = [];
        foreach ((AfkFarmLootCandidate candidate, int ordinal) in candidates.Select((item, index) => (item, index)))
        {
            if (!content.Indexes.LootTablesById.TryGetValue(candidate.LootTableId, out LootTableDefinition? table))
                throw new InvalidOperationException($"AFK loot table '{candidate.LootTableId}' is missing from content.");
            SeededGameRandom random = new(Seed(sessionId, intervalIndex, candidate.LootTableId, ordinal));
            if (random.NextUnit() < multiplier)
                result.AddRange(LootRoller.Roll(table, random));
        }
        return result.GroupBy(roll => roll.ItemId, StringComparer.Ordinal)
            .Select(group => new LootRoll(group.Key, checked(group.Sum(roll => roll.Quantity)))).ToArray();
    }

    private static int Seed(Guid sessionId, int intervalIndex, string value, int ordinal) =>
        BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{sessionId:N}:{intervalIndex}:{value}:{ordinal}")), 0);

    private static int Scale(int value, decimal multiplier) => checked((int)Math.Floor(value * multiplier));

    private static DateTimeOffset Min(params DateTimeOffset[] values) => values.Min();

    private static void ValidateProfile(AfkFarmRewardProfile profile)
    {
        if (profile.ProcessingIntervalSeconds is < 1 or > 3600
            || profile.XpMultiplier is < 0 or > 1
            || profile.GoldMultiplier is < 0 or > 1
            || profile.LootMultiplier is < 0 or > 1)
            throw new InvalidOperationException("AFK reward profile is invalid.");
    }
}
