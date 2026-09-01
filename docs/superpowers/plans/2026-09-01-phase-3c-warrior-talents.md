# Phase 3C Warrior Talents Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the bounded Phase 3C Warrior talent engine, content, persistence, and mobile UI with Berserker artwork.

**Architecture:** Keep the existing modular-monolith and `content/package.json` layout. Resolve the active loadout into typed talent overlays consumed by existing stat, ability, damage, healing, resource, and effect boundaries; mark mechanics owned by later phases as validated deferred contracts. PostgreSQL stores selections, while final gameplay values remain server-derived.

**Tech Stack:** .NET / ASP.NET Core, EF Core, PostgreSQL, Vue 3, TypeScript, Vite, WebP assets.

**Spec:** `docs/superpowers/specs/2026-09-01-phase-3c-warrior-talents-design.md`

## Global Constraints

- Do not add Monster, CombatSession, Party, Boss, Elite, XP, loot, equipment, or economy runtime.
- Backend remains authoritative; the client sends talent intent only.
- Preserve exactly two persisted talent loadouts.
- Preserve the existing content package layout and Arcane Minimal Vue stack.
- Use the ten new art sources only for Berserker; leave Guardian and Warlord generated icons intact.
- Tests are minimal but must cover each distinct supported modifier family and mutation safety.

---

### Task 1: Talent coverage manifest and content validation

**Files:**
- Create: `docs/development/phase-3c-talent-coverage.md`
- Modify: `src/Elyndor.Core/Talents/TalentModels.cs`
- Modify: `src/Elyndor.Core/Content/GameContentPackageValidator.cs`
- Modify: `content/package.json`
- Test: `tests/Elyndor.Core.Tests/Content/GameContentPackageValidatorTests.cs`

**Interfaces:**
- Produces: canonical `IconId`, typed modifier keys, `Supported`/`Deferred` ownership for every node.
- Consumes: the existing 96-node `TalentTreeDefinition` and Warrior Source of Truth.

- [ ] Extract every node into a coverage table containing node ID, branch, mechanic family, target, runtime status, and deferred owner.
- [ ] Add an optional canonical `IconId` to `TalentDefinition` without changing the outer content package layout.
- [ ] Replace description-only nodes with typed modifier definitions or explicit deferred ownership.
- [ ] Extend validation for icon IDs, modifier keys, targets, circular prerequisites, and invalid supported/deferred combinations.
- [ ] Add one table-driven content validation test covering valid full content and representative invalid references.
- [ ] Run the focused content validator test and review the content diff.

### Task 2: Supported talent overlay resolver

**Files:**
- Create: `src/Elyndor.Core/Talents/TalentModifierCatalog.cs`
- Create: `src/Elyndor.Core/Talents/ResolvedTalentModifiers.cs`
- Create: `src/Elyndor.Core/Talents/TalentModifierResolver.cs`
- Modify: `src/Elyndor.Core/Talents/TalentStatModifierResolver.cs`
- Test: `tests/Elyndor.Core.Tests/Talents/TalentModifierResolverTests.cs`

**Interfaces:**
- Produces: `TalentModifierResolver.Resolve(TalentTreeDefinition, IReadOnlyDictionary<string,int>) -> ResolvedTalentModifiers`.
- Consumes: validated nodes and active-loadout ranks.

- [ ] Define the finite modifier-key catalog required by current Warrior content.
- [ ] Resolve rank-indexed values into typed stat, ability, effect, resource, damage, and event overlays.
- [ ] Ignore deferred runtime hooks during execution while preserving them for content/UI inspection.
- [ ] Keep primary-stat calculation compatible with the existing Base-Class-Equipment-Talent-Effect order.
- [ ] Add one parameterized test for each distinct supported modifier family rather than one test per node.
- [ ] Run the focused resolver tests and inspect for talent-ID branching.

### Task 3: Ability unlock and execution integration

**Files:**
- Modify: `src/Elyndor.Core/Combat/Abilities/AbilityModels.cs`
- Modify: existing Phase 3 ability execution/resolution files under `src/Elyndor.Core/Combat/Abilities/`
- Modify: `src/Elyndor.Infrastructure/World/BootstrapService.cs`
- Modify: `content/package.json`
- Test: existing focused Warrior/Talent tests under `tests/Elyndor.Core.Tests/Combat/`

**Interfaces:**
- Produces: server-derived known abilities and modified effective ability definitions.
- Consumes: `ResolvedTalentModifiers`, active loadout, canonical content abilities/effects.

