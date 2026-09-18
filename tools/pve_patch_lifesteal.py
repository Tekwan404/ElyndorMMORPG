from pathlib import Path

path = Path("src/Elyndor.Core/Combat/Abilities/AbilityEngine.cs")
text = path.read_text(encoding="utf-8")
replacements = [
    (
        """                        events.AddRange(damage.Events);\n                        break;""",
        """                        events.AddRange(damage.Events);\n                        if (action.LifestealPercent > 0\n                            && damage.HpDamage > 0\n                            && !runtime.Actor.IsDead)\n                        {\n                            HealingResult lifesteal = HealingPipeline.Resolve(\n                                new HealingRequest(\n                                    runtime.Actor,\n                                    damage.HpDamage * action.LifestealPercent / 100m,\n                                    OccurredAtUtc: now,\n                                    Source: runtime.Actor,\n                                    CanCrit: false,\n                                    Origin: HealingOrigin.Secondary,\n                                    DefinitionId: ability.Id));\n                            events.AddRange(lifesteal.Events);\n                        }\n                        break;""",
    ),
    (
        """        if (ability.Actions?.Any(action =>\n                action.Type != AbilityActionType.Interrupt\n                && action.InterruptLockout is not null) == true)\n        {\n            throw new InvalidOperationException(\n                \"Interrupt lockout is only valid for interrupt actions.\");\n        }\n    }""",
        """        if (ability.Actions?.Any(action =>\n                action.Type != AbilityActionType.Interrupt\n                && action.InterruptLockout is not null) == true)\n        {\n            throw new InvalidOperationException(\n                \"Interrupt lockout is only valid for interrupt actions.\");\n        }\n        if (ability.Actions?.Any(action =>\n                action.LifestealPercent < 0\n                || action.Type != AbilityActionType.Damage\n                    && action.LifestealPercent != 0) == true)\n        {\n            throw new InvalidOperationException(\n                \"Lifesteal must be non-negative and is only valid for damage actions.\");\n        }\n    }""",
    ),
]

for index, (old, new) in enumerate(replacements, start=1):
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"Patch fragment {index} expected exactly once, found {count}.")
    text = text.replace(old, new, 1)

path.write_text(text, encoding="utf-8")
