using System.Security.Cryptography;
using System.Text;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Items;

public static class ItemEnhancementErrorCodes
{
    public const string CharacterNotFound = "item_enhancement_character_not_found";
    public const string ItemNotFound = "item_enhancement_item_not_found";
    public const string ItemNotGenerated = "item_enhancement_item_not_generated";
    public const string ItemLocked = "item_enhancement_item_locked";
    public const string ItemTransactionLocked = "item_enhancement_item_transaction_locked";
    public const string MaxEnhancement = "item_enhancement_max_level";
    public const string ProfileMissing = "item_enhancement_profile_missing";
    public const string NotEnoughGold = "item_enhancement_not_enough_gold";
    public const string NotEnoughMaterial = "item_enhancement_not_enough_material";
    public const string MissingCatalyst = "item_enhancement_missing_catalyst";
    public const string MutationConflict = "item_enhancement_mutation_conflict";
}

public sealed record ItemEnhancementResult(
    bool Succeeded,
    string? ErrorCode,
    GeneratedItemInstance? Item,
    int EnhancementLevel,
    decimal EnhancementBonusPercent,
    decimal? FinalItemPower)
{
    public static ItemEnhancementResult Failure(string code) => new(false, code, null, 0, 0m, null);
}

public sealed record ItemEnhancementPreviewResult(
    bool Succeeded,
    string? ErrorCode,
    Guid ItemInstanceId,
    int TargetEnhancementLevel,
    decimal EnhancementBonusPercent,
    int Gold,
    string EnhancementMaterialItemId,
    int EnhancementMaterialQuantity,
    string? CatalystItemId,
    int CatalystQuantity,
    decimal? IntrinsicItemPower,
    decimal? FinalItemPower)
{
    public static ItemEnhancementPreviewResult Failure(string code) =>
        new(false, code, Guid.Empty, 0, 0m, 0, string.Empty, 0, null, 0, null, null);
}

