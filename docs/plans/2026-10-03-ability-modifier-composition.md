# Ability Modifier Composition Implementation Plan

> **For agentic workers:** Execute inline with characterization checkpoints; use executing-plans for task-by-task implementation.

**Goal:** Remove ordered ability/target modifier composition from CombatSession without changing executable definitions or execution semantics.

**Architecture:** A Core AbilityModifierComposer owns generic application and ordered class stages. Session-bound adapters read the active participant and unchanged class runtime; no actor state is copied or cached. Per-target composition returns AbilityTargetModifier without resolving target identities.

**Tech Stack:** C#/.NET 10, xUnit, existing CombatSession/hosted Arena.

**Spec:** User's Combat Core Hardening requirements and docs/source-of-truth/gameplay/10_ABILITY_SYSTEM.md.

## Global constraints

- Do not change CombatEventRouter, AbilityEngine, execution, formulas, balance, scheduler, targeting, reactions, death or content.
- Keep single-writer state, conditional-effect synchronization and snapshot timing at current boundaries.
- Preserve generic additive aggregation, then generic percent-before-flat cost, then class-specific multipliers/overrides.
- Retain Arena standalone fallback; hosted Arena composes from BaseAbilities, never its premodified fallback definitions.
- No database/transaction/API changes; no merge into main.

## Task 1: Characterize executable abilities

**Files:** tests/Elyndor.UnitTests/Combat/AbilityModifierCompositionCharacterizationTests.cs

- [x] Add real PvE/hosted-Arena fixtures capturing the definition passed to AbilityEngine via ActiveCast or PendingAbilityAction; compare the entire definition and literal expected numeric fields.
- [x] Cover generic additive talents plus class multipliers, Berserker/Warlord cost and duration, Pyromancer/Arcane/Frost overlap, Archer conditional mark/one-shot effects, Paladin crit/cast/duration overrides, target-specific modifiers, clamps, unchanged targeting/resource actions/runtime parameters, snapshot repeatability.
- [x] Run unchanged production baseline:

```powershell
dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj -c Release --filter FullyQualifiedName~AbilityModifierCompositionCharacterizationTests
```

Example independently derived assertion: cost 100 with generic 10%+20%, flat 5, class 10% then 20% reduction must be 46.8, not 45 or 50.4.

## Task 2: Extract composition, not mechanics

**Files:** src/Elyndor.Core/Combat/Abilities/AbilityModifierComposer.cs; src/Elyndor.Core/Combat/Sessions/CombatSession.AbilityComposition.cs; CombatSession.cs; CombatSession.PlayerMechanics.cs; CombatSession.Berserker.cs

**Interface:** Compose(AbilityDefinition, AbilityModifierContext) returns AbilityDefinition; context contains resolved talents, class identity and authoritative time. ComposeTarget(AbilityDefinition, CombatActorState, DateTimeOffset) returns AbilityTargetModifier.

- [x] Register existing class stages once using session-bound method groups.
- [x] Move TalentAbilityResolver.Apply and explicit stage order to composer: generic, Berserker, Paladin, Warlord, Pyromancer, Arcane, Frost, Archer.
- [x] Move target modifier order to composer: Berserker, Pyromancer, Mage, Archer. Target enumeration/validation remains in session.
- [x] Replace all PvE command/snapshot and hosted Arena composition call sites; keep sync before commands only, not snapshots.
- [x] Run characterization and combat/PvP/talent regression:

```powershell
dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj -c Release --filter 'FullyQualifiedName~Combat|FullyQualifiedName~Talents|FullyQualifiedName~Arena'
```

## Task 3: Verification and architecture map

**Files:** docs/development/ability-modifier-composition.md; this checklist.

- [x] Document each modifier owner and distinctions between definition composition, actor stats/effects, targeting, charges, execution-time free cost, and resource refunds.
- [x] Verify PostgreSQL/Testcontainers availability, then full solution test and build sequentially:

```powershell
dotnet test Elyndor.slnx --configuration Release
dotnet build Elyndor.slnx --configuration Release --no-restore
git diff --check
```

- [x] Review complete diff; no gameplay/formula/router changes or unresolved findings.
- [ ] Commit the verified logical change, push current feature branch/update existing PR; report actual CI status without merging.

Verification: 25 characterization cases passed before production extraction; 29
pass after regression additions. Deliberate stage-order mutation fails as expected
(cost 46.4 instead of 45) and was reverted. Final focused suite: 786 passed. Full
Release suite: 1051 unit + 485 integration passed, no failures/skips. Release build:
zero warnings/errors. Frontend unchanged; no frontend checks required for this pass.
