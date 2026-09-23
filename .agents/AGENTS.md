# Elyndor — AGENTS.md

## 1. Purpose

This file is the default operating guide for AI coding agents working in the Elyndor repository.

The goal is not to produce isolated patches. The goal is to move Elyndor forward safely as a coherent mobile MMORPG:

**inspect → understand → implement → test → fix → verify → review**

Use the smallest relevant set of project skills for each task. Do not invoke every skill by default.

---

## 2. Product Direction

Elyndor is a **mobile-first dark-fantasy MMORPG** built primarily as a web application.

Current technology direction:

- Backend: ASP.NET Core / .NET
- Persistence: PostgreSQL + EF Core
- Realtime: SignalR
- Frontend: Vue 3 + TypeScript + Vite
- State: Pinia
- Routing: Vue Router
- Browser verification: Playwright
- Observability and infrastructure follow the repository configuration

### Identity and platform rule

Do **not** treat Telegram as the permanent identity boundary of the game.

Telegram is an integration / authentication channel, not the domain model itself.

New systems should avoid unnecessary coupling to Telegram so Elyndor can support:

- normal browser play;
- persistent browser sessions;
- guest / provisional play where approved;
- account linking;
- Telegram authentication;
- other identity providers in the future.

Do not redesign authentication speculatively. Preserve current behavior unless the task explicitly changes it.

---

## 3. Current Scope

Primary active gameplay areas:

- solo gameplay;
- companions where explicitly supported;
- Party gameplay;
- dungeons;
- multi-enemy combat;
- targeting and AoE;
- abilities;
- talents;
- threat;
- loot and rewards;
- equipment;
- inventory;
- content;
- reconnect / restore;
- mobile UI/UX;
- browser session reliability;
- admin/content tooling where already present.

### Raids are frozen

Raid development is currently **deferred**.

Unless the user explicitly reactivates raid development:

- do not add raid-specific targeting;
- do not add subgroup logic;
- do not redesign Party around future raids;
- do not add speculative raid persistence;
- do not add raid reward abstractions;
- do not introduce hidden 5-player raid subdivisions;
- do not expand unfinished raid code during unrelated work.

Preserve existing raid code where safe, but do not build new architecture around it.

---

## 4. Instruction Precedence

When deciding what to do, use this order:

1. System / platform instructions.
2. The user's current explicit request.
3. This `AGENTS.md`.
4. Current repository implementation and tests.
5. Current Source of Truth documentation.
6. Older design documents and historical plans.
7. External references.

If an older document conflicts with an explicit current user decision, do not silently restore the old design.

When implementation, tests, and docs disagree, investigate the disagreement before changing behavior.

---

## 5. Source of Truth

Read only the smallest relevant set before changing code.

Important locations include:

- `docs/source-of-truth/architecture/`
- `docs/source-of-truth/gameplay/`
- `docs/source-of-truth/ui/`
- `docs/source-of-truth/phases/`
- `content/`
- `.github/workflows/`
- repository test projects
- current frontend configuration
- current EF Core migrations and mappings

Do not blindly follow an old phase document when the repository has clearly progressed beyond it.

For gameplay values and content, inspect the current runtime implementation, schema, loader, validator, and existing content before proposing new fields or numbers.

---

# 6. Skill Routing

Project skills live under:

`/.agents/skills/`

Select skills automatically from the task. Prefer the smallest useful combination.

## 6.1 Always-consider project skills

### `elyndor-development`

Use for substantial Elyndor development:

- new features;
- bug fixes;
- refactors;
- API changes;
- backend changes;
- frontend changes;
- architecture changes;
- persistence changes;
- cross-layer work;
- regressions;
- implementation planning.

This is the primary Elyndor engineering workflow.

### `elyndor-combat`

Use when the task touches:

- CombatSession;
- actors;
- attacks;
- abilities;
- cooldowns;
- talents;
- damage;
- healing;
- effects;
- buffs / debuffs;
- threat;
- targeting;
- AoE;
- companions in combat;
- Party combat;
- dungeon combat;
- enemies;
- multi-enemy combat;
- combat reconnect;
- combat rewards;
- kill credit;
- combat finalization.

### `elyndor-content`

