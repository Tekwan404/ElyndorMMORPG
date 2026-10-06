using System.Globalization;
using System.Text;
using System.Text.Json;
using Elyndor.Core.Items;

namespace Elyndor.Infrastructure.Characters;

public static class CharacterBuildSnapshotFormatter
{
    internal static readonly IReadOnlyDictionary<string, string> StatNames = new Dictionary<string, string>
    {
        ["strength"] = "Сила", ["agility"] = "Ловкость", ["intellect"] = "Интеллект",
        ["stamina"] = "Выносливость", ["maxHp"] = "HP", ["attackPower"] = "Сила атаки",
        ["spellPower"] = "Сила заклинаний", ["criticalChance"] = "Крит",
        ["criticalDamage"] = "Критический урон", ["accuracy"] = "Точность",
        ["armorPenetration"] = "Пробивание брони", ["magicPenetration"] = "Пробивание магии",
        ["attackSpeed"] = "Скорость атаки", ["armor"] = "Броня", ["magicResistance"] = "Сопротивление магии",
        ["dodge"] = "Уклонение", ["blockChance"] = "Блок", ["blockValueMin"] = "Блок: минимум",
        ["blockValueMax"] = "Блок: максимум", ["armorDamageReductionPercent"] = "Физическое снижение урона",
        ["magicDamageReductionPercent"] = "Магическое снижение урона"
    };

