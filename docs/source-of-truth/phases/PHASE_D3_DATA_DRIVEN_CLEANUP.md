# Phase D3 — Data-Driven Cleanup

Status: in progress

## Runtime talent parameter contract

Berserker and Pyromancer combat semantics remain C# rules, but their balance parameters now live
in typed talent content.

`TalentModifierDefinition` supports event-runtime fields for:

- primary rank values;
- secondary rank values;
- threshold;
- chance percent;
- duration;
- tick interval;
- internal cooldown;
- trigger count;
- conditional cast time;
- conditional resource-cost reduction.

`ResolvedTalentEventHook` carries those values into CombatSession. Runtime catalogs only declare
which talent id/event-key pairs are owned by the corresponding combat module; they no longer own
numeric balance dictionaries.

This preserves the project rule:

```text
C#      = what the mechanic does
content = with which values it does it
```

The current Berserker and Pyromancer content was migrated without intended balance changes.


## Ability presentation

`AbilityDefinition` now owns:

```text
DisplayName
Description
IconId
```

Bootstrap and combat responses carry those fields. Combat UI no longer contains a normal-ability
name/art dictionary; it renders server-provided presentation and resolves optional art through
`assets/abilityArt.ts` using `import.meta.glob`.

The server validator requires non-empty ability presentation for content version 0.9.3 and newer.
Existing Warrior, monster and Mage/Pyromancer abilities were migrated to content. Missing optional
icon assets fall back to text initials without changing gameplay.
