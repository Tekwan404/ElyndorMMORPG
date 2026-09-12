using System.Security.Cryptography;
using System.Text;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Items;

public static class ItemStarUpgradeErrorCodes
{
    public const string CharacterNotFound = "star_upgrade_character_not_found";
    public const string ItemNotFound = "star_upgrade_item_not_found";
    public const string ItemNotGenerated = "star_upgrade_item_not_generated";
    public const string ItemLocked = "star_upgrade_item_locked";
    public const string ItemEquipped = "star_upgrade_item_equipped";
    public const string ItemTransactionLocked = "star_upgrade_item_transaction_locked";
    public const string MaxStars = "star_upgrade_max_stars";
    public const string ProfileMissing = "star_upgrade_profile_missing";
    public const string NotEnoughGold = "star_upgrade_not_enough_gold";
    public const string NotEnoughStones = "star_upgrade_not_enough_stones";
    public const string MissingCatalyst = "star_upgrade_missing_catalyst";
    public const string MutationConflict = "star_upgrade_mutation_conflict";
    public const string Conflict = "star_upgrade_conflict";
}

public sealed record ItemStarUpgradeResult(bool Succeeded, string? ErrorCode, GeneratedItemInstance? Item)
{
    public static ItemStarUpgradeResult Failure(string code) => new(false, code, null);
}

public sealed record ItemStarUpgradePreviewResult(
    bool Succeeded,
    string? ErrorCode,
    Guid ItemInstanceId,
    int TargetStars,
    int Gold,
    string ReforgeStoneItemId,
    int ReforgeStoneQuantity,
    string? CatalystItemId,
    int CatalystQuantity)
{
    public static ItemStarUpgradePreviewResult Failure(string code) =>
        new(false, code, Guid.Empty, 0, 0, string.Empty, 0, null, 0);
}

