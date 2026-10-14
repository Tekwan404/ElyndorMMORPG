using System.Security.Cryptography;
using System.Text;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Economy;
using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Pvp;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Economy;

public sealed record ProfileCosmeticOffer(ProfileCosmeticDefinition Definition, bool Owned);
public sealed record PersonalizationStoreSnapshot(long CrystalBalance, string CharacterName,
    string? ActiveFrameId, string? ActiveTitleId, CharacterRenameOffer? Rename,
    IReadOnlyList<ProfileCosmeticOffer> Cosmetics);
public sealed record PersonalizationMutationResult(bool Succeeded, string? ErrorCode, long CrystalBalance = 0,
    string? CharacterName = null, string? ActiveFrameId = null, string? ActiveTitleId = null)
{
    public static PersonalizationMutationResult Fail(string code) => new(false, code);
}

public sealed partial class PersonalizationStoreService(GameDbContext db, IContentSnapshotProvider contentProvider, TimeProvider timeProvider,
    ArenaMatchRuntime? arenaRuntime = null, ArenaTestRegistry? testArena = null)
{
    public async Task<PersonalizationStoreSnapshot?> GetAsync(Guid accountId, CancellationToken ct)
    {
        Character? character = await db.Characters.AsNoTracking().SingleOrDefaultAsync(item => item.AccountId == accountId, ct);
        if (character is null) return null;
        string[] owned = await db.CharacterCosmeticOwnerships.AsNoTracking()
            .Where(item => item.CharacterId == character.Id).Select(item => item.CosmeticId).ToArrayAsync(ct);
        GameContentPackage content = contentProvider.GetCurrent().Package;
        return new(await BalanceAsync(accountId, ct), character.Name, character.ActiveProfileFrameId, character.ActiveTitleId,
            content.CharacterRename is { Enabled: true } rename ? rename : null,
            (content.ProfileCosmetics ?? []).Where(item => item.Enabled || owned.Contains(item.Id))
                .Select(item => new ProfileCosmeticOffer(item, owned.Contains(item.Id))).ToArray());
    }

    public Task<PersonalizationMutationResult> PurchaseAsync(Guid accountId, string cosmeticId, Guid operationId, CancellationToken ct) =>
        operationId == Guid.Empty || string.IsNullOrWhiteSpace(cosmeticId) || cosmeticId.Length > 64
            ? Task.FromResult(PersonalizationMutationResult.Fail("personalization_invalid_operation"))
            : db.Database.CreateExecutionStrategy().ExecuteAsync(() => PurchaseCoreAsync(accountId, cosmeticId, operationId, ct));

    private async Task<PersonalizationMutationResult> PurchaseCoreAsync(Guid accountId, string cosmeticId, Guid operationId, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        Character? character = await LockCharacterAsync(accountId, ct);
        if (character is null) return PersonalizationMutationResult.Fail("personalization_character_not_found");
        string reference = "cosmetic:" + cosmeticId;
        string fingerprint = Fingerprint(reference);
        CrystalLedgerEntry? replay = await ReceiptAsync(accountId, operationId, ct);
        if (replay is not null) return replay.Reference == reference && replay.RequestFingerprint == fingerprint
            ? await SuccessAsync(character, ct) : PersonalizationMutationResult.Fail("personalization_operation_conflict");
        ProfileCosmeticDefinition? cosmetic = FindCosmetic(cosmeticId);
        if (cosmetic is null || !cosmetic.Enabled) return PersonalizationMutationResult.Fail("cosmetic_not_for_sale");
        if (await db.CharacterCosmeticOwnerships.AnyAsync(item => item.CharacterId == character.Id && item.CosmeticId == cosmeticId, ct))
            return PersonalizationMutationResult.Fail("cosmetic_already_owned");
        CrystalWallet? wallet = await LockWalletAsync(accountId, ct);
        if (wallet is null || !wallet.TryDebit(cosmetic.CrystalPrice))
            return PersonalizationMutationResult.Fail("cosmetic_insufficient_crystals");
        db.CharacterCosmeticOwnerships.Add(new(character.Id, cosmeticId, cosmetic.CrystalPrice, timeProvider.GetUtcNow()));
        AddReceipt(accountId, operationId, reference, fingerprint, cosmetic.CrystalPrice, wallet.Balance);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await SuccessAsync(character, ct);
    }

    public Task<PersonalizationMutationResult> EquipAsync(Guid accountId, string kind, string? cosmeticId, CancellationToken ct) =>
        kind is not ("Frame" or "Title")
            ? Task.FromResult(PersonalizationMutationResult.Fail("cosmetic_invalid_slot"))
            : db.Database.CreateExecutionStrategy().ExecuteAsync(() => EquipCoreAsync(accountId, kind, cosmeticId, ct));

    private async Task<PersonalizationMutationResult> EquipCoreAsync(Guid accountId, string kind, string? cosmeticId, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        Character? character = await LockCharacterAsync(accountId, ct);
        if (character is null) return PersonalizationMutationResult.Fail("personalization_character_not_found");
        if (cosmeticId is not null)
        {
            ProfileCosmeticDefinition? cosmetic = FindCosmetic(cosmeticId);
            if (cosmetic is null || cosmetic.Kind != kind) return PersonalizationMutationResult.Fail("cosmetic_invalid_slot");
            if (!await db.CharacterCosmeticOwnerships.AnyAsync(item => item.CharacterId == character.Id && item.CosmeticId == cosmeticId, ct))
                return PersonalizationMutationResult.Fail("cosmetic_not_owned");
        }
        character.SelectProfileCosmetic(kind, cosmeticId);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await SuccessAsync(character, ct);
    }

    private ProfileCosmeticDefinition? FindCosmetic(string id) =>
        contentProvider.GetCurrent().Package.ProfileCosmetics?.FirstOrDefault(item => item.Id == id);
    private Task<Character?> LockCharacterAsync(Guid accountId, CancellationToken ct) =>
        db.Characters.FromSqlInterpolated($"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE").SingleOrDefaultAsync(ct);
    private Task<CrystalWallet?> LockWalletAsync(Guid accountId, CancellationToken ct) =>
        db.CrystalWallets.FromSqlInterpolated($"SELECT * FROM game.crystal_wallets WHERE \"AccountId\" = {accountId} FOR UPDATE").SingleOrDefaultAsync(ct);
    private Task<CrystalLedgerEntry?> ReceiptAsync(Guid accountId, Guid operationId, CancellationToken ct) =>
        db.CrystalLedgerEntries.AsNoTracking().SingleOrDefaultAsync(item => item.AccountId == accountId && item.OperationId == operationId, ct);
    private async Task<long> BalanceAsync(Guid accountId, CancellationToken ct) =>
        await db.CrystalWallets.AsNoTracking().Where(item => item.AccountId == accountId).Select(item => (long?)item.Balance).SingleOrDefaultAsync(ct) ?? 0;
    private async Task<PersonalizationMutationResult> SuccessAsync(Character character, CancellationToken ct) =>
        new(true, null, await BalanceAsync(character.AccountId, ct), character.Name, character.ActiveProfileFrameId, character.ActiveTitleId);
    private void AddReceipt(Guid accountId, Guid operationId, string reference, string fingerprint, long price, long balance) =>
        db.CrystalLedgerEntries.Add(new(Guid.CreateVersion7(), accountId, operationId, CrystalLedgerEntryType.StorePurchase,
            -price, balance, reference, fingerprint, timeProvider.GetUtcNow()));
    private static string Fingerprint(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
