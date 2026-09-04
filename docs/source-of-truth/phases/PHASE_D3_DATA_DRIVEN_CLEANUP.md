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
