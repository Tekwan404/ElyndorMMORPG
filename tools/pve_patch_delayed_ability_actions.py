from pathlib import Path

path = Path("src/Elyndor.Core/Combat/Abilities/AbilityEngine.cs")
text = path.read_text(encoding="utf-8")
replacements = [
    (
        """            foreach (AbilityActionDefinition action in ability.Actions)\n            {\n                switch (action.Type)""",
        """            foreach (AbilityActionDefinition action in ability.Actions)\n            {\n                if (action.Delay is { } delay && delay > TimeSpan.Zero)\n                {\n                    runtime.SchedulePendingAction(\n                        ability,\n                        action with { Delay = null },\n                        targetId,\n                        targetModifier,\n                        now + delay);\n                    continue;\n                }\n\n                switch (action.Type)""",
    ),
    (
        """    private static Guid[] ResolveTargetIds(\n        AbilityDefinition ability,\n        AbilityIntent intent)""",
        """    public static IReadOnlyList<CombatEvent> ResolvePendingActions(\n        CombatRuntimeState runtime,\n        DateTimeOffset now,\n        IGameRandom? random = null)\n    {\n        ArgumentNullException.ThrowIfNull(runtime);\n        PendingAbilityAction[] due = runtime.PendingActions\n            .Where(action => action.ExecuteAtUtc <= now)\n            .OrderBy(action => action.ExecuteAtUtc)\n            .ThenBy(action => action.Sequence)\n            .ToArray();\n        if (due.Length == 0)\n            return [];\n\n        List<CombatEvent> events = [];\n        foreach (PendingAbilityAction pending in due)\n        {\n            runtime.PendingActions.Remove(pending);\n            if (!runtime.Actors.TryGetValue(\n                    pending.TargetId,\n                    out CombatActorState? target)\n                || target.IsDead)\n            {\n                continue;\n            }\n\n            AbilityDefinition delayedAbility = pending.Ability with\n            {\n                Actions = [pending.Action]\n            };\n            EnsureExecutable(delayedAbility, random);\n            Dictionary<Guid, AbilityTargetModifier> targetModifiers = new()\n            {\n                [pending.TargetId] = pending.TargetModifier\n            };\n            IReadOnlyList<CombatEvent> resolved = ResolveActions(\n                runtime,\n                delayedAbility,\n                [pending.TargetId],\n                targetModifiers,\n                pending.ExecuteAtUtc,\n                random);\n            events.AddRange(resolved.Select(combatEvent => combatEvent with\n            {\n                DefinitionId = combatEvent.DefinitionId ?? pending.Ability.Id,\n                SourceActorId = combatEvent.SourceActorId ?? runtime.Actor.ActorId,\n                TargetActorId = combatEvent.TargetActorId ?? pending.TargetId\n            }));\n        }\n\n        runtime.Version++;\n        return events;\n    }\n\n    private static Guid[] ResolveTargetIds(\n        AbilityDefinition ability,\n        AbilityIntent intent)""",
    ),
    (
        """        if (ability.Actions?.Any(action =>\n                action.Type == AbilityActionType.Dispel\n                && string.IsNullOrWhiteSpace(action.DispelCategory)) == true)\n        {\n            throw new InvalidOperationException(\n                \"Dispel actions require a dispel category.\");\n        }\n    }""",
        """        if (ability.Actions?.Any(action =>\n                action.Type == AbilityActionType.Dispel\n                && string.IsNullOrWhiteSpace(action.DispelCategory)) == true)\n        {\n            throw new InvalidOperationException(\n                \"Dispel actions require a dispel category.\");\n        }\n        if (ability.Actions?.Any(action =>\n                action.Delay is { } delay && delay < TimeSpan.Zero) == true)\n        {\n            throw new InvalidOperationException(\n                \"Ability action delay cannot be negative.\");\n        }\n    }""",
    ),
]

for index, (old, new) in enumerate(replacements, start=1):
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"Patch fragment {index} expected exactly once, found {count}.")
    text = text.replace(old, new, 1)

path.write_text(text, encoding="utf-8")
