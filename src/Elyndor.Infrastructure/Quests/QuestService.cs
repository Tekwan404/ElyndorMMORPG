using System.Text.Json;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Progression;
using Elyndor.Core.Quests;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Quests;

public static class QuestErrorCodes
{
    public const string CharacterNotFound = "quest_character_not_found";
    public const string QuestNotFound = "quest_not_found";
    public const string LevelRequired = "quest_level_required";
    public const string InvalidLocation = "quest_invalid_location";
    public const string PrerequisiteRequired = "quest_prerequisite_required";
    public const string AlreadyActive = "quest_already_active";
    public const string AlreadyCompleted = "quest_already_completed";
    public const string NotActive = "quest_not_active";
    public const string NotReady = "quest_not_ready";
    public const string ProtectedItems = "quest_items_protected";
    public const string ClaimConflict = "quest_claim_conflict";
}

public sealed record QuestObjectiveSnapshot(
    string Id,
    string Type,
    string TargetId,
    int CurrentCount,
    int RequiredCount,
    bool Completed,
    bool ConsumeOnClaim);

public sealed record QuestJournalEntry(
    string Id,
    string DisplayName,
    string Description,
    string Type,
    int RequiredLevel,
    string OfferLocationId,
    string Status,
    IReadOnlyList<QuestObjectiveSnapshot> Objectives,
    int RewardXp,
    int RewardGold,
    IReadOnlyList<QuestItemRewardDefinition> RewardItems,
    IReadOnlyList<string> PrerequisiteQuestIds,
    string? UnlockLocationId);

public sealed record QuestJournalSnapshot(
    IReadOnlyList<QuestJournalEntry> Quests);

public sealed record QuestMutationResult(
    bool IsSuccess,
    string? ErrorCode,
    string? QuestId)
{
    public static QuestMutationResult Success(string questId) =>
        new(true, null, questId);

    public static QuestMutationResult Failure(string code) =>
        new(false, code, null);
}

public sealed record QuestClaimResult(
    bool IsSuccess,
    bool Granted,
    string? ErrorCode,
    string? QuestId,
    int XpEarned,
    int GoldEarned,
    CharacterProgressionResult? Progression,
    IReadOnlyList<QuestItemRewardDefinition> Items)
{
    public static QuestClaimResult Failure(string code) =>
        new(false, false, code, null, 0, 0, null, []);
}

