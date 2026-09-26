# Elyndor — Item, Equipment and Inventory System

**System:** Items / Equipment / Inventory  
**Status:** Source of Truth  
**Updated:** 2026-09-25

## 1. Ownership

Item System owns:

- `ItemDefinition` and `ItemInstance`;
- stacks and inventory occupancy;
- equipment slots and equip/unequip/swap;
- item requirements and stat modifiers;
- weapon/armor/off-hand profiles;
- Spatial Artifact capacity;
- safe item mutations and persistence.

Loot chance, encounter rewards, quests, trade, crafting and combat formulas belong to their own systems.

## 2. Definition vs instance

`ItemDefinition` is versioned content. `ItemInstance` is an owned concrete item/stack.

A definition may contain rarity, max stack, equipment slot, weapon/armor profile, level/class requirements, stats, set id, icon/art id, consumable actions, trade policy and appearance metadata.

Equipment is normally non-stackable. Materials/consumables may stack according to their definition.

## 3. Inventory model

Elyndor uses **one continuous inventory**, not a bag-window model.

```text
BaseCapacity = 30
EffectiveCapacity = BaseCapacity + EquippedSpatialArtifact.CapacityBonus
```

Rules:

- exactly one Spatial Artifact may provide capacity at a time;
- the equipped Spatial Artifact does not consume a normal inventory slot;
- an unequipped/replaced artifact is an ordinary inventory item and therefore consumes a slot;
- player-created overflow is not allowed;
- equip, unequip, swap, salvage and other occupancy-changing mutations must validate **effective capacity**, never a hardcoded base capacity;
- UI presents `used / effective capacity` as the primary value.

Spatial Artifact content may expose different capacity bonuses; current content is authoritative for the exact catalog.

There is no Weight System.

## 4. Slot accounting

One inventory slot contains either:

- one non-stackable item instance; or
- one stack of a stackable definition.

Equipped normal gear does not occupy an inventory slot. When gear is unequipped, the projected inventory must have room for it.

For stackable rewards, the server fills compatible partial stacks first and creates new stacks only when required.

A reward must never disappear silently because inventory is full. The calling reward/loot system must preserve or reject the reward according to its authoritative flow.

## 5. Equipment slots

Current equipment model includes the regular character slots used by runtime/content, including main hand, off hand, armor and accessories. The exact set is defined by current contracts/content and may evolve without introducing client-side authority.

Equip validation is server-authoritative and includes at minimum:

- ownership/existence;
- item is equipable;
- target-slot compatibility;
- level/class requirements;
- weapon/armor permissions;
- handedness/off-hand rules;
- combat-state restrictions;
- projected inventory capacity for displaced items.

Equip/swap is atomic: either all involved moves succeed or none do.

Changing equipment during active combat is disallowed unless a future system contract explicitly introduces a controlled exception.

## 6. Weapons and off-hand

Two-handed weapons occupy `MAIN_HAND` and require a compatible empty/displaced off-hand state in the same atomic transaction.

A one-handed weapon in `OFF_HAND` requires an authoritative dual-wield permission; merely being a one-handed weapon does not grant that permission.

Shields are an off-hand equipment category with their own defensive profile. Shield/block mechanics are resolved by current Stats/Damage/Talent contracts and runtime; clients do not calculate authoritative block results.

## 7. Consumables

Consumables are data-driven server-authoritative actions such as:

```text
RESTORE_HP
RESTORE_RESOURCE
APPLY_EFFECT
REMOVE_EFFECT
```

Definitions validate action shape, referenced resources/effects and cooldown category. Items are consumed only after the full command is valid. Combat-only effects are not executed through an out-of-combat durable shortcut.

Consumable cooldowns are authoritative and category-based where defined.

## 8. Sets and presentation

`SetId`, set display name and bonus thresholds come from backend/content presentation data. The client must not hardcode a set name or assume fixed thresholds.

`IconId`/art resolution is presentation data attached to authoritative item definitions. Missing art may use a safe fallback, but presentation fallback must never change gameplay identity.

Gameplay item identity and displayed cosmetic appearance are separate concepts. Cosmetic/skin systems do not mutate the underlying combat item definition.

## 9. Persistence and safety

Item mutations that can be retried or race with another mutation must remain transaction-safe and idempotent where the operation requires it.

Never:

- trust client-provided final stats/capacity;
- silently drop displaced gear or rewards;
- use a frontend capacity constant;
- let an equipped Spatial Artifact count as both equipped capacity and an occupied backpack slot;
- duplicate an item during reconnect/retry.

## 10. Current implementation boundary

As of the 2026-09-25 `main` snapshot:

- base inventory capacity is 30;
- Spatial Inventory V1 is implemented;
- effective capacity is used by equipment and salvage paths covered by current regressions;
- mobile inventory UX is implemented;
- full all-item icon/presentation audit is still ongoing work and should not be described as universally complete.