/// <summary>
/// Player investment applied after an item has been generated.
/// Enhancement is deliberately external to the generated-instance budget: it must never
/// rewrite Stars, RollQuality, IsPerfect, generated names or rolled affixes.
/// </summary>
public sealed class ItemEnhancementService(
    GameDbContext dbContext,
    IContentSnapshotProvider contentProvider,
    TimeProvider timeProvider)
{
    private const string OperationType = "ITEM_ENHANCEMENT_V2";
    private const string LegacyOperationType = "ITEM_STAR_UPGRADE";

    public Task<ItemEnhancementResult> EnhanceAsync(
        Guid accountId,
        Guid itemId,
        Guid mutationId,
        CancellationToken cancellationToken) =>
        mutationId == Guid.Empty
            ? Task.FromResult(ItemEnhancementResult.Failure(ItemEnhancementErrorCodes.MutationConflict))
            : dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
                () => EnhanceCoreAsync(accountId, itemId, mutationId, cancellationToken));

    public async Task<ItemEnhancementPreviewResult> GetPreviewAsync(
        Guid accountId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        GameContentSnapshot content = contentProvider.GetCurrent();
        ItemStarUpgradeProfileDefinition? profile = content.Package.Itemization?.StarUpgrades;
        if (profile is null)
            return ItemEnhancementPreviewResult.Failure(ItemEnhancementErrorCodes.ProfileMissing);

        Character? character = await dbContext.Characters.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.AccountId == accountId, cancellationToken);
        if (character is null)
            return ItemEnhancementPreviewResult.Failure(ItemEnhancementErrorCodes.CharacterNotFound);

        CharacterItem? item = await dbContext.CharacterItems.AsNoTracking()
            .Include(candidate => candidate.Affixes)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == itemId && candidate.CharacterId == character.Id,
                cancellationToken);
        if (item is null)
            return ItemEnhancementPreviewResult.Failure(ItemEnhancementErrorCodes.ItemNotFound);
        if (item.IsLocked)
            return ItemEnhancementPreviewResult.Failure(ItemEnhancementErrorCodes.ItemLocked);
        if (item.TransactionLockId.HasValue)
            return ItemEnhancementPreviewResult.Failure(ItemEnhancementErrorCodes.ItemTransactionLocked);
        if (!content.Indexes.ItemsById.TryGetValue(item.ItemDefinitionId, out ItemDefinition? definition))
            return ItemEnhancementPreviewResult.Failure(ItemEnhancementErrorCodes.ItemNotFound);

        GeneratedItemInstance? current = ItemInstancePersistenceFactory.ToGeneratedInstance(
            item,
            definition,
            content.Package.Itemization);
        if (current is null)
            return ItemEnhancementPreviewResult.Failure(ItemEnhancementErrorCodes.ItemNotGenerated);
        if (item.EnhancementLevel >= ItemEnhancementRules.MaxEnhancementLevel)
            return ItemEnhancementPreviewResult.Failure(ItemEnhancementErrorCodes.MaxEnhancement);

        int targetLevel = item.EnhancementLevel + 1;
        if (!TryResolveCost(profile, targetLevel, out EnhancementCost cost))
            return ItemEnhancementPreviewResult.Failure(ItemEnhancementErrorCodes.ProfileMissing);

        decimal? intrinsicPower = current.ActualItemPower;
        decimal? finalPower = intrinsicPower.HasValue
            ? ItemEnhancementRules.CalculateEnhancedItemPower(
                definition,
                current.Affixes,
                current.ActualItemPower,
                targetLevel,
                content.Package.Itemization)
            : null;

        return new ItemEnhancementPreviewResult(
            true,
            null,
            item.Id,
            targetLevel,
            ItemEnhancementRules.ResolveBonusPercent(targetLevel),
            cost.Gold,
            profile.ReforgeStoneItemId,
            cost.EnhancementMaterialQuantity,
            cost.CatalystItemId,
            cost.CatalystQuantity,
            intrinsicPower,
            finalPower);
    }

    private async Task<ItemEnhancementResult> EnhanceCoreAsync(
        Guid accountId,
        Guid itemId,
        Guid mutationId,
        CancellationToken cancellationToken)
    {
        string fingerprint = Fingerprint(OperationType, itemId);
        string legacyFingerprint = Fingerprint(LegacyOperationType, itemId);
        GameContentSnapshot content = contentProvider.GetCurrent();
        ItemStarUpgradeProfileDefinition? profile = content.Package.Itemization?.StarUpgrades;
        if (profile is null)
            return ItemEnhancementResult.Failure(ItemEnhancementErrorCodes.ProfileMissing);

        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        Character? character = await dbContext.Characters
            .FromSqlInterpolated($"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (character is null)
            return await Fail(transaction, ItemEnhancementErrorCodes.CharacterNotFound, cancellationToken);

        CharacterMutation? replay = await dbContext.CharacterMutations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CharacterId == character.Id && x.MutationId == mutationId, cancellationToken);
        if (replay is not null)
        {
            bool currentReplay = replay.OperationType == OperationType && replay.RequestFingerprint == fingerprint;
            bool legacyReplay = replay.OperationType == LegacyOperationType && replay.RequestFingerprint == legacyFingerprint;
            if (!currentReplay && !legacyReplay)
                return await Fail(transaction, ItemEnhancementErrorCodes.MutationConflict, cancellationToken);

            LoadedItem? existing = await LoadGeneratedAsync(character.Id, itemId, content, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return existing is null
                ? ItemEnhancementResult.Failure(ItemEnhancementErrorCodes.ItemNotFound)
                : CreateSuccess(existing.Item, existing.Generated, existing.Definition, content.Package.Itemization);
        }

        CharacterItem? item = await dbContext.CharacterItems
            .Include(x => x.Affixes)
            .SingleOrDefaultAsync(x => x.Id == itemId && x.CharacterId == character.Id, cancellationToken);
        if (item is null)
            return await Fail(transaction, ItemEnhancementErrorCodes.ItemNotFound, cancellationToken);
        if (item.IsLocked)
            return await Fail(transaction, ItemEnhancementErrorCodes.ItemLocked, cancellationToken);
        if (item.TransactionLockId.HasValue)
            return await Fail(transaction, ItemEnhancementErrorCodes.ItemTransactionLocked, cancellationToken);
        if (!content.Indexes.ItemsById.TryGetValue(item.ItemDefinitionId, out ItemDefinition? definition))
            return await Fail(transaction, ItemEnhancementErrorCodes.ItemNotFound, cancellationToken);

        GeneratedItemInstance? current = ItemInstancePersistenceFactory.ToGeneratedInstance(
            item,
            definition,
            content.Package.Itemization);
        if (current is null)
            return await Fail(transaction, ItemEnhancementErrorCodes.ItemNotGenerated, cancellationToken);
        if (item.EnhancementLevel >= ItemEnhancementRules.MaxEnhancementLevel)
            return await Fail(transaction, ItemEnhancementErrorCodes.MaxEnhancement, cancellationToken);

        int targetLevel = item.EnhancementLevel + 1;
        if (!TryResolveCost(profile, targetLevel, out EnhancementCost cost))
            return await Fail(transaction, ItemEnhancementErrorCodes.ProfileMissing, cancellationToken);
        if (character.Gold < cost.Gold)
            return await Fail(transaction, ItemEnhancementErrorCodes.NotEnoughGold, cancellationToken);
        if (!await HasMaterial(character.Id, profile.ReforgeStoneItemId, cost.EnhancementMaterialQuantity, cancellationToken))
            return await Fail(transaction, ItemEnhancementErrorCodes.NotEnoughMaterial, cancellationToken);
        if (cost.CatalystQuantity > 0
            && (string.IsNullOrWhiteSpace(cost.CatalystItemId)
                || !await HasMaterial(character.Id, cost.CatalystItemId, cost.CatalystQuantity, cancellationToken)))
            return await Fail(transaction, ItemEnhancementErrorCodes.MissingCatalyst, cancellationToken);

        // Snapshot the complete generated-item classification before the external progression mutation.
        // These values are intentionally not used to rebuild the instance afterwards: they are birth history.
        int stars = current.Stars;
        decimal rollQuality = current.RollQuality;
        bool isPerfect = current.IsPerfect;
        string? perfectOrigin = current.PerfectOrigin;
        decimal? actualItemPower = current.ActualItemPower;
        string generatedName = current.GeneratedName;
        GeneratedItemAffix[] affixes = current.Affixes.ToArray();

        character.TrySpendGold(cost.Gold);
        await Consume(character.Id, profile.ReforgeStoneItemId, cost.EnhancementMaterialQuantity, cancellationToken);
        if (cost.CatalystQuantity > 0)
            await Consume(character.Id, cost.CatalystItemId!, cost.CatalystQuantity, cancellationToken);

        item.ApplyEnhancement(targetLevel);

        // Defensive invariant: enhancement has no authority over generated-instance identity/quality.
        GeneratedItemInstance unchanged = ItemInstancePersistenceFactory.ToGeneratedInstance(
            item,
            definition,
            content.Package.Itemization)
            ?? throw new InvalidOperationException("Generated item disappeared during enhancement.");
        if (unchanged.Stars != stars
            || unchanged.RollQuality != rollQuality
            || unchanged.IsPerfect != isPerfect
            || unchanged.PerfectOrigin != perfectOrigin
            || unchanged.ActualItemPower != actualItemPower
            || unchanged.GeneratedName != generatedName
            || !unchanged.Affixes.SequenceEqual(affixes))
        {
            throw new InvalidOperationException("Enhancement mutated intrinsic generated-item properties.");
        }

        dbContext.CharacterMutations.Add(new CharacterMutation(
            character.Id,
            mutationId,
            OperationType,
            fingerprint,
            timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CreateSuccess(item, unchanged, definition, content.Package.Itemization);
    }

    private static ItemEnhancementResult CreateSuccess(
        CharacterItem item,
        GeneratedItemInstance generated,
        ItemDefinition definition,
        ItemizationDefinition? itemization)
    {
        decimal? finalPower = generated.ActualItemPower.HasValue && itemization is not null
            ? ItemEnhancementRules.CalculateEnhancedItemPower(
                definition,
                generated.Affixes,
                generated.ActualItemPower,
                item.EnhancementLevel,
                itemization)
            : generated.ActualItemPower;

        return new ItemEnhancementResult(
            true,
            null,
            generated,
            item.EnhancementLevel,
            ItemEnhancementRules.ResolveBonusPercent(item.EnhancementLevel),
            finalPower);
    }

    private static string Fingerprint(string operationType, Guid itemId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{operationType}|{itemId:N}")));

    private static bool TryResolveCost(
        ItemStarUpgradeProfileDefinition profile,
        int targetEnhancementLevel,
        out EnhancementCost cost)
    {
        if (!profile.GoldByTargetStars.TryGetValue(targetEnhancementLevel, out int gold)
            || !profile.ReforgeStoneQuantityByTargetStars.TryGetValue(targetEnhancementLevel, out int materialQuantity))
        {
            cost = default;
            return false;
        }

        bool catalystRequired = targetEnhancementLevel >= 4 && profile.HighEndCatalystQuantity > 0;
        cost = new EnhancementCost(
            gold,
            materialQuantity,
            catalystRequired ? profile.HighEndCatalystItemId : null,
            catalystRequired ? profile.HighEndCatalystQuantity : 0);
        return true;
    }

    private async Task<LoadedItem?> LoadGeneratedAsync(
        Guid characterId,
        Guid itemId,
        GameContentSnapshot content,
        CancellationToken cancellationToken)
    {
        CharacterItem? item = await dbContext.CharacterItems.AsNoTracking()
            .Include(x => x.Affixes)
            .SingleOrDefaultAsync(x => x.Id == itemId && x.CharacterId == characterId, cancellationToken);
        if (item is null || !content.Indexes.ItemsById.TryGetValue(item.ItemDefinitionId, out ItemDefinition? definition))
            return null;

        GeneratedItemInstance? generated = ItemInstancePersistenceFactory.ToGeneratedInstance(
            item,
            definition,
            content.Package.Itemization);
        return generated is null ? null : new LoadedItem(item, definition, generated);
    }

    private async Task<bool> HasMaterial(Guid characterId, string itemId, int quantity, CancellationToken cancellationToken)
    {
        if (quantity <= 0)
            return true;

        int available = await dbContext.CharacterItems.AsNoTracking()
            .Where(x => x.CharacterId == characterId
                && x.ItemDefinitionId == itemId
                && !x.IsLocked
                && x.TransactionLockId == null)
            .SumAsync(x => x.Quantity, cancellationToken);
        return available >= quantity;
    }

    private async Task Consume(Guid characterId, string itemId, int quantity, CancellationToken cancellationToken)
    {
        foreach (CharacterItem stack in await dbContext.CharacterItems
                     .Where(x => x.CharacterId == characterId
                         && x.ItemDefinitionId == itemId
                         && !x.IsLocked
                         && x.TransactionLockId == null)
                     .OrderBy(x => x.AcquiredAtUtc)
                     .ToArrayAsync(cancellationToken))
        {
            if (quantity == 0)
                break;

            int take = Math.Min(quantity, stack.Quantity);
            stack.RemoveQuantity(take);
            quantity -= take;
            if (stack.Quantity == 0)
                dbContext.CharacterItems.Remove(stack);
        }

        if (quantity != 0)
            throw new InvalidOperationException("Material availability changed inside enhancement transaction.");
    }

    private static async Task<ItemEnhancementResult> Fail(
        IDbContextTransaction transaction,
        string code,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        return ItemEnhancementResult.Failure(code);
    }

    private readonly record struct EnhancementCost(
        int Gold,
        int EnhancementMaterialQuantity,
        string? CatalystItemId,
        int CatalystQuantity);

    private sealed record LoadedItem(
        CharacterItem Item,
        ItemDefinition Definition,
        GeneratedItemInstance Generated);
}
