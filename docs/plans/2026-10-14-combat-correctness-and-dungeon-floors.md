# Combat correctness and authored dungeon floors

## Scope

Fix the five reproduced gameplay defects: recovery using changed content, shared
Death's Embrace state, shared Marked Shot counters, another Archer resetting Spirit
Bond, and companion targeting depending on participant ordering. Add selectable dungeon
floors with sequential encounters inside each floor. Raid development remains frozen.

## Implementation boundaries

1. Keep combat counters and timers in participant runtime state; resolve pet actions
   under their owner's context. Test both participant orderings.
2. Before terminal rewards, archive immutable battle content in the existing
   `ContentRevisions` table and journal its id. Hash-check and validate it at recovery.
   Pass the snapshot through rewards and dungeon outcome processing. Missing or ambiguous
   versioned legacy content must preserve the journal and stop unsafe recovery.
3. Author floor ranges and optional encounter victory prerequisites in content. Validate
   full coverage, references and cycles. Preserve persisted encounter indexes.
4. Select floors under the existing run advisory lock, leader-only for groups and never
   during combat. Recompute the cursor from completed encounters. Bind must reject a
   preparation made before a floor change. Each encounter remains reward-idempotent;
   completion requires all encounters, regardless of the order of floors.
5. Expose server floor state to Vue. Persist selection before offering the next fight.
   Verify mobile layout, locked/completed states and switching back to unfinished floors.

## Persistence and failure cases

No migration or new dependency is needed. Content archival commits before the terminal
journal; an interruption between them can leave an unused revision but cannot apply a
reward without its recovery identity. Existing reward idempotency and encounter
transactions remain in place. Replaying a terminal journal completes a dungeon outcome
even when its definition is absent from the newly loaded content. An unfinished live
battle still follows the existing restart/wipe policy.

All bundled floors are initially open in content 0.58.0. Authors may add
`requiredEncounterId` to gate a floor. Completed encounters cannot be replayed in the
same run. Legacy content without floors retains sequential advancement.

## Verification

Regression coverage includes per-participant talent state, owner-specific companion
targeting/healing, restart after monster/dungeon removal, duplicate recovery, independent
floor progress, locked and active transitions, stale preparation, validation cycles and
mobile floor selection through the API. Run the backend solution build/tests, content
validator, frontend lint/unit/typecheck/build and the mobile dungeon Playwright scenario.
