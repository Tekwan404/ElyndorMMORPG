# Promo, Admin Economy and Final Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete Phase 8–10 with server-authoritative promo redemption, Admin V2 content support and final item-economy regression coverage.

**Architecture:** Promo definitions live in the existing versioned content package. A durable redemption row keyed by account and promo idempotency key is committed in the same PostgreSQL transaction as item grants and the existing Crystal ledger credit. Admin V2 edits the same content document and its existing publish validation pipeline validates promo and economy settings.

**Tech Stack:** ASP.NET Core, EF Core/PostgreSQL, Vue 3/TypeScript, existing content package, Crystal wallet and inventory services.

**Spec:** User-approved itemization phases 8–10 in the current conversation.

## Global Constraints

- Keep one Crystal wallet and immutable ledger; all mutations are server-side, transactional and replay-safe.
- Reuse the existing inventory, content package and Admin V2; no new admin app or premium currency.
- Never sell gameplay-gated catalysts or perform destructive migrations.

---

### Task 1: Phase 8 — Promo domain and redemption

**Files:** content package/validator, economy contracts/service/endpoints, persistence entity/configuration/migration, integration tests, player promo UI.

- [ ] Write integration tests for valid, unavailable, exhausted, replayed and concurrent redemption.
- [ ] Run the focused tests and observe the missing promo API/service failure.
- [ ] Add content-defined promo definitions, normalized-code lookup and validation.
- [ ] Add one transaction: lock account/character, resolve replay, enforce limits/time, grant items and Crystal ledger credit, persist redemption.
- [ ] Add player-facing redeem flow and Russian error states.
- [ ] Run focused backend/frontend tests and commit Phase 8.

### Task 2: Phase 9 — Admin V2 economy content

**Files:** existing Admin V2 contracts/endpoints/forms/content editing helpers and validator tests.

- [ ] Write failing validator/admin tests for malformed/duplicate promos and forbidden premium item eligibility.
- [ ] Extend existing content editor forms for Forge, Store and Promo content; do not create a second admin surface.
- [ ] Expose existing economy ledger/purchases/redemptions in an admin read model with authorization.
- [ ] Run Admin tests/build and commit Phase 9.

### Task 3: Phase 10 — Integration and regression polish

**Files:** affected item/economy views, relevant regression tests and docs only where required.

- [ ] Add regression tests for quality, lock, salvage, reroll, star upgrade, store and promo coexistence.
- [ ] Run the repository backend, content, player/admin frontend and browser verification commands.
- [ ] Review migrations, diff and production publish layout; fix scoped issues only.
- [ ] Commit Phase 10, open PR, wait for green CI and merge to main.
