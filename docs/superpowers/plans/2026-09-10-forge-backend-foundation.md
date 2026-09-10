# Forge Backend Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the existing server-authoritative affix reroll consume Gold and Reforge Stones without changing item identity or rarity.

**Architecture:** `ItemReforgeService` remains the sole Forge mutation owner. It locks the character row, validates ownership, lock/equipment state and resources, then atomically spends resources and persists a replay-safe `ItemReforgeOperation`. Existing proposal/accept persistence remains the canonical result record; no new Forge entity or UI is introduced.

**Tech Stack:** ASP.NET Core, EF Core, PostgreSQL, xUnit, existing versioned game content.

**Spec:** User Phase 3 Forge Backend Foundation requirements in the active task.

## Global Constraints

- Forge mutates an existing generated equipment instance; it never creates an item.
- Server determines eligibility, price and deterministic roll; the client sends only the intended item, affix slot and operation ID.
- Cost is content-driven and consists of Gold plus `REFORGE_STONE`; no profession XP or premium currency.
- Item identity and rarity remain unchanged; locked/equipped/transaction-locked items are rejected.
- PostgreSQL transaction and the existing operation ID guarantee retry safety.

---

### Task 1: Move existing reforge costs to Reforge Stones

**Files:**
- Modify: `content/itemization/system.json`
- Test: `tests/Elyndor.UnitTests/Items/ItemReforgeCostTests.cs`

**Produces:** A content profile whose material cost is `REFORGE_STONE` and whose catalyst quantity is zero for every rarity.

- [ ] Write a failing content-cost test which loads the package and asserts `MaterialItemId == "REFORGE_STONE"`, all normal material costs are positive, and all catalyst quantities are zero.
- [ ] Run the test and verify it fails against the old Spider Silk/Venom configuration.
- [ ] Update only `reforgeCosts` in itemization content to use Reforge Stones and no catalyst consumption.
- [ ] Run the cost test and the content validator; verify both pass.
- [ ] Commit the content-cost change with its test.

### Task 2: Verify Forge mutation invariants with PostgreSQL

**Files:**
- Create: `tests/Elyndor.IntegrationTests/Items/ItemReforgeServiceTests.cs`
- Reuse: `src/Elyndor.Infrastructure/Items/ItemReforgeService.cs`

**Produces:** Regression coverage for atomic resource spending, replay, lock rejection, and immutable item identity/rarity.

- [ ] Write failing integration tests for a successful reroll and replay, insufficient Reforge Stones, locked item, equipped item, and declined proposal.
- [ ] Run the focused Docker integration tests and verify the first missing behavior fails before any production change.
- [ ] Make only the smallest service correction if a test exposes a violated invariant; do not add a second reroll architecture.
- [ ] Run focused integration tests, all unit tests, solution build, content validation and frontend typecheck/build.
- [ ] Commit the test coverage and any necessary service correction.

### Task 3: Review and publish the phase

**Files:**
- Review: all Phase 3 diff files

- [ ] Inspect `git diff --check`, migration state and content validation output.
- [ ] Push the branch, open a PR, and wait for green CI before merge.
- [ ] Report verified commands and any deferred UI work (Phase 4).
