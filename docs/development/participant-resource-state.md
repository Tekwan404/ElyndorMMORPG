# Participant-owned temporal resource state

Baseline: main `1166094196ec148262d75abb18bffbecd2effe20`, following #284.
Scope: ownership correction, not resource rules or scheduler extraction.

## Ownership audit

| State | Before | After | Consumer |
| --- | --- | --- | --- |
| LastMageManaSpendAtUtc | Session field | CombatPlayerRuntimeState | Meditation regen rate/boundary |
| PendingMageResourceRefunds | One session list | One list per participant | Mana Shield delayed refunds and hosted NextMechanicsDueAt |
| ArcanePowerManaSpendCount | Session counter | CombatPlayerRuntimeState | Every third paid cast grants Clearcasting |
| CombustionCritCount | Session counter | CombatPlayerRuntimeState | Own Combustion window ends after its crit limit; Mana recovery stays in that window |
| ArcherShotSequence | Session counter | CombatPlayerRuntimeState | Combat Rhythm grants Focus on own Nth physical shot |
| ColdBloodReadyAtUtc | Session clock | CombatPlayerRuntimeState | Post-Ice Block readiness for Frost Mana discount |
| FireDirectCritStreak / LastFireDirectCritAt | Session counter/clock | CombatPlayerRuntimeState | Hot Streak grants own Pyroblast Mana discount |
| SurvivalPreparationArmed | Session flag | CombatPlayerRuntimeState | Own next trap grants Preparation Focus recovery |
| LastResourceRegenAtUtc | Already participant-owned | Unchanged | Regen cursor |
| Paladin LastResourceSpendByAbility | Already actor-keyed Paladin state | Unchanged | Ability refunds |
| Generic talent counters and ICD | Actor-bound TalentRuntimeState / existing ProcGuard keys | Unchanged | Generic resource triggers |
| Warrior Rage generation/refunds | Actor mutation + existing hook and ProcGuard | Unchanged | No additional session-owned resource clock/queue/counter found |
| Resource maxima/current values, temporary modifiers | CombatActorState / own ActiveEffects | Unchanged | Clamp, spend, resource rate/discount |
| Combat started/current time and due-time aggregation | Session | Unchanged | Authoritative fight clock/scheduler |

Existing class adapter names now read/write the active participant's fields, using
the same pattern as LastResourceRegenAtUtc. No new service, state registry or guard.
All handler bodies, defaults, resets, RNG calls and formulas remain in place.

## Characterization and bug reproduction

The existing 25 resource scenarios passed before production changes, including
PvE/hosted Arena regen, spend ordering, refunds and publication semantics. Eight
new cases failed on unchanged main: shared Meditation last-spend, multiple refund
ownership, Arcane paid-cast counting, Archer rhythm, death/flee isolation,
reconnect snapshot isolation, and Combustion recovery-window ownership. Further
audit reproduced two additional failures for Hot Streak and Cold Blood discounts.
Independent review identified Preparation Focus theft and starvation of isolated
Mage queues when a different participant is active at the existing PvE sync site.
Both were reproduced before fixing: 13 new failing regression cases in total.

Additional characterization retains passive PvE's lack of a refund wakeup and
hosted Arena's independent refund due-times. Six parity cases verify Arcane,
Combustion and Hot Streak windows; two verify three-second Cold Blood readiness.
All checks use production actor mutation/handlers, not a second resource engine.
Synthetic Mana Shield result batches enter the existing authoritative notification
path after their resource spend, without applying ResourceChanged a second time.

## Timing and lifecycle intentionally unchanged

PvE still drains refunds only at its existing Mage conditional sync sites. Adding
refunds does not add a NextDueAtUtc wakeup. Those two existing PvE boundaries
(enemy AI action and non-player cast completion) now sync every active living
Mage, restoring the previous context afterward. Otherwise an unrelated last-active
Warrior would prevent any owner from consuming its now-isolated queue. Public-path
tests pin actor order and assert AdvanceTo drains the owner exactly at the existing
enemy action time, with another Mage or Warrior last. Hosted Arena continues to advertise
each mechanics owner's refund/Cold Blood due-time through NextMechanicsDueAt and
sync at AdvanceMechanics. There is no new periodic scheduler or catch-up policy.

Death/Flee stop normal owner execution and cannot move its queue into another
participant. Terminal roster entries cannot attach again. Active reconnect uses
existing state/snapshots: duplicate attach is rejected rather than replacing the
participant. Snapshot projection and target/active participant switching do not
erase temporal resource state. Process-restart durability is not added here.

CombatResourceRuntime, AbilityModifierComposer, CombatEventRouter and ProcGuard
are unchanged. Amount remains a notification of an already-applied mutation;
IsProc, ProcDepth, ProcOriginId, Sequence and admission rules are preserved.

## Deferred / next safe extraction

Archer marked-shot counters, owner/companion hit history, Spirit Bond healing
timer and Bestial Wrath flags are not action-resource bookkeeping. They remain
unchanged, rather than extending this fix into full class
state extraction. The next safe pass is characterization of participant-owned
non-resource class reaction history (including companion ownership boundaries),
then moving those fields without changing targeting, damage or schedules.

## Verification

- 23 added cases: 13 reproduced ownership regressions + 10 timing/parity cases.
- Combat/PvP/talent suite: 856 passed, zero failures/skips.
- Release solution build: zero warnings/errors.
- Full Release solution suite: 1112 unit + 486 integration passed, zero failures/
  skips. Integration tests used the existing PostgreSQL/Testcontainers infrastructure.
- Repository layout guard tests: 9 passed; layout/link and whitespace checks clean.
- Independent read-only review's two Important findings were reproduced and fixed;
  subsequent review found no remaining Critical/Important findings.
- No frontend, content, database/schema or API changes. Local frontend tests/build
  were not rerun; the normal PR workflow still covers frontend and browser checks.