public sealed class QuestService(
    GameDbContext dbContext,
    IContentSnapshotProvider contentProvider,
    CharacterDerivedStateService derivedStateService,
    IGameRandomFactory randomFactory,
    TimeProvider timeProvider)
{
    public async Task<QuestJournalSnapshot> GetAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        Character? character = await dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.AccountId == accountId,
                cancellationToken);
        if (character is null)
            return new QuestJournalSnapshot([]);

        GameContentSnapshot contentSnapshot = contentProvider.GetCurrent();
        GameContentPackage content = contentSnapshot.Package;
        IReadOnlyList<QuestDefinition> quests = QuestCatalog.Resolve(content);

        CharacterQuestState[] states = await dbContext.CharacterQuestStates
            .AsNoTracking()
            .Where(state => state.CharacterId == character.Id)
            .ToArrayAsync(cancellationToken);
        Dictionary<string, CharacterQuestState> stateById =
            states.ToDictionary(state => state.QuestId, StringComparer.Ordinal);

        HashSet<string> legacyAccepted = (await dbContext.CharacterContractAcceptances
                .AsNoTracking()
                .Where(state => state.CharacterId == character.Id)
                .Select(state => state.ContractId)
                .ToArrayAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> legacyCompleted = (await dbContext.CharacterContractCompletions
                .AsNoTracking()
                .Where(state => state.CharacterId == character.Id)
                .Select(state => state.ContractId)
                .ToArrayAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> rewarded = (await dbContext.QuestRewardGrants
                .AsNoTracking()
                .Where(grant => grant.CharacterId == character.Id)
                .Select(grant => grant.QuestId)
                .ToArrayAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        HashSet<string> completedIds = states
            .Where(state => state.Status == QuestStateStatuses.Completed)
            .Select(state => state.QuestId)
            .Concat(legacyCompleted)
            .Concat(rewarded)
            .ToHashSet(StringComparer.Ordinal);

        string? currentLocation = await dbContext.CharacterLocations
            .AsNoTracking()
            .Where(location => location.CharacterId == character.Id)
            .Select(location => location.LocationId)
            .SingleOrDefaultAsync(cancellationToken);

        Dictionary<string, int> inventoryCounts =
            await LoadTurnInInventoryCountsAsync(
                character.Id,
                cancellationToken);

        List<QuestJournalEntry> entries = [];
        foreach (QuestDefinition quest in quests)
        {
            stateById.TryGetValue(quest.Id, out CharacterQuestState? state);
            Dictionary<string, int> progress = QuestProgressJson.Read(state?.ProgressJson);
            QuestObjectiveSnapshot[] objectives = quest.Objectives
                .Select(objective => ToObjectiveSnapshot(
                    objective,
                    progress,
                    inventoryCounts,
                    legacyCompleted.Contains(quest.Id)))
                .ToArray();

            bool allObjectivesComplete = objectives.All(objective => objective.Completed);
            string status;
            if (rewarded.Contains(quest.Id)
                || state?.Status == QuestStateStatuses.Completed
                || legacyCompleted.Contains(quest.Id) && state is null)
            {
                status = QuestStateStatuses.Completed;
            }
            else if (state is not null || legacyAccepted.Contains(quest.Id))
            {
                status = allObjectivesComplete
                    ? QuestStateStatuses.ReadyToClaim
                    : QuestStateStatuses.Active;
            }
            else
            {
                bool prerequisitesMet = (quest.PrerequisiteQuestIds ?? [])
                    .All(completedIds.Contains);
                status = character.Level >= quest.RequiredLevel
                    && prerequisitesMet
                    && string.Equals(
                        currentLocation,
                        quest.OfferLocationId,
                        StringComparison.Ordinal)
                        ? "AVAILABLE"
                        : "LOCKED";
            }

            entries.Add(new QuestJournalEntry(
                quest.Id,
                quest.DisplayName,
                quest.Description,
                quest.Type.ToString().ToUpperInvariant(),
                quest.RequiredLevel,
                quest.OfferLocationId,
                status,
                objectives,
                quest.RewardXp,
                quest.RewardGold,
                quest.RewardItems ?? [],
                quest.PrerequisiteQuestIds ?? [],
                quest.UnlockLocationId));
        }

        return new QuestJournalSnapshot(entries);
    }

    public async Task<QuestMutationResult> AcceptAsync(
        Guid accountId,
        string questId,
        CancellationToken cancellationToken)
    {
        Character? character = await dbContext.Characters
            .SingleOrDefaultAsync(
                candidate => candidate.AccountId == accountId,
                cancellationToken);
        if (character is null)
            return QuestMutationResult.Failure(QuestErrorCodes.CharacterNotFound);

        GameContentPackage content = contentProvider.GetCurrent().Package;
        QuestDefinition? quest = QuestCatalog.Find(content, questId);
        if (quest is null)
            return QuestMutationResult.Failure(QuestErrorCodes.QuestNotFound);
        if (character.Level < quest.RequiredLevel)
            return QuestMutationResult.Failure(QuestErrorCodes.LevelRequired);

        bool completed = await IsCompletedAsync(
            character.Id,
            quest.Id,
            cancellationToken);
        if (completed)
            return QuestMutationResult.Failure(QuestErrorCodes.AlreadyCompleted);

        CharacterQuestState? existing = await dbContext.CharacterQuestStates
            .SingleOrDefaultAsync(
                state => state.CharacterId == character.Id
                    && state.QuestId == quest.Id,
                cancellationToken);
        if (existing is not null)
            return QuestMutationResult.Failure(QuestErrorCodes.AlreadyActive);

        string? locationId = await dbContext.CharacterLocations
            .Where(location => location.CharacterId == character.Id)
            .Select(location => location.LocationId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!string.Equals(
                locationId,
                quest.OfferLocationId,
                StringComparison.Ordinal))
        {
            return QuestMutationResult.Failure(QuestErrorCodes.InvalidLocation);
        }

        foreach (string prerequisite in quest.PrerequisiteQuestIds ?? [])
        {
            if (!await IsCompletedAsync(
                    character.Id,
                    prerequisite,
                    cancellationToken))
            {
                return QuestMutationResult.Failure(
                    QuestErrorCodes.PrerequisiteRequired);
            }
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        dbContext.CharacterQuestStates.Add(
            new CharacterQuestState(character.Id, quest.Id, now));

        if (QuestCatalog.IsLegacyContract(content, quest.Id))
        {
            bool accepted = await dbContext.CharacterContractAcceptances
                .AnyAsync(
                    state => state.CharacterId == character.Id
                        && state.ContractId == quest.Id,
                    cancellationToken);
            if (!accepted)
            {
                dbContext.CharacterContractAcceptances.Add(
                    new Elyndor.Core.World.CharacterContractAcceptance(
                        character.Id,
                        quest.Id,
                        now));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return QuestMutationResult.Success(quest.Id);
    }

    public async Task<QuestMutationResult> AbandonAsync(
        Guid accountId,
        string questId,
        CancellationToken cancellationToken)
    {
        Character? character = await dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.AccountId == accountId,
                cancellationToken);
        if (character is null)
            return QuestMutationResult.Failure(QuestErrorCodes.CharacterNotFound);

        CharacterQuestState? state = await dbContext.CharacterQuestStates
            .SingleOrDefaultAsync(
                candidate => candidate.CharacterId == character.Id
                    && candidate.QuestId == questId,
                cancellationToken);
        if (state is null)
            return QuestMutationResult.Failure(QuestErrorCodes.NotActive);
        if (state.Status == QuestStateStatuses.Completed)
            return QuestMutationResult.Failure(QuestErrorCodes.AlreadyCompleted);

        dbContext.CharacterQuestStates.Remove(state);

        CharacterContractAcceptance? legacyAcceptance =
            await dbContext.CharacterContractAcceptances.SingleOrDefaultAsync(
                candidate => candidate.CharacterId == character.Id
                    && candidate.ContractId == questId,
                cancellationToken);
        bool legacyCompleted = await dbContext.CharacterContractCompletions
            .AnyAsync(
                candidate => candidate.CharacterId == character.Id
                    && candidate.ContractId == questId,
                cancellationToken);
        if (legacyAcceptance is not null && !legacyCompleted)
            dbContext.CharacterContractAcceptances.Remove(legacyAcceptance);

        await dbContext.SaveChangesAsync(cancellationToken);
        return QuestMutationResult.Success(questId);
    }

    public async Task<QuestClaimResult> ClaimAsync(
        Guid accountId,
        string questId,
        Guid mutationId,
        CancellationToken cancellationToken)
    {
        if (mutationId == Guid.Empty)
            return QuestClaimResult.Failure(QuestErrorCodes.ClaimConflict);

        GameContentSnapshot contentSnapshot = contentProvider.GetCurrent();
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            () => ClaimCoreAsync(
                accountId,
                questId,
                mutationId,
                contentSnapshot,
                cancellationToken));
    }

    private async Task<QuestClaimResult> ClaimCoreAsync(
        Guid accountId,
        string questId,
        Guid mutationId,
        GameContentSnapshot contentSnapshot,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        Character? character = await dbContext.Characters
            .FromSqlInterpolated(
                $"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (character is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return QuestClaimResult.Failure(QuestErrorCodes.CharacterNotFound);
        }

        QuestDefinition? quest =
            QuestCatalog.Find(contentSnapshot.Package, questId);
        if (quest is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return QuestClaimResult.Failure(QuestErrorCodes.QuestNotFound);
        }

        QuestRewardGrant? existing = await dbContext.QuestRewardGrants
            .AsNoTracking()
            .SingleOrDefaultAsync(
                grant => grant.CharacterId == character.Id
                    && grant.QuestId == quest.Id,
                cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            QuestItemRewardDefinition[] replayItems =
                JsonSerializer.Deserialize<QuestItemRewardDefinition[]>(
                    existing.RewardItemsJson) ?? [];
            return new QuestClaimResult(
                true,
                false,
                null,
                quest.Id,
                existing.XpEarned,
                existing.GoldEarned,
                null,
                replayItems);
        }

        CharacterQuestState? state = await dbContext.CharacterQuestStates
            .SingleOrDefaultAsync(
                candidate => candidate.CharacterId == character.Id
                    && candidate.QuestId == quest.Id,
                cancellationToken);
        if (state is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return QuestClaimResult.Failure(
                await dbContext.CharacterContractCompletions
                    .AsNoTracking()
                    .AnyAsync(
                        completion => completion.CharacterId == character.Id
                            && completion.ContractId == quest.Id,
                        cancellationToken)
                    ? QuestErrorCodes.AlreadyCompleted
                    : QuestErrorCodes.NotActive);
        }

        Dictionary<string, int> progress =
            QuestProgressJson.Read(state.ProgressJson);
        Dictionary<string, int> inventoryCounts =
            await LoadTurnInInventoryCountsAsync(
                character.Id,
                cancellationToken);
        if (!quest.Objectives.All(objective =>
                ToObjectiveSnapshot(
                    objective,
                    progress,
                    inventoryCounts,
                    false).Completed))
        {
            await transaction.RollbackAsync(cancellationToken);
            return QuestClaimResult.Failure(QuestErrorCodes.NotReady);
        }

        foreach (QuestObjectiveDefinition objective in quest.Objectives)
        {
            if (objective.Type == QuestObjectiveType.CollectItem
                && objective.ConsumeOnClaim)
            {
                bool consumed = await ConsumeTurnInItemsAsync(
                    character.Id,
                    objective.TargetId,
                    objective.RequiredCount,
                    cancellationToken);
                if (!consumed)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return QuestClaimResult.Failure(
                        QuestErrorCodes.ProtectedItems);
                }
            }
        }

        LevelProgressionDefinition leveling =
            contentSnapshot.Package.LevelProgression
            ?? throw new InvalidOperationException(
                "Level progression content is required for quest rewards.");
        CharacterProgressionResult progression =
            CharacterProgression.GrantExperience(
                character,
                quest.RewardXp,
                leveling);

        if (quest.RewardGold > 0)
        {
            await dbContext.Characters
                .Where(candidate => candidate.Id == character.Id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        candidate => candidate.Gold,
                        candidate => candidate.Gold + quest.RewardGold),
                    cancellationToken);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        foreach (QuestItemRewardDefinition reward in quest.RewardItems ?? [])
        {
            await AddRewardItemAsync(
                character.Id,
                mutationId,
                reward,
                now,
                contentSnapshot,
                cancellationToken);
        }

        if (progression.LeveledUp)
        {
            CharacterVitals vitals = await dbContext.CharacterVitals
                .SingleAsync(
                    candidate => candidate.CharacterId == character.Id,
                    cancellationToken);
            CharacterDerivedState derived =
                await derivedStateService.ResolveAsync(
                    character.Id,
                    character.ClassId,
                    character.Level,
                    contentSnapshot,
                    cancellationToken);
            vitals.Checkpoint(
                derived.Stats.MaxHp,
                Math.Min(
                    vitals.CurrentResource,
                    derived.EffectiveResourceProfile.MaxValue),
                now);
        }

        state.MarkCompleted(now);
        string itemJson = JsonSerializer.Serialize(quest.RewardItems ?? []);
        dbContext.QuestRewardGrants.Add(new QuestRewardGrant(
            character.Id,
            quest.Id,
            mutationId,
            quest.RewardXp,
            quest.RewardGold,
            itemJson,
            now));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new QuestClaimResult(
            true,
            true,
            null,
            quest.Id,
            quest.RewardXp,
            quest.RewardGold,
            progression,
            quest.RewardItems ?? []);
    }

    private async Task<bool> IsCompletedAsync(
        Guid characterId,
        string questId,
        CancellationToken cancellationToken) =>
        await dbContext.QuestRewardGrants.AsNoTracking().AnyAsync(
            grant => grant.CharacterId == characterId
                && grant.QuestId == questId,
            cancellationToken)
        || await dbContext.CharacterQuestStates.AsNoTracking().AnyAsync(
            state => state.CharacterId == characterId
                && state.QuestId == questId
                && state.Status == QuestStateStatuses.Completed,
            cancellationToken)
        || await dbContext.CharacterContractCompletions.AsNoTracking().AnyAsync(
            state => state.CharacterId == characterId
                && state.ContractId == questId,
            cancellationToken);

    private async Task<Dictionary<string, int>> LoadTurnInInventoryCountsAsync(
        Guid characterId,
        CancellationToken cancellationToken) =>
        await dbContext.CharacterItems
            .AsNoTracking()
            .Where(item => item.CharacterId == characterId && !item.IsLocked)
            .GroupBy(item => item.ItemDefinitionId)
            .Select(group => new
            {
                ItemId = group.Key,
                Quantity = group.Sum(item => item.Quantity)
            })
            .ToDictionaryAsync(
                item => item.ItemId,
                item => item.Quantity,
                StringComparer.Ordinal,
                cancellationToken);

    private static QuestObjectiveSnapshot ToObjectiveSnapshot(
        QuestObjectiveDefinition objective,
        IReadOnlyDictionary<string, int> progress,
        IReadOnlyDictionary<string, int> inventoryCounts,
        bool legacyCompleted)
    {
        int current = legacyCompleted
            ? objective.RequiredCount
            : objective.Type == QuestObjectiveType.CollectItem
                ? inventoryCounts.GetValueOrDefault(objective.TargetId)
                : progress.GetValueOrDefault(objective.Id);
        current = Math.Min(current, objective.RequiredCount);
        return new QuestObjectiveSnapshot(
            objective.Id,
            objective.Type.ToString(),
            objective.TargetId,
            current,
            objective.RequiredCount,
            current >= objective.RequiredCount,
            objective.ConsumeOnClaim);
    }

    private async Task<bool> ConsumeTurnInItemsAsync(
        Guid characterId,
        string itemDefinitionId,
        int requiredQuantity,
        CancellationToken cancellationToken)
    {
        CharacterItem[] stacks = await dbContext.CharacterItems
            .Where(item => item.CharacterId == characterId
                && item.ItemDefinitionId == itemDefinitionId
                && !item.IsLocked)
            .OrderBy(item => item.AcquiredAtUtc)
            .ToArrayAsync(cancellationToken);
        if (stacks.Sum(item => item.Quantity) < requiredQuantity)
            return false;

        int remaining = requiredQuantity;
        foreach (CharacterItem stack in stacks)
        {
            if (remaining <= 0)
                break;
            int take = Math.Min(stack.Quantity, remaining);
            stack.RemoveQuantity(take);
            remaining -= take;
            if (stack.Quantity == 0)
                dbContext.CharacterItems.Remove(stack);
        }

        return remaining == 0;
    }

    private async Task AddRewardItemAsync(
        Guid characterId,
        Guid rewardResolutionId,
        QuestItemRewardDefinition reward,
        DateTimeOffset acquiredAtUtc,
        GameContentSnapshot contentSnapshot,
        CancellationToken cancellationToken)
    {
        if (!contentSnapshot.Indexes.ItemsById.TryGetValue(
                reward.ItemId,
                out ItemDefinition? definition))
        {
            throw new InvalidOperationException(
                $"Quest reward item '{reward.ItemId}' is missing from content.");
        }

        int remaining = reward.Quantity;
        if (definition.Stackable)
        {
            CharacterItem[] stacks = await dbContext.CharacterItems
                .Where(item => item.CharacterId == characterId
                    && item.ItemDefinitionId == definition.Id
                    && item.DefinitionVersion == definition.Version
                    && item.Quantity < definition.MaxStack)
                .OrderBy(item => item.AcquiredAtUtc)
                .ToArrayAsync(cancellationToken);
            foreach (CharacterItem stack in stacks)
            {
                if (remaining <= 0)
                    break;
                int add = Math.Min(
                    definition.MaxStack - stack.Quantity,
                    remaining);
                if (add <= 0)
                    continue;
                stack.AddQuantity(add, definition.MaxStack);
                remaining -= add;
            }
        }

        int freeSlots = await InventoryCapacity.FreeSlotsAsync(
            dbContext,
            characterId,
            contentSnapshot,
            cancellationToken);
        while (remaining > 0 && freeSlots > 0)
        {
            int quantity = definition.Stackable
                ? Math.Min(definition.MaxStack, remaining)
                : 1;
            PrimaryStats? rolled = definition.Type == ItemType.Equipment
                ? ItemInstanceStatRoller.Resolve(
                    definition,
                    randomFactory.Create())
                : null;
            dbContext.CharacterItems.Add(new CharacterItem(
                Guid.NewGuid(),
                characterId,
                definition.Id,
                quantity,
                acquiredAtUtc,
                definition.Version,
                rolled));
            remaining -= quantity;
            freeSlots--;
        }

        while (remaining > 0)
        {
            int quantity = definition.Stackable
                ? Math.Min(definition.MaxStack, remaining)
                : 1;
            PrimaryStats? rolled = definition.Type == ItemType.Equipment
                ? ItemInstanceStatRoller.Resolve(
                    definition,
                    randomFactory.Create())
                : null;
            dbContext.PendingLootItems.Add(new PendingLootItem(
                Guid.NewGuid(),
                characterId,
                rewardResolutionId,
                definition.Id,
                quantity,
                definition.Version,
                acquiredAtUtc,
                rolled));
            remaining -= quantity;
        }
    }
}
