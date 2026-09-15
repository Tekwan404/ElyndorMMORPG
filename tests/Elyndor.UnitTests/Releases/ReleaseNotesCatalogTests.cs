using Elyndor.Core.Releases;

namespace Elyndor.UnitTests.Releases;

public sealed class ReleaseNotesCatalogTests
{
    [Fact]
    public void CurrentReturnsTheLatestPlayerVisibleRelease()
    {
        ReleaseNotesCatalog catalog = new(
            new ReleaseNotesDocument(
                [
                    new ReleaseNoteDefinition(
                        "0.23.0",
                        "Блок щитом",
                        new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero),
                        true,
                        [new ReleaseNoteEntry(ReleaseNoteEntryKind.Added, "Добавлен блок щитом.")]),
                    new ReleaseNoteDefinition(
                        "0.23.1",
                        "Стабильность боя",
                        new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero),
                        true,
                        [new ReleaseNoteEntry(ReleaseNoteEntryKind.Fixed, "Исправлено зависание боя.")])
                ]));

        Assert.Equal("0.23.1", catalog.Current!.Id);
    }

    [Fact]
    public void RejectsDuplicateReleaseIdentifiers()
    {
        Assert.Throws<ArgumentException>(() => new ReleaseNotesCatalog(
            new ReleaseNotesDocument(
                [
                    new ReleaseNoteDefinition(
                        "0.23.1",
                        "Первый",
                        new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero),
                        true,
                        [new ReleaseNoteEntry(ReleaseNoteEntryKind.Added, "Первый пункт.")]),
                    new ReleaseNoteDefinition(
                        "0.23.1",
                        "Второй",
                        new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero),
                        true,
                        [new ReleaseNoteEntry(ReleaseNoteEntryKind.Fixed, "Второй пункт.")])
                ])));
    }
}
