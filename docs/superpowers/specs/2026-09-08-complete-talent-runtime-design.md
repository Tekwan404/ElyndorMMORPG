# Complete Talent Runtime Design

**Date:** 2026-09-08  
**Status:** Approved for implementation by the project owner  
**Scope:** All current Warrior, Mage, and Archer talent trees

## Goal

Make every existing talent node executable in the authoritative combat runtime. A talent is complete only when its Russian player-facing description, structured content values, resolver output, runtime behavior, all meaningful ranks, UI state, and behavioral tests agree.

The current content contains 288 nodes across three 96-node trees. The current package/fragments contain 226 nodes with deferred modifiers and 209 nodes whose modifiers are entirely deferred. The implementation must remove that gap rather than relabeling it.

## Design decisions

### Runtime ownership

`Elyndor.Core` owns combat events, runtime state, target selection, threat, effects, resource/cooldown changes, and talent behavior. `Elyndor.Infrastructure` owns persistence, content loading, and application orchestration. `Elyndor.Server` remains transport-only. Vue renders authoritative snapshots and sends intent commands.

Every mutation enters the existing `CombatSessionRegistry` single-writer pipeline. Timers use absolute timestamps and `TimeProvider`; important random outcomes use the existing injectable game RNG boundary.

### Generic event pipeline

Talent hooks consume typed combat events instead of talent-ID conditionals. The initial event set is:

- ability started and completed;
- hit, critical hit, damage dealt, damage taken, dodge, heal, and kill;
- HP/resource threshold crossed;
- effect applied, refreshed, expired, or removed;
- combat start, victory, defeat, leave, and reset.

Each event contains source actor, target actor, ability/effect identity when applicable, timestamp, outcome values, and a proc-chain marker. Runtime state tracks stacks, temporary flags, internal cooldowns, shields, forced targets, threat, and proc history per combat session. Proc recursion and duplicate event application are prevented by explicit event metadata and per-session state.

### Actors and targets

Combat uses an actor collection rather than a compatibility single `_enemy` field. An actor has identity, side, vitals, resources, effects, threat context, and optional companion/friendly metadata. Threat is stored per hostile actor and target selection is deterministic. `Taunt`/`Provoke` creates a time-bounded forced-target rule; after expiry normal threat selection resumes.

The current single-player session supports player, companion, and multiple enemies. The actor contracts are intentionally compatible with future party actors but do not introduce a Party subsystem in this slice.

### Content contract

Talent modifiers remain data-driven. Balance values are never duplicated in runtime catalogs. The validator will require:

- valid values for every rank used by a modifier;
- valid ability/effect/profile references;
- valid trigger/event keys and runtime ownership;
- non-empty Russian name and description;
- no deferred or partial runtime status for gameplay talent definitions;
- branch node counts and prerequisite graph consistency;
- no all-zero modifier values unless the mechanic explicitly defines a zero rank.

Legacy `Deferred` compatibility helpers are removed or made migration-only after all current content is executable. Runtime must not silently skip a talent hook.

### Class rollout

The implementation order is:

1. Audit tooling, content validation, event contracts, runtime state, and behavioral-test helpers.
2. Warrior: Guardian, Berserker, Warlord.
3. Mage: Pyromancer, Arcane, Cryomancer.
4. Archer: Marksman, Beast Mastery, Arcane Archer.
5. Russian UI/localization, talent-gated ability regression, multi-enemy/companion regression, and CI/E2E.

Each class slice may add only the generic primitives needed by its source-of-truth mechanics. It must not add fake values, no-op supported talents, or unrelated future systems.

## Failure and recovery rules

- Duplicate client commands remain idempotent through existing command IDs and session single-writer semantics.
- Proc effects must not recursively trigger themselves unless the content explicitly enables it.
- Combat-only stacks, shields, forced targets, and cooldown reductions reset on terminal session transitions.
- Reconnect returns the authoritative session snapshot and content identity.
- Process restart behavior remains explicit: until full combat rehydration exists, interrupted combat is recovered atomically without duplicate rewards or consumable loss.
- Reward and inventory changes remain transactional and server-authoritative.

## Verification contract

The final change must provide:

- an audit report/table for all 288 nodes;
- a behavioral test for every talent, with all meaningful ranks covered;
- deterministic RNG tests for proc success/failure and exact thresholds;
- multi-enemy and companion tests;
- talent learn/reset/loadout/ability-gating regression tests;
- Russian content/UI validation;
- backend build, unit tests, PostgreSQL integration tests, content validation, frontend checks, admin checks, production publish validation, and Browser → Server → PostgreSQL E2E.

No final completion claim is allowed while any talent remains deferred, partial, untested, or only status-labeled as supported.
