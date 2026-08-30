# Elyndor Phase 0 Completion Implementation Plan

> **For Codex:** REQUIRED SUB-SKILL: Use Superpowers `executing-plans` to implement this plan task-by-task.

**Goal:** Complete the remaining engineering-foundation work without entering Phase 1 or adding Redis without a measured need.

**Architecture:** Keep the existing modular monolith. Content rules and semantic validation live in Core; JSON/file loading lives at the infrastructure boundary; Server loads one immutable validated package at startup; a small CLI runs the same validation in CI.

**Tech Stack:** .NET 10, System.Text.Json, xUnit, ASP.NET Core, Vue 3, Vitest, Playwright, GitHub Actions.

---

### Task 1: Project workflow and Source of Truth

**Files:** `AGENTS.md`, `.agents/skills/elyndor-*/SKILL.md`, product/phase documents, roadmap/stack/UI indexes.

1. Create the concise project map and five repo-local workflows.
2. Record Phase 0 as current and Phase 1 as blocked.
3. Resolve Redis and visual-reference drift in owner documents.
4. Validate local skill metadata and discovery.

### Task 2: Versioned content validation with TDD

**Files:** Core content models/validator, infrastructure JSON loader, content package, unit tests.

1. Add failing tests for valid metadata, duplicate typed IDs, missing typed references, and invalid identifiers.
2. Run the focused tests and confirm the expected compile/test failure.
3. Add the smallest content package model and semantic validator.
4. Add JSON loading with strict deserialization and aggregated validation errors.
5. Run the focused tests until green.

### Task 3: Startup and CI validation

**Files:** Server startup/project file, content validation CLI/project, solution, CI, development docs.

1. Make Server load the copied active package before accepting traffic.
2. Add a CLI that validates the repository package and returns a non-zero exit code on failure.
3. Add the command to CI and local verification docs.
4. Run the validator against the actual package.

### Task 4: Full verification and review

1. Run Release backend build and tests.
2. Run frontend lint, format check, unit tests, typecheck/build, and Playwright mobile checks.
3. Attempt AppHost runtime smoke and record Docker/Aspire blockers exactly.
4. Inspect `git diff --check`, `git diff`, and `git status`.
5. Perform an Elyndor severity review and fix all Critical/High findings in scope.
