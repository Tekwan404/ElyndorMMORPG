# Phase 4B — Mage Foundation & Pyromancer Vertical Slice

## Status

Active implementation slice.

## Source contracts

- `docs/source-of-truth/gameplay/25_MAGE_TALENT_TREE.md`
- `docs/source-of-truth/gameplay/02_COMBAT_SYSTEM.md`
- `docs/source-of-truth/gameplay/07_RESOURCE_SYSTEM.md`
- `docs/source-of-truth/gameplay/08_EFFECT_SYSTEM.md`
- `docs/source-of-truth/gameplay/09_DAMAGE_AND_HEALING_SYSTEM.md`
- `docs/source-of-truth/gameplay/10_ABILITY_SYSTEM.md`
- `docs/source-of-truth/phases/PHASE_04A_FIRST_PLAYABLE_COMBAT.md`

## Goal

Make `MAGE` the second fully playable prototype class in the existing single-player `CombatSession`, with the Fire/Pyromancer branch implemented end to end and without creating a parallel combat or talent system.

## Playable Mage baseline

The class uses:

```text
Primary Attribute = INTELLECT
Resource = MANA
Armor = LIGHT
Weapons = STAFF / WAND
```

Phase 4B starts the Mage with the three base abilities from the Mage Source of Truth:

```text
MAGE_FIREBALL
MAGE_ARCANE_SPARK
MAGE_ICE_SHARD
```

Mana regenerates during combat from the authoritative resource profile. Cast timing, resource spending, damage, critical hits, effects, cooldowns and outcomes remain server authoritative.

## Fire/Pyromancer tree

`MAGE_TREE` exposes the `FIRE` branch in this slice. The branch contains exactly 32 nodes and 69 possible rank-points. All 32 nodes participate in the single-player runtime when learned.

Talent-unlocked abilities:

```text
F-3-1 -> FLAME_FLASH
F-4-1 -> FIRE_WAVE
F-5-1 -> COMBUSTION
F-6-1 -> FIRE_COMET while HEAT_LIMIT is active
```

The implementation follows the exact values, prerequisites, thresholds, durations and internal cooldowns defined in `25_MAGE_TALENT_TREE.md`.

## Runtime mechanics included

- FIRE-specific Accuracy, CriticalChance, CriticalDamage, SpellPower scaling and MagicPenetration.
- Fireball damage, target HP threshold bonuses and Burn synergy.
- `BURN` as a source-specific Magical periodic effect using snapshot SpellPower.
- Quick Kindling, Hot Blood, Fire Rhythm and Flame Trail next-cast windows.
- `COMBUSTION` burst window and its Pyromancer upgrades.
- `HEAT_LIMIT`, three-critical-Fireball streak tracking and dynamic `FIRE_COMET` availability.
- Comet Burn, Comet aftershock, execute bonus and Avatar cooldown interaction.
- Inferno stacks during Combustion.
- Magical-critical reactive damage buff.
- FIRE kill Mana restore and Flame Flash reset.
- Avatar of Flame passive and next-Fireball reward after consuming Heat Limit.
- Proc-created damage remains non-recursive according to the Mage Source of Truth.

## Engine extensions

Phase 4B extends existing generic engine contracts rather than adding a Mage-only damage engine:

- ability damage can scale from SpellPower as well as AttackPower;
- an ability can carry temporary accuracy/critical/critical-damage/magic-penetration bonuses;
- periodic damage can specify its `DamageType` and resolve through the authoritative damage pipeline;
- `CombatSession` supports class resource regeneration and dynamic ability availability.

These extensions are reusable by later Arcane and Frost slices.

## UI

The combat UI must:

- label and render Mana instead of hard-coded Rage for Mage;
- show learned Mage/Pyromancer abilities;
- surface Fireball critical streak, Heat Limit, Burn and Combustion state;
- expose `FIRE_COMET` only while Heat Limit is active.

The talent UI is class-driven and can render both Warrior and Mage trees from the same talent API.

## Non-goals

Phase 4B does not implement:

- Arcane talent runtime;
- Frost talent runtime;
- party, raid or guild combat;
- a second combat engine;
- elites or bosses;
- new economy systems.

## Definition of Done

- A newly created Mage receives the intended Mage class profile and Mana resource.
- Mage can start normal Whispering Forest combat through the existing combat API.
- Fireball, Arcane Spark and Ice Shard use SpellPower and Magical mitigation.
- Combat Mana regeneration is deterministic and server authoritative.
- The Fire tree exposes 32 nodes with the documented ranks and prerequisites.
- Every Fire node is reported as runtime-supported when its behavior is implemented.
- Talent-unlocked Pyromancer abilities appear in bootstrap/combat snapshots.
- Heat Limit dynamically exposes and consumes Fire Comet.
- Burn and other periodic Fire damage can kill a target and finish combat through the normal damage/death pipeline.
- Warrior/Berserker behavior remains supported by the same engine.
- Relevant unit/integration/frontend checks are green before merge.
