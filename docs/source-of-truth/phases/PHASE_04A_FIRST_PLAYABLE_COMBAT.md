# Phase 4A — First Playable Combat

## Goal

Deliver the first server-authoritative Telegram Mini App combat slice in `WHISPERING_FOREST` with a small prototype monster roster and a fully executable Berserker talent branch for Warrior.

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
- `G-1-2` remains the first Guardian CombatSession hook.
- All 32 Berserker nodes are executable in the current single-player CombatSession. The Phase 3C `Deferred -> COMBAT_SESSION` contracts for Berserker are promoted through the typed `BerserkerTalentRuntimeCatalog`; Party/Guardian/Warlord deferred contracts remain deferred.
- Berserker runtime includes HP-threshold modifiers, Rage-spend procs, critical/auto-attack procs, Berserk-conditional behavior, cooldown reduction/reset, personal vulnerability, snapshot bleeds, periodic kills, and once-per-session next-attack state.
- Existing Phase 3C content IDs and talent descriptions remain authoritative. No second talent system is introduced.

## Playable flow

```text
Telegram Mini App
→ Мир
→ Whispering Forest
→ Исследовать
→ WOLF / FOREST_BOAR / GIANT_SPIDER encounter
→ Начать бой
→ server CombatSession snapshot
→ Monster AI + player auto attack/abilities + Berserker runtime
→ Victory / Defeat
→ return to Whispering Forest
```

The separate bottom-navigation Combat entry is intentionally removed. Combat is entered through the world exploration loop.

## Content layout

The base package remains `content/package.json`. Phase 4A monster runtime data lives in the versioned `content/whispering-forest-monsters.json` overlay. `GameContentPackageLoader` merges that overlay before running the normal package validator, so build/publish copies it automatically and no manual generation step is required.

The overlay currently reports content/balance versions `0.6.1 / 0.5.1`.

The Berserker compatibility runtime does not redefine talent selection or progression. It interprets the already-versioned Phase 3C Berserker `COMBAT_SESSION` contracts until those legacy `Deferred` flags are regenerated into a richer content schema.

## Explicitly deferred

XP, loot, equipment rewards, durable combat persistence, multiple simultaneous enemies, threat/party combat, elites, bosses, server-owned/random encounter selection, Guardian CombatSession hooks beyond the currently supported slice, and Warlord/Party deferred talent hooks.

## Verification status

Implementation includes deterministic coverage for the Berserker runtime catalog, low-HP conditional effects, Berserk/Frenzy/control cleanse, Double Strike, Whirlwind bleed/true-damage extension, Death's Embrace, and periodic combat-ending damage. Server build, full backend tests, content validation, frontend checks, and Telegram manual playtest remain required before marking Phase 4A fully verified.
