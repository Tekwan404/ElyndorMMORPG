# Phase 4A — First Playable Combat

## Goal

Deliver the first server-authoritative Telegram Mini App combat slice: one Warrior versus one `WOLF`.

## Bounded scope

- `CombatSession` is the single writer of runtime HP, Rage, effects, cooldowns, casts, death, and outcome.
- `CombatSessionRegistry` keeps at most one normal active fight per account/character in memory and serializes its commands.
- Restart cancels unfinished combat without rewards; combat runtime is not persisted in PostgreSQL or Redis.
- SignalR is transport only. The client sends `StartCombat`, `UseAbility`, `StartAutoAttack`, `StopAutoAttack`, `ResumeCombat`, and `LeaveCombat` intents.
- `WOLF`, `BITE`, Wolf AI, and prototype auto-attack profiles are versioned content.
- Wolf AI uses `BITE` when the shared ability pipeline permits it and otherwise auto-attacks.
- The client restores the combat screen from the latest authoritative snapshot after reconnect.
- The first CombatSession-owned Warrior hooks are `G-1-2` (`ON_DAMAGE_TAKEN`), `B-3-1` (`ON_CRITICAL_HIT`, one-second ICD), and `B-1-2` (`ON_ENEMY_KILLED`).

## Playable flow

```text
Telegram Mini App
→ Бой
→ Start WOLF combat
→ server snapshot
→ Wolf AI + player auto attack/abilities
→ Victory / Defeat
```

## Explicitly deferred

XP, loot, equipment rewards, durable combat persistence, multiple enemies, threat/party combat, elites, bosses, full Whispering Forest encounter content, and all remaining deferred talent hooks.

## Verification status

Implementation is present. Automated deterministic session, death, ICD, post-end command, serialized command, server build, frontend typecheck, and focused UI checks must be green. Telegram manual playtest is required before marking Phase 4A fully verified.
