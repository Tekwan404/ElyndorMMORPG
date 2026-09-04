# Phase D3 — Data-Driven Cleanup

Status: D3.1-D3.5 implemented for current prototype; legacy root overlays are compatibility-only

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


## Category content loader

The runtime entry point is now a composition pipeline instead of a list of feature-named
`Apply...` calls:

```text
package.json
  -> LegacyContentOverlayComposer   (compatibility only)
  -> CategoryContentComposer        (content/<category>/*.json)
  -> ContentValidationPipeline
  -> GameContentIndexes
  -> gameplay
```

New content files are scanned deterministically by category and merged by stable entity id.
`content/locations/*.json` supports both full location fragments and the existing encounter patch
shape. Whispering Forest monsters now load from `content/monsters/whispering-forest.json`.

The remaining root overlays are intentionally isolated behind
`LegacyContentOverlayComposer`; they can be migrated category-by-category without changing
gameplay consumers.

## Content indexes

`GameContentIndexes` is cached per validated `GameContentPackage` snapshot and exposes:

```text
DefinitionsByKey
ClassesById
ResourcesById
AbilitiesById
TalentTreesById
TalentTreesByClassId
ItemsById
EquipmentSetsById
MerchantsById
LootTablesById
MonstersById
MonsterAiProfilesById
LocationsById
```

Hot gameplay paths now use these indexes instead of repeatedly scanning content lists.

## Modular validation pipeline

`ContentValidationPipeline` executes explicit validation stages:

```text
MetadataValidator
DefinitionValidator
CharacterValidator
AbilityValidator
TalentValidator
ItemValidator
MonsterValidator
WorldValidator
```

The previous monolithic validator implementation is physically split into domain partials while
`GameContentPackageValidator.Validate` remains a compatibility facade for tests and callers.
World encounter validation is part of the pipeline, so loaders and future Admin publish validation
share the same entry point.
