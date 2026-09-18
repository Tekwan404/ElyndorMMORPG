from pathlib import Path

patches = {
    Path("src/Elyndor.Core/Combat/Abilities/AbilityEngine.cs"): [
        (
            """        if (targetIds.Length == 0\n            || targetIds.Distinct().Count() != targetIds.Length\n            || targetIds.Any(targetId =>\n                !runtime.Actors.TryGetValue(targetId, out CombatActorState? target)\n                || target.IsDead))\n        {\n            return AbilityErrorCode.InvalidTarget;\n        }\n\n        if (ability.TargetType == AbilityTargetType.Self""",
            """        if (targetIds.Length == 0\n            || targetIds.Distinct().Count() != targetIds.Length\n            || targetIds.Any(targetId =>\n                !runtime.Actors.TryGetValue(targetId, out CombatActorState? target)\n                || target.IsDead))\n        {\n            return AbilityErrorCode.InvalidTarget;\n        }\n        if (targetIds.Any(targetId =>\n                targetId != runtime.Actor.ActorId\n                && !runtime.Actors[targetId].IsTargetable(now)))\n        {\n            return AbilityErrorCode.InvalidTarget;\n        }\n\n        if (ability.TargetType == AbilityTargetType.Self""",
        ),
        (
            """                    case AbilityActionType.Fixate:\n                        events.Add(new CombatEvent(\n                            CombatEventType.FixateApplied,\n                            now,\n                            target.ActorId,\n                            ability.Id,\n                            (decimal)(action.Duration ?? TimeSpan.Zero).TotalSeconds,\n                            SourceActorId: runtime.Actor.ActorId,\n                            TargetActorId: target.ActorId));\n                        break;""",
            """                    case AbilityActionType.Fixate:\n                        events.Add(new CombatEvent(\n                            CombatEventType.FixateApplied,\n                            now,\n                            target.ActorId,\n                            ability.Id,\n                            (decimal)(action.Duration ?? TimeSpan.Zero).TotalSeconds,\n                            SourceActorId: runtime.Actor.ActorId,\n                            TargetActorId: target.ActorId));\n                        break;\n                    case AbilityActionType.TemporaryUntargetable:\n                        target.SetTemporaryUntargetable(now, action.Duration!.Value);\n                        events.Add(new CombatEvent(\n                            CombatEventType.TargetabilityChanged,\n                            now,\n                            target.ActorId,\n                            ability.Id,\n                            (decimal)action.Duration.Value.TotalSeconds,\n                            SourceActorId: runtime.Actor.ActorId,\n                            TargetActorId: target.ActorId));\n                        break;""",
        ),
        (
            """        if (ability.Actions?.Any(action =>\n                action.Type == AbilityActionType.AddThreat && action.Amount <= 0\n                || action.Type == AbilityActionType.DropThreatPercent\n                    && (action.Amount <= 0 || action.Amount > 100)\n                || action.Type == AbilityActionType.ClearThreat\n                    && (action.Amount != 0 || action.Duration is not null)\n                || action.Type == AbilityActionType.Fixate\n                    && (action.Amount != 0\n                        || action.Duration is null\n                        || action.Duration <= TimeSpan.Zero)) == true)\n        {\n            throw new InvalidOperationException(\n                \"Threat actions contain values outside their valid range.\");\n        }\n    }""",
            """        if (ability.Actions?.Any(action =>\n                action.Type == AbilityActionType.AddThreat && action.Amount <= 0\n                || action.Type == AbilityActionType.DropThreatPercent\n                    && (action.Amount <= 0 || action.Amount > 100)\n                || action.Type == AbilityActionType.ClearThreat\n                    && (action.Amount != 0 || action.Duration is not null)\n                || action.Type == AbilityActionType.Fixate\n                    && (action.Amount != 0\n                        || action.Duration is null\n                        || action.Duration <= TimeSpan.Zero)) == true)\n        {\n            throw new InvalidOperationException(\n                \"Threat actions contain values outside their valid range.\");\n        }\n        if (ability.Actions?.Any(action =>\n                action.Type == AbilityActionType.TemporaryUntargetable\n                && (action.Amount != 0\n                    || action.Duration is null\n                    || action.Duration <= TimeSpan.Zero)) == true)\n        {\n            throw new InvalidOperationException(\n                \"Temporary untargetable actions require a positive duration and zero amount.\");\n        }\n    }""",
        ),
    ],
    Path("src/Elyndor.Core/Content/Validation/GameContentPackageValidator.Abilities.cs"): [
        (
            """                        || action.Type == AbilityActionType.Fixate\n                            && (action.Amount != 0\n                                || action.Duration is null\n                                || action.Duration <= TimeSpan.Zero)\n                        || action.Type == AbilityActionType.ApplyEffect && action.Effect is null""",
            """                        || action.Type == AbilityActionType.Fixate\n                            && (action.Amount != 0\n                                || action.Duration is null\n                                || action.Duration <= TimeSpan.Zero)\n                        || action.Type == AbilityActionType.TemporaryUntargetable\n                            && (action.Amount != 0\n                                || action.Duration is null\n                                || action.Duration <= TimeSpan.Zero)\n                        || action.Type == AbilityActionType.ApplyEffect && action.Effect is null""",
        ),
    ],
    Path("src/Elyndor.Core/Combat/Sessions/CombatSession.cs"): [
        (
            """        Guid[] targetActorIds = ResolvePlayerAbilityTargetIds(ability, command.TargetActorId);""",
            """        Guid[] targetActorIds = ResolvePlayerAbilityTargetIds(ability, command.TargetActorId, now);""",
        ),
        (
            """    private Guid[] ResolvePlayerAbilityTargetIds(\n        AbilityDefinition ability,\n        Guid requestedTargetActorId)""",
            """    private Guid[] ResolvePlayerAbilityTargetIds(\n        AbilityDefinition ability,\n        Guid requestedTargetActorId,\n        DateTimeOffset now)""",
        ),
        (
            """            return _enemiesById.TryGetValue(\n                    targetActorId,\n                    out CombatParticipantDefinition? selected)\n                && !selected.Actor.IsDead\n                    ? [targetActorId]\n                    : [];""",
            """            return _enemiesById.TryGetValue(\n                    targetActorId,\n                    out CombatParticipantDefinition? selected)\n                && !selected.Actor.IsDead\n                && selected.Actor.IsTargetable(now)\n                    ? [targetActorId]\n                    : [];""",
        ),
        (
            """        return _enemies\n            .Where(enemy => !enemy.Actor.IsDead)\n            .Take(targetLimit)""",
            """        return _enemies\n            .Where(enemy => !enemy.Actor.IsDead && enemy.Actor.IsTargetable(now))\n            .Take(targetLimit)""",
        ),
        (
            """        if (!_enemiesById.TryGetValue(\n                command.TargetActorId,\n                out CombatParticipantDefinition? target)\n            || target.Actor.IsDead)""",
            """        if (!_enemiesById.TryGetValue(\n                command.TargetActorId,\n                out CombatParticipantDefinition? target)\n            || target.Actor.IsDead\n            || !target.Actor.IsTargetable(now))""",
        ),
        (
            """        DateTimeOffset? nextAtUtc = isOffHand\n            ? _nextPlayerOffHandAutoAttackAtUtc\n            : _nextPlayerMainHandAutoAttackAtUtc;\n        if (_playerRuntime.ActiveCast is null)""",
            """        DateTimeOffset? nextAtUtc = isOffHand\n            ? _nextPlayerOffHandAutoAttackAtUtc\n            : _nextPlayerMainHandAutoAttackAtUtc;\n        if (!_enemy.Actor.IsTargetable(due)\n            && _enemy.Actor.UntargetableUntilUtc is { } targetableAtUtc)\n        {\n            nextAtUtc = targetableAtUtc;\n            if (isOffHand)\n                _nextPlayerOffHandAutoAttackAtUtc = nextAtUtc;\n            else\n                _nextPlayerMainHandAutoAttackAtUtc = nextAtUtc;\n            return;\n        }\n        if (_playerRuntime.ActiveCast is null)""",
        ),
        (
            """                CombatParticipantDefinition? companionTarget = _enemiesById\n                    .GetValueOrDefault(_selectedTargetActorId);\n                if (companionTarget is null || companionTarget.Actor.IsDead)\n                    companionTarget = _enemies.FirstOrDefault(enemy => !enemy.Actor.IsDead);""",
            """                CombatParticipantDefinition? companionTarget = _enemiesById\n                    .GetValueOrDefault(_selectedTargetActorId);\n                if (companionTarget is null\n                    || companionTarget.Actor.IsDead\n                    || !companionTarget.Actor.IsTargetable(due))\n                {\n                    companionTarget = _enemies.FirstOrDefault(enemy =>\n                        !enemy.Actor.IsDead && enemy.Actor.IsTargetable(due));\n                }""",
        ),
    ],
}

for path, replacements in patches.items():
    text = path.read_text(encoding="utf-8")
    for index, (old, new) in enumerate(replacements, start=1):
        count = text.count(old)
        if count != 1:
            raise SystemExit(f"{path}: patch fragment {index} expected exactly once, found {count}.")
        text = text.replace(old, new, 1)
    path.write_text(text, encoding="utf-8")
