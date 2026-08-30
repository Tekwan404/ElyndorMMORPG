# Phase 2 Character Stats and Resources Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the persistent Phase 1 character into a server-authoritative RPG profile with class-specific stats, health, Rage/Focus/Mana, and a playable mobile character screen.

**Architecture:** Versioned JSON owns prototype balance. Core calculates immutable final stats and resource transitions. PostgreSQL stores only current HP/resource checkpoints; bootstrap recalculates maxima from character identity, level, and content.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core, PostgreSQL, Vue 3, TypeScript, Vite.

**Spec:** `docs/source-of-truth/phases/PHASE_02_CHARACTER_STATS_RESOURCES.md`

## Global Constraints

- Warrior uses Strength/Rage, Archer uses Agility/Focus, Mage uses Intellect/Mana.
- Race and gender never affect stats.
- Do not add abilities, damage, effects, combat, equipment behavior, or Phase 3 systems.
- Keep verification focused: one calculator unit suite, one resource suite, existing regression suite, build, and manual-ready runtime.

---

### Task 1: Versioned class and balance content

**Files:**
- Modify: `content/package.json`
- Modify: `src/Elyndor.Core/Content/GameContentPackage.cs`
- Modify: `src/Elyndor.Core/Content/GameContentPackageValidator.cs`

**Interfaces:**
- Produces `ClassProfile`, `StatFormulaProfile`, and `ResourceProfile` loaded with the existing package.
- Rejects missing prototype classes, forbidden stat IDs, unsupported resources, negative rates, and invalid references before server startup.

- [ ] Add initial Level 1–10 prototype values for all three classes.
- [ ] Add allowed weapon/armor category metadata without implementing items.
- [ ] Validate the package and run `Elyndor.ContentValidator`.
- [ ] Commit the content slice.

### Task 2: Deterministic stat and resource domain

**Files:**
- Create: `src/Elyndor.Core/Characters/CharacterStats.cs`
- Create: `src/Elyndor.Core/Characters/CharacterStatCalculator.cs`
- Create: `src/Elyndor.Core/Characters/CharacterResourceRules.cs`
- Create: `tests/Elyndor.UnitTests/Characters/CharacterStatCalculatorTests.cs`
- Create: `tests/Elyndor.UnitTests/Characters/CharacterResourceRulesTests.cs`

**Interfaces:**
- `CharacterStatCalculator.Calculate(classId, level, package)` returns the complete immutable approved stat set.
- `CharacterResourceRules` clamps, spends, restores, applies elapsed recovery/decay, and returns respawn values without I/O or wall-clock access.

- [ ] Write one focused calculator theory for Warrior/Archer/Mage and race-neutral inputs.
- [ ] Write one focused resource theory covering Rage/Focus/Mana boundaries.
- [ ] Implement aggregation stages Base/Class/Equipment/Talent/Effect with future stages empty in Phase 2.
- [ ] Run only the new unit suites, then commit.

### Task 3: Authoritative checkpoint persistence and API

**Files:**
- Create: `src/Elyndor.Core/Characters/CharacterVitals.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Configurations/CharacterVitalsConfiguration.cs`
- Modify: `src/Elyndor.Infrastructure/Persistence/GameDbContext.cs`
- Modify: `src/Elyndor.Infrastructure/Characters/CharacterCreationService.cs`
- Modify: `src/Elyndor.Infrastructure/World/BootstrapService.cs`
- Modify: `src/Elyndor.Contracts/World/WorldContracts.cs`
- Add: EF Core Phase 2 migration

**Interfaces:**
- Character creation inserts character, initial location, and initial vitals in one transaction.
- Bootstrap applies out-of-combat elapsed rules using `TimeProvider`, saves a checkpoint, and returns calculated stats plus current/max HP/resource.
- No request DTO contains stat, HP, or resource fields.

- [ ] Add `character_vitals` with one row per character, UTC checkpoint, and decimal resource precision.
- [ ] Backfill existing Phase 1 characters safely in the migration.
- [ ] Extend bootstrap read model and HTTP response.
- [ ] Build and run the existing backend regression suite, then commit.

### Task 4: Playable character HUD and stats screen

**Files:**
- Modify: `web/elyndor-web/src/api/contracts.ts`
- Modify: `web/elyndor-web/src/app/AppShell.vue`
- Modify: `web/elyndor-web/src/game/world/views/WorldView.vue`
- Create: `web/elyndor-web/src/game/character/views/CharacterStatsView.vue`

**Interfaces:**
- HUD renders server-provided HP and class resource without recalculation.
- World/Hero navigation switches in session memory.
- Stats screen groups Primary, Attack, and Defense exactly as UI Source of Truth and highlights the class primary attribute.

- [ ] Add compact HP/resource bars with Rage, Focus, and Mana colors.
- [ ] Add the Hero/Characteristics view and working bottom navigation.
- [ ] Preserve loading/error/reconnect behavior.
- [ ] Run lint/typecheck/build, then commit.

### Task 5: Product handoff

**Files:**
- Modify: `docs/source-of-truth/phases/PHASE_02_CHARACTER_STATS_RESOURCES.md`
- Modify: `AGENTS.md` only when the Phase 2 gate is genuinely complete.

- [ ] Review the diff for server authority, stored derived stats, Phase 3 scope, and secrets.
- [ ] Run one backend regression command and one frontend build command; avoid expanding the test matrix.
- [ ] Restart `Start-Elyndor.cmd`, leave the game running for manual testing, and report the URL.
- [ ] Commit and push `feature/phase-2`.
