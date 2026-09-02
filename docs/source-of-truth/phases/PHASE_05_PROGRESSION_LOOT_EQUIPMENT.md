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

The current finalizer persists:

- final player HP;
- final player action resource;
- checkpoint timestamp.

Combat ticks remain in-memory. PostgreSQL is written only when the session reaches `Victory`, `Defeat`, or `Cancelled`.

This finalization seam is intentionally the future integration point for permanent combat rewards. Do not move EF Core writes into `CombatSession`.

### Starter Town recovery

`BootstrapService` treats `STARTER_TOWN` as the prototype recovery checkpoint.

When authoritative location is `STARTER_TOWN`:

- `CurrentHp = MaxHp`;
- `CurrentResource = ResourceProfile.RespawnValue`;
- the recovered vitals are persisted.

Victory in the forest does not automatically heal the player.

### Data-driven progression foundation

`LevelProgressionDefinition` exists in Core and is loaded through Phase 5 content.

Current prototype profile:

- maximum level: 60;
- level 1 → 2: 100 XP;
- threshold grows by a content-defined factor of 1.5.

### Monster reward metadata

Current monsters carry reward metadata:

| Monster | XP | Loot table |
| --- | ---: | --- |
| `WOLF` | 35 | `WHISPERING_FOREST_WOLF` |
| `FOREST_BOAR` | 30 | `WHISPERING_FOREST_BOAR` |
| `GIANT_SPIDER` | 25 | `WHISPERING_FOREST_SPIDER` |

### Materials

The following real item definitions already exist in versioned content:

- `WOLF_HIDE` — Шкура волка
- `WOLF_FANG` — Волчий клык
- `BOAR_HIDE` — Шкура кабана
- `BOAR_TUSK` — Кабаний клык
- `SPIDER_SILK` — Паучий шёлк
- `SPIDER_VENOM_SAC` — Ядовитая железа паука

Materials are stackable with `MaxStack = 99` and are deliberately only crafting foundations. Crafting itself is deferred.

### Equipment

Prototype item definitions:

- `WOLF_FANG_BLADE` — Weapon, +2 Strength
- `BOAR_HIDE_VEST` — Chest, +2 Stamina
- `SPIDER_SILK_HOOD` — Head, +2 Agility

`EquipmentStatModifierResolver` converts equipped item definitions to the existing `CharacterStatInputs.Equipment` primary-stat input. Do not add a second character stat calculator.

### Loot

Versioned loot tables are already defined for all three Whispering Forest monsters.

`LootRoller` uses the existing injectable `IGameRandom`, so reward rolls can be deterministic in the one focused reward test.

## Content files

- `content/whispering-forest-monsters.json`
- `content/phase5-progression-items.json`

`GameContentPackageLoader` composes both overlays and exposes:

- `LevelProgression`;
- `Items`;
- `LootTables`;
- monster XP reward and loot table IDs.

Current resulting content version is `0.7.0`, balance version `0.6.0`.

## Remaining implementation for Codex

Do not redesign the foundation above. Finish these bounded tasks:

1. Add persistent `Experience` to Character and generate the EF migration locally with `dotnet ef`.
2. Add `CharacterItem`, `CharacterEquipment`, and `CombatRewardGrant` persistence models/configurations.
3. Generate the EF migration and model snapshot locally; do not hand-edit generated designer/snapshot files unless required.
4. Implement `CombatRewardService` at the existing terminal finalization seam:
   - Victory only;
   - unique `CombatSessionId` idempotency;
   - XP and multi-level-up;
   - deterministic loot roll;
   - material stack updates;
   - equipment item creation.
5. On Level Up restore HP to the recalculated MaxHP.
6. Add inventory/equipment read + equip/unequip application service.
7. Feed equipped item definitions through `EquipmentStatModifierResolver` into `CharacterStatInputs.Equipment` in Bootstrap.
8. Add structured reward/inventory contracts and minimal mobile UI:
   - XP bar;
   - Victory reward card;
   - inventory material quantities;
   - Weapon / Head / Chest equipment slots.
9. Prefer Defeat → Starter Town recovery if it fits the existing world transaction cleanly; otherwise persist defeat vitals and allow travel back to town in this phase.
10. Extend the existing content validator for progression/items/loot references.

## Minimal automated checks only

Do not build a large test matrix. Keep only the critical checks:

1. Victory XP crosses a threshold and levels up.
2. Same CombatSession reward applied twice grants XP/loot once.
3. Equipping +Strength changes Strength and derived AttackPower through `CharacterStatCalculator`.
4. Starter Town bootstrap restores damaged HP.
5. Phase 5 content loads and validates.

Fix existing tests if signatures/contracts change. Manual Telegram gameplay verification is the main acceptance pass.

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
