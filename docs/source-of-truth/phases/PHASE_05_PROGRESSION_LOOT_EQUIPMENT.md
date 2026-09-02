# Phase 5 — Progression, Loot, Equipment & Recovery

## Goal

Extend the working world/combat loop into persistent character progression:

```text
Combat Victory
→ XP
→ Level Up
→ Materials / Equipment Loot
→ Inventory
→ Equip
→ stronger authoritative stats
→ next combat
```

The phase also makes combat damage meaningful by persisting terminal vitals and restores characters in `STARTER_TOWN`.

## Implemented foundation in this branch

### Combat-end vitals

`CombatSessionRegistry` finalizes a terminal CombatSession exactly once through `ICombatSessionFinalizer`.

The finalizer persists:

- final player HP;
- final player action resource;
- checkpoint timestamp.

Combat ticks remain in-memory. PostgreSQL is written only after a terminal combat state. Victory then flows into the permanent reward service. Do not move EF Core writes into `CombatSession`.

### Starter Town recovery

`BootstrapService` treats `STARTER_TOWN` as the prototype recovery checkpoint.

When authoritative location is `STARTER_TOWN`:

- `CurrentHp = MaxHp`;
- `CurrentResource = ResourceProfile.RespawnValue`;
- recovered vitals are persisted.

Victory in the forest does not automatically heal the player. Defeat currently persists terminal vitals; automatic defeat teleport remains optional for the finishing pass.

### Persistent progression schema

`Character` now owns persistent `Experience` in addition to `Level`.

The Core and EF model already define:

- `CharacterItem` with `Quantity` for inventory stacks;
- `CharacterEquipment` keyed by `(CharacterId, Slot)`;
- `CombatRewardGrant` keyed by unique `CombatSessionId` for reward idempotency;
- DbSets and EF configurations for all three.

The generated EF migration/model snapshot is intentionally left to the local finishing pass using `dotnet ef`.

### XP and Level Up engine

`LevelProgressionDefinition` is loaded from content.

Current prototype profile:

- max level: 60;
- level 1 → 2: 100 XP;
- threshold grows by content-defined factor `1.5`.

`CharacterProgression.GrantExperience`:

- applies XP;
- carries remaining XP after a level;
- supports multiple level-ups from one grant;
- clamps max-level XP state.

Level-up full-heal still needs to be wired to authoritative recalculated MaxHP during the finishing pass.

### Monster reward metadata

| Monster | XP | Loot table |
| --- | ---: | --- |
| `WOLF` | 35 | `WHISPERING_FOREST_WOLF` |
| `FOREST_BOAR` | 30 | `WHISPERING_FOREST_BOAR` |
| `GIANT_SPIDER` | 25 | `WHISPERING_FOREST_SPIDER` |

### Materials

Real versioned item definitions already exist:

- `WOLF_HIDE` — Шкура волка
- `WOLF_FANG` — Волчий клык
- `BOAR_HIDE` — Шкура кабана
- `BOAR_TUSK` — Кабаний клык
- `SPIDER_SILK` — Паучий шёлк
- `SPIDER_VENOM_SAC` — Ядовитая железа паука

Materials are stackable with `MaxStack = 99`. Crafting itself remains deferred.

### Equipment

Prototype item definitions:

- `WOLF_FANG_BLADE` — Weapon, +2 Strength
- `BOAR_HIDE_VEST` — Chest, +2 Stamina
- `SPIDER_SILK_HOOD` — Head, +2 Agility

`EquipmentStatModifierResolver` converts equipped item definitions into the existing `CharacterStatInputs.Equipment` primary-stat input. Do not add a second character stat calculator.

### Loot and permanent Victory rewards

Versioned loot tables exist for all three current forest monsters.

`LootRoller` uses the existing injectable `IGameRandom`.

`CombatRewardService` is already wired into terminal Victory finalization and currently performs:

- `CombatSessionId` replay guard through `CombatRewardGrant`;
- configured monster XP;
- multi-level progression through `CharacterProgression`;
- deterministic/server-side loot roll;
- stack filling for materials;
- creation of non-stackable equipment items;
- PostgreSQL transaction around the permanent reward mutation.

Defeat and Cancelled grant no XP or loot.

## Content files

- `content/whispering-forest-monsters.json`
- `content/phase5-progression-items.json`

`GameContentPackageLoader` composes both overlays and exposes:

- `LevelProgression`;
- `Items`;
- `LootTables`;
- monster XP reward and loot table IDs.

Resulting content version: `0.7.0`.
Balance version: `0.6.0`.

## Remaining implementation for Codex

Do not redesign the foundation above. Finish only these bounded tasks:

1. Generate and review the EF migration/model snapshot locally with `dotnet ef` for:
   - `Character.Experience`;
   - `character_items`;
   - `character_equipment`;
   - `combat_reward_grants`.
2. Fix any compile/analyzer issues found by the local build without changing architecture.
3. Wire Level Up full-heal to the newly recalculated authoritative MaxHP.
4. Add inventory/equipment application service:
   - read inventory;
   - equip;
   - unequip;
   - verify ownership, type, slot, required level.
5. Feed equipped definitions through `EquipmentStatModifierResolver` into `CharacterStatInputs.Equipment` in `BootstrapService`.
6. Extend bootstrap/contracts with:
   - current XP;
   - XP to next level;
   - inventory;
   - equipped slots.
7. Expose the already-applied Victory reward result to the client in a structured form suitable for the result card. Preserve reward idempotency; do not reroll on reconnect.
8. Add minimal mobile UI only:
   - XP bar;
   - Victory rewards card;
   - inventory list with material quantities;
   - Weapon / Head / Chest slots;
   - Equip / Unequip actions.
9. Extend the existing `GameContentPackageValidator` for progression/items/loot references. Do not create a second validator.
10. Optional only if clean and bounded: Defeat → `STARTER_TOWN`. Otherwise leave manual travel to town; town recovery already works.

## Minimal automated checks only

Do not build a large test matrix. Keep only the critical checks:

1. Victory XP crosses a threshold and levels up.
2. Same CombatSession reward applied twice grants XP/loot once.
3. Equipping +Strength changes Strength and derived AttackPower through `CharacterStatCalculator`.
4. Starter Town restores damaged HP.
5. Phase 5 content loads and validates.

Fix existing tests if signatures/contracts change. Manual Telegram gameplay verification is the main acceptance pass.

## Manual acceptance

```text
Starter Town (full HP)
→ Whispering Forest
→ fight Wolf / Boar / Spider
→ Victory with damaged HP
→ see XP + materials / possible equipment
→ repeat until Level Up
→ inventory contains accumulated materials
→ equip dropped item
→ character stats update immediately
→ next CombatSession uses stronger stats
→ return to Starter Town
→ HP restores
→ reload
→ Level / XP / inventory / equipment remain persistent
```

## Explicitly deferred

- crafting recipes / crafting UI;
- professions;
- vendors / gold / economy;
- trading / auction house;
- durability;
- random item affixes;
- sockets / enchanting;
- boss loot;
- party loot rules.
