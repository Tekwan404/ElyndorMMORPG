---
name: elyndor-combat
description: Design, implement, test, or review Elyndor combat, abilities, resources, damage, effects, monsters, combat UI, or simulation. Use for any Combat System change.
---

# Elyndor Combat

Read `docs/source-of-truth/gameplay/02_COMBAT_SYSTEM.md`, `docs/source-of-truth/gameplay/07_RESOURCE_SYSTEM.md`, `docs/source-of-truth/gameplay/08_EFFECT_SYSTEM.md`, `docs/source-of-truth/gameplay/09_DAMAGE_AND_HEALING_SYSTEM.md`, `docs/source-of-truth/gameplay/10_ABILITY_SYSTEM.md`, relevant class/monster documents, and the current phase before changes.

## Mandatory checks

- Server authority: commands express intent; the server resolves validation, timing, RNG, damage, effects, rewards, and outcomes.
- Determinism: use `TimeProvider` and an injectable game RNG boundary; simulation and unit tests must control both.
- Concurrency: all client commands, AI decisions, scheduled actions, and effect ticks enter the combat session's single-writer pipeline.
- Timing: use absolute timestamps and deterministic priority ordering; do not use Quartz or a fixed global physics tick.
- Mechanics: verify resource generation/spend, priority/procs, reaction windows, cast timing, interrupts, telegraphs, and risk/reward.
- Recovery: define disconnect, reconnect snapshot/version, process restart, interrupted combat, and stale command behavior.
- Economy safety: completion and rewards are atomic/idempotent; an interrupted or duplicate completion cannot grant rewards twice.
- Simulation: important formulas and kits must run headlessly without Vue or infrastructure.

## Prototype scope

- Warrior: Rage, offense versus defense, reactive play.
- Archer: Focus, priority/procs, faster actions.
- Mage: Mana, cast timing, sequencing, interrupt risk.
- Prefer approximately 5–7 meaningful active abilities per prototype class when the Source of Truth does not specify a smaller slice.
- Do not build production-size ability trees or add a fourth class before external validation of these three.
