from pathlib import Path

patches = {
    Path("src/Elyndor.Core/Combat/Sessions/CombatSession.cs"): [
        (
            """            RegisterThreat(normalized);\n            Append(normalized);\n            ProcessGenericEncounterEvent(normalized);""",
            """            RegisterThreat(normalized);\n            Append(normalized);\n            ProcessGenericDamageReflection(normalized);\n            if (Status != CombatSessionStatus.Active)\n                break;\n            ProcessGenericEncounterEvent(normalized);""",
        ),
    ],
    Path("src/Elyndor.Core/Combat/Effects/EffectEngine.cs"): [
        (
            """        bool periodic = definition.Kind is EffectKind.DamageOverTime or EffectKind.HealingOverTime;\n        if (periodic != definition.TickInterval.HasValue)\n        {\n            throw new ArgumentException(\"Only periodic effects require a tick interval.\", nameof(definition));\n        }""",
            """        bool periodic = definition.Kind is EffectKind.DamageOverTime or EffectKind.HealingOverTime;\n        if (periodic != definition.TickInterval.HasValue)\n        {\n            throw new ArgumentException(\"Only periodic effects require a tick interval.\", nameof(definition));\n        }\n        if (definition.Kind == EffectKind.DamageReflection\n            && (definition.Magnitude <= 0\n                || definition.ReflectedDamageCap is { } cap && cap <= 0))\n        {\n            throw new ArgumentException(\n                \"Damage reflection requires a positive ratio and positive cap when specified.\",\n                nameof(definition));\n        }""",
        ),
    ],
}

for path, replacements in patches.items():
    text = path.read_text(encoding="utf-8")
    for index, (old, new) in enumerate(replacements, start=1):
        count = text.count(old)
        if count != 1:
            raise SystemExit(
                f"{path}: patch fragment {index} expected exactly once, found {count}."
            )
        text = text.replace(old, new, 1)
    path.write_text(text, encoding="utf-8")
