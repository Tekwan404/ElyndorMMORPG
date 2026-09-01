# Phase 4A — First Playable Combat Design

**Status:** Approved from the user-provided Phase 4A contract on 2026-09-01.

## Goal

Deliver the first server-authoritative playable combat slice in the Telegram Mini App: an existing Warrior starts a fight against the data-driven `WOLF`, uses known Warrior abilities and auto attack, observes authoritative HP/Rage/cooldowns/events over SignalR, and can reconnect to the in-memory session snapshot.

## Scope

Phase 4A includes one player, one normal monster, one active normal combat per character, `WOLF`, `BITE`, simple server AI, SignalR transport, a minimal Arcane Minimal combat screen, and the first three CombatSession-owned Warrior talent hooks.

It explicitly excludes XP, loot, inventory, gold, quests, elites, bosses, Party, PvP, durable combat persistence, history/replay, and multi-enemy encounter infrastructure.

## Architecture

The command path is:

```text
Telegram UI
  -> authenticated CombatHub command
  -> CombatApplicationService
  -> CombatSessionRegistry (per-session serialization)
  -> CombatSession (only runtime-state writer)
  -> existing Ability / Damage / Effect / Talent / RNG engines
  -> sequenced CombatEvent list + authoritative snapshot
  -> SignalR CombatUpdated envelope
  -> Telegram UI
```

`CombatHub` performs authentication and transport mapping only. It never calculates damage, resources, cooldowns, effects, death, or AI decisions.

`CombatSession` remains a synchronous deterministic Core aggregate. It owns the two actor runtimes, session status, event sequence, auto-attack schedules, AI schedule, cooldown/cast/effect state, and talent-hook internal cooldowns. The registry wraps each session in a per-session asynchronous gate, so commands for one session are sequential while unrelated sessions remain independent.

Production time comes only from injected `TimeProvider`. A one-shot session timer wakes the registry at the session's next meaningful due time. Tests use a controlled `TimeProvider` and may advance combat time explicitly; no production client command accepts a time delta.

## Runtime and restart boundary

Active normal combat state exists only in the in-memory registry. PostgreSQL is read at combat creation to obtain the character, calculated stats, active talent loadout, and known abilities. No combat tick or damage event is written to PostgreSQL.

On process restart the registry is empty. The unfinished fight is treated as cancelled and produces no rewards, XP, loot, or retrospective death. Reconnect within the same process calls `ResumeCombat` and receives the latest authoritative snapshot.

## Content

The existing single `content/package.json` entry point is extended compatibly with typed monster and monster-AI definitions. `WOLF` contains versioned level, HP, combat stats, auto-attack damage/interval, known ability IDs, and AI profile. `BITE` is a normal `AbilityDefinition` and goes through `AbilityEngine` and `DamagePipeline`.

The first balance profile is intentionally small and versioned; changing its numbers later is a content/balance change, not a CombatSession code change.

## Commands and scheduling

External commands are `StartCombat`, `UseAbility`, `StartAutoAttack`, `StopAutoAttack`, `ResumeCombat`, and `LeaveCombat`. Command identifiers prevent duplicate mutation within a session. Commands after terminal status fail with `combat_ended`.

The Wolf AI evaluates only at meaningful scheduled moments. It tries `BITE` when valid and ready, otherwise uses auto attack. It cannot receive client commands and cannot bypass normal ability validation. Auto attack and AI actions use the same actor state and damage pipeline as player abilities.

## Events and snapshots

The existing Core `CombatEvent` is evolved with monotonically increasing sequence and explicit source/target fields. Required session events are:

- `CombatStarted`
- `AbilityUsed`
- `AutoAttackStarted`
- `AutoAttackStopped`
- `DamageDealt`
- `CriticalHit`
- `EffectApplied`
- `EffectExpired`
- `ResourceChanged`
- `ActorDied`
- `EnemyKilled`
- `CombatEnded`

Snapshots contain session ID, sequence, status, player/enemy HP, player Rage, effects, cooldowns, known abilities, auto-attack state, and server time. Snapshot is sufficient to rebuild UI after reconnect; the client does not replay local state as authority.

## Talent hooks activated

Only three previously deferred CombatSession-owned hooks become supported:

- `G-1-2 Combat Stance`: positive HP damage received grants `2/3/4` Rage.
- `B-3-1 Critical Instinct`: a critical hit grants `4/8` Rage with a server-authoritative 1-second internal cooldown.
- `B-1-2 Bloodthirst`: a direct enemy kill grants `8/12/16` Rage and cannot proc from a periodic-effect kill.

The resolver exposes typed resolved event hooks including talent ID, rank, value, target, and internal-cooldown metadata. All other deferred hooks retain their current status and owner.

## SignalR and frontend

`/hubs/combat` requires the existing JWT. The JWT handler accepts `access_token` only for the combat hub path. The hub returns/publishes a single `CombatUpdated` envelope containing snapshot, new sequenced events, and optional stable error code. Reconnect invokes `ResumeCombat`; a missing session closes the combat view with an interrupted message.

The Pinia combat store keeps only the latest snapshot, deduplicated recent events, and connection state. Sequence gaps trigger `ResumeCombat`. The mobile combat view reuses current health bar, ability button, panel, loading, and error primitives. It shows Wolf/player HP, Rage, known available abilities, server cooldown state, auto-attack controls, leave action, and a compact event log.

## Verification

Five focused backend tests cover the user-mandated minimum: deterministic fight, one-time death/end, Critical Instinct ICD, command rejection after end, and serialized concurrent commands. One focused frontend test covers snapshot/event deduplication and the combat screen's server-driven controls. Final verification includes backend build/tests, content validation, frontend lint/typecheck/build/tests, a real browser mobile smoke test, SignalR connect/reconnect, and the local Warrior-vs-Wolf flow where credentials permit.

