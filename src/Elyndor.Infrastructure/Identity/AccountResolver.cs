using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Elyndor.Infrastructure.Identity;

public sealed class AccountResolver(
    GameDbContext dbContext,
    TimeProvider timeProvider)
{
    private const string TelegramUserIdConstraint = "uq_accounts_telegram_user_id";

    private readonly GameDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public Task<Account> ResolveAsync(
        long telegramUserId,
        CancellationToken cancellationToken) =>
        ResolveAsync(telegramUserId, null, cancellationToken);

    public async Task<Account> ResolveAsync(
        long telegramUserId,
        string? telegramUsername,
        CancellationToken cancellationToken)
    {
        if (telegramUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(telegramUserId),
                "Telegram user ID must be positive.");
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        IExecutionStrategy executionStrategy = _dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(
            () => ResolveCoreAsync(
                telegramUserId,
                telegramUsername,
                now,
                cancellationToken));
    }

    private async Task<Account> ResolveCoreAsync(
        long telegramUserId,
        string? telegramUsername,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        _dbContext.ChangeTracker.Clear();

        await using IDbContextTransaction transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await SynchronizeUsernameOwnerAsync(telegramUserId, telegramUsername, cancellationToken);

        bool exists = await _dbContext.Accounts
            .AnyAsync(
                account => account.TelegramUserId == telegramUserId,
                cancellationToken);

        if (exists)
        {
            Account account = await TouchAndLoadAsync(
                telegramUserId,
                telegramUsername,
                now,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return account;
        }

        Account created = new(Guid.CreateVersion7(), telegramUserId, now);
        if (!string.IsNullOrWhiteSpace(telegramUsername))
            created.SetTelegramUsername(telegramUsername);
        _dbContext.Accounts.Add(created);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException exception) when (IsTelegramUserIdConflict(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            return await ResolveWinnerAsync(
                telegramUserId,
                telegramUsername,
                now,
                cancellationToken);
        }
    }

    private async Task<Account> ResolveWinnerAsync(
        long telegramUserId,
        string? telegramUsername,
        DateTimeOffset seenAtUtc,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await SynchronizeUsernameOwnerAsync(telegramUserId, telegramUsername, cancellationToken);
        Account account = await TouchAndLoadAsync(
            telegramUserId,
            telegramUsername,
            seenAtUtc,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return account;
    }

    private async Task<Account> TouchAndLoadAsync(
        long telegramUserId,
        string? telegramUsername,
        DateTimeOffset seenAtUtc,
        CancellationToken cancellationToken)
    {
        Account account = await _dbContext.Accounts
            .SingleAsync(
                candidate => candidate.TelegramUserId == telegramUserId,
                cancellationToken);
        account.RecordSeen(seenAtUtc);
        account.SetTelegramUsername(telegramUsername);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return account;
    }

    private async Task SynchronizeUsernameOwnerAsync(
        long telegramUserId,
        string? username,
        CancellationToken cancellationToken)
    {
        // Serialize account and username transfers together, including removal. Authentication
        // is infrequent; one transaction lock also avoids deadlocks when two users swap names.
        await _dbContext.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(hashtext('telegram-username-ownership'))", cancellationToken);
        if (string.IsNullOrWhiteSpace(username)) return;
        string normalized = TelegramUsernamePolicy.Normalize(username);
        await _dbContext.Accounts
            .Where(account => account.TelegramUserId != telegramUserId
                && account.NormalizedTelegramUsername == normalized)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(account => account.TelegramUsername, (string?)null)
                .SetProperty(account => account.NormalizedTelegramUsername, (string?)null), cancellationToken);
    }

    private static bool IsTelegramUserIdConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: TelegramUserIdConstraint
        };
}
