using Elyndor.Core.Characters;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Elyndor.Infrastructure.Economy;

public sealed partial class PersonalizationStoreService
{
    public Task<PersonalizationMutationResult> RenameAsync(Guid accountId, string name, Guid operationId, CancellationToken ct) =>
        operationId == Guid.Empty || string.IsNullOrWhiteSpace(name) || name.Length > 100
            ? Task.FromResult(PersonalizationMutationResult.Fail("personalization_invalid_operation"))
            : db.Database.CreateExecutionStrategy().ExecuteAsync(() => RenameCoreAsync(accountId, name, operationId, ct));

    private async Task<PersonalizationMutationResult> RenameCoreAsync(Guid accountId, string name, Guid operationId, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        Character? character = await LockCharacterAsync(accountId, ct);
        if (character is null) return PersonalizationMutationResult.Fail("personalization_character_not_found");
        string fingerprint = Fingerprint("rename:" + name);
        var replay = await ReceiptAsync(accountId, operationId, ct);
        if (replay is not null) return replay.Reference == "rename" && replay.RequestFingerprint == fingerprint
            ? await SuccessAsync(character, ct) : PersonalizationMutationResult.Fail("personalization_operation_conflict");
        var offer = contentProvider.GetCurrent().Package.CharacterRename;
        if (offer is null || !offer.Enabled) return PersonalizationMutationResult.Fail("rename_unavailable");
        CharacterNameValidationResult validation = CharacterNamePolicy.Validate(name);
        if (!validation.IsValid) return PersonalizationMutationResult.Fail(validation.ErrorCode!);
        if (character.NormalizedName == validation.NormalizedName) return PersonalizationMutationResult.Fail("rename_same_name");
        if (arenaRuntime?.IsInMatch(accountId) == true || testArena?.Get(accountId) is not null
            || await db.ActiveCombatSessions.AnyAsync(item => item.CharacterId == character.Id, ct))
            return PersonalizationMutationResult.Fail("personalization_in_combat");
        if (await db.Characters.AsNoTracking().AnyAsync(item => item.NormalizedName == validation.NormalizedName && item.Id != character.Id, ct))
            return PersonalizationMutationResult.Fail("rename_name_taken");
        var wallet = await LockWalletAsync(accountId, ct);
        if (wallet is null || !wallet.TryDebit(offer.CrystalPrice))
            return PersonalizationMutationResult.Fail("cosmetic_insufficient_crystals");
        character.Rename(validation.DisplayName!, validation.NormalizedName!);
        AddReceipt(accountId, operationId, "rename", fingerprint, offer.CrystalPrice, wallet.Balance);
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "uq_characters_normalized_name" })
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return PersonalizationMutationResult.Fail("rename_name_taken");
        }
        return await SuccessAsync(character, ct);
    }
}