    public static string Format(CharacterBuildSnapshot build, bool gearOnly = false, bool talentsOnly = false)
    {
        var text = new StringBuilder();
        text.AppendLine("BUILD SNAPSHOT").AppendLine(CultureInfo.InvariantCulture, $"{build.Name} · {build.ClassId} · ур.{build.Level}");
        text.AppendLine(CultureInfo.InvariantCulture, $"content={build.ContentVersion} · balance={build.BalanceVersion}");
        text.AppendLine(CultureInfo.InvariantCulture, $"BuildHash: {build.BuildHash}");
        text.AppendLine(CultureInfo.InvariantCulture, $"EquipmentHash: {build.EquipmentHash}");
        text.AppendLine(CultureInfo.InvariantCulture, $"TalentHash: {build.TalentHash}");
        if (!gearOnly && !talentsOnly)
        {
            text.AppendLine("\nХАРАКТЕРИСТИКИ (постоянный билд; без временных боевых эффектов)");
            foreach (var (key, stat) in build.Stats)
            {
                text.Append(StatNames.GetValueOrDefault(key, key)).Append(": ").Append(Number(stat.Raw)).Append(stat.Unit);
                if (stat.Raw != stat.Effective)
                    text.Append(" (эффективно ").Append(Number(stat.Effective)).Append(stat.Unit).Append(')');
                text.AppendLine();
            }
            text.AppendLine(CultureInfo.InvariantCulture, $"Ресурс {build.ResourceType}: {Number(build.MaxResource)}");
            text.AppendLine(CultureInfo.InvariantCulture, $"Начальный ресурс: {Number(build.ResourceStartValue)} · реген в бою: {Number(build.ResourceRegenPerSecond)}/с");
        }
        if (!talentsOnly)
        {
            text.AppendLine("\nЭКИПИРОВКА");
            foreach (var item in build.Equipment) WriteItem(text, item);
            foreach (var slot in Enum.GetValues<EquipmentSlot>().Where(slot =>
                !build.Equipment.Any(item => item.Slot == slot.ToString())))
                text.AppendLine(CultureInfo.InvariantCulture, $"{slot}: пусто");
            text.AppendLine("\nПРОСТРАНСТВЕННЫЙ АРТЕФАКТ");
            if (build.SpatialArtifact is { } artifact) WriteItem(text, artifact);
            else text.AppendLine("Не надет");
            text.AppendLine("\nКОМПЛЕКТЫ");
            foreach (var set in build.Sets)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"{set.Definition.Name} ({set.Definition.Id}) · {set.EquippedPieces} предметов");
                foreach (var bonus in set.ActiveBonuses)
                {
                    text.AppendLine(CultureInfo.InvariantCulture, $"{bonus.RequiredPieces} предмета:");
                    WriteNumbers(text, JsonSerializer.SerializeToElement(bonus), "RequiredPieces");
                }
                foreach (var passive in set.ActivePassives ?? [])
                    text.AppendLine(CultureInfo.InvariantCulture,
                        $"{passive.RequiredPieces} предмета · {passive.Id}: {string.Join("; ", passive.Actions.Select(action => action.Description ?? action.ReferenceId))}");
            }
            if (build.Sets.Count == 0) text.AppendLine("Нет комплектов");
        }
        if (!gearOnly)
        {
            text.AppendLine("\nТАЛАНТЫ");
            foreach (var talent in build.Talents)
                text.AppendLine(CultureInfo.InvariantCulture, $"{talent.BranchName} · {talent.Definition.Id} · {talent.Definition.Name} {talent.Rank}/{talent.Definition.MaxRank}");
            if (build.Talents.Count == 0) text.AppendLine("Не выбраны");
            text.AppendLine("\nСПОСОБНОСТИ ИЗ ТАЛАНТОВ");
            foreach (string abilityId in build.TalentAbilityIds) text.AppendLine(abilityId);
            text.AppendLine("\nДОСТУПНАЯ ПАНЕЛЬ (server order)");
            text.AppendLine(string.Join(" · ", build.AbilityPanel));
        }
        return text.ToString();
    }

    public static string FormatSummary(CharacterBuildSnapshot build) =>
        $"── BUILD SNAPSHOT ──\nBuildHash: {build.BuildHash}\nLevel: {build.Level}\nClass: {build.ClassId}\n"
        + string.Join("\n", build.Stats.Select(pair => $"{pair.Key}: {Number(pair.Value.Raw)}{pair.Value.Unit} (effective {Number(pair.Value.Effective)}{pair.Value.Unit})"))
        + $"\n{build.ResourceType}: {Number(build.MaxResource)}\nEquipmentHash: {build.EquipmentHash}\nTalentHash: {build.TalentHash}\n";

    public static string FormatJson(CharacterBuildSnapshot build) => JsonSerializer.Serialize(
        new { build.BuildHash, build.EquipmentHash, build.TalentHash, Snapshot = build }, CharacterBuildSnapshot.JsonOptions);

    private static void WriteItem(StringBuilder text, BuildEquipment item)
    {
        text.AppendLine(CultureInfo.InvariantCulture, $"\n{item.Slot} · {item.Generated?.DisplayName ?? item.BaseDefinition.Name}");
        text.AppendLine(CultureInfo.InvariantCulture, $"id={item.BaseDefinition.Id} · definitionVersion={item.DefinitionVersion}");
        text.AppendLine(CultureInfo.InvariantCulture, $"instance={item.ItemInstanceId:D} · ilvl={item.ItemLevel} · {item.BaseDefinition.Rarity} · +{item.EnhancementLevel}");
        text.AppendLine("Базовые характеристики:");
        WriteItemStats(text, item.BaseDefinition);
        if (item.RolledPrimaryStats is { } rolled)
        {
            text.AppendLine("Сохранённые primary rolls:");
            WriteNumbers(text, JsonSerializer.SerializeToElement(rolled));
        }
        text.AppendLine("Итоговые характеристики (+N и аффиксы):");
        WriteItemStats(text, item.EffectiveDefinition);
        if (item.Generated is { } generated)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"Качество: {Number(generated.RollQuality)} · звёзды: {generated.Stars} · generationVersion={generated.GenerationVersion}");
            foreach (var affix in generated.Affixes)
                text.AppendLine(CultureInfo.InvariantCulture, $"{affix.SlotKey}: {affix.AffixDefinitionId} · {affix.StatId} +{Number(affix.Value)}{(ItemStatIds.IsPercentage(affix.StatId) ? "%" : "")}");
        }
        foreach (var reforge in item.Reforges)
            text.AppendLine(CultureInfo.InvariantCulture, $"Перековка [{reforge.SlotKey}]: {reforge.Before?.StatId} {Number(reforge.Before?.Value ?? 0)} → {reforge.After?.StatId} {Number(reforge.After?.Value ?? 0)}");
    }

    private static void WriteItemStats(StringBuilder text, ItemDefinition definition)
    {
        WriteNumbers(text, JsonSerializer.SerializeToElement(definition.Stats));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(definition));
        foreach (var property in document.RootElement.EnumerateObject())
            if ((property.Name.EndsWith("Flat", StringComparison.Ordinal) || property.Name.EndsWith("Percent", StringComparison.Ordinal)
                || property.Name is "WeaponDamageMin" or "WeaponDamageMax" or "WeaponBaseAttackIntervalSeconds" or "BlockValueMin" or "BlockValueMax")
                && property.Value.TryGetDecimalSafe(out decimal value) && value != 0)
                text.AppendLine(CultureInfo.InvariantCulture, $"{property.Name}: {Number(value)}");
    }

    private static void WriteNumbers(StringBuilder text, JsonElement element, string? skip = null)
    {
        foreach (var property in element.EnumerateObject())
            if (property.Name != skip && property.Value.TryGetDecimalSafe(out decimal value) && value != 0)
                text.AppendLine(CultureInfo.InvariantCulture, $"{property.Name}: {Number(value)}");
    }

    private static bool TryGetDecimalSafe(this JsonElement element, out decimal value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out value);
    }

    private static string Number(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
}
