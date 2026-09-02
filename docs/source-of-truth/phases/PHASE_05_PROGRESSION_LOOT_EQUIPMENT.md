# Phase 5 — Progression, Loot, Equipment & Recovery

## Goal

Extend the playable combat loop into persistent progression:

```text
Combat Victory
→ XP / Level Up
→ Materials + possible Equipment
→ Inventory
→ Equip
→ stronger authoritative stats
→ next combat
```

Phase 5 also makes combat damage persistent and gives safe locations a real recovery role.

## Current playable state

The current branch supports:

- persistent XP and levels;
- multi-level progression;
- server-side Victory rewards;
- reward idempotency by `CombatSessionId`;
- material stacks;
- equipment ownership and equipped slots;
- equipment modifiers through the existing `CharacterStatCalculator` pipeline;
- reward card, XP UI and inventory/equipment UI;
- terminal combat HP/resource persistence;
- live out-of-combat resource updates;
- gradual Starter Town healing.

## Combat-end vitals

`CombatSessionRegistry` finalizes a terminal session once through `ICombatSessionFinalizer`.

Terminal state persists:

- final player HP;
- final action resource;
- checkpoint timestamp.

The finalizer also begins a new out-of-combat vitals context. This is important for class resource rules such as Warrior Rage decay.

Combat ticks remain in-memory. PostgreSQL is not written on every combat tick.

## Starter Town recovery

`STARTER_TOWN` is the prototype safe recovery location.

Healing is **not instant**.

Current prototype rule:

```text
Starter Town HP recovery = 5 HP / second
```

Recovery starts from the authoritative location arrival timestamp (`CharacterLocation.UpdatedAtUtc`). Time spent wounded in Whispering Forest is therefore not counted as town rest.

The frontend refreshes authoritative vitals while recovery is active, so the HUD visibly changes without a reload.

Victory in the forest does not restore HP automatically.

## Resource lifecycle

Resource behavior stays data-driven through `ResourceProfile` and `CharacterResourceRules`.

For Warrior Rage the current profile uses out-of-combat decay. Combat finalization resets the out-of-combat context so Rage no longer appears frozen after battle.

Focus and Mana continue to follow their configured out-of-combat regeneration rules.

## Persistent progression

`Character` owns persistent:

- `Level`;
- `Experience`.

Current prototype progression profile:

- maximum level: 60;
- level 1 → 2: 100 XP;
- growth factor: 1.5.

`CharacterProgression.GrantExperience` supports remaining XP and multiple level-ups from one grant.

## Monster rewards

| Monster | XP | Loot table |
| --- | ---: | --- |
| `WOLF` | 35 | `WHISPERING_FOREST_WOLF` |
| `FOREST_BOAR` | 30 | `WHISPERING_FOREST_BOAR` |
| `GIANT_SPIDER` | 25 | `WHISPERING_FOREST_SPIDER` |

Rewards are rolled only by the server.

## Crafting materials foundation

Crafting itself is deferred, but real persistent crafting materials already drop and stack in inventory:

- `WOLF_HIDE` — Шкура волка;
- `WOLF_FANG` — Волчий клык;
- `BOAR_HIDE` — Шкура кабана;
- `BOAR_TUSK` — Кабаний клык;
- `SPIDER_SILK` — Паучий шёлк;
- `SPIDER_VENOM_SAC` — Ядовитая железа паука.

Materials have no equipment slot and must always have `EquippedSlot = null`.

The inventory UI separates **Материалы для крафта** from actual equipment and shows stack quantities and descriptions.

## Equipment

Prototype equipment:

- `WOLF_FANG_BLADE` — Weapon, +2 Strength;
- `BOAR_HIDE_VEST` — Chest, +2 Stamina;
- `SPIDER_SILK_HOOD` — Head, +2 Agility.

Equipped definitions are resolved through `EquipmentStatModifierResolver` and fed into the existing `CharacterStatInputs.Equipment` pipeline.

Do not add a second stat calculator.

## Inventory correctness

`CharacterEquipment` is keyed by `(CharacterId, Slot)` and each equipped item is unique.

A fixed bug previously treated the default enum value (`Weapon`) as if it were a real equipped slot when an inventory item had no equipment record. That caused materials such as Wolf Hide to display as `Надето` and eventually caused duplicate `Weapon` keys during snapshot construction.

The service now explicitly maps missing equipment records to nullable `null`.

## Reward idempotency

`CombatRewardGrant` uses unique `CombatSessionId`.

A repeated/reconnected terminal combat must not:

- grant XP twice;
- reroll loot;
- create duplicate rewards.

## Manual acceptance

```text
Starter Town
→ travel to Whispering Forest
→ fight Wolf / Boar / Spider
→ Victory
→ HP remains damaged
→ Rage visibly decays out of combat
→ XP/materials/equipment reward shown
→ fight again without returning to town
→ inventory remains stable
→ material is NOT marked equipped
→ return to Starter Town
→ HP rises gradually by 5/sec
→ equip a real item
→ stats update
→ next combat uses stronger stats
→ reload
→ XP / level / inventory / equipment remain persistent
```

## Explicitly deferred

- crafting recipes and crafting UI;
- professions;
- gold/economy/vendors;
- trading/auction house;
- durability;
- random affixes;
- sockets/enchanting;
- boss loot;
- party loot rules.

## Verification priority

Keep automated coverage focused. Manual Telegram/browser gameplay verification remains the main acceptance pass for this prototype phase.
