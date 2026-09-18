from pathlib import Path

patches = {
    Path("src/Elyndor.Core/Combat/Effects/EffectEngine.cs"): [
        (
            """    public static IReadOnlyList<CombatEvent> Process(\n        CombatActorState target,\n        DateTimeOffset now,\n        Func<ActiveEffect, DateTimeOffset, IReadOnlyList<CombatEvent>>? periodicDamageResolver = null)""",
            """    public static IReadOnlyList<CombatEvent> Process(\n        CombatActorState target,\n        DateTimeOffset now,\n        Func<ActiveEffect, DateTimeOffset, IReadOnlyList<CombatEvent>>? periodicDamageResolver = null,\n        Func<ActiveEffect, DateTimeOffset, IReadOnlyList<CombatEvent>>? expirationResolver = null)""",
        ),
        (
            """            events.Add(new CombatEvent(\n                CombatEventType.EffectExpired,\n                expired.ExpiresAtUtc,\n                target.ActorId,\n                expired.Definition.Id,\n                SourceActorId: expired.SourceId,\n                TargetActorId: target.ActorId));\n        }""",
            """            events.Add(new CombatEvent(\n                CombatEventType.EffectExpired,\n                expired.ExpiresAtUtc,\n                target.ActorId,\n                expired.Definition.Id,\n                SourceActorId: expired.SourceId,\n                TargetActorId: target.ActorId));\n            if (expirationResolver is not null\n                && expired.Definition.OnExpireActions is { Count: > 0 })\n            {\n                events.AddRange(expirationResolver(expired, expired.ExpiresAtUtc));\n            }\n        }""",
        ),
        (
            """        if (definition.Kind == EffectKind.DamageReflection\n            && (definition.Magnitude <= 0\n                || definition.ReflectedDamageCap is { } cap && cap <= 0))\n        {\n            throw new ArgumentException(\n                \"Damage reflection requires a positive ratio and positive cap when specified.\",\n                nameof(definition));\n        }""",
            """        if (definition.Kind == EffectKind.DamageReflection\n            && (definition.Magnitude <= 0\n                || definition.ReflectedDamageCap is { } cap && cap <= 0))\n        {\n            throw new ArgumentException(\n                \"Damage reflection requires a positive ratio and positive cap when specified.\",\n                nameof(definition));\n        }\n        if (definition.OnExpireActions?.Any(action =>\n                action.Type == EffectExpirationActionType.Damage && action.Amount <= 0\n                || action.Type == EffectExpirationActionType.ApplyEffect && action.Effect is null\n                || action.Type != EffectExpirationActionType.ApplyEffect && action.Effect is not null) == true)\n        {\n            throw new ArgumentException(\n                \"Effect expiration actions contain invalid values.\",\n                nameof(definition));\n        }""",
        ),
    ],
    Path("src/Elyndor.Core/Combat/Sessions/CombatSession.cs"): [
        (
            """                    (effect, tickAt) => ResolvePeriodicEffectDamage(effect, _player.Actor, tickAt)),""",
            """                    (effect, tickAt) => ResolvePeriodicEffectDamage(effect, _player.Actor, tickAt),\n                    (effect, expiresAt) => ResolveExpiredEffectActions(effect, _player.Actor, expiresAt)),""",
        ),
        (
            """                    (effect, tickAt) => ResolvePeriodicEffectDamage(effect, _companion.Actor, tickAt)),""",
            """                    (effect, tickAt) => ResolvePeriodicEffectDamage(effect, _companion.Actor, tickAt),\n                    (effect, expiresAt) => ResolveExpiredEffectActions(effect, _companion.Actor, expiresAt)),""",
        ),
        (
            """                    (effect, tickAt) => ResolvePeriodicEffectDamage(effect, enemy.Actor, tickAt)),""",
            """                    (effect, tickAt) => ResolvePeriodicEffectDamage(effect, enemy.Actor, tickAt),\n                    (effect, expiresAt) => ResolveExpiredEffectActions(effect, enemy.Actor, expiresAt)),""",
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
