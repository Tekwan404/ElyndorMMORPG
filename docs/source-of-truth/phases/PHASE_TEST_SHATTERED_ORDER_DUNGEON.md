# Test Slice — Shattered Order Dungeon Encounters

**Status:** approved experimental implementation slice  
**Branch:** `feat/shattered-order-test-dungeon`  
**Purpose:** validate reusable boss/encounter mechanics in party combat  
**Rewards:** intentionally disabled for this slice

## Goal

Build the test dungeon **Цитадель Расколотого Ордена** as an encounter laboratory for Elyndor.

The slice must prove that bosses can create meaningful combat decisions without introducing movement, positioning, facing, cover buttons, or other pseudo-3D actions. Mechanics must operate through the existing server-authoritative combat model: target selection, multiple enemies, resources, casts, interrupts, effects, shields, damage, healing, threat, deaths, and encounter state.

## Core constraints

- No movement/position mechanics.
- No loot tables, unique equipment, cosmetics, gold, XP farming, contracts, or progression dependency.
- Existing `CombatSession` remains the combat engine; there is no second dungeon-specific combat engine.
- Boss state is single-writer encounter runtime state owned by the active combat session/coordinator.
- Damage, healing, threat, abilities, resources, and effects continue through their existing authoritative systems.
- Dungeon run/checkpoint persistence remains owned by the existing dungeon layer.
- Boss transient phase state does not require new database persistence for the test slice; a wiped encounter is reconstructed from its checkpoint.
- Mechanics must expose enough state/events for the Telegram-first combat UI to explain why player decisions matter.

## Dungeon roster

The test dungeon contains four boss encounters:

1. **Зеркальный Кастелян** — linked adds, conditional reflection barrier, burst window.
2. **Архимаг Веллариус** — boss Mana, interrupt-driven resource denial, Mana-to-shield conversion.
3. **Хранитель Душ Мор-Эт** — timed hostile soul copies based on player class profiles and permanent failure stacks.
4. **Триединый Магистр Азраэль** — three linked boss targets, healing/shielding roles and synchronized kill/revive window.

Short trash packs may be added only when they teach a mechanic used by the next boss.

## Implementation order

### Slice 1 — Mirror encounter runtime

Create a small reusable linked-encounter state model for the Mirror Castellan:

- HP threshold phases at 70% and 35%;
- three required add roles: Guardian, Priest, Executioner;
- barrier active while the linked add wave is alive;
- 50% base direct-damage reflection;
- Guardian adds +20% reflection while alive;
- all adds dead removes the barrier;
- barrier break starts **Расколотое зеркало** for 8 seconds;
- boss takes 25% increased damage during that burst window.

The state object must not calculate damage, threat, healing, rewards, or AI actions. It only owns phase/linkage state and exposes deterministic decisions for the combat runtime.

### Slice 2 — Mirror integration

Wire the runtime into the existing multi-enemy `CombatSession`:

- threshold crossing spawns the three linked adds;
- each add receives its own existing Monster AI/runtime/threat state;
- direct damage against the boss while the barrier is active produces reflected damage against the attacker through the authoritative damage pipeline;
- Guardian death immediately reduces reflection from 70% to 50%;
- last linked add death removes reflection and applies the 8-second damage-taken window;
- Priest has an interruptible heal;
- Executioner gains encounter pressure over time without inventing a second damage system;
- combat events/logging explain barrier activation, reflection changes, add deaths and the broken-mirror window.

### Slice 3 — Velarius resource boss

Extend monster participants/content so bosses can use real resources:

- visible Mana resource;
- ability costs validated by the existing Ability/Resource systems;
- configurable cost timing, including cost-at-cast-start for interruptible boss spells;
- mana-feeder adds;
- at 10% HP remaining Mana converts to an absorb shield;
- test rule: 1 Mana = 0.5% boss Max HP shield.

### Slice 4 — Mor-Et soul copies

Add reusable timed encounter adds with combat profiles:

- select party members server-side;
- spawn a hostile soul profile by class/archetype instead of cloning the entire Character aggregate;
- owner receives the encounter debuff while soul exists;
- soul death grants the owner a temporary success buff;
- timeout lets the boss consume the soul, heal, and gain a permanent encounter stack;
- second wave increases the number of souls.

### Slice 5 — Azrael linked revive window

Add a reusable linked-target revive rule:

- Fire, Frost and Void clones have independent HP/AI/runtime state;
- first clone death starts a 10-second window;
- all linked clones must die before the window expires;
- otherwise dead clones revive at 35% HP;
- every successful revive increments a permanent final-phase boss damage stack;
- after all three die in one window, the main boss returns for the final phase.

## Failure cases to cover

The implementation and tests must explicitly cover:

- HP jumps across a threshold from one large hit;
- a second threshold cannot trigger while the first barrier wave is still unresolved;
- add deaths in any order;
- Guardian dies first versus last;
- duplicate add registration/events do not break encounter state;
- reflected damage cannot recursively reflect itself;
- boss death cannot bypass required linked encounter completion unless the mechanic explicitly allows it;
- wipe/reset clears transient encounter state and does not retain old add actor ids/timers/stacks;
- interrupt and resource spending remain single-writer and deterministic;
- target switching after an add dies remains valid;
- reconnect snapshots contain enough state to reconstruct the visible encounter correctly.

## Transaction and ownership boundaries

Boss phase mechanics are transient combat state and execute inside the existing single-writer combat processing boundary.

The encounter runtime may emit/return intentions such as:

- begin phase;
- spawn linked add;
- apply reflection;
- barrier broken;
- start revive timer;
- convert resource to shield.

The existing combat systems remain authoritative for the resulting gameplay mutations.

No boss mechanic in this test slice directly writes rewards, inventory, currency, progression, or loot.

Dungeon persistence owns only run/encounter/checkpoint lifecycle. Combat encounter internals are reset on wipe/retry according to the existing dungeon policy.

## Acceptance criteria

1. Mirror Castellan has two deterministic add/barrier phases at 70% and 35% HP.
2. Hitting the mirrored boss remains possible and causes authoritative reflected damage rather than `IMMUNE`.
3. Reflection changes when the Guardian dies and disappears only after the whole linked wave is dead.
4. Breaking the barrier creates a visible 8-second +25% incoming-damage window.
5. Velarius has a real visible Mana pool whose remaining value changes the 10% HP phase.
6. Interrupting configured casts can deny an already-paid boss resource cost.
7. Mor-Et can create timed hostile class-profile soul actors and punish unresolved souls without an instant scripted wipe.
8. Azrael requires linked targets to die within a configured revive window.
9. Existing normal combat, party combat and current dungeons remain on the same `CombatSession` runtime.
10. The test dungeon grants no designed loot/reward/progression value.
11. Unit/integration tests cover the reusable encounter state and critical regression cases.
12. The compact combat UI/logs make phase state, important casts, resources, linked adds and failure reasons understandable without movement controls.

## Explicit non-goals

This test slice does not finalize:

- dungeon loot;
- item sets;
- cosmetics;
- economy rewards;
- XP/gold tuning;
- final level range;
- final boss HP/DPS balance;
- production progression placement;
- movement, distance, facing, arena sectors, cover, or pathfinding;
- a generic visual encounter editor.

Those are intentionally deferred until the mechanics themselves pass playtesting.
