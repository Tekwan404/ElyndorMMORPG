# Phase 4B — Mage Foundation & Talent Runtime

## Status

**Implemented and superseded by the full Mage Fire / Arcane / Frost rework.**

The original Phase 4B Pyromancer vertical slice introduced Mage combat support, but its old Fire Comet / Heat Limit, Arcane Charges and Frostbite-stack assumptions are no longer authoritative.

Current gameplay source of truth:

- `docs/source-of-truth/gameplay/25_MAGE_TALENT_TREE.md`
- `content/talents/mage-pyromancer.json`
- `content/abilities/mage-pyromancer.json`

Related engine contracts remain:

- `docs/source-of-truth/gameplay/02_COMBAT_SYSTEM.md`
- `docs/source-of-truth/gameplay/07_RESOURCE_SYSTEM.md`
- `docs/source-of-truth/gameplay/08_EFFECT_SYSTEM.md`
- `docs/source-of-truth/gameplay/09_DAMAGE_AND_HEALING_SYSTEM.md`
- `docs/source-of-truth/gameplay/10_ABILITY_SYSTEM.md`

## Goal retained from Phase 4B

`MAGE` remains a fully playable class inside the same authoritative `CombatSession`; the rework does not create a parallel combat, effect, resource or talent engine.

Class baseline:

```text
Primary Attribute = INTELLECT
Resource = MANA
Armor = CLOTH
Weapons = STAFF / WAND
```

Mana regeneration, cast timing, resource spending, damage, critical hits, effects, cooldowns, control and outcomes remain server authoritative.

## Current tree contract

`MAGE_TREE` now contains all three production branches:

```text
FIRE   = 32 talents
ARCANE = 32 talents
FROST  = 32 talents
TOTAL  = 96 talents
```

The tree uses 9 progression rows with branch-spend thresholds:

```text
0 / 5 / 10 / 15 / 20 / 25 / 30 / 35 / 40
```

A level-60 character can spend 59 points and hybrid builds are allowed.

Stable IDs `F-*`, `A-*`, `I-*` are preserved across the rework.

## Current branch loops

### Fire

```text
Crit → Ignite → Scorch → Pyroblast → Combustion
```

The old Heat Limit / Fire Comet loop is removed from the authoritative design.

### Arcane

```text
Mana → Clearcasting → free casts → Presence of Mind → Arcane Power
```

Arcane Charges are no longer the core loop.

### Frost

```text
Chill → Freeze / Deep Chill → Shatter → Ice Lance → Winter's Chill
```

Normal enemies can be Frozen. Bosses receive Deep Chill instead of forbidden hard Freeze control.

## Runtime mechanics now included

- SpellPower-based Magical damage through the shared damage pipeline.
- Talent-driven ability unlocks rather than automatic Mage starter spells.
- Fire rolling Ignite with residual damage preservation.
- Scorch Fire Vulnerability stacks.
- Pyroblast, Blast Wave and Combustion runtime.
- Hot Streak / Pyromaniac-style Fire sequencing.
- Clearcasting charge creation and consumption.
- Presence of Mind instant-cast window.
- Arcane Power burst and cost behavior.
- Mana Shield resource-backed absorption.
- Counterspell using the generic `AbilityEngine.Interrupt` path.
- Chill, Freeze, Deep Chill, Shatter and Winter's Chill.
- Ice Lance frozen/deep-chilled target multipliers.
- Cold Snap, Ice Barrier and Deep Freeze behavior.
- Ice Block immunity, cleanse and 3-second action lockout.
- Cold Blood after Ice Block expiry.
- Fire / Arcane / Frost threat reductions through the shared threat pipeline.
- Generic persisted talent-rank normalization when a published tree changes rank boundaries.

## Engine limitation: channels

The generic combat kernel does not yet expose a `Channelled` ability type.

Until that exists, the intended channels are represented as 4-second casted abilities:

```text
MAGE_ARCANE_MISSILES
MAGE_BLIZZARD
MAGE_EVOCATION
```

This is a representation limitation only. It does not change talent IDs or the intended branch loops.

## Persistence compatibility

When an existing character loads a talent state created against an older tree version:

- known IDs remain;
- ranks above current `MaxRank` are clamped;
- removed IDs are discarded;
- normalized loadouts and the current talent-tree version are persisted;
- normal new talent spending still uses strict `TalentRules` validation.

This protects existing Mage characters from rank-boundary changes without weakening the live allocation rules.

## Definition of Done for the full rework

- `MAGE_TREE` contains exactly 32 Fire, 32 Arcane and 32 Frost talents.
- All intended active abilities are content-defined and talent-unlocked.
- Fire, Arcane and Frost runtime mechanics use existing authoritative combat systems.
- Boss Frost control uses Deep Chill instead of hard Freeze.
- Counterspell interrupts an actual active cast.
- Ice Block prevents actions during immunity and can lead into Cold Blood.
- Legacy saved ranks normalize safely to the published tree.
- Content validation is green.
- Backend unit/integration tests are green.
- Web and admin checks are green.
- Production publish-layout validation is green.
- Real Browser → Server → PostgreSQL E2E is green.

The phase is not considered merge-ready if any of those checks fail.