Use when the task touches:

- mobs;
- bosses;
- enemies;
- locations;
- biomes;
- dungeons;
- items;
- equipment;
- weapons;
- armor;
- loot;
- drop tables;
- sets;
- professions;
- materials;
- contracts;
- content JSON;
- content loaders;
- content validators;
- progression numbers;
- balance.

---

## 6.2 Backend

### `aspnet-core`

Use for:

- ASP.NET Core;
- controllers;
- minimal APIs;
- middleware;
- DI;
- hosted services;
- authentication;
- authorization;
- SignalR server code;
- HttpClient;
- configuration;
- server lifecycle.

Usually combine with `elyndor-development`.

---

## 6.3 PostgreSQL / EF Core

### `supabase-postgres-best-practices`

Use for PostgreSQL concerns even though Elyndor does not use Supabase itself.

Use for:

- schema design;
- indexes;
- query plans;
- locking;
- concurrency;
- transactions;
- PostgreSQL performance;
- connection management.

Pair it with repository-specific EF Core conventions. Do not introduce Supabase-specific architecture.

---

## 6.4 Vue

### `vue-best-practices`

Use for normal Vue implementation and refactoring.

### `vue-pinia-best-practices`

Use when touching:

- Pinia stores;
- shared client state;
- state synchronization;
- store actions;
- derived state;
- reconnect/refetch behavior.

### `vue-router-best-practices`

Use when touching:

- routes;
- navigation;
- guards;
- route state;
- page transitions;
- deep links.

### `vue-debug-guides`

Use when investigating Vue/runtime/frontend bugs.

### `vue-testing-best-practices`

Use for Vue unit/component tests and frontend test strategy.

---

## 6.5 UI / UX

### `frontend-design`

Use when creating or substantially redesigning visual UI.

Focus on:

- visual hierarchy;
- composition;
- spacing;
- typography;
- polish;
- coherent visual language;
- avoiding generic dashboard-like AI UI.

### `game-ui-ux`

Use for gameplay-facing UI:

- combat HUD;
- inventory;
- equipment;
- map;
- guilds;
- shops;
- character screens;
- talents;
- groups;
- dungeons;
- game navigation;
- touch interaction;
- mobile safe areas;
- game-state feedback.

### `design-systems-frontend-architecture`

Use when work affects:

- design tokens;
- reusable UI components;
- shared spacing;
- typography system;
- component states;
- consistent visual patterns;
- responsive system behavior.

### `performance`

Use when the problem involves:

- slow UI;
- rendering;
- loading;
- images;
- bundle/runtime performance;
- responsiveness;
- mobile web performance.

---

## 6.6 Browser verification

### `playwright`

Use when behavior should be verified in a real browser.

Especially use for:

- mobile layout;
- navigation;
- combat interactions;
- reconnect;
- session persistence;
- form flows;
- admin flows;
- console errors;
- page errors;
- narrow viewport regressions.

Do not replace repository Playwright tests with ad-hoc browser checks when a regression test belongs in the test suite.

---

## 6.7 Debugging

### `systematic-debugging`

Use for bugs where the cause is not already obvious.

Required mindset:

1. reproduce;
2. trace;
3. identify the real source;
4. prove the hypothesis;
5. fix the source rather than the symptom;
6. add the smallest regression test that would have caught it.

Do not make a chain of speculative patches.

---

## 6.8 Security

### `security-best-practices`

Use when touching:

- authentication;
- authorization;
- JWT/session handling;
- admin access;
- user input;
- secrets;
- cookies;
- account linking;
- identity;
- permissions;
- public API exposure.

Security rules must be enforced server-side.

---

## 6.9 Git isolation

### `using-git-worktrees`

Use when:

- parallel work is requested;
- multiple branches must remain active;
- the current checkout must not be disturbed;
- isolated feature work is safer in a separate worktree.

Do not create worktrees without a concrete need.

---

# 7. Common Skill Combinations

## UI redesign

Example request:

> Redesign the inventory screen.

Use:

- `elyndor-development`
- `frontend-design`
- `game-ui-ux`
- `design-systems-frontend-architecture`
- `vue-best-practices`
- `vue-pinia-best-practices` if store/state changes
- `playwright`