- [ ] Add Phase 3-owned talent abilities and referenced effects to versioned content.
- [ ] Derive known abilities from learned unlock modifiers; never accept unlocked ability IDs from the client.
- [ ] Apply supported cost, cooldown, duration, coefficient, penetration, and effect-strength overlays before authoritative execution.
- [ ] Keep Party/CombatSession-dependent targeting and event hooks deferred with stable unavailability behavior.
- [ ] Add compact deterministic tests for unlock plus representative offensive, defensive, and resource modifiers.
- [ ] Run the focused Combat/Talent test filters.

### Task 4: Transactional loadouts and retry safety

**Files:**
- Modify: `src/Elyndor.Core/Talents/CharacterTalentState.cs`
- Modify: `src/Elyndor.Infrastructure/Talents/TalentService.cs`
- Modify: `src/Elyndor.Server/Talents/TalentEndpoints.cs`
- Modify: `src/Elyndor.Contracts` talent request/response contracts as needed.
- Modify migration only if the existing schema cannot persist the required idempotency state safely.
- Test: focused infrastructure/API talent tests.

**Interfaces:**
- Produces: retry-safe `learn`, `reset`, and `switch` mutations returning an authoritative snapshot/version.
- Consumes: authenticated account, active character, expected state version, mutation request ID.

- [ ] Add a stable mutation request identifier to retryable talent commands using existing API conventions.
- [ ] Execute validation and mutation in one PostgreSQL transaction with optimistic concurrency.
- [ ] Return the prior authoritative result for an exact retry and a stable conflict for stale competing mutations.
- [ ] Recalculate server-derived character stats and known abilities after learn, reset, and switch.
- [ ] Add the smallest integration coverage for duplicate learn and loadout switching.
- [ ] Run the focused persistence/API checks.

### Task 5: Berserker icon asset pipeline

**Files:**
- Create: optimized files under `web/elyndor-web/src/assets/game/talents/berserker/`
- Create: `web/elyndor-web/src/game/talents/talentArt.ts`
- Modify: `content/package.json`
- Preserve: original sources in `talant/` unless moved into a clearly documented source-art directory.

**Interfaces:**
- Produces: `resolveTalentArt(iconId)` and canonical ability-art mappings.
- Consumes: `TalentNode.iconId` from the server.

- [ ] Map the ten visual sources to Berserker mechanic families and assign stable English filenames/IDs.
- [ ] Generate browser-sized WebP variants with transparency and materially smaller payloads.
- [ ] Register Berserker assets through lazy dynamic imports grouped by visible branch.
- [ ] Reuse the same icon ID for an active talent and the ability it unlocks.
- [ ] Leave Guardian/Warlord resolution on the existing generated-icon fallback.
- [ ] Inspect dimensions, encoded sizes, and repository diff; reject multi-megabyte runtime assets.

### Task 6: Mobile Talent UI completion

**Files:**
- Modify: `web/elyndor-web/src/api/contracts.ts`
- Modify: `web/elyndor-web/src/game/talents/views/WarriorTalentTreeView.vue`
- Create or modify: focused Talent UI component test.

**Interfaces:**
- Produces: content-driven talent art, real server states, retry-safe mutation requests, and ability unlock presentation.
- Consumes: authoritative `TalentSnapshot`, icon IDs, modifier/deferred metadata.

- [ ] Render Berserker image art with lazy loading and generated fallback for other branches.
- [ ] Show locked, available, learned, maxed, prerequisite, insufficient-points, and deferred-runtime states clearly.
- [ ] Preserve branch connections, rank display, two-loadout switch, reset, loading, conflict refresh, and errors.
- [ ] Expose unlocked active abilities in the detail sheet with the canonical ability icon.
- [ ] Verify layout at 320px and Telegram-like phone width without horizontal page overflow.
- [ ] Run the focused frontend test, typecheck, and build.

### Task 7: Phase 3C closeout

**Files:**
- Modify: `docs/source-of-truth/phases/ELYNDOR_PHASES_0-5.md` only to record verified completion facts.
- Modify: `docs/source-of-truth/architecture/00_DEVELOPMENT_ROADMAP.md` only to record verified completion facts.

**Interfaces:**
- Consumes: completed Tasks 1-6 and actual verification evidence.
- Produces: an honest Phase 3C completion record and handoff to Phase 4.

- [ ] Run focused backend Talent/Combat tests, `dotnet build`, and the configured frontend typecheck/build commands.
- [ ] Do not run broad browser or integration suites unless a focused check exposes a regression.
- [ ] Review `git diff`, `git status`, content payload sizes, secrets, scope creep, and Source of Truth drift.
- [ ] Classify remaining Party/CombatSession/Boss/Elite hooks as deferred, not broken or silently supported.
- [ ] Update phase documents only for checks that actually passed.
- [ ] Prepare a compact report; do not push without explicit permission.