public sealed class ItemStarUpgradeService(
    GameDbContext dbContext,
    IContentSnapshotProvider contentProvider,
    TimeProvider timeProvider)
{
    private const string OperationType = "ITEM_STAR_UPGRADE";

    public Task<ItemStarUpgradeResult> UpgradeAsync(Guid accountId, Guid itemId, Guid mutationId, CancellationToken cancellationToken) =>
        mutationId == Guid.Empty
            ? Task.FromResult(ItemStarUpgradeResult.Failure(ItemStarUpgradeErrorCodes.MutationConflict))
            : dbContext.Database.CreateExecutionStrategy().ExecuteAsync(() => UpgradeCoreAsync(accountId, itemId, mutationId, cancellationToken));

    public async Task<ItemStarUpgradePreviewResult> GetPreviewAsync(
        Guid accountId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        GameContentSnapshot content = contentProvider.GetCurrent();
        ItemStarUpgradeProfileDefinition? profile = content.Package.Itemization?.StarUpgrades;
        if (profile is null)
            return ItemStarUpgradePreviewResult.Failure(ItemStarUpgradeErrorCodes.ProfileMissing);

        Character? character = await dbContext.Characters.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.AccountId == accountId, cancellationToken);
        if (character is null)
            return ItemStarUpgradePreviewResult.Failure(ItemStarUpgradeErrorCodes.CharacterNotFound);

        CharacterItem? item = await dbContext.CharacterItems.AsNoTracking()
            .Include(candidate => candidate.Affixes)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == itemId && candidate.CharacterId == character.Id,
                cancellationToken);
        if (item is null)
            return ItemStarUpgradePreviewResult.Failure(ItemStarUpgradeErrorCodes.ItemNotFound);
        if (item.IsLocked)
            return ItemStarUpgradePreviewResult.Failure(ItemStarUpgradeErrorCodes.ItemLocked);
        if (item.TransactionLockId.HasValue)
            return ItemStarUpgradePreviewResult.Failure(ItemStarUpgradeErrorCodes.ItemTransactionLocked);
        if (!content.Indexes.ItemsById.TryGetValue(item.ItemDefinitionId, out ItemDefinition? definition))
            return ItemStarUpgradePreviewResult.Failure(ItemStarUpgradeErrorCodes.ItemNotFound);

        GeneratedItemInstance? current = ItemInstancePersistenceFactory.ToGeneratedInstance(item, definition);
        if (current is null)
            return ItemStarUpgradePreviewResult.Failure(ItemStarUpgradeErrorCodes.ItemNotGenerated);
        if (current.Stars >= 5)
            return ItemStarUpgradePreviewResult.Failure(ItemStarUpgradeErrorCodes.MaxStars);

        int targetStars = current.Stars + 1;
        if (!TryResolveCost(profile, targetStars, out StarUpgradeCost cost))
            return ItemStarUpgradePreviewResult.Failure(ItemStarUpgradeErrorCodes.ProfileMissing);

        return new ItemStarUpgradePreviewResult(
            true,
            null,
            item.Id,
            targetStars,
            cost.Gold,
            profile.ReforgeStoneItemId,
            cost.ReforgeStoneQuantity,
            cost.CatalystItemId,
            cost.CatalystQuantity);
    }

    private async Task<ItemStarUpgradeResult> UpgradeCoreAsync(Guid accountId, Guid itemId, Guid mutationId, CancellationToken cancellationToken)
    {
        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{OperationType}|{itemId:N}")));
        GameContentSnapshot content = contentProvider.GetCurrent();
        ItemStarUpgradeProfileDefinition? profile = content.Package.Itemization?.StarUpgrades;
        if (profile is null) return ItemStarUpgradeResult.Failure(ItemStarUpgradeErrorCodes.ProfileMissing);
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        Character? character = await dbContext.Characters.FromSqlInterpolated($"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (character is null) return await Fail(transaction, ItemStarUpgradeErrorCodes.CharacterNotFound, cancellationToken);
        CharacterMutation? replay = await dbContext.CharacterMutations.AsNoTracking().SingleOrDefaultAsync(x => x.CharacterId == character.Id && x.MutationId == mutationId, cancellationToken);
        if (replay is not null)
        {
            if (replay.OperationType != OperationType || replay.RequestFingerprint != fingerprint) return await Fail(transaction, ItemStarUpgradeErrorCodes.MutationConflict, cancellationToken);
            GeneratedItemInstance? existing = await LoadGeneratedAsync(character.Id, itemId, content, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return existing is null ? ItemStarUpgradeResult.Failure(ItemStarUpgradeErrorCodes.ItemNotFound) : new(true, null, existing);
        }

        CharacterItem? item = await dbContext.CharacterItems.Include(x => x.Affixes).SingleOrDefaultAsync(x => x.Id == itemId && x.CharacterId == character.Id, cancellationToken);
        if (item is null) return await Fail(transaction, ItemStarUpgradeErrorCodes.ItemNotFound, cancellationToken);
        if (item.IsLocked) return await Fail(transaction, ItemStarUpgradeErrorCodes.ItemLocked, cancellationToken);
        if (item.TransactionLockId.HasValue) return await Fail(transaction, ItemStarUpgradeErrorCodes.ItemTransactionLocked, cancellationToken);
        if (!content.Indexes.ItemsById.TryGetValue(item.ItemDefinitionId, out ItemDefinition? definition))
            return await Fail(transaction, ItemStarUpgradeErrorCodes.ItemNotFound, cancellationToken);
        GeneratedItemInstance? current = ItemInstancePersistenceFactory.ToGeneratedInstance(item, definition);
        if (current is null || content.Package.Itemization is not { } itemization) return await Fail(transaction, ItemStarUpgradeErrorCodes.ItemNotGenerated, cancellationToken);
        if (current.Stars >= 5) return await Fail(transaction, ItemStarUpgradeErrorCodes.MaxStars, cancellationToken);
        int targetStars = current.Stars + 1;
        if (!TryResolveCost(profile, targetStars, out StarUpgradeCost cost)) return await Fail(transaction, ItemStarUpgradeErrorCodes.ProfileMissing, cancellationToken);
        if (character.Gold < cost.Gold) return await Fail(transaction, ItemStarUpgradeErrorCodes.NotEnoughGold, cancellationToken);
        if (!await HasMaterial(character.Id, profile.ReforgeStoneItemId, cost.ReforgeStoneQuantity, cancellationToken)) return await Fail(transaction, ItemStarUpgradeErrorCodes.NotEnoughStones, cancellationToken);
        if (cost.CatalystQuantity > 0 && (string.IsNullOrWhiteSpace(cost.CatalystItemId) || !await HasMaterial(character.Id, cost.CatalystItemId, cost.CatalystQuantity, cancellationToken))) return await Fail(transaction, ItemStarUpgradeErrorCodes.MissingCatalyst, cancellationToken);

        GeneratedItemInstance recalculated = ItemInstanceGenerator.Recalculate(
            definition,
            itemization,
            current.ItemLevel,
            ItemStarUpgradeCalculator.IncreaseToTargetStar(current.Affixes, targetStars),
            perfectOrigin: "STAR_UPGRADE");

        // Drop stars describe how much of the original random item budget happened to roll.
        // Forge stars are explicit progression: the player pays to advance exactly one tier
        // while preserving the existing affix identities/count. Sparse legal rolls can never
        // reach the next natural drop-quality threshold through Recalculate alone, so do not
        // reject a valid upgrade just because that classifier remains below the target tier.
        GeneratedItemInstance upgraded = recalculated with { Stars = targetStars };

        character.TrySpendGold(cost.Gold);
        await Consume(character.Id, profile.ReforgeStoneItemId, cost.ReforgeStoneQuantity, cancellationToken);
        if (cost.CatalystQuantity > 0) await Consume(character.Id, cost.CatalystItemId!, cost.CatalystQuantity, cancellationToken);
        item.ApplyStarUpgrade(upgraded);
        dbContext.CharacterMutations.Add(new CharacterMutation(character.Id, mutationId, OperationType, fingerprint, timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, null, upgraded);
    }

    private static bool TryResolveCost(
        ItemStarUpgradeProfileDefinition profile,
        int targetStars,
        out StarUpgradeCost cost)
    {
        if (!profile.GoldByTargetStars.TryGetValue(targetStars, out int gold)
            || !profile.ReforgeStoneQuantityByTargetStars.TryGetValue(targetStars, out int stones))
        {
            cost = default;
            return false;
        }

        bool catalystRequired = targetStars == 5 && profile.HighEndCatalystQuantity > 0;
        cost = new StarUpgradeCost(
            gold,
            stones,
            catalystRequired ? profile.HighEndCatalystItemId : null,
            catalystRequired ? profile.HighEndCatalystQuantity : 0);
        return true;
    }

    private async Task<GeneratedItemInstance?> LoadGeneratedAsync(Guid characterId, Guid itemId, GameContentSnapshot content, CancellationToken ct)
    {
        CharacterItem? item = await dbContext.CharacterItems.AsNoTracking().Include(x => x.Affixes).SingleOrDefaultAsync(x => x.Id == itemId && x.CharacterId == characterId, ct);
        return item is not null && content.Indexes.ItemsById.TryGetValue(item.ItemDefinitionId, out ItemDefinition? definition) ? ItemInstancePersistenceFactory.ToGeneratedInstance(item, definition) : null;
    }
    private async Task<bool> HasMaterial(Guid characterId, string itemId, int quantity, CancellationToken ct)
    {
        if (quantity <= 0) return true;
        int available = await dbContext.CharacterItems.AsNoTracking()
            .Where(x => x.CharacterId == characterId && x.ItemDefinitionId == itemId && !x.IsLocked && x.TransactionLockId == null)
            .SumAsync(x => x.Quantity, ct);
        return available >= quantity;
    }
    private async Task Consume(Guid characterId, string itemId, int quantity, CancellationToken ct)
    {
        foreach (CharacterItem stack in await dbContext.CharacterItems.Where(x => x.CharacterId == characterId && x.ItemDefinitionId == itemId && !x.IsLocked && x.TransactionLockId == null).OrderBy(x => x.AcquiredAtUtc).ToArrayAsync(ct))
        { if (quantity == 0) break; int take = Math.Min(quantity, stack.Quantity); stack.RemoveQuantity(take); quantity -= take; if (stack.Quantity == 0) dbContext.CharacterItems.Remove(stack); }
        if (quantity != 0) throw new InvalidOperationException("Material availability changed inside star upgrade transaction.");
    }
    private static async Task<ItemStarUpgradeResult> Fail(IDbContextTransaction transaction, string code, CancellationToken ct) { await transaction.RollbackAsync(ct); return ItemStarUpgradeResult.Failure(code); }

    private readonly record struct StarUpgradeCost(
        int Gold,
        int ReforgeStoneQuantity,
        string? CatalystItemId,
        int CatalystQuantity);
}