## Frontend bug

Example:

> The map breaks on a narrow screen.

Use:

- `elyndor-development`
- `systematic-debugging`
- `vue-debug-guides`
- `game-ui-ux`
- `vue-best-practices`
- `playwright`

Add `performance` only if the issue is actually performance-related.

## Backend gameplay feature

Use:

- `elyndor-development`
- `aspnet-core`

Add:

- `elyndor-combat` for combat;
- `elyndor-content` for content;
- `supabase-postgres-best-practices` for significant DB work;
- `security-best-practices` for auth/security.

## Combat bug

Example:

> Reward is duplicated after reconnect.

Use:

- `elyndor-development`
- `elyndor-combat`
- `systematic-debugging`
- `aspnet-core`
- `supabase-postgres-best-practices` when persistence/transactions are involved
- relevant tests

## New content

Example:

> Add enemies, loot and a new location.

Use:

- `elyndor-development`
- `elyndor-content`

Add code-specific skills only when runtime/schema/UI changes are actually required.

---

# 8. Development Workflow

For substantial implementation, do not stop after analysis.

Work through the complete logical block:

1. Inspect current branch and working tree.
2. Read the relevant code and smallest relevant documentation.
3. Trace the current behavior end-to-end.
4. Identify invariants and failure cases.
5. Inspect existing tests.
6. Implement the smallest complete solution.
7. Run focused tests.
8. Fix failures.
9. Run relevant broader regression checks.
10. Review the complete diff.
11. Remove accidental scope creep.
12. Report what changed and what was actually verified.

If one completed step naturally enables the next step in the same requested block, continue automatically.

Do not return control after every small fix.

Do not finish with:

- “next I would…”;
- “I can continue…”;
- “the next step is…”;

when that next step can reasonably be performed now.

---

# 9. Branch Safety

Before editing:

- inspect the current branch;
- inspect `git status`;
- preserve unrelated user changes.

Rules:

- Do not perform feature work directly on `main` unless the user explicitly requests it.
- Do not merge into `main` without explicit permission.
- Do not push to `main` without explicit permission.
- Do not reset, discard, overwrite, or stash unrelated user work.
- Do not reuse an unrelated stale feature branch for a new task.
- Prefer one logical branch per logical change set.
- When the user names a branch, stay on that branch.
- When the user explicitly says another branch must not be touched, treat that as a hard constraint.

A dirty working tree is not permission to delete or rewrite existing changes.

---

# 10. Scope Control

Avoid speculative architecture.

Do not introduce systems merely because they may be useful later.

In particular, avoid:

- premature microservices;
- event sourcing without a current need;
- message brokers without a current need;
- generic repository wrappers over EF Core;
- speculative raid abstractions;
- future identity-provider abstractions with no immediate use;
- giant framework rewrites;
- broad design-system rewrites for a one-screen fix.

Prefer concrete improvements that solve the current problem cleanly.

---

# 11. Backend Rules

The server is authoritative for gameplay.

The client may request an action, but does not decide the result.

Important gameplay rules must be validated on the backend.

For state mutations consider:

- authorization;
- idempotency;
- concurrency;
- retry;
- duplicate requests;
- reconnect;
- process restart;
- partial failure;
- cancellation;
- transaction boundaries;
- observability.

Do not swallow meaningful exceptions.

Do not hide failure by returning a fake success state.

Use structured logging around important failure paths.

Avoid unnecessary extra database round trips in hot paths.

---

# 12. Identity Rules

Never conflate:

- account;
- authentication provider identity;
- Telegram user;
- character;
- combat actor;
- companion;
- Party member.

Keep domain identity independent of a specific external auth provider where practical.

Account linking must not silently create duplicate game identities.

Session persistence and auth persistence are separate concerns from gameplay persistence.

---

# 13. Database Rules

Before modifying persistence:

1. inspect entity model;
2. inspect mapping;
3. inspect existing migrations;
4. inspect current indexes and constraints;
5. inspect read paths;
6. inspect write paths;
7. consider existing production rows;
8. consider partial deployment;
9. consider concurrent writes.

Prefer database constraints for invariants that must never be violated.

