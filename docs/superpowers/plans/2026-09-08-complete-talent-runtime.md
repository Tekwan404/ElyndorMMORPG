# Complete Talent Runtime Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task with review checkpoints.

**Goal:** Make all 288 current Warrior, Mage, and Archer talent nodes executable, localized, behavior-tested, and verified through the full Elyndor pipeline.

**Architecture:** Extend the existing Core combat kernel with typed events and session-local runtime state. Keep content values in versioned JSON, validate them before publication, and route every event through the existing single-writer combat session. Add class behavior through generic primitives and focused class adapters instead of talent-ID conditionals.

**Tech Stack:** C#/.NET 10, ASP.NET Core, EF Core/PostgreSQL, SignalR, Vue 3/TypeScript/Vite, xUnit, Vitest, Playwright, content validator, Aspire.

**Spec:** `docs/superpowers/specs/2026-09-08-complete-talent-runtime-design.md`

## Global Constraints

- Backend remains authoritative; clients send intent only.
- All important time uses `TimeProvider`; all important randomness uses injectable deterministic RNG.
- Every combat command, AI action, timer action, and effect tick enters the session single-writer pipeline.
- Balance values live in content; runtime catalogs map behavior, not numbers.
- PostgreSQL remains permanent truth; no Redis or new distributed infrastructure is added without a measured current need.
- No player-facing `Deferred`, `Partial`, `Supported`, `Runtime`, or English technical labels remain in talent UI.
- Every changed mechanic receives a real behavioral test, not only catalog/status coverage.
- Work stays on `feat/complete-talent-runtime`; each task ends with focused verification and a reviewable commit.

---

### Task 1: Build the complete talent audit and strict content contract

**Files:**
- Modify: `src/Elyndor.Core/Talents/TalentModels.cs`
- Modify: `src/Elyndor.Core/Content/Validation/GameContentPackageValidator.Talents.cs`
- Modify: `src/Elyndor.Core/Talents/TalentModifierResolver.cs`
- Create: `tools/Elyndor.ContentValidator/TalentAuditReport.cs`
- Modify: `tools/Elyndor.ContentValidator/Program.cs`
- Modify: `content/package.json`
- Modify: `content/talents/mage-pyromancer.json`
- Modify: `content/talents/archer.json`
- Test: `tests/Elyndor.UnitTests/Content/GameContentPackageValidatorTests.cs`
- Test: `tests/Elyndor.UnitTests/Talents/TalentModifierResolverTests.cs`
- Create: `tests/Elyndor.UnitTests/Talents/TalentAuditReportTests.cs`

**Interfaces:**
- Produces a deterministic audit model containing tree, branch, node, modifier, rank, status, Russian text, references, and test-coverage identity.
- Produces strict validation errors for deferred gameplay modifiers, zero-value fake modifiers, invalid rank counts, unresolved references, and missing Russian text.

- [ ] Write failing tests proving the audit counts all current trees and reports deferred nodes.
- [ ] Write failing tests proving strict mode rejects deferred gameplay content and incorrect rank/value mapping.
- [ ] Implement the audit model and CLI output without changing runtime behavior.
- [ ] Implement strict validator rules behind the normal content validation pipeline.
- [ ] Update content schema/version fields and convert only mechanics already supported by runtime; leave unsupported branches explicitly failing validation until their task is implemented.
- [ ] Run the focused content and resolver tests.
- [ ] Commit the audit/contract slice.

### Task 2: Add typed combat events and session-local talent runtime state

**Files:**
- Create: `src/Elyndor.Core/Combat/Runtime/CombatRuntimeEvent.cs`
- Create: `src/Elyndor.Core/Combat/Runtime/TalentRuntimeState.cs`
- Create: `src/Elyndor.Core/Combat/Runtime/TalentRuntimeEngine.cs`
- Modify: `src/Elyndor.Core/Combat/Sessions/CombatSession.cs`
- Modify: `src/Elyndor.Core/Combat/Effects/EffectEngine.cs`
- Modify: `src/Elyndor.Core/Combat/Abilities/AbilityEngine.cs`
- Test: `tests/Elyndor.UnitTests/Combat/TalentRuntimeEngineTests.cs`
- Test: `tests/Elyndor.UnitTests/Combat/CombatSessionTests.cs`

**Interfaces:**
- `CombatRuntimeEvent` carries event kind, source/target actor IDs, time, ability/effect IDs, numeric outcome, and proc-chain metadata.
- `TalentRuntimeState.Publish(CombatRuntimeEvent, ResolvedTalentModifiers)` returns deterministic runtime actions and updates stacks/cooldowns/flags.
- `CombatSession` publishes events only from its existing single-writer command/timer paths.

