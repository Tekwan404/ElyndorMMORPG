using System.Security.Cryptography;
using System.Text;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Economy;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Elyndor.Infrastructure.Economy;

public static class PromoCodeErrorCodes
{
    public const string InvalidRequest = "promo_code_invalid_request";
    public const string NotFound = "promo_code_not_found";
    public const string Disabled = "promo_code_disabled";
    public const string NotStarted = "promo_code_not_started";
    public const string Expired = "promo_code_expired";
    public const string AccountLimitReached = "promo_code_account_limit_reached";
    public const string GlobalLimitReached = "promo_code_global_limit_reached";
    public const string CharacterNotFound = "promo_code_character_not_found";
    public const string InventoryFull = "promo_code_inventory_full";
    public const string OperationConflict = "promo_code_operation_conflict";
    public const string Conflict = "promo_code_conflict";
}

public sealed record PromoCodeRedemptionResult(bool Succeeded, string? ErrorCode, Guid CharacterId, long CrystalBalance)
{
    public static PromoCodeRedemptionResult Failure(string code) => new(false, code, Guid.Empty, 0);
}

public sealed class PromoCodeService(GameDbContext dbContext, IContentSnapshotProvider contentProvider, TimeProvider timeProvider)
{
    public Task<PromoCodeRedemptionResult> RedeemAsync(Guid accountId, string? rawCode, Guid operationId, CancellationToken cancellationToken)
    {
        string? code = Normalize(rawCode);
        return accountId == Guid.Empty || operationId == Guid.Empty || code is null
            ? Task.FromResult(PromoCodeRedemptionResult.Failure(PromoCodeErrorCodes.InvalidRequest))
            : dbContext.Database.CreateExecutionStrategy().ExecuteAsync(() => RedeemCoreAsync(accountId, code, operationId, cancellationToken));
    }

