using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elyndor.Core.Content;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Content;

public sealed class ContentRevisionStore(
    GameDbContext dbContext,
    TimeProvider timeProvider)
{
    public Task<ContentRevision> CreateRevisionAsync(
        GameContentPackage package,
        string payloadJson,
        string actor,
        string? note,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);
        ValidateJson(payloadJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using IDbContextTransaction transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);

            DateTimeOffset now = timeProvider.GetUtcNow();
            string payloadSha256 = ComputeSha256(payloadJson);
            ContentRevision revision = new(
                Guid.CreateVersion7(),
                package.ContentVersion,
                package.BalanceVersion,
                package.PublishedAtUtc,
                payloadJson,
                payloadSha256,
                now,
                actor.Trim(),
                note);
            ContentAuditEntry audit = new(
                Guid.CreateVersion7(),
                ContentAuditActions.RevisionCreated,
                revision.Id,
                null,
                actor.Trim(),
                now,
                JsonSerializer.Serialize(new
                {
                    contentVersion = revision.ContentVersion,
                    balanceVersion = revision.BalanceVersion,
                    payloadSha256 = revision.PayloadSha256,
                    note = revision.Note
                }));

            dbContext.ContentRevisions.Add(revision);
            dbContext.ContentAuditEntries.Add(audit);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return revision;
        });
    }

    public Task<ContentRelease?> PublishAsync(
        Guid revisionId,
        string actor,
        string? note,
        CancellationToken cancellationToken)
    {
        if (revisionId == Guid.Empty)
            throw new ArgumentException("Revision id cannot be empty.", nameof(revisionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync<ContentRelease?>(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using IDbContextTransaction transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);

            bool exists = await dbContext.ContentRevisions
                .AsNoTracking()
                .AnyAsync(revision => revision.Id == revisionId, cancellationToken);
            if (!exists)
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            ContentRelease release = new(
                Guid.CreateVersion7(),
                revisionId,
                now,
                actor.Trim(),
                note);
            ContentAuditEntry audit = new(
                Guid.CreateVersion7(),
                ContentAuditActions.ReleasePublished,
                revisionId,
                release.Id,
                actor.Trim(),
                now,
                JsonSerializer.Serialize(new
                {
                    revisionId,
                    note = release.Note
                }));

            dbContext.ContentReleases.Add(release);
            dbContext.ContentAuditEntries.Add(audit);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return release;
        });
    }

    public Task<ContentRevision?> GetRevisionAsync(
        Guid revisionId,
        CancellationToken cancellationToken) =>
        dbContext.ContentRevisions
            .AsNoTracking()
            .SingleOrDefaultAsync(revision => revision.Id == revisionId, cancellationToken);

    public Task<ContentRelease?> GetLatestReleaseAsync(
        CancellationToken cancellationToken) =>
        dbContext.ContentReleases
            .AsNoTracking()
            .OrderByDescending(release => release.PublishedAtUtc)
            .ThenByDescending(release => release.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ContentRelease?> GetReleaseAsync(
        Guid releaseId,
        CancellationToken cancellationToken)
    {
        if (releaseId == Guid.Empty)
            throw new ArgumentException("Release id cannot be empty.", nameof(releaseId));

        return dbContext.ContentReleases
            .AsNoTracking()
            .SingleOrDefaultAsync(release => release.Id == releaseId, cancellationToken);
    }

    public async Task<IReadOnlyList<ContentRevision>> GetRecentRevisionsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        int take = Math.Clamp(limit, 1, 100);
        return await dbContext.ContentRevisions
            .AsNoTracking()
            .OrderByDescending(revision => revision.CreatedAtUtc)
            .ThenByDescending(revision => revision.Id)
            .Take(take)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContentRelease>> GetRecentReleasesAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        int take = Math.Clamp(limit, 1, 100);
        return await dbContext.ContentReleases
            .AsNoTracking()
            .OrderByDescending(release => release.PublishedAtUtc)
            .ThenByDescending(release => release.Id)
            .Take(take)
            .ToArrayAsync(cancellationToken);
    }

    private static void ValidateJson(string payloadJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        using JsonDocument _ = JsonDocument.Parse(payloadJson);
    }

    private static string ComputeSha256(string payloadJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));
}
