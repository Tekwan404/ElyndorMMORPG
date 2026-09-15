using System.Text.Json;
using Elyndor.Core.Releases;

namespace Elyndor.Infrastructure.Releases;

public static class ReleaseNotesFileLoader
{
    public static async Task<IReleaseNotesCatalog> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            await using FileStream stream = File.OpenRead(path);
            ReleaseNotesDocument? document = await JsonSerializer.DeserializeAsync<ReleaseNotesDocument>(
                stream,
                cancellationToken: cancellationToken);

            return new ReleaseNotesCatalog(document ?? throw new InvalidDataException(
                $"Release notes file '{path}' is empty."));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Release notes file '{path}' does not match the required JSON shape.",
                exception);
        }
    }
}
