using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Combat;

internal static class CombatRecoveryContent
{
    public static Task<Guid> ArchiveAsync(GameDbContext db, GameContentSnapshot content,
        DateTimeOffset archivedAtUtc, CancellationToken cancellationToken)
    {
        string payload = GameContentPackageCodec.SerializeCanonical(content.Package);
        string hash = GameContentPackageCodec.ComputeSha256(payload);
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({$"combat-content:{hash}"}))", cancellationToken);
            Guid? existing = await db.ContentRevisions.AsNoTracking()
                .Where(revision => revision.PayloadSha256 == hash)
                .OrderBy(revision => revision.Id).Select(revision => (Guid?)revision.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (existing.HasValue)
            {
                await transaction.CommitAsync(cancellationToken);
                return existing.Value;
            }
            ContentRevision revision = new(Guid.CreateVersion7(), content.ContentVersion,
                content.BalanceVersion, content.PublishedAtUtc, payload, hash, archivedAtUtc,
                "combat-runtime", "Immutable content pinned for terminal combat recovery.");
            db.ContentRevisions.Add(revision);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return revision.Id;
            }
            finally
            {
                db.Entry(revision).State = EntityState.Detached;
            }
        });
    }

    public static async Task<GameContentSnapshot> LoadAsync(GameDbContext db, Guid revisionId,
        string contentVersion, string balanceVersion, CancellationToken cancellationToken)
    {
        ContentRevision revision = await db.ContentRevisions.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == revisionId, cancellationToken)
            ?? throw new InvalidDataException($"Combat content revision '{revisionId}' is missing.");
        if (GameContentPackageCodec.ComputeSha256(revision.PayloadJson) != revision.PayloadSha256)
            throw new InvalidDataException($"Combat content revision '{revisionId}' failed integrity validation.");
        GameContentPackage package = GameContentPackageCodec.DeserializeValidated(revision.PayloadJson);
        if (package.ContentVersion != contentVersion || package.BalanceVersion != balanceVersion)
            throw new InvalidDataException("Combat content identity does not match its recovery journal.");
        return GameContentSnapshot.Create(package);
    }
}
