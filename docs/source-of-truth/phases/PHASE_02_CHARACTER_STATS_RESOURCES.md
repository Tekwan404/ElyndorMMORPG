# Phase 2 — Character Stats and Class Resources

**Status:** Approved for implementation
**Owner:** Phase 2 execution contract
**Entry gate:** Every applicable Phase 1 Definition of Done item is verified.

## Outcome

The persistent Phase 1 character becomes a server-authoritative RPG entity whose class profile, primary/derived stats, health, and Rage/Focus/Mana state are loaded from versioned content, calculated deterministically, restored after reload, and displayed in the mobile client.

## Source of Truth

- `docs/source-of-truth/phases/ELYNDOR_PHASES_0-5.md`
- `docs/source-of-truth/gameplay/05_CHARACTER_SYSTEM.md`
- `docs/source-of-truth/gameplay/06_ATTRIBUTES_AND_STATS_SYSTEM.md`
- `docs/source-of-truth/gameplay/07_RESOURCE_SYSTEM.md`
- `docs/source-of-truth/gameplay/11_PROGRESSION_SYSTEM.md`
- `docs/source-of-truth/gameplay/12_CLASS_SYSTEM.md`
- `docs/source-of-truth/gameplay/19_CLASS_ROSTER_AND_CHARACTER_CREATION.md`
- `docs/source-of-truth/architecture/00_CONTENT_AND_BALANCE_PROFILES.md`
- `docs/source-of-truth/ui/UI_03_HERO.md`
- `docs/source-of-truth/ui/UI_05_CHARACTER_STATS.md`

## Scope

Phase 2 includes data-driven class/stat/resource profiles, deterministic stat calculation, persistent health/resource recovery checkpoints, Warrior Rage, Archer Focus, Mage Mana, character UI, and verification.

Phase 2 excludes abilities, damage, effects, combat sessions, monsters, loot, equipment mutations, talents, companions, and combat UI.

## Approved stat set

Primary: `Strength`, `Agility`, `Intellect`, `Stamina`.

Offensive: `AttackPower`, `SpellPower`, `CriticalChance`, `CriticalDamage`, `Accuracy`, `ArmorPenetration`, `MagicPenetration`, `AttackSpeed`.

Defensive: `Armor`, `MagicResistance`, `Dodge`.

System values include `MaxHP`, `CurrentHP`, `MaxResource`, and `CurrentResource`. Spirit, Block, Parry, CastSpeed, MovementSpeed, and new stats not listed above are forbidden in Phase 2.

## Stat pipeline

```text
Base → Class → Equipment → Talent → Effect → Final
```

All stages exist as explicit empty-or-populated inputs so later phases can add sources without replacing the calculator. Phase 2 populates Base/Class and leaves Equipment/Talent/Effect empty; it does not implement those future systems.

Final stats are immutable runtime values calculated by a pure domain service. They are never accepted from the client and are not stored as permanent truth. PostgreSQL stores character identity/progression and authoritative checkpoint state; content plus those inputs reproduce final stats.

Every balance coefficient and initial class value is owned by the versioned class/stat content profile. No coefficient is scattered through controllers or Vue code.

## Class profiles

```text
WARRIOR → Strength → Rage
ARCHER  → Agility  → Focus
MAGE    → Intellect → Mana
```

Each definition contains stable IDs for base stats, level growth, resource profile, allowed weapon categories, allowed armor categories, and prototype identity metadata. Race and gender do not modify stats.

## Resource rules

```text
Mana:  Max 100, Start 100, Respawn 100, combat regen 4/sec, out-of-combat regen 12/sec
Rage:  Max 100, Start 0, Respawn 0, out-of-combat decay 5/sec after the configured delay
Focus: Max 100, Start 100, Respawn 100, combat regen 8/sec, out-of-combat regen 12/sec
```

Without Phase 3 abilities/combat, Phase 2 exposes deterministic operations for clamp, spend, restore, elapsed-time regeneration/decay, and respawn. It does not generate Rage from attacks or damage because those events do not exist yet.

Elapsed-time recovery uses injected `TimeProvider`, persisted checkpoint value, its UTC timestamp, and current context. Repeating a calculation for the same inputs is deterministic. Values are clamped to `[0, MaxResource]`.

## Persistence boundary

```text
Permanent character state → PostgreSQL
Active combat runtime state → future authoritative single-writer CombatSession
Persistence → defined checkpoints, combat completion, and recovery policy
```

Phase 2 may persist current HP/resource and checkpoint timestamps when character state is created, restored, or explicitly checkpointed. It must not introduce database writes per future attack, tick, or resource change.

## API and frontend

The authenticated bootstrap/character response is extended with calculated stats, health, resource type/value/max, class profile version, and balance version. There is no endpoint that accepts client-provided stats or resource totals.

The mobile UI adds real HP/resource bars and a character-stat screen with loading, error, and restored states. Resource colors and labels differ for Rage, Focus, and Mana. Tooltips describe server-provided values without recalculating them in TypeScript.

## Testing contract

- Unit: aggregation order, clamps, formulas, class selection, level inputs, resource spend/restore/elapsed time, and race/gender neutrality.
- Content: duplicate IDs, missing profile references, forbidden stats, unsupported classes/resources, and invalid numeric ranges fail validation.
- PostgreSQL integration: checkpoint persistence, UTC timestamps, restart restoration, and no stored final-stat columns.
- API: server rejects attempted stat injection and returns reproducible snapshots.
- Frontend: all three resource models, zero/full values, loading/error/restored presentation.
- Playwright: Warrior/Archer/Mage fixtures, reload, and restored stats/resources in a Telegram-like viewport.

## Definition of Done

- [ ] Warrior, Archer, and Mage load different versioned class/resource profiles.
- [ ] The complete approved stat set is calculated server-side through the deterministic pipeline.
- [ ] Final stats are reproducible and are not persisted as permanent truth.
- [ ] Rage, Focus, and Mana clamp/spend/restore/time/respawn rules pass deterministic tests.
- [ ] No HTTP contract permits client-owned stats or resources.
- [ ] HP/resource checkpoints restore correctly after reload and process restart.
- [ ] No per-combat-action PostgreSQL persistence is introduced.
- [ ] Character HUD and stat UI render all three class resource models.
- [ ] Backend, content, PostgreSQL, frontend, and Playwright checks pass.
- [ ] Diff review finds no abilities, damage, effects, combat, or other Phase 3 scope.
