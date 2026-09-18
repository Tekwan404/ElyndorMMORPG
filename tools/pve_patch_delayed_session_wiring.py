from pathlib import Path

path = Path("src/Elyndor.Core/Combat/Sessions/CombatSession.cs")
text = path.read_text(encoding="utf-8")
replacements = [
    (
        """            next = Min(next, _nextSummonAtUtc);\n            next = Min(next, NextGenericEncounterDueAtUtc);\n            foreach (CombatParticipantDefinition enemy in _enemies)""",
        """            next = Min(next, _nextSummonAtUtc);\n            next = Min(next, NextGenericEncounterDueAtUtc);\n            next = Min(next, NextDelayedAbilityActionAtUtc);\n            foreach (CombatParticipantDefinition enemy in _enemies)""",
    ),
    (
        """            ProcessGenericEncounterDue(due);\n            ProcessEffects(due);\n            if (Status != CombatSessionStatus.Active) break;\n\n            SyncAllPlayerConditionalEffects(due);""",
        """            ProcessGenericEncounterDue(due);\n            ProcessEffects(due);\n            if (Status != CombatSessionStatus.Active) break;\n            ProcessDelayedAbilityActions(due);\n            if (Status != CombatSessionStatus.Active) break;\n\n            SyncAllPlayerConditionalEffects(due);""",
    ),
]

for index, (old, new) in enumerate(replacements, start=1):
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"Patch fragment {index} expected exactly once, found {count}.")
    text = text.replace(old, new, 1)

path.write_text(text, encoding="utf-8")
