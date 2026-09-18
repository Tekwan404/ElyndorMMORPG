from pathlib import Path

patches = {
    Path("src/Elyndor.Core/Combat/Sessions/CombatSession.cs"): [
        (
            """            next = Min(next, _nextSummonAtUtc);""",
            """            next = Min(next, _nextSummonAtUtc);\n            next = Min(next, NextGenericEncounterDueAtUtc);""",
        ),
        (
            """            CurrentTimeUtc = due;\n            ProcessEffects(due);""",
            """            CurrentTimeUtc = due;\n            ProcessGenericEncounterDue(due);\n            ProcessEffects(due);""",
        ),
        (
            """                _random,\n                currentThreatTargetId,\n                excludedAbilityIds: failedAbilityIds);""",
            """                _random,\n                currentThreatTargetId,\n                ownerLinkedTargetId: ResolveGenericEncounterOwnerTarget(enemyActorId),\n                excludedAbilityIds: failedAbilityIds);""",
        ),
        (
            """            RegisterThreat(normalized);\n            Append(normalized);\n            if (normalized.Type == CombatEventType.ActorDied""",
            """            RegisterThreat(normalized);\n            Append(normalized);\n            ProcessGenericEncounterEvent(normalized);\n            if (normalized.Type == CombatEventType.ActorDied""",
        ),
    ],
    Path("src/Elyndor.Infrastructure/Combat/CombatSessionFactory.cs"): [
        (
            """        if (!isTraining)\n            DungeonEncounterCombatConfigurator.Configure(session, monster.Id, contentSnapshot);""",
            """        if (!isTraining)\n        {\n            DungeonEncounterCombatConfigurator.Configure(session, monster.Id, contentSnapshot);\n            GenericEncounterCombatConfigurator.Configure(session, monster.Id, contentSnapshot);\n        }""",
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
