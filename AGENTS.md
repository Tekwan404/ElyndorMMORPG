# Elyndor Codex Guide

## Project

Elyndor is a mobile-first dark-fantasy MMORPG built as a modular monolith with .NET 10, ASP.NET Core, EF Core, PostgreSQL 18, SignalR, Vue 3, TypeScript, Vite, Aspire and OpenTelemetry.

The repository `README.md` is the current **as-built snapshot**. Do not infer implementation status from an old phase plan or design note.

## Documentation map

Read only the smallest relevant set:

1. `README.md` — what is implemented in `main` now.
2. `docs/source-of-truth/README.md` — documentation ownership and precedence.
3. Relevant `docs/source-of-truth/gameplay/*.md` — gameplay contracts.
4. Relevant `docs/source-of-truth/ui/*.md` — UI/UX contracts.
5. `docs/source-of-truth/architecture/*.md` — stack, roadmap and product boundaries.
6. `docs/development/getting-started.md` / `git-workflow.md` — active developer workflow.
7. `docs/deployment/vps-production.md` — production operations.

Temporary implementation plans, historical audits and agent planning notes are intentionally not part of active documentation. Git history is the archive.

## Current implementation boundaries

- Playable character-creation roster: `WARRIOR`, `ARCHER`, `MAGE`, `PALADIN`.
- Party runtime: up to 5 players.
- Current authored group content uses the dungeon pipeline for 1–5 players.
- Raid design/domain work exists, but unfinished raid work outside `main` must not be treated as production functionality.
- Inventory base capacity is 30 plus one equipped Spatial Artifact bonus.
- Unified BattleScreen is the current solo/party combat presentation.
- Talent/content coverage is broader than verified runtime coverage; never assume every authored node is fully supported without checking implementation/tests.

## Core invariants

- Backend is server-authoritative and never trusts client-provided gameplay results or Telegram identity.
- PostgreSQL is permanent truth. Redis is only introduced for a measured need and is never permanent truth.
- Retryable mutations and rewards are idempotent/transaction-safe where required.
- Active combat follows single-writer semantics.
- Use `TimeProvider` for authoritative time and deterministic/injectable RNG boundaries for important randomness.
- Gameplay content is data-driven, versioned and validated.
- Reconnect/restart behavior must be explicit.
- UX is mobile-first and game-like rather than a generic dashboard.
- Keep the modular monolith simple; no infrastructure for hypothetical future scale.

## Development rules

For each change:

1. Read the current README and the relevant Source of Truth.
2. Inspect existing implementation, tests, migrations and content before designing a replacement.
3. If the intended mechanic changes, update the authoritative system/UI document in the same logical change.
4. Add focused regression coverage for behavior that can break rewards, persistence, targeting, reconnect or combat lifecycle.
5. Implement the smallest complete vertical slice.
6. Run the relevant backend/frontend/content/browser checks.
7. Review the diff for scope creep, secrets, stale docs and duplicated sources of truth.
8. Report anything not verified.

## Repository workflow

Repository contribution rules live in `CONTRIBUTING.md` and `docs/development/git-workflow.md`.

- Never do feature work directly on `main`.
- Use a short-lived branch for one logical change set.
- Open a PR and merge only after required CI is green.
- Do not force-push or rewrite `main` history.
- Close superseded PRs and abandon stale branches instead of stacking new work on them.
