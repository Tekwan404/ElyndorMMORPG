# Phase 4D — Data-driven world encounters

**Status:** approved implementation slice  
**Depends on:** Phase 4A–4C  
**Scope:** normal single-player world encounters only

## Goal

Remove the prototype monster-roster hardcode from both the server combat factory and the Vue world view. Locations own their normal encounter roster as versioned content, the server performs the encounter roll, and combat starts only from the server-issued encounter token.

## Authoritative flow

```text
Character is in a non-safe location
→ POST /api/v1/world/explore
→ server reads LocationDefinition.Encounters
→ server performs weighted encounter roll
→ server returns presentation + opaque short-lived EncounterId
→ player chooses "Вступить в бой"
→ SignalR StartCombat(EncounterId)
→ EncounterId is consumed once
→ CombatSessionFactory verifies current location and that the selected monster still belongs to that location
→ normal CombatSession starts
```

The client never sends a MonsterId to select a normal encounter.

## Content ownership

Location encounter rosters live under:

```text
content/locations/*.json
```

Each entry contains:

```json
{ "monsterId": "WOLF", "weight": 1 }
```

Encounter-visible normal monsters define presentation in monster content:

```text
displayName
description
artId
```

Monster art is convention-based and discovered by Vite from:

```text
web/elyndor-web/src/assets/monsters/<artId>.*
```

No TypeScript monster map is required.

## Validation

The composed content package must reject:

- a location encounter referencing a missing monster;
- duplicate monster entries in the same location;
- zero/negative encounter weights;
- ordinary hostile encounters in `SAFE` locations;
- encounter-visible monsters without `displayName`, `description`, or `artId`.

## Encounter token

`EncounterId` is ephemeral process-local state, not persistent character progression.

Rules:

- one pending encounter per account;
- a new exploration replaces the previous pending encounter;
- token is opaque and single-use;
- token expires after 5 minutes;
- wrong token does not consume the valid token;
- server restart may discard pending encounters; the player can explore again;
- changing location before combat makes the encounter invalid at CombatSession creation.

## Training dummy

`TRAINING_DUMMY` remains a special safe-town training action from Phase 4C. It does not participate in ordinary location encounter rolls and uses a dedicated `StartTraining` command.

## Explicit non-goals

This slice does not add:

- Elite or Boss encounter selection;
- rare encounters or pity rules;
- multi-wave/reinforcement encounters;
- Party encounter ownership;
- AFK encounter generation;
- location-specific background/content editor UI.

Those require separate slices.

## Acceptance criteria

1. `CombatSessionFactory` contains no `WOLF` / `FOREST_BOAR` / `GIANT_SPIDER` whitelist.
2. `WorldView.vue` contains no normal-monster roster array and does not choose a MonsterId locally.
3. Server exploration selects from location content.
4. Direct arbitrary MonsterId combat start is no longer part of the client/server API.
5. Adding a normal monster requires monster content, a location encounter entry, optional loot, and an art file — not changes to combat C# or Vue encounter lists.
6. Existing Warrior, Mage/Pyromancer, rewards, and training dummy behavior remain on the same combat runtime.
