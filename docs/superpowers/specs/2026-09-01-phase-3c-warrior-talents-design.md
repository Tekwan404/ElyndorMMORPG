# Phase 3C Warrior Talents Design

## Goal

Complete the Phase 3C Talent Engine and Warrior talent slice without starting CombatSession, Monster, Party, Boss, XP, loot, or equipment systems.

## Current baseline

- The content package contains one Warrior tree with 3 branches and 96 nodes.
- Talent ranks, prerequisites, point spending, two persisted loadouts, and server-backed UI already exist.
- Only three primary-stat percentage modifiers currently execute at runtime; most nodes are descriptive content only.
- Party/Boss/Elite-dependent mechanics are explicitly owned by later phases and remain deferred contracts.

## Bounded completion contract

Every Warrior talent node must have one of these explicit outcomes:

1. `Supported`: its typed modifier is executed by an existing Phase 3 stat, resource, damage, healing, effect, cooldown, or ability boundary.
2. `Deferred`: its schema and references validate, but its runtime owner is a later phase such as CombatSession, Party, Monster, Boss, Elite, equipment, or leveling.

Descriptions alone are not a valid implementation. Runtime code consumes modifier families and tags, never scattered talent IDs.

## Domain and content

- Preserve the existing `content/package.json` layout and extend it compatibly.
- Add canonical icon identifiers to talent definitions.
- Active talent nodes unlock canonical `AbilityDefinition` entries. The talent tree and ability button resolve the same icon identifier.
- Implement only modifier families required by the Warrior Source of Truth and owned by Phase 3.
- Validate duplicate IDs, invalid rank values, missing talent/ability/effect references, circular prerequisites, unsupported modifier keys, and incorrect runtime ownership.

## Runtime integration

- The server derives active modifiers from the persisted active loadout.
- Permanent character truth stays in PostgreSQL; final derived stats remain calculated values.
- Ability unlocks, costs, cooldowns, durations, damage/healing coefficients, resource generation, and effect strength use typed overlays composed before execution.
- Proc/event definitions whose event source does not exist until CombatSession remain deferred instead of receiving fake self-only behavior.
- Talent mutations remain authoritative, transactional, concurrency-protected, and safe to retry.

## UI and art

- The ten new files in `talant/` belong only to the Berserker branch.
- Optimize them from 1254x1254 multi-megabyte PNG sources into browser-sized WebP assets while preserving transparency.
- Assign them to Berserker passive and active nodes by visual meaning. Reuse is allowed across closely related ranks/mechanics because ten images cover more than ten Berserker nodes.
- Guardian and Warlord continue using the current generated icons until their art is supplied.
- Load only the currently visible branch artwork. Talent and unlocked ability use the same canonical art when the talent grants an ability.
- Preserve Arcane Minimal, mobile-first layout, node states, prerequisites, ranks, loadout controls, loading/error states, and touch usability.

## Verification policy

Keep automated tests focused:

- one content-validation matrix for the complete tree;
- one table-driven unit test per distinct supported modifier family;
- focused mutation coverage for retry/concurrency and two loadouts;
- frontend typecheck/build plus a compact Talent UI test;
- manual Telegram Mini App playtest remains the user's primary product check.

Compilation alone is not completion. Checks that are not actually run must be reported as unverified.

## Explicitly deferred

- Party targeting and party-wide application;
- CombatSession event dispatch and auto-attack hooks;
- Monster, threat, kill, Boss, and Elite runtime behavior;
- equipment-conditional runtime where equipment does not yet exist;
- XP/level mutations, loot, and economy.