Use migrations that are safe for existing data.

Do not create a required non-null field without a migration/backfill strategy.

For inventory, rewards, purchases and other economy mutations, explicitly consider atomicity and duplication.

---

# 14. Combat Rules

Combat correctness takes priority over convenient shortcuts.

### Identity

`AccountId`, `CharacterId`, `ActorId`, companion identity and Party membership are different concepts.

Do not substitute one for another.

### Captured combat state

When combat behavior requires immutable membership or identity, prefer captured combat state rather than mutable outside state.

### Party

Party is an active cooperative gameplay concept.

Do not redesign Party for hypothetical raids.

### Multi-enemy

Never assume exactly one enemy exists.

Consider:

- current target;
- target replacement;
- AoE;
- enemy death during a tick;
- threat per enemy;
- effects per target;
- reward eligibility;
- finalization with multiple enemies.

### Companions

Do not accidentally make companions behave like ordinary player actors.

Check:

- ownership;
- targeting;
- rewards;
- threat;
- reconnect;
- death;
- persistence.

### Abilities and talents

A content/UI definition does not prove that the runtime effect exists.

When adding or repairing a talent:

- verify runtime wiring;
- verify effect application;
- verify ranks;
- verify conditions;
- verify cooldown/resource behavior;
- verify UI state;
- add a behavioral test.

### Rewards

Reward logic must avoid:

- double XP;
- double loot;
- duplicate kill credit;
- reconnect duplication;
- finalizer duplication;
- race-based duplication.

---

# 15. Content Rules

Gameplay content should be data-driven where values are expected to be tuned.

Do not hardcode balance values in C# when the content system already owns those values.

Use stable machine IDs.

Separate:

- machine ID;
- Russian display name;
- description;
- runtime behavior.

Before adding new JSON structure:

1. inspect canonical schema;
2. inspect loader;
3. inspect validators;
4. inspect existing examples;
5. inspect runtime consumption.

Do not invent a parallel content format.

### Acquisition integrity

Every required:

- material;
- currency;
- item;
- key;
- boss drop;
- crafting component

must have an actual acquisition path.

Never make gameplay require an item that does not exist in obtainable content.

### Balance

Use Elyndor's current formulas as the primary basis.

WoW Classic may be used as a conceptual reference for:

- progression pacing;
- loot structure;
- profession logic;
- item identity;
- encounter reward philosophy.

Do not copy WoW numbers blindly.

### Inventory stacks

Do not manufacture value through annoying stack limits.

Common materials such as ore may use generous stack sizes such as `99` where appropriate.

Rarity should primarily come from acquisition difficulty and drop rate, not storage friction.

---

# 16. UI / UX Rules

Elyndor is a game, not an admin dashboard.

The main player UI should feel like a mobile MMORPG.

### Primary constraints

Design for narrow mobile screens first.

Always consider:

- safe areas;
- thumb reach;
- touch target size;
- scroll behavior;
- keyboard appearance;
- text readability;
- combat visibility;
- state hierarchy;
- one-handed use where reasonable.

Desktop is an enhancement, not the baseline layout.

### Avoid

Avoid:

- giant dashboard cards;
- excessive nested panels;
- wasted empty space;
- tiny precision controls;
- duplicate information;
- excessive permanent explanatory text;
- desktop navigation copied directly to mobile;
- horizontal overflow;
- UI that requires hover;
- decorative blocks with no gameplay function.

### Prefer

Prefer:

- clear game-state hierarchy;
- compact HUD information;
- progressive disclosure;
- bottom sheets / contextual overlays where appropriate;
- consistent game tokens;
- meaningful icons;
- readable numbers;
- immediate feedback after actions;
- clear disabled/loading/error/reconnect states.

### WoW Classic reference

WoW Classic is a reference for:

- information hierarchy;
- RPG readability;
- item identity;
- equipment semantics;
- talent readability;
- loot expectations;
- recognizable MMO interaction patterns.

Do not reproduce desktop WoW layouts literally.

Translate the useful principles to a modern mobile interface.

### Visual consistency

Before inventing a new component:

- inspect existing shared components;
- inspect tokens;
- inspect similar screens;
- reuse established interaction patterns when they work.

