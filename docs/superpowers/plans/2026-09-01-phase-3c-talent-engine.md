# Phase 3C Talent Engine Completion Plan

## Goal

Finish the approved Phase 3 contract by connecting the complete 96-node Warrior tree to authoritative content, PostgreSQL state, server-side mutations, and the existing Arcane Minimal Talent UI. Do not add CombatSession, monsters, encounters, XP, loot, Party runtime, Boss runtime, or Elite runtime.

## Authoritative ownership

- `content/package.json` owns the versioned Warrior tree structure and typed modifier declarations.
- PostgreSQL owns each character's two saved rank maps and active loadout.
- `TalentService` owns validation and the transaction boundary for learn, switch, and reset.
- Core owns pure rank/prerequisite/points/content validation and derived Talent-stage modifiers.
- The browser sends only mutation intent and renders the returned snapshot.

## Block A - Core and content

1. Add typed talent tree, branch, node, prerequisite, and modifier definitions to `GameContentPackage`.
2. Add fail-fast validation for IDs, exactly 96 Warrior nodes, branch totals, duplicate/circular/missing prerequisites, tier/rank/value errors, and supported modifier families.
3. Move the current Source-of-Truth-derived 96-node preview data into `content/package.json`; retain deferred runtime mechanics as typed metadata rather than inventing Party/Boss/Elite fallbacks.
4. Add focused tests that first fail for invalid prerequisites and for learn validation (points, tier, prerequisite, max rank).

## Block B - persistence and API

1. Add one `CharacterTalentState` per character with `ActiveLoadoutId`, `StateVersion`, and two JSONB selected-rank maps.
2. Create an EF Core migration with the character FK, unique key, JSONB columns, loadout/state constraints, and UTC timestamps.
3. Add authenticated `GET /api/v1/talents`, `POST /api/v1/talents/learn`, `POST /api/v1/talents/switch`, and `POST /api/v1/talents/reset` endpoints.
4. Run each mutation in the EF retry strategy plus one database transaction, check an expected state version, and return stable machine-readable errors. Repeated learn requests with a stale version are rejected rather than spending twice.
5. Derive earned points from `max(0, Character.Level - 1)`; do not persist an independently drift-prone available-points counter.

## Block C - stat hooks and UI

1. Apply supported `STAT_MODIFIER` values in the existing Talent stage of `CharacterStatCalculator`; keep ability/effect/resource/event/equipment modifiers typed and queryable for later owning runtime slices.
2. Replace the Vue-local tree source with API contracts and a store/client flow. Keep a bundled loading skeleton only; do not duplicate talent content in Vue.
3. Render branches, ranks, locked/unlocked states, prerequisites, point counter, exactly two loadout tabs, learn/reset actions, and the existing mobile node details sheet.

## Failure cases

- Non-Warrior characters receive a stable unavailable error until their own trees exist.
- Unknown node, invalid loadout, stale state version, max rank, missing prerequisite, tier lock, insufficient points, or invalid content cannot mutate persistence.
- Switch/reset never restores HP/resource, changes class, or resets combat cooldowns.
- Party/Boss/Elite dependent modifiers remain declared but inactive until their owning phase.

## Verification

- Focused Core/content and PostgreSQL/API tests only.
- `dotnet build`, focused `dotnet test`, frontend typecheck/build, one mobile browser smoke.
- Review the diff for server authority, idempotency, concurrency, secrets, content drift, and Phase 4 scope creep.

