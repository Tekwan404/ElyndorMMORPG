# Citadel of Eclipse PvE abilities

## Goal

Turn the existing production `ECLIPSED_CITADEL` level-25 dungeon into a solo-to-five-player PvE learning sequence. The dungeon keeps its current stable dungeon, monster, loot, XP, gold and encounter IDs. Only its monster combat content gains the authored abilities and phase rules described here.

No raid mechanics, raid targeting, database schema, public API or UI subsystem is introduced.

## Runtime decision

Use existing data-driven monster abilities, `MonsterAiProfile` rules and the generic encounter runtime. The latter already supports HP thresholds, one-shot phase triggers, linked combat-object summons, summon auras removed on add death, ability-set changes, `CastInterrupted` triggers, shields and temporary vulnerability effects.

Do not add a dedicated Archon `CombatSession` partial. A specialised runtime would duplicate generic primitives without unlocking a current gameplay need.

## Content layout

New content stays in canonical category fragments:

- `content/abilities/eclipsed-citadel.json`: abilities and durable effect definitions.
- `content/monsters/eclipsed-citadel.json`: existing monster records, Citadel AI profiles and a passive Core definition.
- `content/bosses/eclipsed-citadel.json`: generic encounter definitions for the Golem and Archon.

The content package composer and validator remain authoritative. IDs are additive except for each existing Citadel monster's `abilityIds` and `aiProfileId` replacements.

## Trash encounters

### Ashen Sentinel

- `SENTINEL_ASHEN_THRUST`: instant current-target physical strike equal to 125% of the Sentinel's normal pre-mitigation attack and a refresh-only, non-stacking 10% Armor reduction for eight seconds.
- `SENTINEL_OATH_OF_OUTER_SEAL`: 1.8-second interruptible self cast, initial delay five seconds, 16-second cooldown; grants 20% outgoing damage for eight seconds.
- `ECLIPSED_CITADEL_SENTINEL_AI` gives the Oath priority over the Thrust.

### Bastion Stone Guardian

- `GOLEM_BLACKSTONE_FRACTURE`: instant current-target physical strike equal to 135% of a normal pre-mitigation attack and a refresh-only 15% Armor reduction for eight seconds.
- `ECLIPSED_CITADEL_GOLEM_ENCOUNTER`: a single `HpAtOrBelow(50)` generic trigger applies an 896-point shield for ten seconds (16% of the current 5,600 MaxHP).
- `ECLIPSED_CITADEL_GOLEM_AI` selects Fracture on its eight-second cooldown.

### Void Weaver

The inherited `BITE` and `SPIDER_BASIC_AI` are removed. The Weaver receives a spell-power baseline appropriate for a level-25 caster while preserving its existing HP, rewards and encounter position.

- `VOID_WEAVER_VOID_THREAD`: 1.5-second interruptible random-target Shadow cast every six seconds; direct damage plus a three-tick, six-second Shadow DoT.
- `VOID_WEAVER_VEIL_RUPTURE`: 2.3-second interruptible all-party Shadow cast, initial delay seven seconds and 13-second cooldown; applies a 15% multiplicative MagicResistance reduction for eight seconds.
- `ECLIPSED_CITADEL_VOID_WEAVER_AI` prioritises Veil Rupture above Void Thread.

### Black Constellation Executioner

The inherited `BITE` and `WOLF_BASIC_AI` are removed.

- `EXECUTIONER_BLACK_CONSTELLATION_MARK`: instant current-target physical strike equal to a normal pre-mitigation attack; refreshes a 10-second incoming-damage debuff with up to three 5% stacks.
- `EXECUTIONER_BLACK_VERDICT`: 2-second interruptible current-target physical strike equal to 180% of a normal pre-mitigation attack. Its AI rule is only eligible at the Executioner's own HP of 35% or lower.
- `ECLIPSED_CITADEL_EXECUTIONER_AI` prioritises the Verdict over Mark while it is eligible.

## Archon encounter

The Archon gets an appropriate level-25 spell-power baseline in addition to its existing melee profile. `ECLIPSED_CITADEL_ARCHON_ENCOUNTER` controls phase transitions; normal spell selection stays in `ECLIPSED_CITADEL_ARCHON_AI`.

### Base ability set (100% to 70%)

- `ARCHON_DEAD_STAR_BRAND`: instant current-target magical damage and an eight-second multiplicative 15% healing-received reduction (`0.85`).
- `ARCHON_ECLIPSE_ASH`: 1.4-second interruptible random-target Shadow cast with an eight-second, four-tick DoT.
- `ARCHON_STAR_FRACTURE`: 2.4-second interruptible party-wide spell; initial delay six seconds and 12-second cooldown.

### Core transition (at 70%)

`ARCHON_DEAD_STAR_CORE` is a no-reward linked combat object with 2,500 HP (10% of the Archon's current 25,000 MaxHP), no auto attack and no abilities. It receives normal combat-object presentation and despawns when its owner dies.

The Core applies `ARCHON_DEAD_STAR_VEIL`, a 15,000-point shield aura to the Archon. This makes ignoring the Core materially inefficient while remaining a survivable, non-wipe solo decision. The linked aura is removed automatically when the Core dies.

The Core's `AddDeath` trigger applies `ARCHON_BROKEN_VEIL`: 25% increased incoming damage to the Archon for eight seconds. It also changes the Archon to the phase-two ability set.

### Phase two (70% to 35%)

`ARCHON_DEVOUR_LIGHT` is a 2.5-second interruptible self heal every 18 seconds. It restores exactly 2,000 HP (8% of the current 25,000 MaxHP). The base abilities remain active.

### Final phase (at 35%)

The one-shot threshold applies two internal effects because one `EffectDefinition` changes one stat only:

- `ARCHON_FINAL_ECLIPSE_DAMAGE`: +15% outgoing damage.
- `ARCHON_FINAL_ECLIPSE_HASTE`: +15% attack speed.

The phase-three ability set adds `ARCHON_DEAD_STAR_COLLAPSE`: a three-second, interruptible, party-wide high-damage Shadow cast on a 13-second cooldown. An encounter `CastInterrupted` trigger keyed specifically to this ability applies `ARCHON_UNSTABLE_CORE`, increasing Archon incoming damage by 25% for six seconds. Interrupting any other cast does not activate this reward.

## Balance method

Physical abilities preserve the stated relative ratios by scaling both the monster base-damage and attack-power coefficients. Spell direct damage and DoT values are calibrated against the existing level-25 dungeon caster conventions and checked through deterministic combat scenarios.

The target is that a missed interrupt is recoverable by a normally equipped solo player, while repeated missed high-priority casts create unsustainable pressure. The Core receives no reward and cannot create an extra loot, XP or dungeon-completion path.

## Verification

Tests must cover:

1. every new AI priority, initial delay, cooldown and self-HP gate;
2. refresh/stack caps for Armor, MagicResistance, healing-received and incoming-damage effects;
3. Golem's one-shot 50% shield;
4. Archon 70% Core spawn, no reward, shield aura removal and Broken Veil;
5. Archon 35% final effects and phase-three ability set;
6. Collapse-specific interrupt reward, while an interrupt of another Archon cast grants no reward;
7. solo and party target selection, Core death, boss death cleanup and reconnect snapshot safety;
8. existing dungeon navigation, finalization and reward regressions.

Content validation, focused unit and integration tests, the relevant combat suite, backend build and final diff review are required before merge.