- [ ] Add tests for event ordering, rank value selection, internal cooldown, proc-chain blocking, and reset behavior.
- [ ] Implement the state container with immutable snapshots for externally visible state.
- [ ] Publish events from ability completion, damage/effect resolution, resource changes, thresholds, kills, and terminal transitions.
- [ ] Ensure runtime state is session-local and cannot leak to the next combat.
- [ ] Run combat unit tests and the full unit test project.
- [ ] Commit the generic runtime slice.

### Task 3: Implement generic actors, threat, forced target, shields, and companion hooks

**Files:**
- Modify: `src/Elyndor.Core/Combat/CombatModels.cs`
- Create: `src/Elyndor.Core/Combat/Targeting/CombatActor.cs`
- Create: `src/Elyndor.Core/Combat/Targeting/ThreatTable.cs`
- Create: `src/Elyndor.Core/Combat/Targeting/TargetSelectionPolicy.cs`
- Create: `src/Elyndor.Core/Combat/Targeting/ForcedTargetState.cs`
- Create: `src/Elyndor.Core/Combat/Effects/ShieldState.cs`
- Modify: `src/Elyndor.Core/Combat/Sessions/CombatSession.cs`
- Modify: `src/Elyndor.Infrastructure/Combat/ArcherCompanionRuntimeResolver.cs`
- Test: `tests/Elyndor.UnitTests/Combat/ThreatTableTests.cs`
- Test: `tests/Elyndor.UnitTests/Combat/CombatTargetingTests.cs`
- Test: `tests/Elyndor.UnitTests/Combat/CompanionCombatTests.cs`
- Test: `tests/Elyndor.UnitTests/Combat/CombatSessionTests.cs`

- [ ] Test N enemies, player plus companion, deterministic target selection, threat modifiers, taunt duration, and forced-target expiry.
- [ ] Test shield absorption, overkill handling, lethal prevention, and reset between sessions.
- [ ] Replace compatibility single-enemy access only where the current mechanic requires a collection.
- [ ] Route all actor actions through the single-writer session.
- [ ] Run focused combat tests and content validation.
- [ ] Commit the actor/targeting slice.

### Task 4: Complete Warrior talent trees

**Files:**
- Modify: `content/package.json`
- Modify: `src/Elyndor.Core/Combat/Sessions/CombatSession.Berserker.cs`
- Create: `src/Elyndor.Core/Combat/Sessions/CombatSession.Guardian.cs`
- Create: `src/Elyndor.Core/Combat/Sessions/CombatSession.Warlord.cs`
- Modify: `src/Elyndor.Core/Talents/BerserkerTalentRuntimeCatalog.cs`
- Create: `src/Elyndor.Core/Talents/GuardianTalentRuntimeCatalog.cs`
- Create: `src/Elyndor.Core/Talents/WarlordTalentRuntimeCatalog.cs`
- Test: `tests/Elyndor.UnitTests/Combat/GuardianCombatSessionTests.cs`
- Test: `tests/Elyndor.UnitTests/Combat/BerserkerCombatSessionTests.cs`
- Test: `tests/Elyndor.UnitTests/Combat/WarlordCombatSessionTests.cs`
- Test: `tests/Elyndor.UnitTests/Talents/WarriorTalentBehaviorMatrixTests.cs`

- [ ] Create a behavior matrix from every Guardian, Berserker, and Warlord source-of-truth node.
- [ ] Implement Guardian threat, dodge reactions, shields, mitigation, HP thresholds, and lethal prevention through generic primitives.
- [ ] Verify Berserker rage, low-HP, crit, bleed, extra attacks, AoE, cooldown, and proc mechanics against content values.
- [ ] Implement Warlord-friendly actor primitives without creating Party persistence.
- [ ] Add rank-aware behavioral tests and remove all Warrior deferred modifiers.
- [ ] Run the Warrior matrix, combat tests, content validator, and integration tests covering talent mutation.
- [ ] Commit the Warrior slice.

### Task 5: Complete Mage talent trees

**Files:**
- Modify: `content/talents/mage-pyromancer.json`
- Modify: `src/Elyndor.Core/Combat/Sessions/CombatSession.Mage.cs`
- Modify: `src/Elyndor.Core/Combat/Sessions/CombatSession.Pyromancer.cs`
- Create: `src/Elyndor.Core/Combat/Sessions/CombatSession.Arcane.cs`
- Create: `src/Elyndor.Core/Combat/Sessions/CombatSession.Cryomancer.cs`
- Modify: `src/Elyndor.Core/Talents/MageTalentRuntimeCatalog.cs`
- Test: `tests/Elyndor.UnitTests/Combat/PyromancerCombatSessionTests.cs`
- Create: `tests/Elyndor.UnitTests/Combat/ArcaneCombatSessionTests.cs`
- Create: `tests/Elyndor.UnitTests/Combat/CryomancerCombatSessionTests.cs`
- Create: `tests/Elyndor.UnitTests/Talents/MageTalentBehaviorMatrixTests.cs`

