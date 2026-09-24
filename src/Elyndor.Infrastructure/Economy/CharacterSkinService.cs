using System.Security.Cryptography;
using System.Text;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Economy;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Economy;

public sealed record CharacterSkinOffer(CharacterSkinDefinition Definition, bool Owned, bool Eligible);
public sealed record CharacterSkinStoreSnapshot(long CrystalBalance, string? ActiveSkinId, IReadOnlyList<CharacterSkinOffer> Skins);
public sealed record CharacterSkinMutationResult(bool Succeeded, string? ErrorCode, long CrystalBalance, string? ActiveSkinId)
{
    public static CharacterSkinMutationResult Fail(string code) => new(false, code, 0, null);
}

public sealed class CharacterSkinService(GameDbContext db, IContentSnapshotProvider contentProvider, TimeProvider timeProvider)
{
    public async Task<CharacterSkinStoreSnapshot?> GetAsync(Guid accountId, CancellationToken ct)
    {
        Character? character = await db.Characters.AsNoTracking().SingleOrDefaultAsync(item => item.AccountId == accountId, ct);
        if (character is null) return null;
        long balance = await db.CrystalWallets.AsNoTracking().Where(item => item.AccountId == accountId)
            .Select(item => (long?)item.Balance).SingleOrDefaultAsync(ct) ?? 0;
        string[] owned = await db.CharacterSkinOwnerships.AsNoTracking()
            .Where(item => item.CharacterId == character.Id).Select(item => item.SkinId).ToArrayAsync(ct);
        CharacterSkinOffer[] skins = (contentProvider.GetCurrent().Package.CharacterSkins ?? [])
            .Where(item => item.Enabled && item.ClassId == character.ClassId && item.GenderId == character.GenderId)
            .Select(item => new CharacterSkinOffer(item, !item.Purchasable || owned.Contains(item.Id), true))
            .ToArray();
        return new(balance, character.ActiveSkinId, skins);
    }

    public Task<CharacterSkinMutationResult> PurchaseAsync(Guid accountId, string skinId, Guid operationId, CancellationToken ct) =>
        operationId == Guid.Empty || string.IsNullOrWhiteSpace(skinId)
            ? Task.FromResult(CharacterSkinMutationResult.Fail("skin_invalid_operation"))
            : db.Database.CreateExecutionStrategy().ExecuteAsync(() => PurchaseCoreAsync(accountId, skinId, operationId, ct));

    private async Task<CharacterSkinMutationResult> PurchaseCoreAsync(Guid accountId, string skinId, Guid operationId, CancellationToken ct)
    {
        CharacterSkinDefinition? skin = contentProvider.GetCurrent().Indexes.CharacterSkinsById.GetValueOrDefault(skinId);
        if (skin is null || !skin.Enabled || !skin.Purchasable || skin.GenderId != "FEMALE")
            return CharacterSkinMutationResult.Fail("skin_not_for_sale");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        Character? character = await db.Characters.FromSqlInterpolated($"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (character is null) return CharacterSkinMutationResult.Fail("skin_character_not_found");
        CharacterSkinOwnership? replay = await db.CharacterSkinOwnerships.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == operationId, ct);
        if (replay is not null)
        {
            if (replay.CharacterId != character.Id || replay.SkinId != skinId)
                return CharacterSkinMutationResult.Fail("skin_operation_conflict");
            long replayBalance = await db.CrystalWallets.Where(item => item.AccountId == accountId)
                .Select(item => item.Balance).SingleAsync(ct);
            return new(true, null, replayBalance, character.ActiveSkinId);
        }
        if (skin.ClassId != character.ClassId || skin.GenderId != character.GenderId)
            return CharacterSkinMutationResult.Fail("skin_incompatible");
        if (await db.CharacterSkinOwnerships.AnyAsync(item => item.CharacterId == character.Id && item.SkinId == skinId, ct))
            return CharacterSkinMutationResult.Fail("skin_already_owned");
        CrystalWallet? wallet = await db.CrystalWallets.SingleOrDefaultAsync(item => item.AccountId == accountId, ct);
        if (wallet is null || !wallet.TryDebit(skin.CrystalPrice))
            return CharacterSkinMutationResult.Fail("skin_insufficient_crystals");
        db.CharacterSkinOwnerships.Add(new(character.Id, skin.Id, operationId, skin.CrystalPrice, timeProvider.GetUtcNow()));
        db.CrystalLedgerEntries.Add(new(Guid.CreateVersion7(), accountId, operationId,
            CrystalLedgerEntryType.StorePurchase, -skin.CrystalPrice, wallet.Balance,
            skin.Id, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(skin.Id))), timeProvider.GetUtcNow()));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(true, null, wallet.Balance, character.ActiveSkinId);
    }

    public Task<CharacterSkinMutationResult> EquipAsync(Guid accountId, string? skinId, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(() => EquipCoreAsync(accountId, skinId, ct));

    private async Task<CharacterSkinMutationResult> EquipCoreAsync(Guid accountId, string? skinId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        Character? character = await db.Characters.FromSqlInterpolated($"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (character is null) return CharacterSkinMutationResult.Fail("skin_character_not_found");
        if (skinId is not null)
        {
            CharacterSkinDefinition? skin = contentProvider.GetCurrent().Indexes.CharacterSkinsById.GetValueOrDefault(skinId);
            if (skin is null || !skin.Enabled || skin.ClassId != character.ClassId || skin.GenderId != character.GenderId)
                return CharacterSkinMutationResult.Fail("skin_incompatible");
            if (skin.Purchasable && !await db.CharacterSkinOwnerships.AnyAsync(
                    item => item.CharacterId == character.Id && item.SkinId == skinId, ct))
                return CharacterSkinMutationResult.Fail("skin_not_owned");
        }
        character.SelectSkin(skinId);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        long balance = await db.CrystalWallets.AsNoTracking().Where(item => item.AccountId == accountId)
            .Select(item => (long?)item.Balance).SingleOrDefaultAsync(ct) ?? 0;
        return new(true, null, balance, skinId);
    }
}
