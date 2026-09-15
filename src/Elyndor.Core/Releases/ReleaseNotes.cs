namespace Elyndor.Core.Releases;

public enum ReleaseNoteEntryKind
{
    Added,
    Changed,
    Fixed
}

public sealed record ReleaseNoteEntry(
    ReleaseNoteEntryKind Kind,
    string Text);

public sealed record ReleaseNoteDefinition(
    string Id,
    string Title,
    DateTimeOffset PublishedAtUtc,
    bool PlayerVisible,
    IReadOnlyList<ReleaseNoteEntry> Entries);

public sealed record ReleaseNotesDocument(
    IReadOnlyList<ReleaseNoteDefinition> Releases);

public interface IReleaseNotesCatalog
{
    ReleaseNoteDefinition? Current { get; }

    IReadOnlyList<ReleaseNoteDefinition> History { get; }

    bool Contains(string releaseId);
}

public sealed class ReleaseNotesCatalog : IReleaseNotesCatalog
{
    public ReleaseNotesCatalog(ReleaseNotesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        IReadOnlyList<ReleaseNoteDefinition> releases = document.Releases ?? [];
        HashSet<string> identifiers = new(StringComparer.Ordinal);
        foreach (ReleaseNoteDefinition release in releases)
        {
            if (string.IsNullOrWhiteSpace(release.Id)
                || !identifiers.Add(release.Id))
            {
                throw new ArgumentException(
                    "Release note identifiers must be present and unique.",
                    nameof(document));
            }

            if (string.IsNullOrWhiteSpace(release.Title)
                || release.PublishedAtUtc.Offset != TimeSpan.Zero
                || release.Entries is null
                || release.Entries.Count == 0
                || release.Entries.Any(entry => string.IsNullOrWhiteSpace(entry.Text)))
            {
                throw new ArgumentException(
                    $"Release note '{release.Id}' is invalid.",
                    nameof(document));
            }
        }

        History = releases
            .Where(release => release.PlayerVisible)
            .OrderByDescending(release => release.PublishedAtUtc)
            .ThenByDescending(release => release.Id, StringComparer.Ordinal)
            .ToArray();
        Current = History.Count == 0 ? null : History[0];
        releaseIds = identifiers;
    }

    private readonly HashSet<string> releaseIds;

    public ReleaseNoteDefinition? Current { get; }

    public IReadOnlyList<ReleaseNoteDefinition> History { get; }

    public bool Contains(string releaseId) =>
        !string.IsNullOrWhiteSpace(releaseId) && releaseIds.Contains(releaseId);
}