    private async Task<PromoCodeRedemptionResult> RedeemCoreAsync(Guid accountId, string code, Guid operationId, CancellationToken cancellationToken)
    {
        PromoCodeDefinition? promo = contentProvider.GetCurrent().Indexes.PromoCodesByCode.GetValueOrDefault(code);
        if (promo is null) return PromoCodeRedemptionResult.Failure(PromoCodeErrorCodes.NotFound);
        string fingerprint = Fingerprint(code);
        dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({code}))", cancellationToken);
            Character? character = await dbContext.Characters.FromSqlInterpolated(
                $"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
            if (character is null) return PromoCodeRedemptionResult.Failure(PromoCodeErrorCodes.CharacterNotFound);
            PromoCodeRedemption? replay = await dbContext.PromoCodeRedemptions.AsNoTracking().SingleOrDefaultAsync(redemption => redemption.OperationId == operationId, cancellationToken);
            if (replay is not null)
            {
                if (replay.AccountId != accountId || replay.RequestFingerprint != fingerprint) return PromoCodeRedemptionResult.Failure(PromoCodeErrorCodes.OperationConflict);
                long replayBalance = await BalanceAsync(accountId, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new PromoCodeRedemptionResult(true, null, replay.CharacterId, replayBalance);
            }
            DateTimeOffset now = timeProvider.GetUtcNow();
            string? availabilityError = AvailabilityError(promo, now);
            if (availabilityError is not null) return PromoCodeRedemptionResult.Failure(availabilityError);
            if (promo.PerAccountRedemptionLimit is int perAccountLimit && await dbContext.PromoCodeRedemptions.CountAsync(redemption => redemption.AccountId == accountId && redemption.Code == code, cancellationToken) >= perAccountLimit)
                return PromoCodeRedemptionResult.Failure(PromoCodeErrorCodes.AccountLimitReached);
            if (promo.GlobalRedemptionLimit is int globalLimit && await dbContext.PromoCodeRedemptions.CountAsync(redemption => redemption.Code == code, cancellationToken) >= globalLimit)
                return PromoCodeRedemptionResult.Failure(PromoCodeErrorCodes.GlobalLimitReached);
            GameContentSnapshot content = contentProvider.GetCurrent();
            (ItemDefinition Definition, int Quantity)[] rewards = ResolveItemRewards(content, promo);
            if (!await CanGrantAsync(character.Id, rewards, content, cancellationToken)) return PromoCodeRedemptionResult.Failure(PromoCodeErrorCodes.InventoryFull);
            foreach ((ItemDefinition definition, int quantity) in rewards) await GrantAsync(character.Id, definition, quantity, cancellationToken);
            long balance = await CreditCrystalsAsync(accountId, operationId, code, promo.CrystalAmount, cancellationToken);
            dbContext.PromoCodeRedemptions.Add(new PromoCodeRedemption(operationId, accountId, character.Id, code, fingerprint, now));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new PromoCodeRedemptionResult(true, null, character.Id, balance);
        }
        catch (DbUpdateException exception) when (IsOperationConflict(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return await ResolveReplayAsync(accountId, operationId, fingerprint, cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return PromoCodeRedemptionResult.Failure(PromoCodeErrorCodes.Conflict);
        }
    }

    private async Task<long> CreditCrystalsAsync(Guid accountId, Guid operationId, string code, long amount, CancellationToken cancellationToken)
    {
        CrystalWallet? wallet = await dbContext.CrystalWallets.SingleOrDefaultAsync(item => item.AccountId == accountId, cancellationToken);
        if (amount <= 0) return wallet?.Balance ?? 0;
        wallet ??= new CrystalWallet(accountId);
        wallet.Credit(amount);
        if (dbContext.Entry(wallet).State == EntityState.Detached) dbContext.CrystalWallets.Add(wallet);
        dbContext.CrystalLedgerEntries.Add(new CrystalLedgerEntry(Guid.CreateVersion7(), accountId, operationId, CrystalLedgerEntryType.PromoCode, amount, wallet.Balance, code, Fingerprint(code), timeProvider.GetUtcNow()));
        return wallet.Balance;
    }

    private async Task<PromoCodeRedemptionResult> ResolveReplayAsync(Guid accountId, Guid operationId, string fingerprint, CancellationToken cancellationToken)
    {
        PromoCodeRedemption? replay = await dbContext.PromoCodeRedemptions.AsNoTracking().SingleOrDefaultAsync(redemption => redemption.OperationId == operationId, cancellationToken);
        if (replay is null) return PromoCodeRedemptionResult.Failure(PromoCodeErrorCodes.Conflict);
        if (replay.AccountId != accountId || replay.RequestFingerprint != fingerprint) return PromoCodeRedemptionResult.Failure(PromoCodeErrorCodes.OperationConflict);
        return new PromoCodeRedemptionResult(true, null, replay.CharacterId, await BalanceAsync(accountId, cancellationToken));
    }

    private static string? AvailabilityError(PromoCodeDefinition promo, DateTimeOffset now) => !promo.Enabled ? PromoCodeErrorCodes.Disabled
        : promo.StartsAtUtc is DateTimeOffset startsAt && now < startsAt ? PromoCodeErrorCodes.NotStarted
        : promo.ExpiresAtUtc is DateTimeOffset expiresAt && now >= expiresAt ? PromoCodeErrorCodes.Expired : null;

    private static (ItemDefinition Definition, int Quantity)[] ResolveItemRewards(GameContentSnapshot content, PromoCodeDefinition promo) =>
        (promo.ItemRewards ?? []).GroupBy(reward => reward.ItemDefinitionId, StringComparer.Ordinal).Select(group =>
            (content.Indexes.ItemsById[group.Key], group.Sum(reward => reward.Quantity))).ToArray();

    private async Task<bool> CanGrantAsync(Guid characterId, IReadOnlyList<(ItemDefinition Definition, int Quantity)> rewards, GameContentSnapshot content, CancellationToken cancellationToken)
    {
        int used = await InventoryCapacity.CountUsedSlotsAsync(dbContext, characterId, cancellationToken);
        int needed = 0;
        foreach ((ItemDefinition definition, int quantity) in rewards) needed += await InventoryCapacity.AdditionalSlotsRequiredAsync(dbContext, characterId, definition, quantity, cancellationToken);
        return used + needed <= InventoryCapacity.Resolve(content);
    }

    private async Task GrantAsync(Guid characterId, ItemDefinition definition, int quantity, CancellationToken cancellationToken)
    {
        CharacterItem[] stacks = await dbContext.CharacterItems.Where(item => item.CharacterId == characterId && item.ItemDefinitionId == definition.Id && item.DefinitionVersion == definition.Version && item.Quantity < definition.MaxStack).OrderBy(item => item.AcquiredAtUtc).ToArrayAsync(cancellationToken);
        int remaining = quantity;
        foreach (CharacterItem stack in stacks) { int added = Math.Min(definition.MaxStack - stack.Quantity, remaining); stack.AddQuantity(added, definition.MaxStack); remaining -= added; if (remaining == 0) return; }
        while (remaining > 0) { int size = Math.Min(definition.MaxStack, remaining); dbContext.CharacterItems.Add(new CharacterItem(Guid.CreateVersion7(), characterId, definition.Id, size, timeProvider.GetUtcNow(), definition.Version)); remaining -= size; }
    }

    private async Task<long> BalanceAsync(Guid accountId, CancellationToken cancellationToken) =>
        await dbContext.CrystalWallets.AsNoTracking().Where(wallet => wallet.AccountId == accountId)
            .Select(wallet => (long?)wallet.Balance).SingleOrDefaultAsync(cancellationToken) ?? 0;
    private static string? Normalize(string? value) { string normalized = value?.Trim().ToUpperInvariant() ?? string.Empty; return normalized.Length is > 0 and <= 64 && normalized[0] is >= 'A' and <= 'Z' && normalized.All(character => char.IsAsciiLetterOrDigit(character) || character == '_') ? normalized : null; }
    private static string Fingerprint(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"PROMO_CODE|{code}")));
    private static bool IsOperationConflict(DbUpdateException exception) => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "pk_promo_code_redemptions" };
}
