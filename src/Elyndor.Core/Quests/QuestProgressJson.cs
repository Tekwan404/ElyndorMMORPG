using System.Text.Json;

namespace Elyndor.Core.Quests;

public static class QuestProgressJson
{
    public static Dictionary<string, int> Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, int>(StringComparer.Ordinal);

        Dictionary<string, int>? values =
            JsonSerializer.Deserialize<Dictionary<string, int>>(json);
        return values is null
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : new Dictionary<string, int>(values, StringComparer.Ordinal);
    }

    public static string Write(IReadOnlyDictionary<string, int> progress) =>
        JsonSerializer.Serialize(progress);
}
