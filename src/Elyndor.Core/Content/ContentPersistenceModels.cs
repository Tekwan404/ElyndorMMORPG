namespace Elyndor.Core.Content;

public static class ContentAuditActions
{
    public const string RevisionCreated = "REVISION_CREATED";
    public const string ReleasePublished = "RELEASE_PUBLISHED";
}

public sealed class ContentRevision
{
    private ContentRevision()
    {
        ContentVersion = null!;
        BalanceVersion = null!;
        PayloadJson = null!;
        PayloadSha256 = null!;
        CreatedBy = null!;
    }

    public ContentRevision(
        Guid id,
        string contentVersion,
        string balanceVersion,
        DateTimeOffset sourcePublishedAtUtc,
        string payloadJson,
        string payloadSha256,
        DateTimeOffset createdAtUtc,
        string createdBy,
        string? note)
    {
        if (id == Guid.Empty) throw new ArgumentException("Revision id cannot be empty.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(contentVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(balanceVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        if (payloadSha256.Length != 64)
            throw new ArgumentException("Content payload SHA-256 must be a 64-character hex string.", nameof(payloadSha256));

        Id = id;
        ContentVersion = contentVersion;
        BalanceVersion = balanceVersion;
        SourcePublishedAtUtc = sourcePublishedAtUtc;
        PayloadJson = payloadJson;
        PayloadSha256 = payloadSha256;
        CreatedAtUtc = createdAtUtc;
        CreatedBy = createdBy;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public Guid Id { get; private set; }
    public string ContentVersion { get; private set; }
    public string BalanceVersion { get; private set; }
    public DateTimeOffset SourcePublishedAtUtc { get; private set; }
    public string PayloadJson { get; private set; }
    public string PayloadSha256 { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public string? Note { get; private set; }
}

public sealed class ContentRelease
{
    private ContentRelease()
    {
        PublishedBy = null!;
    }

    public ContentRelease(
        Guid id,
        Guid revisionId,
        DateTimeOffset publishedAtUtc,
        string publishedBy,
        string? note)
    {
        if (id == Guid.Empty) throw new ArgumentException("Release id cannot be empty.", nameof(id));
        if (revisionId == Guid.Empty) throw new ArgumentException("Revision id cannot be empty.", nameof(revisionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(publishedBy);

        Id = id;
        RevisionId = revisionId;
        PublishedAtUtc = publishedAtUtc;
        PublishedBy = publishedBy;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public Guid Id { get; private set; }
    public Guid RevisionId { get; private set; }
    public DateTimeOffset PublishedAtUtc { get; private set; }
    public string PublishedBy { get; private set; }
    public string? Note { get; private set; }
}

public sealed class ContentAuditEntry
{
    private ContentAuditEntry()
    {
        Action = null!;
        Actor = null!;
        DetailsJson = null!;
    }

    public ContentAuditEntry(
        Guid id,
        string action,
        Guid? revisionId,
        Guid? releaseId,
        string actor,
        DateTimeOffset occurredAtUtc,
        string detailsJson)
    {
        if (id == Guid.Empty) throw new ArgumentException("Audit id cannot be empty.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(detailsJson);
        if (revisionId is null && releaseId is null)
            throw new ArgumentException("Content audit entries must reference a revision or release.");

        Id = id;
        Action = action;
        RevisionId = revisionId;
        ReleaseId = releaseId;
        Actor = actor;
        OccurredAtUtc = occurredAtUtc;
        DetailsJson = detailsJson;
    }

    public Guid Id { get; private set; }
    public string Action { get; private set; }
    public Guid? RevisionId { get; private set; }
    public Guid? ReleaseId { get; private set; }
    public string Actor { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string DetailsJson { get; private set; }
}
