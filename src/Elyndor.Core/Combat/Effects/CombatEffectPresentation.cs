using System.Globalization;
using Elyndor.Core.Combat.Sessions;

namespace Elyndor.Core.Combat.Effects;

public static partial class CombatEffectPresentation
{
    // This flag only prevents a second duration extension; it adds no separate effect.
    public static bool IsVisible(ActiveEffect effect) => effect.Definition.Id is not ("MAGE_FROST_EXTENDED" or "PALADIN_CLEANSE_CAST")
        && effect.Definition.Kind != EffectKind.ConditionalModifier;

    public static CombatEffectSnapshot Snapshot(ActiveEffect active)
    {
        EffectDefinition definition = active.Definition;
        return new(definition.Id, active.Stacks, active.ExpiresAtUtc,
            NonEmpty(definition.DisplayName) ?? Names.GetValueOrDefault(definition.Id) ?? KindName(definition.Kind),
            Description(active),
            NonEmpty(definition.IconId) ?? Icon(definition),
            definition.Kind.ToString(), Disposition(definition), active.InstanceId,
            active.SourceId, active.AppliedAtUtc, definition.MaxStacks);
    }

    private static string? NonEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    private static string Number(decimal value) => value.ToString("0.##", CultureInfo.GetCultureInfo("ru-RU"));

    private static string Description(ActiveEffect active)
    {
        string? context = NonEmpty(active.Definition.Description);
        if (context is null) return Describe(active);
        // Boss instructions remain visible, but cannot replace live absorption/tick/stat values.
        return active.Definition.Kind is EffectKind.Shield or EffectKind.DamageOverTime
            or EffectKind.HealingOverTime or EffectKind.StatModifier
            or EffectKind.DamageReflection or EffectKind.TemporaryMaxHp
            ? $"{context} {Describe(active)}"
            : context;
    }

    private static string Describe(ActiveEffect active)
    {
        EffectDefinition d = active.Definition;
        string amount = Number(d.Magnitude * active.Stacks);
        string? special = DescribeRuntime(active);
        if (special is not null) return special;
        string text = d.Kind switch
        {
            EffectKind.DamageOverTime => $"Наносит {amount} ед. {DamageName(d.PeriodicDamageType)} урона каждые {Number((decimal)(d.TickInterval?.TotalSeconds ?? 0))} сек. до учёта защиты цели.",
            EffectKind.HealingOverTime => $"Восстанавливает {amount} ед. здоровья каждые {Number((decimal)(d.TickInterval?.TotalSeconds ?? 0))} сек. до модификаторов исцеления.",
            EffectKind.Shield => $"Осталось поглощения: {Number(active.RemainingMagnitude)} ед. урона."
                + (d.ResourceCostPerAbsorbedDamage > 0 ? $" Расходует {Number(d.ResourceCostPerAbsorbedDamage)} маны за единицу поглощённого урона." : ""),
            EffectKind.StatModifier when d.ModifiedStat is { } stat => StatDescription(d, stat, active.Stacks),
            EffectKind.Stun => "Оглушение: нельзя атаковать и использовать умения.",
            EffectKind.Silence => "Немота: блокирует умения, которые нельзя применять в состоянии немоты.",
            EffectKind.Root => "Опутывание: нельзя использовать умения, требующие подвижности. Остальные действия доступны.",
            EffectKind.Fear => "Страх: нельзя атаковать и использовать умения.",
            EffectKind.Disarm => "Обезоруживание: нельзя проводить автоатаки и использовать умения, требующие оружия.",
            EffectKind.DamageReflection => $"Возвращает нападающему {Number(d.Magnitude * active.Stacks * 100)}% полученного прямого урона."
                + (d.ReflectedDamageCap is { } cap ? $" Не более {Number(cap)} ед. за удар." : ""),
            EffectKind.LethalDamagePrevention => "Следующий смертельный удар оставит 1 ед. здоровья и поглотит эту защиту.",
            EffectKind.TemporaryMaxHp => $"Максимальное здоровье увеличено на {Number(d.Magnitude * active.Stacks * 100)}%.",
            _ when d.ControlImmunities is { Count: > 0 } => "Даёт невосприимчивость к эффектам контроля: "
                + string.Join(", ", d.ControlImmunities.Select(KindName)) + ".",
            _ when d.EventActions is { Count: > 0 } => string.Join(" ", d.EventActions.Select(action =>
                $"{(action.AbilityId == "AUTO_ATTACK" ? "Автоатаки" : "Боевые действия")} с вероятностью {Number(action.ChancePercent)}% вызывают "
                + (action.Action.Type == Abilities.AbilityActionType.Damage
                    ? $"дополнительный {DamageName(action.Action.DamageType)} урон: "
                        + (action.EventAmountPercent > 0
                            ? $"{Number(action.EventAmountPercent)}% {(action.UseBaseDamage ? "базового урона удара" : "нанесённого урона")}."
                            : $"{Number(action.Action.Amount)} ед.")
                    : "дополнительный эффект."))),
            _ => d.Kind == EffectKind.Debuff ? "На цель наложено ослабление." : "На цель действует усиление."
        };
        if (d.OnExpireActions is { Count: > 0 })
        {
            foreach (EffectExpirationActionDefinition action in d.OnExpireActions)
                text += action.Type == EffectExpirationActionType.Damage
                    ? $" При завершении наносит {Number(action.Amount)} ед. урона."
                    : action.Effect is { } next ? $" При завершении накладывает эффект «{NonEmpty(next.DisplayName) ?? Names.GetValueOrDefault(next.Id) ?? KindName(next.Kind)}»." : "";
        }
        return text;
    }

