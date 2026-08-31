# Phase 3A — Combat Kernel

Status: approved for implementation on 2026-08-31.

## Goal

Build a reusable, deterministic, server-authoritative Effect, Damage/Healing, and Ability kernel that can execute headlessly before CombatSession and Monster System exist.

## Source of Truth

- Time: `docs/source-of-truth/gameplay/01_TIME_SYSTEM.md`
- Combat boundaries: `docs/source-of-truth/gameplay/02_COMBAT_SYSTEM.md`
- Character and stats: `docs/source-of-truth/gameplay/05_CHARACTER_SYSTEM.md`, `docs/source-of-truth/gameplay/06_ATTRIBUTES_AND_STATS_SYSTEM.md`
- Resources: `docs/source-of-truth/gameplay/07_RESOURCE_SYSTEM.md`
- Effects: `docs/source-of-truth/gameplay/08_EFFECT_SYSTEM.md`
- Damage/healing: `docs/source-of-truth/gameplay/09_DAMAGE_AND_HEALING_SYSTEM.md`
- Abilities: `docs/source-of-truth/gameplay/10_ABILITY_SYSTEM.md`
- Classes: `docs/source-of-truth/gameplay/12_CLASS_SYSTEM.md`
- UI primitives: `docs/source-of-truth/ui/UI_08_NORMAL_COMBAT.md`

System Source of Truth owns mechanics. This contract owns only execution scope and delivery order.

## Ownership and dependency direction

- `Elyndor.Core` owns definitions, runtime state, validation, calculations, deterministic processors, snapshots, and result events.
- Core remains independent of EF Core, PostgreSQL, ASP.NET Core, SignalR, Telegram, Redis, Quartz, and Vue.
- `Elyndor.Infrastructure` may load and validate optional combat content through the existing `GameContentPackage`; `content/package.json` remains the package entry point.
- Vue owns presentation primitives only and never calculates authoritative results.
- A future CombatSession is the single writer. Phase 3A processors assume serialized commands and do not add locks or background jobs.

## Runtime state boundary

Phase 3A uses an explicit in-memory `CombatRuntimeState` suitable for deterministic tests and future CombatSession ownership. It contains actors, HP/resources, active effects, cooldown/GCD/lockout timestamps, active cast, queued intent where supported, and a monotonic state version.

The kernel exposes serializable snapshot records and deterministic restoration from absolute UTC timestamps. It does not persist every tick or damage event to PostgreSQL and does not create a DbContext-backed combat loop.

## Determinism

- Authoritative time enters through `TimeProvider` or explicit `DateTimeOffset` captured from it at a command boundary.
- Important randomness enters through one injectable `IGameRandom` boundary.
- No `DateTime.UtcNow`, `Random.Shared`, `Task.Delay`, Quartz-per-effect, or timer-per-ability exists in domain calculations.
- Same state, content, time, intent, and RNG sequence produce the same results and event ordering.

## Effect scope

Implement definitions and runtime processing required by the current Effect System: Buff, Debuff, DoT, HoT, Shield, Stat Modifier, Conditional Modifier, Stun, Silence, Lethal Damage Prevention, stacking/refresh/replace/independent/strongest-wins policies, snapshot/dynamic values, dispel metadata/removal, expiration, and deterministic periodic ticks.

Party, zone, world-event, AFK, boss, and elite integrations are not implemented. Boss/elite DR and party-effect metadata may be represented only where required for schema compatibility; runtime ownership stays with later phases. Unsupported Slow, Root, Fear, and Charm are rejected by content validation.

## Damage and healing scope

Provide one authoritative damage pipeline and one aligned healing pipeline. Results are structured and expose validation outcome, hit/miss/dodge, critical result, raw amount, mitigation, modifiers, shield absorption, effective HP change, overheal where applicable, lethal prevention, resulting HP, and emitted events.

Pipelines use authoritative stats, explicit rounding rules, injectable RNG, and the ordering defined by the system documents. True Damage bypasses Armor/Magic Resistance but not shields unless an explicit supported flag says otherwise.

## Ability scope

Implement data-driven definitions and execution for Instant, Casted, Next Attack Modifier, and Taunt abilities; authoritative targets; resource validation/spend; cooldown; GCD categories; cast start/completion; interruption; school lockout; queue window; snapshot rules; damage/healing/effect execution; and structured events/error codes.

Phase 3A contains no production Warrior/Archer/Mage kit. Tests use clearly marked kernel fixtures. Phase 3B owns Warrior content.

## Content compatibility

Extend `GameContentPackage` with optional typed collections for effects and abilities while retaining `content/package.json` as the single package entry point. Existing Phase 0–2 content must continue to deserialize unchanged. Validation fails fast for duplicate IDs, invalid values, missing references, unsupported mechanics, and invalid enum values.

## UI scope

Create Arcane Minimal primitives only: ability button/icon states, cooldown overlay, resource cost, cast bar, and active-effect row/badge with stacks and duration. Demonstrate them in the existing development playground. Do not build a production combat screen or client-side combat engine.

## Failure cases

At minimum cover invalid/dead target, insufficient resource, cooldown/GCD/lockout active, stun, silence, cast already active, no cast to interrupt, stale or duplicate command identifier within the harness boundary, exact timestamp boundaries, effect mutation during processing, max stacks, last-tick expiration, shield depletion, overheal, and deterministic RNG exhaustion behavior.

## Verification

- focused red/green unit tests per rule family;
- full `dotnet build` and `dotnet test`;
- content validation tests and actual validator command;
- frontend lint, typecheck, unit tests, and build for UI primitives;
- Playwright at 320 px and a Telegram-like viewport for the playground;
- `git diff --check`, secret scan, and Elyndor review.

## Definition of Done

- Effects, Damage/Healing, RNG/Time, Abilities, cooldown/GCD, cast/interrupt, snapshots, structured results/events, and headless harness meet current system rules.
- Existing Phase 0–2 behavior remains green.
- Content remains versioned, fail-fast, and backward-compatible.
- No Monster, AI, encounter, XP, loot, inventory gameplay, boss, Party, or Talent runtime is introduced.
- No database migration is required for Phase 3A runtime state.

## Next gate

Only after this Definition of Done passes may Phase 3B define and implement the production Warrior Ability Kit.
