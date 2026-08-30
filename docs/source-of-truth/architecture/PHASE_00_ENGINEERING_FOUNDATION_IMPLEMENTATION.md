# Phase 0 — Engineering Foundation Implementation

**Status:** Complete
**Owner:** Engineering
**Scope:** foundation only; no Telegram identity or gameplay domain implementation

## Goal

A fresh checkout has one documented local start path, reproducible backend/frontend builds, automated tests, versioned validated static content, observable health, and a browser-verified mobile shell.

Redis is not part of Phase 0 Definition of Done. It may be introduced later only for a measured cache, presence, rate-limit, leaderboard, or scale-out need. PostgreSQL remains permanent truth.

## Current inventory

| Area | State | Evidence or gap |
|---|---|---|
| Repository conventions | Implemented | `global.json`, central packages, editor/build/git settings |
| Modular monolith projects | Implemented | Core, Contracts, Infrastructure, Server, ServiceDefaults |
| PostgreSQL wiring | Verified | Aspire resource and one `GameDbContext`; PostgreSQL 18.4 reported Healthy in the runtime smoke test |
| Observability and health | Implemented | ServiceDefaults, OpenTelemetry, `/health`, `/alive`, status API |
| Vue mobile shell | Implemented | Vue 3/Vite/Pinia/Router and Telegram-like mobile Playwright project |
| CI | Implemented | backend, frontend, Playwright, and content validation checks exist; migration drift check starts with the first real model |
| Static content package | Implemented | strict JSON loading, version metadata, duplicate/reference validation, startup fail-fast, CLI |
| Aspire end-to-end smoke | Verified | PostgreSQL, `game`, Server, and Vue reported Healthy; API/health/UI returned HTTP 200 |

## Execution checklist

### Repository workflow

- [x] Add concise `AGENTS.md`.
- [x] Add repo-local feature, architecture, combat, testing, and review skills.
- [x] Document product/prototype boundaries and Phase 1 plan.
- [x] Verify project-local skills are discovered by Codex-compatible skill tooling.

### Content foundation

- [x] Add one versioned JSON content package with `ContentVersion`, `BalanceVersion`, and UTC publication time.
- [x] Validate package shape and required metadata.
- [x] Reject duplicate IDs within a definition type.
- [x] Reject missing typed references.
- [x] Load and validate the active package during server startup.
- [x] Add a deterministic CLI validation command and run it in CI.
- [x] Add focused unit/integration tests for failure cases.

### Persistence and CI

- [x] Defer the first EF migration to the first real persistent model; do not create an empty migration.
- [x] Schedule `dotnet ef migrations has-pending-model-changes` with that first model/migration rather than adding a no-op Phase 0 check.
- [x] Keep secrets out of tracked configuration.

### Runtime and browser verification

- [x] Start the AppHost with PostgreSQL, Server, Vue, dashboard, and health checks.
- [x] Verify API and PostgreSQL health from Aspire.
- [x] Verify frontend load, console/page errors, navigation, mobile viewport, and basic accessibility with Playwright.
- [x] Review the complete git diff and run the full repository verification suite.

## Definition of Done

Phase 0 is complete only when all applicable checklist items are verified by commands run against the current checkout. Docker/Aspire runtime smoke is mandatory; an unavailable local container runtime is a blocker, not a pass.

Phase 1 must not start before Phase 0 is complete.
