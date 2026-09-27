# Item Generation Semantics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add explicit fixed/rolled item-generation semantics and stronger equipment validation without changing current item balance or runtime results.

**Architecture:** Extend the existing `ItemDefinition` contract with one orthogonal enum, make the existing procedural policy honor it, and centralize generation-shape validation in the current content validator. Preserve old published snapshots through codec normalization and explicitly annotate all current equipment content.

**Tech Stack:** .NET 10, C#, System.Text.Json, xUnit, Elyndor category JSON composition.

**Spec:** `docs/superpowers/specs/2026-09-27-item-generation-semantics-design.md`

## Global Constraints

- Keep the existing category composition pipeline and runtime DTOs.
- Preserve old published content payloads.
- Do not alter item stats, affix pools, loot, rarity, prices, or gameplay balance.
- Do not add a template engine, profile-ID indirection, or unique-effect runtime.

---

### Task 1: Generation contract and policy

**Files:**
- Modify: `src/Elyndor.Core/Items/ItemModels.cs`
- Modify: `src/Elyndor.Core/Items/ItemizationModels.cs`
- Test: `tests/Elyndor.UnitTests/Items/ItemInstanceStatRollerTests.cs`

- [x] Add failing tests proving fixed definitions cannot enter V2 generation and rolled definitions can.
- [x] Add `ItemGenerationMode` and `ItemDefinition.GenerationMode`.
- [x] Make `ProceduralItemPolicy.IsEnabled` require `Rolled`.
- [x] Run the focused item-generation tests.

### Task 2: Content validation semantics

**Files:**
- Modify: `src/Elyndor.Core/Content/Validation/GameContentPackageValidator.Itemization.cs`
- Modify: `src/Elyndor.Core/Content/Validation/GameContentPackageValidator.Items.cs`
- Test: `tests/Elyndor.UnitTests/Content/GameContentPackageValidatorTests.cs`

- [x] Add failing tests for fixed-with-random-policy, incomplete rolled policy, valid fixed stats, valid rolled policy, mixed legacy/V2 policy, and two-handed off-hand categories.
- [x] Implement centralized generation-shape predicates and stable validation errors.
- [x] Strengthen the existing equipment-category shape check without changing valid current combinations.
- [x] Run focused validator tests.

### Task 3: Historical payload compatibility

**Files:**
- Modify: `src/Elyndor.Infrastructure/Content/GameContentPackageCodec.cs`
- Test: `tests/Elyndor.IntegrationTests/Content/ContentRevisionImporterTests.cs`

- [x] Add a failing compatibility test for a canonical historical payload without `generationMode`.
- [x] Upgrade historical item nodes before deserialization using the previous runtime enablement rule.
- [x] Verify round-trip and revision-import tests.

### Task 4: Explicit current content

**Files:**
- Modify: `content/package.json`
- Modify: `content/items/*.json` containing equipment definitions
- Modify: `tools/Elyndor.ContentValidator/ContentAuditExporter.cs`
- Test: `tests/Elyndor.IntegrationTests/Content/GameContentPackageLoaderTests.cs`

- [x] Add a failing composed-content assertion requiring explicit `Rolled` mode.
- [x] Mechanically annotate all authored equipment definitions as `Rolled` while preserving every other field and value.
- [x] Expose generation mode in the denormalized audit output.
- [x] Run content loading and validation.

### Task 5: Regression verification and review

**Files:**
- Modify if required: item-generation tests that construct procedural templates explicitly.
- Update: `content-analysis/current/01-items.json`

- [x] Regenerate the composed analysis export.
- [x] Run focused item/content tests.
- [x] Run Release build and the complete backend test suite.
- [x] Run `git diff --check` and inspect the full diff for balance drift and unrelated changes.
