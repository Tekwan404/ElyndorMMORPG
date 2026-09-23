---
name: elyndor-development
description: >
  Core development workflow for the Elyndor MMORPG repository.
  Use automatically for any substantial Elyndor implementation, bug fix,
  refactor, architecture change, API change, database change, client change,
  feature development, regression investigation, or pull request work.
  This skill defines repository-wide engineering rules, investigation workflow,
  branch safety, testing expectations, backward-compatibility requirements,
  and completion criteria. Use together with more specialized Elyndor skills
  such as elyndor-combat and elyndor-content when applicable.
---

# Elyndor Development

## Purpose

This skill defines the default engineering workflow for Elyndor.

Use it as the base skill for substantial work in the repository.

Do not wait for the user to explicitly request this skill.

When another Elyndor skill applies, combine it with this skill rather than replacing this skill.

---

# Project context

Elyndor is a mobile-first dark fantasy MMORPG.

Primary technology stack:

- ASP.NET Core / C#
- .NET
- PostgreSQL
- Entity Framework Core
- SignalR / realtime systems where applicable
- Vue
- TypeScript
- mobile-first web client
- Playwright for browser/mobile regression testing

The game may run through Telegram WebApp as well as ordinary browser entry points.

Do not assume Telegram is the sole identity, session, or platform boundary unless the current implementation explicitly requires it.

---

# Core engineering principles

Prefer:

- correctness over speed of implementation;
- explicit domain models over hidden coupling;
- data-driven systems over duplicated constants;
- idempotent operations where retry is possible;
- immutable captured state where runtime consistency requires it;
- backward compatibility for persisted game state;
- centralized domain rules instead of repeated checks;
- small coherent changes over broad opportunistic rewrites.

Avoid:

- magic constants duplicated across services;
- silent exception swallowing;
- speculative abstractions without a current use case;
- changing unrelated systems during a focused task;
- replacing working architecture only because another pattern is fashionable;
- claiming success after compilation alone.

---

# Mandatory workflow

For a substantial task follow:

1. inspect;
2. trace;
3. understand invariants;
4. identify tests;
5. implement;
6. run focused verification;
7. fix failures;
8. run wider regression verification;
9. inspect the final diff.

Do not stop after analysis if implementation is possible.

---

# Step 1 — inspect before editing

Before modifying an existing system:

- locate the actual implementation;
- find public entry points;
- find service callers;
- find persistence boundaries;
- find API/SignalR contracts;
- find frontend consumers;
- find relevant tests;
- search for duplicated implementations of the same rule.

Do not assume a filename is the only implementation of a concept.

Search by:

- type name;
- method name;
- endpoint;
- domain identifier;
- content key;
- error string;
- database field;
- client contract.

---

# Step 2 — establish invariants

Before changing behavior, identify what must remain true.

Examples:

- account operations should remain idempotent when designed that way;
- one-character-per-account restrictions must remain consistent across all creation paths;
- item restrictions must be enforced server-side;
- persisted state must remain loadable;
- reconnect must not duplicate state transitions;
- rewards must not be issued twice;
- client state must not become the authority for protected game rules.

When a rule is important, enforce it at the server/domain layer.

UI restrictions alone are not sufficient.

---

# Step 3 — branch safety

Never modify `main` unless the user explicitly asks for work on `main`.

Before editing:

- inspect the current branch;
- inspect working tree status;
- preserve unrelated uncommitted work;
- do not reset, discard, stash, or overwrite user changes without explicit necessity.

When the user names a branch, work only on that branch unless technically impossible.

Do not merge automatically unless explicitly requested.

Do not silently push to `main`.

---

# Step 4 — database changes

For database-affecting changes:

1. inspect the current entity model;
2. inspect mappings;
3. inspect migrations;
4. inspect all read/write paths;
5. consider existing production rows;
6. consider concurrent requests;
7. consider partial deployment compatibility where relevant.

Avoid migrations that unnecessarily destroy or recreate data.

For new non-nullable persisted fields, define a safe migration/default/backfill strategy.

For state transitions, consider:

