# Phase 4A First Playable Combat Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a server-authoritative Telegram-playable Warrior-versus-WOLF combat slice with SignalR realtime updates and three CombatSession talent hooks.

**Architecture:** A deterministic Core `CombatSession` owns all fight runtime state. A singleton in-memory registry serializes commands per session and schedules only the next meaningful due action through `TimeProvider`; a thin authenticated SignalR hub transports authoritative snapshots/events to a Pinia-backed mobile UI.

**Tech Stack:** .NET 10, ASP.NET Core SignalR, existing Elyndor Core combat kernel, EF Core/PostgreSQL read boundary, Vue 3, TypeScript, Pinia, `@microsoft/signalr`, Vitest.

**Spec:** `docs/superpowers/specs/2026-09-01-phase-4a-first-playable-combat-design.md`

## Global Constraints

- Server calculates every combat outcome; SignalR is transport only.
- `CombatSession` is the only writer for its runtime state and commands are serialized per session.
- Production clients cannot advance combat time.
- No combat ticks are persisted; restart cancels unfinished normal combat without rewards.
- Reuse the existing Ability, Damage, Healing, Effect, Rage, Talent, `TimeProvider`, and `IGameRandom` boundaries.
- No XP, loot, inventory, gold, quests, elites, bosses, Party, PvP, durable combat history, or multi-enemy infrastructure.
- Keep automated coverage to the five required strong backend scenarios plus one focused frontend scenario.

---

### Task 1: Versioned WOLF and BITE content

**Files:**
- Modify: `src/Elyndor.Core/Content/GameContentPackage.cs`
- Modify: `src/Elyndor.Core/Content/GameContentPackageValidator.cs`
- Modify: `content/package.json`
- Modify: `scripts/update-phase3c-talent-content.mjs`
- Modify: `docs/development/phase-3c-talent-coverage.md`
- Test: `tests/Elyndor.UnitTests/Content/GameContentPackageValidatorTests.cs`

**Interfaces:**
- Produces typed `MonsterDefinition`, `MonsterAiProfile`, and a normal `BITE` `AbilityDefinition` addressable by ID.
- Marks only `G-1-2`, `B-3-1`, and `B-1-2` event hooks supported with their actual rank values.

- [ ] Add one failing validator test for invalid monster ability/AI references and verify RED.
- [ ] Add the minimal content records and package fields.
- [ ] Add validation for duplicate IDs, positive combat values, and missing ability/AI references.
- [ ] Add `WOLF`, its simple AI profile, and `BITE`; update content/balance versions.
- [ ] Update the content generator/coverage output for the three supported hooks.
- [ ] Run the focused content validator tests and CLI validator.

### Task 2: Deterministic CombatSession aggregate

**Files:**
- Modify: `src/Elyndor.Core/Combat/CombatModels.cs`
- Modify: `src/Elyndor.Core/Combat/Damage/DamagePipeline.cs`
- Modify: `src/Elyndor.Core/Combat/Abilities/AbilityEngine.cs`
- Create: `src/Elyndor.Core/Combat/Sessions/CombatSessionModels.cs`
- Create: `src/Elyndor.Core/Combat/Sessions/CombatCommands.cs`
- Create: `src/Elyndor.Core/Combat/Sessions/CombatSession.cs`
- Test: `tests/Elyndor.UnitTests/Combat/CombatSessionTests.cs`

**Interfaces:**
- Produces `CombatSession.Handle(CombatCommand, DateTimeOffset)` and `CombatSession.AdvanceTo(DateTimeOffset)` returning a snapshot plus newly sequenced events.
- Exposes `NextDueAtUtc` for the application scheduler without exposing a production time-advance command.

- [ ] Write failing tests for deterministic full fight, one-time death/end, and command-after-end; verify RED.
- [ ] Evolve the existing event model with source, target, critical, and sequence without a parallel event hierarchy.
- [ ] Implement player/enemy runtimes, status transitions, command deduplication, ability execution, auto attacks, effect/cast processing, and terminal-state guards.
- [ ] Implement the Wolf decision rule: `BITE` when ready, otherwise auto attack.
- [ ] Verify GREEN for the three session tests.

### Task 3: Typed CombatSession talent hooks

**Files:**
- Modify: `src/Elyndor.Core/Talents/ResolvedTalentModifiers.cs`
- Modify: `src/Elyndor.Core/Talents/TalentModifierResolver.cs`
- Modify: `src/Elyndor.Core/Combat/Sessions/CombatSession.cs`
- Test: `tests/Elyndor.UnitTests/Combat/CombatSessionTests.cs`
- Test: `tests/Elyndor.UnitTests/Talents/TalentModifierResolverTests.cs`

**Interfaces:**
- Produces `ResolvedTalentEventHook(TalentId, Key, Rank, Value, TargetId, InternalCooldown)` for supported event hooks.
- `CombatSession` consumes the three resolved hooks on damage taken, critical hit, and direct enemy kill.