    private static string StatDescription(EffectDefinition effect, EffectStat stat, int stacks)
    {
        decimal delta = effect.ModifierMode switch
        {
            EffectModifierMode.Multiplicative => (effect.Magnitude - 1) * 100,
            EffectModifierMode.Percent => effect.Magnitude * stacks * 100,
            _ => effect.Magnitude * stacks
        };
        string unit = effect.ModifierMode != EffectModifierMode.Flat ? "%"
            : stat is EffectStat.CriticalChance or EffectStat.Accuracy or EffectStat.Dodge or EffectStat.BlockChance ? " п.п." : "";
        return $"{StatName(stat)}: {(delta > 0 ? "+" : "")}{Number(delta)}{unit}.";
    }

    private static string Disposition(EffectDefinition d)
    {
        if (d.Id == "MAGE_ICE_BLOCK_ACTIVE") return "Beneficial";
        if (d.Kind is EffectKind.Debuff or EffectKind.DamageOverTime or EffectKind.Stun
            or EffectKind.Silence or EffectKind.Root or EffectKind.Fear or EffectKind.Disarm) return "Harmful";
        if (d.Kind == EffectKind.StatModifier && d.ModifiedStat is { } stat)
        {
            decimal delta = d.ModifierMode == EffectModifierMode.Multiplicative ? d.Magnitude - 1 : d.Magnitude;
            bool incoming = stat is EffectStat.IncomingDamageMultiplier or EffectStat.IncomingMagicalDamageMultiplier or EffectStat.IncomingPhysicalDamageMultiplier or EffectStat.CastTimeMultiplier;
            return delta == 0 ? "Neutral" : (incoming ? delta < 0 : delta > 0) ? "Beneficial" : "Harmful";
        }
        return "Beneficial";
    }

    private static string DamageName(Damage.DamageType kind) => kind switch
    {
        Damage.DamageType.Physical => "физического", Damage.DamageType.Magical => "магического", _ => "чистого"
    };

    private static string KindName(EffectKind kind) => kind switch
    {
        EffectKind.DamageOverTime => "Периодический урон", EffectKind.HealingOverTime => "Периодическое исцеление",
        EffectKind.Shield => "Поглощающий щит", EffectKind.StatModifier => "Изменение характеристик",
        EffectKind.Stun => "Оглушение", EffectKind.Silence => "Немота", EffectKind.Root => "Опутывание",
        EffectKind.Fear => "Страх", EffectKind.Disarm => "Обезоруживание", EffectKind.Debuff => "Ослабление",
        EffectKind.DamageReflection => "Отражение урона", EffectKind.LethalDamagePrevention => "Защита от смерти",
        EffectKind.TemporaryMaxHp => "Запас здоровья", _ => "Усиление"
    };

    private static string StatName(EffectStat stat) => stat switch
    {
        EffectStat.AttackPower => "Сила атаки", EffectStat.SpellPower => "Сила заклинаний",
        EffectStat.CriticalChance => "Шанс критического удара", EffectStat.AttackSpeed => "Скорость атаки",
        EffectStat.Accuracy => "Точность", EffectStat.Dodge => "Уклонение", EffectStat.Armor => "Броня",
        EffectStat.MagicResistance => "Сопротивление магии", EffectStat.BlockChance => "Шанс блока",
        EffectStat.BlockValueMin => "Минимальное поглощение блока", EffectStat.BlockValueMax => "Максимальное поглощение блока",
        EffectStat.IncomingDamageMultiplier => "Получаемый урон", EffectStat.IncomingPhysicalDamageMultiplier => "Получаемый физический урон",
        EffectStat.IncomingMagicalDamageMultiplier => "Получаемый магический урон", EffectStat.OutgoingDamageMultiplier => "Наносимый урон",
        EffectStat.OutgoingPhysicalDamageMultiplier => "Наносимый физический урон", EffectStat.HealingReceivedMultiplier => "Получаемое исцеление",
        EffectStat.CastTimeMultiplier => "Время применения заклинаний", EffectStat.ManaRegenMultiplier => "Базовое восстановление маны",
        EffectStat.OutgoingHealingMultiplier => "Исходящее исцеление", _ => "Характеристика"
    };

