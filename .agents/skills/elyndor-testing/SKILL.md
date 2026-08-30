---
name: elyndor-testing
description: Plan and run Elyndor verification after meaningful implementation. Use for test strategy, regression coverage, database/API tests, Vue tests, Playwright checks, and Definition of Done verification.
---

# Elyndor Testing

Read `AGENTS.md`, the current phase Definition of Done, repository project files, `package.json`, Playwright config, and CI before selecting commands.

## Select the necessary layers

- Unit: deterministic game rules, formulas, state transitions, RNG, time, and edge cases.
- Integration: EF mappings, PostgreSQL constraints, migrations, transactions, concurrency, idempotency, outbox, and auth.
- API: validation, authorization, stable error codes, retries, cancellation, and contracts.
- Frontend: stores, components, presentation rules, loading/empty/error/disabled/reconnect states, typecheck, lint, and build.
- Playwright/browser: real load, console/page errors, Telegram-like mobile viewport, navigation, buttons, basic accessibility, and current critical player flows.
- Regression: every fixed defect gets the smallest test that would have caught it.

## Failure-first checklist

Consider duplicate requests, retry, reconnect, server restart, invalid data, database constraint failure, races, cancellation, and partial failure before the happy path.

Compilation alone is not verification. Run only commands defined by the repository, record their actual results, and call blocked checks blocked rather than passing.
