using Elyndor.Core.Releases;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Elyndor.Infrastructure.Releases;

public sealed class ReleaseAcknowledgementService(
    GameDbContext dbContext,
    IReleaseNotesCatalog catalog,
    TimeProvider timeProvider)
{
    public Task<bool> HasAcknowledgedAsync(
        Guid accountId,
        string releaseId,
        CancellationToken cancellationToken) =>
        dbContext.AccountReleaseAcknowledgements
            .AsNoTracking()
            .AnyAsync(
                acknowledgement => acknowledgement.AccountId == accountId
                    && acknowledgement.ReleaseId == releaseId,
                cancellationToken);

    public async Task AcknowledgeAsync(
        Guid accountId,
        string releaseId,
        CancellationToken cancellationToken)
    {
        if (!catalog.Contains(releaseId))
            throw new ReleaseAcknowledgementException("release_not_found");

        if (await HasAcknowledgedAsync(accountId, releaseId, cancellationToken))
            return;

        dbContext.AccountReleaseAcknowledgements.Add(new AccountReleaseAcknowledgement(
            accountId,
            releaseId,
            timeProvider.GetUtcNow()));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateAcknowledgement(exception))
        {
            dbContext.ChangeTracker.Clear();
        }
    }

    private static bool IsDuplicateAcknowledgement(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "pk_account_release_acknowledgements"
        };
}

public sealed class ReleaseAcknowledgementException(string errorCode) : Exception(errorCode)
{
    public string ErrorCode { get; } = errorCode;
}