- optimistic concurrency;
- duplicate requests;
- retries;
- simultaneous operations;
- transaction boundaries.

---

# Step 5 — API and realtime changes

For endpoint or SignalR changes:

- preserve existing contracts unless intentionally versioning them;
- update server DTOs;
- update client types;
- update runtime consumers;
- update tests;
- handle reconnect/retry behavior;
- avoid trusting client-provided authority-sensitive values.

Any change to an API contract must be traced end-to-end.

---

# Step 6 — frontend changes

Elyndor is mobile-first.

When modifying Vue/TypeScript UI:

- optimize for narrow screens first;
- keep primary actions reachable;
- avoid desktop-sized panels;
- avoid excessive vertical waste;
- avoid layouts that require precision tapping;
- keep combat-critical information visible;
- respect safe areas;
- avoid unnecessary duplicated information;
- preserve existing responsive behavior.

Use existing design tokens/components before introducing new one-off styles.

For state:

- identify the source of truth;
- avoid parallel copies of the same state;
- avoid stale local state after server mutations;
- verify reconnect/refetch behavior.

---

# Step 7 — error handling

Never intentionally swallow meaningful exceptions without observability.

For recoverable failures:

- return an appropriate domain/application error;
- preserve enough diagnostic information in logs;
- avoid leaking secrets to the client.

For background/runtime loops:

- prevent one failed iteration from destroying the service if recovery is intended;
- log failures with enough context to identify the session/entity/action;
- distinguish cancellation from unexpected failure.

---

# Step 8 — performance

Do not optimize blindly.

However, actively watch for:

- N+1 queries;
- unnecessary round trips;
- repeated content parsing;
- unbounded in-memory registries;
- missing TTL/cleanup;
- unnecessarily serialized operations;
- excessive allocations in high-frequency combat/tick paths;
- repeated full-state client payloads.

If a performance concern is not demonstrated, prefer maintainability over premature optimization.

---

# Step 9 — security

Server-side code is authoritative.

Never trust the client for:

- account identity;
- character identity;
- inventory quantities;
- item ownership;
- combat results;
- damage values;
- currency values;
- permissions;
- admin access;
- reward eligibility.

Validate authentication and authorization at the appropriate boundary.

Never expose secrets, environment values, tokens, database credentials, or privileged Telegram/admin configuration.

---

# Testing strategy

After a code change, run the smallest relevant tests first.

Typical order:

1. affected unit tests;
2. affected integration/runtime tests;
3. build;
4. broader regression tests;
5. Playwright/mobile regression tests when UI behavior changed.

Do not run the entire world first if a focused failure gives faster feedback.

When a test fails:

- determine whether it is caused by the current change;
- fix the implementation when behavior is wrong;
- update the test only when the intended contract genuinely changed.

Do not weaken tests just to make CI green.

---

# Regression checklist

Before considering substantial work complete, check relevant areas:

- startup;
- authentication;
- bootstrap;
- character loading;
- persistence;
- inventory;
- equipment;
- combat;
- reconnect;
- rewards;
- party/group systems;
- dungeon/raid systems where affected;
- mobile UI;
- API/client compatibility.

Only test areas touched or plausibly affected by the change.

---

# Completion criteria

A task is not complete merely because code was written.

Before claiming completion:

- code compiles;
- relevant tests pass;
- new failures are investigated;
- important edge cases are covered;
- final diff is inspected;
- no unrelated changes were introduced;
- no obvious TODO was left in a path described as complete.

If something remains unresolved, describe the precise blocker.

Do not report speculative implementation as finished work.

---

# When to combine other skills

Use `elyndor-combat` whenever work touches:

- CombatSession;
- targeting;
- abilities;
- talents;
- combat actors;
- buffs/debuffs;
- threat;
- party combat;
- raid combat;
- companions;
- combat rewards;
- reconnect/finalization.

Use `elyndor-content` whenever work touches:

- mobs;
- items;
- loot;
- bosses;
- locations;
- sets;
- professions;
- content JSON;
- drop tables;
- balance data;
- content validation.

Use both when the task crosses those domains.
