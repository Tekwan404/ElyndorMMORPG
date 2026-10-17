using System.Security.Cryptography;
using System.Text;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.World;

public sealed record WorldPvpOffer(string Id, string Name, int RequiredLevel, long FieldHonorPrice, long TokenPrice);
public sealed record WorldPvpPurchaseResult(bool Succeeded, string? ErrorCode);

public sealed class WorldPvpShopService(GameDbContext db, IContentSnapshotProvider content, TimeProvider time)
{
    private const string Operation = "WORLD_PVP_SHOP_BUY";
    public async Task<IReadOnlyList<WorldPvpOffer>> OffersAsync(Guid accountId, CancellationToken ct)
    {
        var character = await db.Characters.AsNoTracking().SingleOrDefaultAsync(c => c.AccountId == accountId, ct);
        return character is null ? [] : content.GetCurrent().Package.Items!
            .Where(i => Available(i, character.ClassId)).OrderBy(i => i.RequiredLevel).ThenBy(i => i.HonorPrice)
            .Select(i => new WorldPvpOffer(i.Id, i.Name, i.RequiredLevel, i.HonorPrice, TokenPrice(i.HonorPrice))).ToArray();
    }

    public async Task<WorldPvpPurchaseResult> BuyAsync(Guid accountId, string itemId, bool tokens, Guid mutationId, CancellationToken ct)
    {
        if (mutationId == Guid.Empty) return new(false, "invalid_mutation_id");
        if (string.IsNullOrWhiteSpace(itemId) || itemId.Length > 128) return new(false, "item_not_for_sale");
        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{itemId}|{tokens}")));
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var character = await db.Characters.FromSqlInterpolated(
                $"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE").SingleOrDefaultAsync(ct);
            if (character is null) return new WorldPvpPurchaseResult(false, "character_not_found");
            var existing = await db.CharacterMutations.AsNoTracking().SingleOrDefaultAsync(m => m.CharacterId == character.Id && m.MutationId == mutationId, ct);
            if (existing is not null)
                return new WorldPvpPurchaseResult(existing.OperationType == Operation && existing.RequestFingerprint == fingerprint,
                    existing.OperationType == Operation && existing.RequestFingerprint == fingerprint ? null : "mutation_conflict");
            var snapshot = content.GetCurrent();
            if (!snapshot.Indexes.ItemsById.TryGetValue(itemId, out var item) || !Available(item, character.ClassId))
                return new WorldPvpPurchaseResult(false, "item_not_for_sale");
            if (character.Level < item.RequiredLevel) return new WorldPvpPurchaseResult(false, "required_level");
            if (!await InventoryCapacity.CanAddAsync(db, character.Id, item, 1, snapshot, ct))
                return new WorldPvpPurchaseResult(false, "inventory_full");
            var wallet = await db.WorldPvpWallets.SingleOrDefaultAsync(w => w.CharacterId == character.Id, ct);
            long price = tokens ? TokenPrice(item.HonorPrice) : item.HonorPrice;
            if (wallet is null || !wallet.TrySpend(price, tokens)) return new WorldPvpPurchaseResult(false, "not_enough_currency");
            var now = time.GetUtcNow();
            db.CharacterItems.Add(ItemInstancePersistenceFactory.CreateCharacterItem(character.Id, item, mutationId,
                "WORLD_PVP_SHOP", item.Id, 0, now, snapshot.Package));
            db.CharacterMutations.Add(new(character.Id, mutationId, Operation, fingerprint, now));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new WorldPvpPurchaseResult(true, null);
        });
    }

    private static bool Available(ItemDefinition item, string classId) => item.Type == ItemType.Equipment && item.HonorPrice > 0
        && (item.AllowedClassIds is not { Count: > 0 } || item.AllowedClassIds.Contains(classId, StringComparer.Ordinal));
    private static long TokenPrice(long honor) => (honor - 1) / WorldPvpBalance.TokenHonorValue + 1;
}
