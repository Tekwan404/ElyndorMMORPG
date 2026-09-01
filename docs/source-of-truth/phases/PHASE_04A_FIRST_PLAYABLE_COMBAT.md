# Phase 4A — First Playable Combat

## Goal

Deliver the first server-authoritative Telegram Mini App combat slice in `WHISPERING_FOREST` with a small prototype monster roster.

## Bounded scope

- `CombatSession` is the single writer of runtime HP, Rage, effects, cooldowns, casts, death, and outcome.
- `CombatSessionRegistry` keeps at most one normal active fight per account/character in memory and serializes its commands.
- Restart cancels unfinished combat without rewards; combat runtime is not persisted in PostgreSQL or Redis.
- SignalR is transport only. The client sends `StartCombat`, `UseAbility`, `StartAutoAttack`, `StopAutoAttack`, `ResumeCombat`, and `LeaveCombat` intents.
- Realtime authentication refreshes an expiring in-memory JWT before SignalR negotiation; the Hub remains authorized only at `/hubs/combat`.
- Prototype normal-monster roster: `WOLF`, `FOREST_BOAR`, `GIANT_SPIDER`.
- `WOLF` and `GIANT_SPIDER` use the existing `BITE` ability through their AI profile; `FOREST_BOAR` proves the auto-attack fallback with an empty ability priority list.
- The three monsters intentionally have different combat profiles: Wolf is balanced, Boar is slower/tougher/heavier, Spider is faster/more evasive/more fragile.
- These combats can be created only while the authoritative character location is `WHISPERING_FOREST`.
- Prototype exploration cycles through the three encounters deterministically so all can be manually tested. Server-owned/random encounter selection remains deferred.
- The client restores the combat screen from the latest authoritative snapshot after reconnect.
- The first CombatSession-owned Warrior hooks are `G-1-2` (`ON_DAMAGE_TAKEN`), `B-3-1` (`ON_CRITICAL_HIT`, one-second ICD), and `B-1-2` (`ON_ENEMY_KILLED`).

## Playable flow

```text
Telegram Mini App
→ Мир
→ Whispering Forest
→ Исследовать
→ WOLF / FOREST_BOAR / GIANT_SPIDER encounter
→ Начать бой
→ server CombatSession snapshot
→ Monster AI + player auto attack/abilities
→ Victory / Defeat
→ return to Whispering Forest
```

The separate bottom-navigation Combat entry is intentionally removed. Combat is entered through the world exploration loop.

## Content update

The committed updater `scripts/update-phase4-monster-content.mjs` writes the Phase 4A monster roster into `content/package.json` and bumps content/balance versions to `0.6.1 / 0.5.1`.

## Explicitly deferred

XP, loot, equipment rewards, durable combat persistence, multiple simultaneous enemies, threat/party combat, elites, bosses, server-owned/random encounter selection, and all remaining deferred talent hooks.

## Verification status

Implementation is present. Automated deterministic session, death, ICD, post-end command, serialized command, server build, frontend typecheck, realtime-auth, and focused world/combat UI checks must be green. Telegram manual playtest is required before marking Phase 4A fully verified.
