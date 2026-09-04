# Phase D2 — Unified Character State

Status: in progress

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
- CombatSessionFinalizer defeat/respawn.

Defeat now uses the effective derived resource profile, therefore a level-60 Mage respawns at the
scaled Mana maximum instead of the raw base profile value.

Administration and operation-state guarding are the next D2 block.

Do not report automated verification as green until CI or a local test run executes this branch.
