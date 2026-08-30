---
name: elyndor-feature
description: Implement an Elyndor gameplay, backend, frontend, or full-stack feature as a phase-bounded vertical slice. Use when adding or changing player-facing behavior, APIs, domain rules, persistence, or Vue flows.
---

# Elyndor Feature

Keep the feature inside the current development phase and deliver a small, playable or verifiable slice.

## Workflow

1. Read `AGENTS.md`, the current phase document, and the relevant system/UI Source of Truth.
2. Inspect the existing code, contracts, persistence, migrations, frontend flow, and tests before designing changes.
3. Identify dependencies, ownership, failure cases, and the minimum end-to-end scope.
4. Write a short implementation plan. Do not introduce future-phase systems.
5. Use tests first for meaningful game rules, persistence mutations, regressions, or error handling.
6. Implement server authority and persistence before trusting client presentation.
7. Add or update the frontend only when the slice is player-facing; include loading, empty, disabled, error, and reconnect states as applicable.
8. Run the repository's actual build, test, lint, typecheck, and browser commands.
9. Review `git diff` and `git status` before declaring completion.

## Guardrails

- The client sends intent, never authoritative results.
- Keep balance values in content/configuration owned by the correct system.
- Do not add React, Phaser, another engine, Redis, or infrastructure without a current documented need.
- Prefer cohesive services and explicit transaction boundaries over generic abstractions.
