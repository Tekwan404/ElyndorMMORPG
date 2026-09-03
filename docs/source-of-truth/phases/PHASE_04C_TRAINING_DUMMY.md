# Elyndor — Phase 4C — Starter Town Training Dummy

## Goal

Add a safe build-testing target to `STARTER_TOWN` without introducing a separate combat simulator.

The training dummy uses the same server-authoritative `CombatSession`, `AbilityEngine`, `DamagePipeline`, `EffectEngine`, resource rules and talent runtime as normal combat. This ensures Warrior/Berserker and Mage/Pyromancer builds are tested against the real gameplay pipeline.

## Training target

```text
DefinitionId = TRAINING_DUMMY
Location = STARTER_TOWN
MaxHP = 10000
Armor = 0
MagicResistance = 0
Dodge = 0
CanAttack = false
CanDie = false
```

The dummy may reach 1 HP but never reaches a dead state. This intentionally keeps low-HP/execute conditions testable without ending the session.

The full calculated damage remains available through combat events even when the dummy is already at the 1 HP floor, so DPS statistics continue to represent the build's real output.

Kill-only mechanics do not trigger against the dummy because it never emits `ActorDied` or `EnemyKilled`.

## Player state

Starting a training session creates an isolated in-memory combat context:

- player HP starts full;
- action resource starts at the class resource profile's `StartValue`;
- normal combat regeneration applies;
- cooldowns, effects, proc state and talent state are the real CombatSession runtime state;
- consumables are disabled.

Leaving or resetting training must not persist the training session's HP/resource state back to PostgreSQL.

## Rewards and progression

Training grants nothing:

```text
XP = 0
Gold = 0
Loot = none
Progression mutation = none
```

The combat finalizer exits before durable state mutations for `TRAINING_DUMMY` sessions.

## Reset

`ResetTraining` atomically discards the current training session through the CombatSession registry's single-writer gate and creates a new one.

Reset restores:

- player HP/resource start state;
- dummy HP;
- cooldowns;
- active effects;
- talent proc/streak state;
- combat log/stat counters on the client.

The reset must not emit a temporary terminal combat update to the client.

## UI

`STARTER_TOWN` contains a `Тренировочный манекен` service card with a `Тренироваться` action.

During training the combat UI shows:

- elapsed time;
- total calculated damage;
- DPS;
- critical-hit count;
- maximum calculated hit;
- normal class-specific combat state such as Pyromancer Burn / Fireball streak / Heat Limit.

Training controls include auto attack, `Сбросить тренировку`, and `Завершить тренировку`.

No new art asset is required for this slice; the dummy placeholder is rendered with UI/CSS only.

## Scope guardrails

This phase does not add:

- configurable armor/resistance presets;
- boss dummy rules;
- saved DPS history or leaderboards;
- party training;
- fake kill events for kill-proc testing;
- separate combat formulas for training.

Those can be separate slices if later needed.