- [ ] Implement Pyromancer heat, burn, critical, burst, AoE, execute, proc, and cooldown mechanics.
- [ ] Implement Arcane charges, mana thresholds, cast speed, penetration, shields, and sequencing.
- [ ] Implement Cryomancer slow/freeze/root, barriers, control duration, and chilled/frozen interactions.
- [ ] Test cast completion, interruption, deterministic procs, temporary effects, and all meaningful ranks.
- [ ] Remove all Mage deferred modifiers and validate talent-gated abilities after reset/loadout changes.
- [ ] Run Mage matrix and full relevant backend tests.
- [ ] Commit the Mage slice.

### Task 6: Complete Archer talent trees and companion behavior

**Files:**
- Modify: `content/talents/archer.json`
- Modify: `src/Elyndor.Core/Combat/Sessions/CombatSession.Archer.cs`
- Modify: `src/Elyndor.Core/Talents/ArcherTalentRuntimeCatalog.cs`
- Modify: `src/Elyndor.Infrastructure/Combat/ArcherCompanionRuntimeResolver.cs`
- Test: `tests/Elyndor.UnitTests/Combat/ArcherCombatSessionTests.cs`
- Create: `tests/Elyndor.UnitTests/Combat/ArcherCompanionBehaviorTests.cs`
- Create: `tests/Elyndor.UnitTests/Talents/ArcherTalentBehaviorMatrixTests.cs`

- [ ] Implement Marksman accuracy, marks, precision, piercing, burst, cooldown, and execution mechanics.
- [ ] Implement Beast Mastery companion damage, protection, threat, synchronization, and owner/companion events.
- [ ] Implement Arcane Archer mana, magical arrows, spirit companion, penetration, and resource interactions.
- [ ] Test player plus companion against multiple enemies and verify per-enemy effects.
- [ ] Remove all Archer deferred modifiers and run the Archer matrix.
- [ ] Commit the Archer slice.

### Task 7: Finish Russian talent UI and talent-gated ability regression

**Files:**
- Modify: `web/elyndor-web/src/game/talents/views/TalentTreeView.vue`
- Modify: `web/elyndor-web/src/api/contracts.ts`
- Modify: `web/elyndor-web/src/game/combat/views/CombatView.vue`
- Modify: `web/elyndor-web/src/stores/combatSession.ts`
- Test: `web/elyndor-web/src/__tests__/MageTalentTreeView.spec.ts`
- Test: `web/elyndor-web/src/__tests__/WarriorTalentTreeView.spec.ts`
- Create: `web/elyndor-web/src/__tests__/TalentLocalization.spec.ts`
- Create: `web/elyndor-web/src/__tests__/TalentAbilityGating.spec.ts`
- Modify: `content/talents/*.json` and existing talent labels in `web/elyndor-web/src/game/talents/views/*.vue`

- [ ] Ensure all player-facing names, descriptions, rank text, requirements, errors, branch labels, and buttons are Russian.
- [ ] Hide internal identifiers, runtime statuses, modifier keys, and technical English from player-facing UI.
- [ ] Verify learn/reset/loadout behavior changes ability availability authoritatively.
- [ ] Add UI tests for locked, available, max-rank, insufficient-points, error, and reset states.
- [ ] Run web lint, format check, typecheck, unit tests, and build.
- [ ] Commit the UI slice.

### Task 8: Full regression, documentation, and CI gate

**Files:**
- Modify: `AGENTS.md`
- Modify: `docs/source-of-truth/architecture/00_DEVELOPMENT_ROADMAP.md`
- Modify: `docs/source-of-truth/gameplay/02_COMBAT_SYSTEM.md`
- Modify: `docs/source-of-truth/gameplay/16_TALENT_SYSTEM.md`
- Modify: `.github/workflows/ci.yml`
- Modify: `tests/Elyndor.IntegrationTests/Combat/CombatDurabilityServiceTests.cs`
- Modify: `web/elyndor-web/e2e/game-shell.spec.ts`
- Create: `docs/reports/2026-09-08-talent-runtime-audit.md`

- [ ] Add restart/reconnect/reset regression coverage and document the current process-restart behavior.
- [ ] Add full node/rank/matrix counts and list every generic runtime primitive introduced.
- [ ] Align AGENTS and roadmap with the actual completed phase and remove stale deferred claims.
- [ ] Add frontend format checks to CI if they are part of Definition of Done.
- [ ] Run Release build, unit tests, PostgreSQL integration tests, content validator, web/admin lint/format/typecheck/unit/build, production publish validation, and real Browser → Server → PostgreSQL E2E.
- [ ] Review `git diff`, `git status`, secrets, migrations, scope, and documentation drift.
- [ ] Commit the final verification/documentation slice and report exact counts and any remaining blocked environment checks.
