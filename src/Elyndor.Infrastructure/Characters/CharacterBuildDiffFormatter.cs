using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Elyndor.Infrastructure.Characters;

public static class CharacterBuildDiffFormatter
{
    public static string Format(CharacterBuildSnapshot before, CharacterBuildSnapshot after)
    {
        var text = new StringBuilder();
        text.AppendLine("BUILD DIFF");
        text.AppendLine(CultureInfo.InvariantCulture, $"A: {before.Name} · {before.ClassId} · ур.{before.Level}\n{before.BuildHash}");
        text.AppendLine(CultureInfo.InvariantCulture, $"B: {after.Name} · {after.ClassId} · ур.{after.Level}\n{after.BuildHash}");
        if (before.CharacterId != after.CharacterId)
            text.AppendLine("Внимание: сравниваются разные персонажи.");
        int headerLength = text.Length;
        Compare(text, "SchemaVersion", before.SchemaVersion, after.SchemaVersion);
        Compare(text, "CharacterId", before.CharacterId, after.CharacterId);
        Compare(text, "Class", before.ClassId, after.ClassId);
        Compare(text, "Race", before.RaceId, after.RaceId);
        Compare(text, "Gender", before.GenderId, after.GenderId);
        Compare(text, "Level", before.Level, after.Level);
        Compare(text, "content", before.ContentVersion, after.ContentVersion);
        Compare(text, "balance", before.BalanceVersion, after.BalanceVersion);
        Compare(text, "ContentHash", before.ContentHash, after.ContentHash);
        Compare(text, "EngineVersion", before.EngineVersion, after.EngineVersion);
        foreach (string key in Keys(before.Stats.Keys, after.Stats.Keys))
        {
            var a = before.Stats.GetValueOrDefault(key);
            var b = after.Stats.GetValueOrDefault(key);
            if (a == b) continue;
            string label = CharacterBuildSnapshotFormatter.StatNames.GetValueOrDefault(key, key);
            if (a is null || b is null)
                text.AppendLine(CultureInfo.InvariantCulture, $"{label}: {(a is null ? "—" : Number(a.Raw) + a.Unit)} → {(b is null ? "—" : Number(b.Raw) + b.Unit)}");
            else
            {
                WriteNumber(text, label, a.Raw, b.Raw, b.Unit);
                if (a.Raw != a.Effective || b.Raw != b.Effective || a.Effective != b.Effective)
                    WriteNumber(text, label + " (эффективно)", a.Effective, b.Effective, b.Unit);
                if (a.Unit != b.Unit) Compare(text, label + " unit", a.Unit, b.Unit);
            }
        }
        Compare(text, "ResourceType", before.ResourceType, after.ResourceType);
        Compare(text, "MaxResource", before.MaxResource, after.MaxResource);
        Compare(text, "ResourceStartValue", before.ResourceStartValue, after.ResourceStartValue);
        Compare(text, "ResourceRegenPerSecond", before.ResourceRegenPerSecond, after.ResourceRegenPerSecond);
        var oldGear = before.Equipment.ToDictionary(item => item.Slot, StringComparer.Ordinal);
        var newGear = after.Equipment.ToDictionary(item => item.Slot, StringComparer.Ordinal);
        foreach (string slot in Keys(oldGear.Keys, newGear.Keys))
            CompareItem(text, slot, oldGear.GetValueOrDefault(slot), newGear.GetValueOrDefault(slot));
        CompareItem(text, "SpatialArtifact", before.SpatialArtifact, after.SpatialArtifact);
        var oldTalents = before.Talents.ToDictionary(talent => talent.Definition.Id, StringComparer.Ordinal);
        var newTalents = after.Talents.ToDictionary(talent => talent.Definition.Id, StringComparer.Ordinal);
        foreach (string id in Keys(oldTalents.Keys, newTalents.Keys))
        {
            var a = oldTalents.GetValueOrDefault(id);
            var b = newTalents.GetValueOrDefault(id);
            if (a?.Rank != b?.Rank)
                text.AppendLine(CultureInfo.InvariantCulture, $"Талант {id}: {a?.Rank ?? 0} → {b?.Rank ?? 0}");
            if (a is not null && b is not null) Compare(text, "Талант " + id + ".Definition", a.Definition, b.Definition);
        }
        CompareById(text, "Комплект", before.Sets, after.Sets, set => set.Definition.Id);
        CompareById(text, "Способность", before.Abilities, after.Abilities, ability => ability.Id);
        Compare(text, "Способности из талантов", before.TalentAbilityIds, after.TalentAbilityIds);
        Compare(text, "Панель способностей", before.AbilityPanel, after.AbilityPanel);
        Compare(text, "ResolvedTalentModifiers", before.ResolvedTalentModifiers, after.ResolvedTalentModifiers);
        Compare(text, "StatFormula", before.StatFormula, after.StatFormula);
        Compare(text, "ClassProfile", before.ClassProfile, after.ClassProfile);
        if (text.Length == headerLength) text.AppendLine("Изменений нет.");
        return text.ToString();
    }