Do not create a unique button/card language for every screen.

---

# 17. Frontend State Rules

Do not duplicate authoritative state unnecessarily between:

- component local state;
- Pinia;
- URL/router;
- backend snapshot;
- realtime SignalR events.

Define the source of truth.

For realtime screens, explicitly handle:

- initial load;
- stale cache;
- reconnect;
- missed events;
- duplicate events;
- authoritative refetch;
- loading;
- error;
- empty state.

Do not assume SignalR events are the only source needed to reconstruct state.

---

# 18. Performance Rules

Optimize from evidence, not instinct.

Before significant optimization:

- identify the actual slow path;
- measure where possible;
- inspect rendering/network/assets;
- avoid speculative caching.

For mobile UI pay attention to:

- large images;
- unnecessary rerenders;
- long lists;
- animation cost;
- excessive watchers;
- unnecessary API calls;
- oversized bundles;
- layout shifts.

Correctness and maintainability still matter.

---

# 19. Security Rules

Never expose secrets in:

- source;
- logs;
- screenshots;
- test fixtures;
- client bundles;
- error responses.

Authorization must be enforced on the server.

Admin features require explicit authorization.

Do not rely on hidden UI controls as security.

Treat account linking, session tokens and identity-provider callbacks as security-sensitive.

Do not log raw credentials or full auth tokens.

---

# 20. Testing

Use the repository's actual test commands.

Compilation alone is not proof of correctness.

Choose the smallest necessary test layers:

### Backend unit tests

For:

- formulas;
- domain rules;
- state transitions;
- deterministic behavior.

### Integration/database tests

For:

- EF mappings;
- migrations;
- constraints;
- transactions;
- concurrency;
- idempotency;
- persistence.

### API tests

For:

- validation;
- auth;
- permissions;
- stable contracts;
- error behavior.

### Vue tests

For:

- stores;
- components;
- state transitions;
- rendering rules.

### Playwright

For critical browser flows and visual/mobile interaction behavior.

### Regression rule

Every meaningful fixed defect should receive the smallest practical automated test that would have caught it.

---

# 21. Mobile Browser Verification

When UI or browser behavior changes, verify at least one narrow mobile viewport appropriate to the project.

Check:

- no horizontal overflow;
- no clipped interactive controls;
- safe-area behavior;
- scrolling;
- modal/sheet behavior;
- touch controls;
- console errors;
- failed requests;
- loading state;
- error state;
- reconnect when relevant.

For significant UI work, inspect both normal and narrow mobile layouts.

---

# 22. Inventory and Economy Safety

Inventory, loot, merchant and reward flows must share coherent capacity and mutation rules.

When touching inventory capacity, inspect all relevant acquisition/mutation paths, including where applicable:

- normal combat loot;
- boss rewards;
- dungeon rewards;
- merchant purchases;
- contracts;
- crafting;
- admin grants;
- transfers;
- stacking.

Do not allow the displayed capacity and enforced capacity to silently diverge.

Economy mutations must be transaction-safe and duplication-resistant.

---

# 23. Definition of Done

A task is not complete merely because code was written.

For meaningful implementation, completion means:

- requested behavior is implemented;
- relevant tests pass;
- newly found failures in the same scope are fixed;
- no obvious regression remains in adjacent critical behavior;
- browser verification is performed when relevant;
- database behavior is verified when relevant;
- security implications are reviewed when relevant;
- the full diff is reviewed;
- no unrelated files were accidentally changed;
- no secret was introduced;
- no speculative raid scope was added;
- remaining verification limitations are stated explicitly.

Do not claim a check passed unless it actually ran.

---

# 24. Communication

Be concise and concrete.

For long implementation tasks, work autonomously through the requested block instead of reporting after every tiny change.

During execution, surface meaningful blockers or important discoveries.

Final reports should normally include:

- what changed;
- important design decisions;
- tests/checks actually run;
- any real remaining blocker.

Do not pad the response with generic advice.

Do not claim future work was completed.

---

# 25. Final Principle

Prefer a small, complete, tested vertical improvement over a broad unfinished redesign.

Elyndor should become more playable, more coherent and easier to maintain after every substantial change.
