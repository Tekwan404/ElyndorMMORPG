using System.Security.Cryptography;
using System.Text;
using Elyndor.Core.Economy;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Elyndor.Infrastructure.Economy;

public static class CrystalWalletErrorCodes
{
    public const string AccountNotFound = "crystal_account_not_found";
    public const string InvalidOperation = "crystal_operation_invalid";
    public const string InsufficientBalance = "crystal_insufficient_balance";
    public const string OperationConflict = "crystal_operation_conflict";
    public const string Conflict = "crystal_conflict";
}

public sealed record CrystalWalletSnapshot(long Balance);

public sealed record CrystalWalletOperationResult(bool Succeeded, string? ErrorCode, long Balance)
{
    public static CrystalWalletOperationResult Success(long balance) => new(true, null, balance);
    public static CrystalWalletOperationResult Failure(string errorCode, long balance = 0) => new(false, errorCode, balance);
}

public sealed class CrystalWalletService(GameDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<CrystalWalletSnapshot> GetAsync(Guid accountId, CancellationToken cancellationToken)
    {
        long? balance = await dbContext.CrystalWallets.AsNoTracking()
            .Where(wallet => wallet.AccountId == accountId)
            .Select(wallet => (long?)wallet.Balance)
            .SingleOrDefaultAsync(cancellationToken);
        return new CrystalWalletSnapshot(balance ?? 0);
    }

    public Task<CrystalWalletOperationResult> GrantAsync(
        Guid accountId, Guid operationId, CrystalLedgerEntryType entryType, long amount, string reference,
        CancellationToken cancellationToken) =>
        ExecuteAsync(accountId, operationId, entryType, amount, reference, isCredit: true, cancellationToken);

    public Task<CrystalWalletOperationResult> SpendAsync(
        Guid accountId, Guid operationId, CrystalLedgerEntryType entryType, long amount, string reference,
        CancellationToken cancellationToken) =>
        ExecuteAsync(accountId, operationId, entryType, amount, reference, isCredit: false, cancellationToken);

    private Task<CrystalWalletOperationResult> ExecuteAsync(
        Guid accountId, Guid operationId, CrystalLedgerEntryType entryType, long amount, string reference,
        bool isCredit, CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty || operationId == Guid.Empty || amount <= 0 || string.IsNullOrWhiteSpace(reference)
            || reference.Length > 128 || !IsAllowed(entryType, isCredit))
            return Task.FromResult(CrystalWalletOperationResult.Failure(CrystalWalletErrorCodes.InvalidOperation));

        return dbContext.Database.CreateExecutionStrategy().ExecuteAsync(() =>
            ExecuteCoreAsync(accountId, operationId, entryType, amount, reference, isCredit, cancellationToken));
    }

    private async Task<CrystalWalletOperationResult> ExecuteCoreAsync(
        Guid accountId, Guid operationId, CrystalLedgerEntryType entryType, long amount, string reference,
        bool isCredit, CancellationToken cancellationToken)
    {
        string fingerprint = Fingerprint(entryType, amount, reference, isCredit);
        dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            Account? account = await dbContext.Accounts.FromSqlInterpolated(
                $"SELECT * FROM game.accounts WHERE \"Id\" = {accountId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (account is null) return CrystalWalletOperationResult.Failure(CrystalWalletErrorCodes.AccountNotFound);

            CrystalLedgerEntry? replay = await dbContext.CrystalLedgerEntries.AsNoTracking()
                .SingleOrDefaultAsync(entry => entry.AccountId == accountId && entry.OperationId == operationId, cancellationToken);
            if (replay is not null)
            {
                if (replay.RequestFingerprint != fingerprint) return CrystalWalletOperationResult.Failure(CrystalWalletErrorCodes.OperationConflict);
                await transaction.CommitAsync(cancellationToken);
                return CrystalWalletOperationResult.Success(replay.BalanceAfter);
            }

            CrystalWallet? wallet = await dbContext.CrystalWallets.SingleOrDefaultAsync(
                candidate => candidate.AccountId == accountId, cancellationToken);
            if (wallet is null && !isCredit)
                return CrystalWalletOperationResult.Failure(CrystalWalletErrorCodes.InsufficientBalance);
            wallet ??= new CrystalWallet(accountId);
            if (isCredit) wallet.Credit(amount);
            else if (!wallet.TryDebit(amount)) return CrystalWalletOperationResult.Failure(CrystalWalletErrorCodes.InsufficientBalance, wallet.Balance);

            if (dbContext.Entry(wallet).State == EntityState.Detached) dbContext.CrystalWallets.Add(wallet);
            long delta = isCredit ? amount : -amount;
            dbContext.CrystalLedgerEntries.Add(new CrystalLedgerEntry(
                Guid.CreateVersion7(), accountId, operationId, entryType, delta, wallet.Balance, reference,
                fingerprint, timeProvider.GetUtcNow()));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return CrystalWalletOperationResult.Success(wallet.Balance);
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
            return CrystalWalletOperationResult.Failure(CrystalWalletErrorCodes.Conflict);
        }
    }

    private async Task<CrystalWalletOperationResult> ResolveReplayAsync(
        Guid accountId, Guid operationId, string fingerprint, CancellationToken cancellationToken)
    {
        CrystalLedgerEntry? replay = await dbContext.CrystalLedgerEntries.AsNoTracking().SingleOrDefaultAsync(
            entry => entry.AccountId == accountId && entry.OperationId == operationId, cancellationToken);
        if (replay is null) return CrystalWalletOperationResult.Failure(CrystalWalletErrorCodes.Conflict);
        return replay.RequestFingerprint == fingerprint
            ? CrystalWalletOperationResult.Success(replay.BalanceAfter)
            : CrystalWalletOperationResult.Failure(CrystalWalletErrorCodes.OperationConflict);
    }

    private static bool IsAllowed(CrystalLedgerEntryType entryType, bool isCredit) => isCredit
        ? entryType is CrystalLedgerEntryType.Payment or CrystalLedgerEntryType.PromoCode or CrystalLedgerEntryType.AdminGrant or CrystalLedgerEntryType.Refund
        : entryType == CrystalLedgerEntryType.StorePurchase;

    private static string Fingerprint(CrystalLedgerEntryType entryType, long amount, string reference, bool isCredit) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{entryType}|{amount}|{reference}|{isCredit}")));

    private static bool IsOperationConflict(DbUpdateException exception) => exception.InnerException is PostgresException
    {
        SqlState: PostgresErrorCodes.UniqueViolation,
        ConstraintName: "uq_crystal_ledger_entries_account_operation"
    };
}