    private static void CompareItem(StringBuilder text, string slot, BuildEquipment? before, BuildEquipment? after)
    {
        if (Equal(Element(before), Element(after))) return;
        text.AppendLine(CultureInfo.InvariantCulture,
            $"\n{slot}: {ItemName(before)} → {ItemName(after)}");
        if (before is null || after is null)
        {
            var item = (after ?? before)!;
            text.AppendLine(CultureInfo.InvariantCulture,
                $"ilvl={item.ItemLevel} · {item.BaseDefinition.Rarity} · +{item.EnhancementLevel}");
            return;
        }
        Compare(text, slot, before, after);
    }

    private static string ItemName(BuildEquipment? item) => item is null ? "пусто"
        : $"{item.Generated?.DisplayName ?? item.BaseDefinition.Name} [{item.BaseDefinition.Id}; instance={item.ItemInstanceId:D}]";

    private static void CompareById<T>(StringBuilder text, string label, IReadOnlyList<T> before,
        IReadOnlyList<T> after, Func<T, string> id) where T : class
    {
        var oldItems = before.ToDictionary(id, StringComparer.Ordinal);
        var newItems = after.ToDictionary(id, StringComparer.Ordinal);
        foreach (string key in Keys(oldItems.Keys, newItems.Keys))
        {
            var a = oldItems.GetValueOrDefault(key);
            var b = newItems.GetValueOrDefault(key);
            if (a is null || b is null)
                text.AppendLine(CultureInfo.InvariantCulture, $"{(a is null ? "+" : "-")} {label} {key}");
            else Compare(text, label + " " + key, a, b);
        }
    }

    private static IEnumerable<string> Keys(IEnumerable<string> before, IEnumerable<string> after) =>
        before.Union(after, StringComparer.Ordinal).Order(StringComparer.Ordinal);

    private static JsonElement Element<T>(T value) => JsonSerializer.SerializeToElement(value, CharacterBuildSnapshot.JsonOptions);
    private static bool Equal(JsonElement before, JsonElement after) =>
        before.ValueKind == JsonValueKind.Undefined || after.ValueKind == JsonValueKind.Undefined
            ? before.ValueKind == after.ValueKind : JsonElement.DeepEquals(before, after);

    private static void Compare<T>(StringBuilder text, string path, T before, T after) =>
        CompareElements(text, path, Element(before), Element(after));

    private static void CompareElements(StringBuilder text, string path, JsonElement before, JsonElement after)
    {
        if (Equal(before, after)) return;
        if (before.ValueKind == JsonValueKind.Object && after.ValueKind == JsonValueKind.Object)
        {
            var a = before.EnumerateObject().ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
            var b = after.EnumerateObject().ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
            foreach (string key in Keys(a.Keys, b.Keys))
                CompareElements(text, path + "." + key, a.GetValueOrDefault(key), b.GetValueOrDefault(key));
        }
        else if (before.ValueKind == JsonValueKind.Number && after.ValueKind == JsonValueKind.Number
            && before.TryGetDecimal(out decimal a) && after.TryGetDecimal(out decimal b))
            WriteNumber(text, path, a, b, string.Empty);
        else text.AppendLine(CultureInfo.InvariantCulture, $"{path}: {Display(before)} → {Display(after)}");
    }

    private static string Display(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => "—",
        JsonValueKind.String => element.GetString() ?? "—",
        _ => JsonSerializer.Serialize(element)
    };

    private static void WriteNumber(StringBuilder text, string label, decimal before, decimal after, string unit) =>
        text.AppendLine(CultureInfo.InvariantCulture,
            $"{label}: {Number(before)}{unit} → {Number(after)}{unit} ({(after >= before ? "+" : "")}{Number(after - before)}{unit})");

    private static string Number(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
}