    private static string? Icon(EffectDefinition d)
    {
        string id = d.Id;
        string? spellIcon = id switch
        {
            "PALADIN_BEACON_OF_LIGHT" => "paladin-beacon-of-light",
            "PALADIN_BLESSING_WISDOM" => "paladin-blessing-of-wisdom",
            "PALADIN_DIVINE_REPLENISHMENT_PENALTY" => "paladin-divine-replenishment",
            "PALADIN_INTERCESSION" => "paladin-intercession",
            "PALADIN_HAMMER_OF_JUSTICE_MARKER" => "paladin-hammer-of-justice",
            "PALADIN_REPENTANCE_MARKER" => "paladin-repentance",
            "PALADIN_CONCENTRATION_AURA" => "paladin-concentration-aura",
            "PALADIN_DEVOTION_AURA" => "paladin-devotion-aura",
            "PALADIN_SANCTITY_AURA" => "paladin-sanctity-aura",
            "PALADIN_SEAL_COMMAND" => "paladin-seal-of-command",
            "PALADIN_SEAL_RIGHTEOUSNESS" => "paladin-seal-of-righteousness",
            "PALADIN_CONSECRATION_ACTIVE" => "paladin-consecration",
            "MAGE_HOT_STREAK" or "MAGE_PYROMANIAC" or "MAGE_PYROBLAST_BURN" => "mage-pyroblast",
            "MAGE_FLAMESTRIKE_BURN" => "mage-flamestrike",
            "WARLORD_BATTLE_STANDARD" => "warrior-battle-standard",
            "WARLORD_WAR_BANNER" => "warrior-war-banner",
            "WARLORD_CRIT_OF_VENGEANCE" or "WARLORD_VENGEANCE_STACKS" => "warrior-cry-of-vengeance",
            "ARCHER_SNIPER_FOCUS" => "MARKSMAN_16",
            "ARCHER_HUNTER_MARK" => "MARKSMAN_06",
            "ARCHER_HEAVY_ARROW" => "MARKSMAN_23",
            "ARCHER_DETERRENCE_COUNTER" => "SURVIVAL_22",
            "ARCHER_SERPENT_STING" => "SURVIVAL_03",
            "ARCHER_WYVERN_STING" => "SURVIVAL_04",
            "ARCHER_IMMOLATION_TRAP_DOT" => "SURVIVAL_07",
            "ARCHER_EXPLOSIVE_TRAP_DOT" => "SURVIVAL_09",
            "ARCHER_BESTIAL_WRATH_AS" or "ARCHER_BESTIAL_WRATH_DAMAGE" => "BEAST_MASTERY_06",
            "GUARDIAN_REVENGE_WINDOW" => "warrior-revenge",
            "GUARDIAN_CAPSTONE_SHIELD_BLOCK" => "warrior-shield-block",
            _ => null
        };
        if (spellIcon is not null) return spellIcon;
        if (id.StartsWith("MAGE_", StringComparison.Ordinal))
        {
            if (id.Contains("COMBUSTION", StringComparison.Ordinal)) return "mage-combustion";
            if (id.Contains("MANA_SHIELD", StringComparison.Ordinal)) return "mage-mana-shield";
            if (id.Contains("ICE_BLOCK", StringComparison.Ordinal)) return "mage-ice-block";
            if (id.Contains("BARRIER", StringComparison.Ordinal)) return "mage-ice-barrier";
            if (id.Contains("SILENCE", StringComparison.Ordinal)) return "mage-counterspell";
            if (id.Contains("COLD", StringComparison.Ordinal) || id.Contains("CHILL", StringComparison.Ordinal) || id.Contains("FREEZE", StringComparison.Ordinal) || id.Contains("ICE", StringComparison.Ordinal) || id.Contains("FROST", StringComparison.Ordinal) || id.Contains("WINTER", StringComparison.Ordinal)) return "mage-ice-shard";
            if (id.Contains("ARCANE_POWER", StringComparison.Ordinal)) return "mage-arcane-power";
            if (id.Contains("CLEARCASTING", StringComparison.Ordinal) || id.Contains("PRESENCE", StringComparison.Ordinal) || id.Contains("FORTITUDE", StringComparison.Ordinal)) return "mage-presence-of-mind";
            return "mage-fireball";
        }
        if (id.StartsWith("PALADIN_", StringComparison.Ordinal))
            return d.Kind == EffectKind.Shield ? "paladin-holy-shield" : "paladin-divine-favor";
        if (id.StartsWith("ARCHER_", StringComparison.Ordinal)) return "MARKSMAN_01";
        if (id.StartsWith("BERSERKER_", StringComparison.Ordinal)) return "warrior-berserk";
        if (id.StartsWith("WARLORD_", StringComparison.Ordinal) || id.StartsWith("GUARDIAN_", StringComparison.Ordinal)) return "warrior-battle-cry";
        return null;
    }
}
