# Phase 4E — Intellect-based Mana Scaling

Status: implemented

## Rule

Mana capacity is server-authoritative and derived from the character's final Intellect after level growth, equipment, talents, and other approved stat sources.

```text
MaxMana = ManaBase + FinalIntellect × ManaPerIntellect + MaxResourceFlat
```

Current balance values are loaded from `content/resource-scaling.json`:

```text
ManaBase = 100
ManaPerIntellect = 5
```

Example for the current level-60 Mage baseline:

```text
Base Intellect = 11
Level growth = 3 Intellect per completed level
Final Intellect at level 60 without gear/talents = 11 + 59 × 3 = 188
MaxMana = 100 + 188 × 5 = 1040
```

## Resource semantics

- Mana scaling changes `MaxResource`; the client never calculates it.
- A Mana profile whose base `StartValue` equals its base `MaxValue` starts full at the scaled maximum.
- A Mana profile whose base `RespawnValue` equals its base `MaxValue` respawns full at the scaled maximum.
- Existing characters keep their persisted current Mana when capacity increases and then regenerate toward the new maximum according to normal resource rules.
- Flat `MaxResource` talent bonuses are applied after Intellect scaling.
- Rage and Focus are unchanged by this formula.
- Training combat starts with the character's effective maximum resource rather than the unscaled base profile value.

## Acceptance

- A level-60 baseline Mage has 188 Intellect and 1040 maximum Mana.
- Equipment or talents that increase Intellect also increase maximum Mana.
- MaxResourceFlat bonuses stack after the Intellect formula.
- Warrior Rage and Archer Focus retain their existing capacities and behavior.
