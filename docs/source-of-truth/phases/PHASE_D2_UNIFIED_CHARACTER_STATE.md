# Phase D2 — Unified Character State

Status: implemented, automated verification pending

## Goal

Create one authoritative server pipeline for derived character state so Bootstrap, Combat,
Progression, Respawn and Administration cannot silently calculate different stats/resources.

## CharacterDerivedStateService

`CharacterDerivedStateService` resolves, from one character identity/class/level:

- class profile;
- base and effective resource profiles;
- equipped item modifiers;
- active talent ranks;
- resolved talent modifiers;
- final stat calculation and breakdown;
- known abilities;
- final MaxResource.

The service deliberately ignores a persisted talent state whose `TalentTreeId` does not match the
current class tree. This prevents stale talents from another class from affecting derived stats.

Mana remains:

```text
MaxMana = ManaBase + FinalIntellect × ManaPerIntellect + MaxResourceFlat
```

so equipment/talent changes feed the same formula.

## Consumers migrated in this block

- Bootstrap;
- CombatSessionFactory;
- CombatRewardService level-up recalculation;
- CombatSessionFinalizer defeat/respawn;
- CharacterCreationService.

Defeat now uses the effective derived resource profile, therefore a level-60 Mage respawns at the
scaled Mana maximum instead of the raw base profile value.

## Administration lifecycle

Telegram admin operations now use the same derived state:

- `/level` scales HP/resource between old and new authoritative maxima;
- `/restore` restores the effective maximum resource, including Mana scaling;
- `/class` removes incompatible equipped state, resets/reinitializes the talent tree and then
  resolves the new class from the same pipeline.

Lowering a level also validates both talent loadouts and resets a loadout if the persisted build is
no longer legal for the new level.

`TalentService` self-heals legacy class/tree mismatches by reinitializing the persisted talent
state to the current class tree.

## Character operation guard

`CharacterOperationGuard` is now the shared server boundary for state-changing player operations.
It serializes combat start against out-of-combat mutations on bounded account stripes and checks the
authoritative in-memory CombatSession registry while the stripe is held.

While combat is active, the server returns HTTP 409 with:

```text
character_in_combat
```

for:

- explore;
- travel;
- equip / unequip;
- out-of-combat consumable use;
- merchant buy / sell;
- talent learn / switch / reset.

Combat start and training reset use the same exclusive stripe, closing the in-process race where a
world/inventory mutation and combat start could both pass independent prechecks.

Read-only Bootstrap, inventory, merchant and talent reads remain available during combat.

Do not report automated verification as green until CI or a local test run executes this branch.


## Creation path

New-character HP and starting resource now come from `CharacterDerivedStateService` as well.
A level-1 Mage therefore starts from the same formula used by Bootstrap/Combat/Admin:

```text
Intellect = 11
MaxMana = 100 + 11 × 5 = 155
StartingMana = 155
```

The old private stat/resource calculator path was removed from `CharacterCreationService`.


## Respawn regression coverage

PostgreSQL integration coverage now includes a level-60 Mage defeat from Whispering Forest.
The finalizer must relocate the character to `STARTER_TOWN`, restore positive authoritative HP
and set current Mana to the derived respawn maximum of `1040`.
