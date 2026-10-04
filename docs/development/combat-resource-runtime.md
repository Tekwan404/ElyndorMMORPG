# Combat resource orchestration extraction

Baseline: `main` at `e77958a8db93f08d16f912e2fd06607855de2df8`.
Branch: `refactor/combat-resource-runtime`.

## Ownership before and after

| Path previously coordinated by CombatSession | Production owner after extraction | Rule adapter retained |
| --- | --- | --- |
| AddResource: Warlord scaling, actor mutation, clamp result, talent provenance, publication | CombatResourceRuntime.Grant; CombatActorState remains the state owner | ScaleWarlordResource and active talent lookup |
| Incoming direct damage → Rage | CombatResourceRuntime.GenerateDirectDamageTaken at the same router slot | Original base gain 5 × GuardianRageMultiplier |
| Full block and Guardian Shield Fury | Existing Guardian hook → common Grant | Full-block eligibility, original base gain and talent value |
| Autoattack hit → resource | Existing hit eligibility → common Grant | AutoAttackProfile.ResourceOnHit; no scheduler changes |
| Generic talent/proc resource action | Existing TalentRuntimeEngine/ProcGuard → common Grant | Existing action value and admission |
| Berserker Rage, Mage/Archer refunds, Paladin resource gains | Existing class hooks → common Grant | Trigger eligibility, percent/flat formulas, delayed refund queue |
| Warlord party/companion resource recovery | Existing party action → common Grant for each existing recipient | Existing PartyActors, Rage vs non-Rage percentage and Warlord scaling |
| Elapsed resource regen | CombatResourceRuntime.Regenerate integrates sorted effect/time boundaries using decimal ticks | Existing Mage meditation/Afterglow and Archer rate adapters |
| Per-player regen clock and participant activation | CombatSession.Resources adapter | Existing captured active roster and LastResourceRegenAtUtc |
| Ability spend and resource action results | AbilityEngine calls common TrySpend/Change primitive | AbilityEngine retains validation, cost resolution, execution and result routing |
| Standalone Arena fallback regen/talent resource grants | Common Change primitive | Existing fallback eligibility and elapsed-seconds calculation |

Runtime grants read active-participant adapters on demand, not once during lazy
initialization. A party command or nested event must not reuse the first player's
talents for all other actors.

## Mutation → notification → reaction

Resource state is mutated **before** constructing ResourceChanged. Amount is the
actual clamped delta, not requested gain. TrySpend only produces a result after
successful admission; insufficient/negative costs leave state and events unchanged.
Zero-cost abilities retain their zero event. Engine resource actions also retain
zero events; publication-only Grant suppresses them, exactly as before.

AbilityEngine/ DamagePipeline result batches enter the existing normalization and
router path. The router observes ResourceChanged and may invoke class threshold
reactions; it never reapplies its Amount. Existing AddResource grants publish only
and do not introduce another immediate reaction dispatch. Hosted Arena's outer
event sink still forwards defender observations in its original order.

IsProc/ProcDepth/ProcOriginId are preserved by Grant and the existing CaptureProcOrigin
boundary. Sequence remains publication-owned. There is no second ProcGuard, dedup
store, event queue, balance profile, actor state or clock in the new runtime.

## Characterization

18 new scenarios passed on untouched production before extraction: all four classes'
spend/regen/duplicate-command behavior in PvE and hosted Arena, decimal regen across
Afterglow and Meditation boundaries, direct-vs-periodic incoming Rage, delayed Mana
Shield refund without replaying its cost, and clamped ability/generic resource gains.
Existing block/set/Guardian, crit/class/generic/proc, Paladin, resource and companion
tests were included in the baseline combat/PvP/talent run (797 cases).

Additional cases cover generic completion-before-proc order, Pyromancer executed-cost
crit refund, Archer physical miss refund, per-participant regen clocks/unattached
players, and Warlord resource percentages/scaling for player and companion.
Runtime unit cases cover atomic spend failure, zero-event policy, lower/upper clamp,
post-mutation publication, provenance, dead actor regen and tick-precision integration.

## Existing timing difference deliberately retained

PvE drains delayed Mage refunds inside SyncMageConditionalEffects when enemy AI or
cast completion reaches that class sync. Hosted Arena additionally advertises pending
refunds through NextMechanicsDueAt and syncs after AdvanceMechanics. In an artificial
passive fight with no AI/cast activity, elapsed time alone can therefore drain the
Arena refund earlier. The characterization explicitly invokes the same class sync
to verify identical refund amount/admission exactly once, rather than claiming that
these schedulers were already identical. Changing wake-up timing is a separate
gameplay-sensitive scheduler pass, outside this extraction.

## Not extracted

DamagePipeline's Mana Shield cost, encounter-only resource changes, and standalone
Arena's historically silent autoattack resource gain remain at their original
boundaries. Set passive actions currently apply stats/shields, not resource mutation;
their effects and ProcGuard admission are unchanged. AbilityModifierComposer,
CombatEventRouter, damage/healing formulas, scheduler, death/outcome, threat,
targeting, content and frontend are unchanged.

Class-specific rules remain thin calls into existing mechanics partials, including
the Mage delayed-refund list and last-spend timestamp. No class rule has been
reimplemented as a PvP-specific version.

## Next safe extraction

The follow-up ownership correction is documented in
[Participant-owned temporal resource state](participant-resource-state.md).

Characterize and extract class-local timed resource bookkeeping (especially Mage
pending refunds and last-spend state) into a per-participant resource rule adapter.
First establish ownership across multiple Mages and the intentional scheduler
boundary; do not opportunistically change due-times in a structural refactor.

## Verification

- Baseline combat/PvP/talent filter: 797 passed before extraction.
- New resource coverage: 36 passed (25 resource characterization, 10 isolated
  runtime cases, one Warlord/companion case).
- Full combat/PvP/talent filter after extraction: 833 passed, zero failures/skips.
- Release solution build: zero errors and warnings.
- Full Release solution tests: 1087 unit + 485 integration passed, zero
  failures/skips; Testcontainers used PostgreSQL 18.4.
- First full attempt ran while Docker was stopped (370 integration fixture failures).
  After starting the installed daemon and waiting for that testhost to exit, the
  clean full retry passed. No test assertion was weakened for infrastructure failure.
- Repository layout: 9 guard tests passed; layout/link check and diff whitespace check clean.
- Independent read-only review found no required fixes.
- No frontend changes; local frontend tests/build not rerun. Existing PR CI still
  runs repository-wide backend/frontend/browser checks.
