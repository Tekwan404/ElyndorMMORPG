using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.SetPassives;
using Elyndor.Core.Items;
using Elyndor.Core.Talents;

namespace Elyndor.Infrastructure.Characters;

public sealed record BuildStat(decimal Raw, decimal Effective, string Unit);
public sealed record BuildTalent(TalentDefinition Definition, int Rank, string BranchName);
public sealed record BuildReforge(string SlotKey, GeneratedItemAffix? Before, GeneratedItemAffix? After);
public sealed record BuildEquipment(
    string Slot, Guid ItemInstanceId, int DefinitionVersion, int ItemLevel, int EnhancementLevel,
    ItemDefinition BaseDefinition, ItemDefinition EffectiveDefinition,
    PrimaryStats? RolledPrimaryStats, GeneratedItemInstance? Generated,
    IReadOnlyList<BuildReforge> Reforges);
public sealed record BuildSet(EquipmentSetDefinition Definition, int EquippedPieces,
    IReadOnlyList<EquipmentSetBonusDefinition> ActiveBonuses,
    IReadOnlyList<SetPassiveDefinition>? ActivePassives = null);

public sealed record CharacterBuildSnapshot(
    int SchemaVersion, Guid CharacterId, string Name, string ClassId, string RaceId, string GenderId,
    int Level, string ContentVersion, string BalanceVersion, string ContentHash, string EngineVersion,
    IReadOnlyDictionary<string, BuildStat> Stats, decimal MaxResource, string ResourceType,
    decimal ResourceStartValue, decimal ResourceRegenPerSecond,
    IReadOnlyList<BuildEquipment> Equipment, BuildEquipment? SpatialArtifact,
    IReadOnlyList<BuildSet> Sets, IReadOnlyList<BuildTalent> Talents,
    IReadOnlyList<AbilityDefinition> Abilities, IReadOnlyList<string> TalentAbilityIds,
    IReadOnlyList<string> AbilityPanel, JsonElement ResolvedTalentModifiers,
    JsonElement StatFormula, JsonElement ClassProfile)
{
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [JsonIgnore] public string EquipmentHash => ComputeHash(new { Equipment, SpatialArtifact });
    [JsonIgnore] public string TalentHash => ComputeHash(Talents);
    [JsonIgnore] public string BuildHash => ComputeHash(this with { Name = string.Empty });

    // Computed hashes are intentionally excluded from serialization and therefore from hashing.
    private static string ComputeHash<T>(T value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            WriteCanonical(writer, JsonSerializer.SerializeToElement(value, JsonOptions));
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item);
            writer.WriteEndArray();
        }
        else if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out decimal number))
            writer.WriteRawValue(number.ToString("G29", System.Globalization.CultureInfo.InvariantCulture));
        else element.WriteTo(writer);
    }
}
