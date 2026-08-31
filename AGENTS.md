# Elyndor Codex Guide

## Project

Elyndor MMORPG is a Telegram-first, mobile-first MMORPG built as a modular monolith with ASP.NET Core/.NET, EF Core, PostgreSQL, SignalR, Vue 3, TypeScript, Vite, Aspire, and OpenTelemetry.

## Source of Truth

Read the smallest relevant set before changing code. Precedence is system rules, then UI specifications, then visual references.

- Architecture and stack: `docs/source-of-truth/architecture/00_DEVELOPMENT_STACK.md`, `docs/source-of-truth/architecture/00_COMPATIBILITY_MATRIX.md`
- Roadmap and phase order: `docs/source-of-truth/architecture/00_DEVELOPMENT_ROADMAP.md`
- Product and prototype: `docs/source-of-truth/architecture/00_PRODUCT_AND_PROTOTYPE_STRATEGY.md`
- Completed foundation: `docs/source-of-truth/architecture/PHASE_00_ENGINEERING_FOUNDATION_IMPLEMENTATION.md`
- Phase roadmap: `docs/source-of-truth/phases/ELYNDOR_PHASES_0-5.md`
- Completed Phase 1: `docs/source-of-truth/phases/PHASE_01_TELEGRAM_IDENTITY_WORLD.md`
- Completed Phase 2: `docs/source-of-truth/phases/PHASE_02_CHARACTER_STATS_RESOURCES.md`
- Current phase contract: `docs/source-of-truth/phases/PHASE_03A_COMBAT_KERNEL.md`
- Current Warrior kit contract: `docs/source-of-truth/phases/PHASE_03B_WARRIOR_ABILITY_KIT.md`
- Approved phase order: Phase 3A Combat Kernel → Phase 3B Warrior Ability Kit → Phase 3C Talent Engine and Warrior Talent Content → Phase 4 CombatSession/Monsters/Whispering Forest → Phase 5 Progression/Loot/Equipment/Local Boss
- Combat: `docs/source-of-truth/gameplay/02_COMBAT_SYSTEM.md`, `docs/source-of-truth/gameplay/07_RESOURCE_SYSTEM.md`, `docs/source-of-truth/gameplay/08_EFFECT_SYSTEM.md`, `docs/source-of-truth/gameplay/09_DAMAGE_AND_HEALING_SYSTEM.md`, `docs/source-of-truth/gameplay/10_ABILITY_SYSTEM.md`, `docs/source-of-truth/gameplay/15_MONSTER_AND_AI_SYSTEM.md`
- Progression and classes: `docs/source-of-truth/gameplay/11_PROGRESSION_SYSTEM.md`, `docs/source-of-truth/gameplay/12_CLASS_SYSTEM.md`, `docs/source-of-truth/gameplay/19_CLASS_ROSTER_AND_CHARACTER_CREATION.md`
- Items and rewards: `docs/source-of-truth/gameplay/13_ITEM_EQUIPMENT_SYSTEM.md`, `docs/source-of-truth/gameplay/14_LOOT_SYSTEM.md`
- Economy: `docs/source-of-truth/gameplay/26_CURRENCY_AND_ECONOMY_SYSTEM.md`, `docs/source-of-truth/gameplay/27_TRADE_AND_AUCTION_SYSTEM.md`
- UI/UX: `docs/source-of-truth/ui/00_UI_UX_CONCEPT.md`, `docs/source-of-truth/ui/00_MASTER_UI_REFERENCE.md`, relevant `docs/source-of-truth/ui/UI_*.md`, and current assets in `reference/`
- Content: `docs/source-of-truth/architecture/00_CONTENT_AND_BALANCE_PROFILES.md`, `content/README.md`
- Infrastructure/testing: `docs/source-of-truth/architecture/00_DEVELOPMENT_STACK.md`, `.github/workflows/ci.yml`, `docs/development/getting-started.md`

Phase 0, Phase 1, and Phase 2 Definitions of Done are verified in their phase documents. Phase 3B is the active approved slice by explicit product decision; remaining Phase 3A hardening stays tracked separately. Do not implement Phase 3C, Monster System, or later gameplay until their owning slice is explicitly started.

## Core invariants

- Backend is server-authoritative and never trusts client-provided gameplay results or Telegram identity.
- PostgreSQL is permanent truth. Redis is never permanent truth and is added only for a measured need.
- Retryable mutations and rewards are idempotent and transaction-safe.
- Each active combat session has single-writer semantics.
- Use `TimeProvider` for authoritative time and an injectable deterministic RNG boundary for important randomness.
- Gameplay content is data-driven, versioned, and validated.
- Reconnect and restart behavior must be explicit; durable state survives where the architecture requires it.
- UX is Telegram-first, mobile-first, and game-like rather than a generic dashboard.
- Keep the modular monolith simple. No premature microservices, brokers, event sourcing, actors, or generic repository over EF Core.

## Development rules

For each feature:

1. Find the relevant Source of Truth.
2. Inspect the existing implementation, conventions, migrations, tests, and frontend configuration.
3. Do not invent mechanics or implement future phases without a current requirement.
4. Write a small implementation plan with failure cases and transaction boundaries.
5. Add tests first where behavior is important or regression-prone.
6. Implement the smallest complete vertical slice.
7. Run the relevant backend, frontend, database, and browser checks defined by the repository.
8. Review `git diff` for correctness, security, scope creep, secrets, and documentation drift.
9. Report unverified or blocked checks explicitly.

Use the repo-local `elyndor-*` skills for feature, architecture, combat, testing, and review workflows. Use Superpowers for planning/TDD/debugging/verification/review, Game Studio only for game UX/playtesting workflows, and Playwright for actual browser verification.
