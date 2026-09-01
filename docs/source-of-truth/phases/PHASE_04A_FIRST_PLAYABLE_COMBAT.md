# Phase 4A — First Playable Combat

## Goal

Deliver the first server-authoritative Telegram Mini App combat slice: one Warrior versus one `WOLF`.

## Bounded scope

- `CombatSession` is the single writer of runtime HP, Rage, effects, cooldowns, casts, death, and outcome.
- `CombatSessionRegistry` keeps at most one normal active fight per account/character in memory and serializes its commands.
- Restart cancels unfinished combat without rewards; combat runtime is not persisted in PostgreSQL or Redis.
- SignalR is transport only. The client sends `StartCombat`, `UseAbility`, `StartAutoAttack`, `StopAutoAttack`, `ResumeCombat`, and `LeaveCombat` intents.
- Realtime authentication refreshes an expiring in-memory JWT before SignalR negotiation; the Hub remains authorized only at `/hubs/combat`.
- `WOLF`, `BITE`, Wolf AI, and prototype auto-attack profiles are versioned content.
- Wolf AI uses `BITE` when the shared ability pipeline permits it and otherwise auto-attacks.
- `WOLF` combat can be created only while the authoritative character location is `WHISPERING_FOREST`.
- The client restores the combat screen from the latest authoritative snapshot after reconnect.
- The first CombatSession-owned Warrior hooks are `G-1-2` (`ON_DAMAGE_TAKEN`), `B-3-1` (`ON_CRITICAL_HIT`, one-second ICD), and `B-1-2` (`ON_ENEMY_KILLED`).

## Playable flow

```text
Telegram Mini App
→ Мир
→ Whispering Forest
→ Исследовать
→ WOLF encounter
→ Начать бой
→ server CombatSession snapshot
→ Wolf AI + player auto attack/abilities
→ Victory / Defeat
→ return to Whispering Forest
```

The separate bottom-navigation Combat entry is intentionally removed. Combat is entered through the world exploration loop.

## Explicitly deferred

XP, loot, equipment rewards, durable combat persistence, multiple enemies, threat/party combat, elites, bosses, full Whispering Forest encounter content, random encounter selection, and all remaining deferred talent hooks.

## Verification status

Implementation is present. Automated deterministic session, death, ICD, post-end command, serialized command, server build, frontend typecheck, realtime-auth, and focused world/combat UI checks must be green. Telegram manual playtest is required before marking Phase 4A fully verified.
