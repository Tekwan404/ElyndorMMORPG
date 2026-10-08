# Mage Core, Mana economy and channelled abilities

Implemented in gameplay 0.45.0 / balance 0.35.0. Current operational details:
`docs/development/mage-core-mana-channels.md`. Required CI remains the merge gate.

## Goal and boundaries

Implement the requested five steps in order. Preserve stable ability/talent IDs,
saved talent ranks, authoritative combat, the existing router, and actor ownership.
No database schema changes or raid expansion are required. The text-only pass
must preserve every modifier, rank, prerequisite and point threshold.

## Implementation

- [x] Add a deterministic Mage mana benchmark using production stats, equipment,
  talent resolution and CombatSession. Measure first rotation failure due to
  insufficient Mana, spell counts, spending/refunds and remaining Mana. Separate
  sustained rotation from Evocation/potions/burst and report level brackets.
- [x] Define level-based economy targets in content; tune costs using a shared
  authored cost curve before talent discounts. Verify bare/normal/good gear and
  Fire/Arcane/Frost builds, retaining an economic benefit from Intellect gear.
- [x] Grant school fillers and Counterspell through Mage Core. Remove redundant
  unlock modifiers from their existing talents, preserving upgrades and IDs.
- [x] Add Channelled as an ability primitive with a server-owned tick cursor,
  captured targets, per-tick actions and explicit completion/interruption.
  Integrate both schedulers, control/death/flee cleanup, snapshots and UI.
  Convert Arcane Missiles, Blizzard and Evocation; cancelled channels must not
  deliver future ticks or a second completion payoff.
- [x] Extract Fire/Arcane/Frost mechanics into actor-owned runtimes. Keep session
  lifecycle, target selection and event publishing in CombatSession adapters.
  Use the existing ability composer/router and shared damage/effect/resource APIs.
- [x] Rewrite all 96 Russian talent descriptions from actual mechanics. Validate
  the text-only pass against a before/after structural modifier snapshot.
- [x] Update Source of Truth and runtime documentation, run local unit/frontend,
  build/content and PostgreSQL regression checks; request independent review.
  Prepare a PR with full CI including mobile/browser tests required before merge.

## Failure cases

Duplicate commands/timestamps, coarse versus fine time advance, interrupt exactly
at a tick boundary, silence/stun/death during channels, dead targets in AoE,
party owner isolation, Arena parity, no refund duplication, no proc recursion,
reconnect snapshots, restart cancellation under existing durable session policy.
All combat state changes remain under existing single-writer semantics; player
resource persistence and finalization retain their current transactions.
