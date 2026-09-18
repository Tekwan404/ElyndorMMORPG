from pathlib import Path

patches = {
    Path("src/Elyndor.Core/Combat/Abilities/AbilityEngine.cs"): [
        (
            """                    case AbilityActionType.Taunt:\n                        events.Add(new CombatEvent(\n                            CombatEventType.TauntApplied,\n                            now,\n                            target.ActorId,\n                            ability.Id,\n                            (decimal)(action.Duration ?? TimeSpan.Zero).TotalSeconds));\n                        break;""",
            """                    case AbilityActionType.Taunt:\n                        events.Add(new CombatEvent(\n                            CombatEventType.TauntApplied,\n                            now,\n                            target.ActorId,\n                            ability.Id,\n                            (decimal)(action.Duration ?? TimeSpan.Zero).TotalSeconds));\n                        break;\n                    case AbilityActionType.Interrupt:\n                        // Cross-runtime interruption is authoritative at the CombatSession layer.\n                        // ResolveActions intentionally has no local actor-state mutation here.\n                        break;""",
        ),
        (
            """        if (ability.Actions?.Any(action =>\n                action.Delay is { } delay && delay < TimeSpan.Zero) == true)\n        {\n            throw new InvalidOperationException(\n                \"Ability action delay cannot be negative.\");\n        }""",
            """        if (ability.Actions?.Any(action =>\n                action.Delay is { } delay && delay < TimeSpan.Zero) == true)\n        {\n            throw new InvalidOperationException(\n                \"Ability action delay cannot be negative.\");\n        }\n        if (ability.Actions?.Any(action =>\n                action.Type == AbilityActionType.Interrupt\n                && (action.InterruptLockout is null\n                    || action.InterruptLockout < TimeSpan.Zero\n                    || action.Delay is not null)) == true)\n        {\n            throw new InvalidOperationException(\n                \"Interrupt actions require a non-negative lockout and cannot be delayed.\");\n        }\n        if (ability.Actions?.Any(action =>\n                action.Type != AbilityActionType.Interrupt\n                && action.InterruptLockout is not null) == true)\n        {\n            throw new InvalidOperationException(\n                \"Interrupt lockout is only valid for interrupt actions.\");\n        }""",
        ),
    ],
    Path("src/Elyndor.Core/Combat/Sessions/CombatSession.cs"): [
        (
            """            RegisterThreat(normalized);\n            Append(normalized);\n            ProcessGenericDamageReflection(normalized);""",
            """            RegisterThreat(normalized);\n            Append(normalized);\n            ProcessEnemyInterruptUtilityEvent(normalized);\n            ProcessMonsterInterruptActionEvent(normalized);\n            ProcessGenericDamageReflection(normalized);""",
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