- [ ] Write the failing Critical Instinct 1-second ICD test and verify RED.
- [ ] Resolve only supported event hooks with typed values and metadata.
- [ ] Apply G-1-2, B-3-1, and B-1-2 from authoritative sequenced events; emit actual `ResourceChanged` events.
- [ ] Verify GREEN and confirm all other deferred hooks remain deferred.

### Task 4: In-memory registry, application service, and concurrency

**Files:**
- Create: `src/Elyndor.Infrastructure/Combat/CombatSessionFactory.cs`
- Create: `src/Elyndor.Infrastructure/Combat/CombatSessionRegistry.cs`
- Create: `src/Elyndor.Infrastructure/Combat/CombatApplicationService.cs`
- Modify: `src/Elyndor.Infrastructure/DependencyInjection.cs`
- Test: `tests/Elyndor.UnitTests/Combat/CombatSessionRegistryTests.cs`

**Interfaces:**
- Produces one active session per character, lookup by character/session ID, `ExecuteAsync` through a per-session gate, one-shot next-due scheduling, resume, leave, and cleanup.
- Factory loads current character/bootstrap/talent content but never retains `GameDbContext` in a session.

- [ ] Write a failing concurrent-command serialization test and verify RED.
- [ ] Implement the session factory from authoritative character stats, active talent ranks, and known abilities.
- [ ] Implement registry indices, per-entry `SemaphoreSlim`, command dedupe, one-shot scheduling, and lifecycle cleanup.
- [ ] Implement application operations and stable combat error codes.
- [ ] Verify GREEN for the registry concurrency test.

### Task 5: SignalR contracts and transport

**Files:**
- Create: `src/Elyndor.Contracts/Combat/CombatContracts.cs`
- Create: `src/Elyndor.Server/Combat/CombatHub.cs`
- Create: `src/Elyndor.Server/Combat/SignalRCombatUpdatePublisher.cs`
- Modify: `src/Elyndor.Server/Program.cs`

**Interfaces:**
- Client methods: `StartCombat`, `UseAbility`, `StartAutoAttack`, `StopAutoAttack`, `ResumeCombat`, `LeaveCombat`.
- Server message: `CombatUpdated` carrying authoritative `CombatUpdateResponse`.

- [ ] Add DTO mapping from the Core snapshot/event model; do not expose mutable Core objects.
- [ ] Implement authenticated thin hub methods that resolve account identity and call the application service.
- [ ] Configure JWT query-token extraction only for `/hubs/combat` and map the authorized hub.
- [ ] Publish scheduled updates to the character combat group and send terminal updates before cleanup.
- [ ] Build the backend to catch contract/DI issues.

### Task 6: Telegram combat client and minimal Arcane UI

**Files:**
- Modify: `web/elyndor-web/src/api/apiClient.ts`
- Modify: `web/elyndor-web/src/api/contracts.ts`
- Create: `web/elyndor-web/src/stores/combatSession.ts`
- Create: `web/elyndor-web/src/game/combat/views/CombatView.vue`
- Modify: `web/elyndor-web/src/app/AppShell.vue`
- Modify: `web/elyndor-web/src/assets/gameArt.ts` only if an existing icon mapping is needed
- Test: `web/elyndor-web/src/__tests__/CombatView.spec.ts`

**Interfaces:**
- Pinia store owns SignalR lifecycle, latest server snapshot, deduplicated recent events, connection state, gap recovery, and command methods.
- View renders server snapshot only and sends intent only.

- [ ] Write one failing frontend test for server-driven snapshot/event dedupe and controls; verify RED.
- [ ] Add a SignalR store using the in-memory JWT access-token factory and automatic reconnect.
- [ ] Implement sequence dedupe/gap recovery through `ResumeCombat`.
- [ ] Add the enabled Combat navigation surface and mobile view using existing UI primitives.
- [ ] Verify GREEN, then run frontend lint, typecheck, and build.

### Task 7: Documentation, runtime verification, and review

**Files:**
- Modify: `docs/source-of-truth/phases/ELYNDOR_PHASES_0-5.md`
- Modify: `docs/source-of-truth/architecture/00_DEVELOPMENT_ROADMAP.md`
- Modify: `AGENTS.md` only if its active-phase pointer is stale

- [ ] Document Phase 4A boundaries, restart policy, implemented hooks, and remaining Phase 4 work.
- [ ] Run `dotnet build`, the five focused backend tests, content validation, frontend test/lint/typecheck/build.
- [ ] Start through `Elyndor-Control.cmd`/launcher and verify health.
- [ ] Use Playwright on a Telegram-like mobile viewport for load, console errors, navigation, combat controls, and layout.
- [ ] Verify SignalR start/update/resume behavior locally where authentication permits.
- [ ] Review `git diff`, `git status`, secrets, scope, server authority, concurrency, and documentation drift.

