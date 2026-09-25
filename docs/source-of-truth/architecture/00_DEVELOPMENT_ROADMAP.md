# Elyndor — Development Roadmap

**Status:** Engineering Source of Truth  
**Updated:** 2026-09-25  
**Model:** Build → Playtest → Refine → Expand

## 1. Principle

Elyndor is developed as one modular-monolith architecture. We do not maintain a separate throwaway «beta architecture».

Every meaningful milestone ends in a playable state:

```text
contract/content decision
→ implementation
→ automated checks
→ local/browser playtest
→ Telegram/mobile playtest
→ fix UX/balance/reliability
→ next slice
```

`README.md` is the current as-built snapshot. This document defines **development direction and ordering**, not a historical checklist of every completed PR.

## 2. Engineering rules

- Source of Truth changes with the gameplay decision, not months later.
- Backend remains server-authoritative.
- PostgreSQL is permanent state; caches/Redis are introduced only for measured needs.
- Content and balance stay data-driven/versioned/validated.
- Rewards and retryable mutations must be safe against duplicate execution.
- Reconnect/restart behavior is designed with the feature.
- Combat, pet, boss, talent and dungeon mechanics reuse common engines instead of creating parallel implementations.
- Frontend and gameplay are developed together.
- One PR = one logical change set.
- New large systems do not outrank finishing an already-shipped player loop.

## 3. Implemented foundation

The current `main` already contains the broad playable foundation:

- Telegram identity/account/character flow;
- world, travel, locations and server-driven encounters;
- stats/resources/effects/damage/healing/abilities;
- CombatSession with normal enemies and boss mechanics;
- progression, items, equipment, loot and inventory;
- talents/content for the current class roster;
- Party runtime up to 5 players;
- companion/pet foundations;
- contracts/quests and AFK/auto-hunt foundations;
- dungeons/boss content;
- currency/store/skins foundations;
- mobile-first Vue player UI;
- content validator, backend/frontend tests and browser E2E;
- production/VPS packaging and observability.

Playable character-creation roster is currently Warrior, Archer, Mage and Paladin.

## 4. Current development order

### Milestone A — Four-class combat completion

Goal: make the existing classes trustworthy before adding another class/system.

Required pass for Warrior / Archer / Mage / Paladin:

```text
talent unlock
→ ability appears
→ target validation
→ resource cost
→ cooldown / GCD / cast
→ damage / healing
→ effect / threat
→ combat events/log
→ UI feedback
→ reconnect behavior
```

Include solo and party regressions, ally targeting/healing, aggro transfer, death/wipe/victory and no duplicate rewards.

Warrior specifically needs its authored Guardian/Berserker/Commander design reconciled with actual runtime/content coverage.

### Milestone B — Item/content presentation completion

- finish authoritative set presentation (`SetId` → name/thresholds/bonuses);
- finish item icon/art pipeline and missing-asset validation;
- remove remaining player-facing technical/development text;
- keep inventory capacity authoritative (`30 + Spatial Artifact`);
- ensure item cards/store/loot/location screens use backend/content presentation data instead of frontend hardcodes.

### Milestone C — Dungeon vertical-slice quality

Use the existing dungeon pipeline (1–5 players) as the main group-content proving ground.

Validate at least Ancient Mine and Black Bastion end-to-end for:

- solo where allowed;
- 2–3 players;
- full party;
- boss mechanics;
- wipe/retry/reconnect;
- loot/reward idempotency;
- mobile battle presentation.

Add new dungeon content only after existing encounters play cleanly.

### Milestone D — World/content completion

- close location/content gaps;
- improve contracts and progression pacing;
- finish 30–60 content/itemization in coherent bands;
- continue monster/location/item art coverage;
- balance from real playtests rather than isolated JSON values.

### Milestone E — Beta hardening

- lifecycle/reconnect/restart failure passes;
- database/index/query review;
- rate limits and abuse boundaries where measured;
- telemetry/health/admin recovery paths;
- release/build marker consistency;
- browser + Telegram device matrix;
- regression suite for critical player loops.

## 5. Deferred until explicitly resumed

These are valid future systems but are **not the current priority**:

- 20-player Raid gameplay;
- PvP;
- broad Trade/Auction economy completion;
- deep Guild gameplay;
- deep Crafting/Professions loops;
- additional playable classes;
- infrastructure added only for hypothetical scale.

Raid design may remain documented, but current authored group content should use the working dungeon pipeline until Raid is resumed as its own milestone.

## 6. Definition of Done for a milestone

A milestone is done only when the relevant player-facing flow is playable, not merely represented by backend classes or content JSON.

Minimum evidence as applicable:

- Release build;
- backend unit/integration tests;
- content validation;
- frontend type/lint/unit/build;
- browser E2E against the real server/database for critical paths;
- mobile/Telegram visual playtest;
- documentation reflects actual boundaries;
- known limitations are explicit instead of hidden behind placeholders.

## 7. What not to do

Do not restart the project from an old numbered phase document. Those implementation snapshots are historical and belong in Git history.

Do not treat an open/stale PR as current product state.

Do not expand scope because a design contract exists. Current ordering is driven by the playable loop above.
