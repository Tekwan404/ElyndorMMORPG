# Combat event routing extraction

Baseline: `origin/main` at `779dc5b53043b63bbe20e0fbe335e83da3f8d3f4`.
Branch: `refactor/combat-event-routing`.

## Responsibility boundary

`CombatEventRouter` owns normalization of engine results, the CombatEvent-to-
CombatRuntimeEvent metadata bridge, and ordered reaction selection. The session
registers 40 thin delegates to existing mechanics in `CombatSession.EventRouting.cs`.
The router is independent of CombatSession, actors' mutable state, engines, clocks,
AI, encounters, and rewards. Identity is read on demand because nested/party events
can change the active participant.

Damage/healing have already mutated actors before routing. The router must never
replay Amount as damage, healing, or a resource delta. A registered reaction can
generate a new effect/resource/secondary attack using the existing implementation;
its new engine results re-enter ApplyKernelEvents synchronously. There is no new
queue, scheduler, event engine, or ProcGuard.

## Characterized production order

PvE keeps these orchestration boundaries:

1. Eagerly normalize the entire engine batch, including captured proc origin.
2. Deduplicate ActorDied and activate the event participant.
3. RegisterThreat: existing encounters/interrupt utility, then Paladin reaction.
4. Append the primary result with its authoritative sequence.
5. Existing interrupt callbacks, reflection, active-state check, encounter callback,
   and death contribution tracking.
6. Router: guarded set passives, existing legacy event-threat callback, guarded
   talent graph, guarded Guardian block reaction.
7. Session death/outcome handling; after EnemyKilled publication, router dispatches
   generic OnKill, Berserker, Pyromancer, Archer, and Warlord terminal reactions.

The talent graph preserves its event-specific interleaving. For example, an outgoing
crit routes generic talent first, then Berserker, Guardian, Pyromancer, Mage, Archer.
An incoming direct hit generates Rage before generic/Guardian damage-taken reactions,
then Warlord party reactions, Berserker/Mage/Archer damage-taken reactions, and the
existing auto-attack/companion hooks. This is not regrouped by class.

Hosted Arena retains its different publication boundary: Append forwards the event
through the outer Arena log to the defender's existing observation path before the
source host resumes reactions. Its router then runs Paladin, sets, event threat,
talents, and Guardian block. Incoming observation never appends the original event.
PvE seal secondary damage therefore precedes the primary publication; hosted Arena
publishes the primary before the seal secondary. Both orders are tested, not unified.

## Metadata and safety

Normalization fills only missing definition/source/target/weapon fields. It uses
record copies preserving the existing weak dispatch token and all other metadata.
The runtime bridge preserves IsPeriodic, IsProc, ProcDepth, ProcOriginId, and the
token; reflected results continue to be treated as proc-origin in that bridge.
It receives the current session Sequence explicitly, preserving the previous bridge
semantics (not substituting the enclosing Arena event's sequence).

Admission remains in the existing session-owned ProcGuard and RunProcHooks. Nested
callbacks still capture depth/origin with the same scope and restore it in finally.
Periodic, reflected, and secondary/proc hits cannot start a new chain. Dedup,
EveryNth counters, RNG admission, and existing ICD ownership/start rules are unchanged.
Terminal OnKill deliberately bypasses hit-proc admission, including lethal DoT.

## Arena paths retained

Production fighter assembly supplies PlayerDefinition for both fighters and uses
CreatePlayerMechanics. Those hosts and PvE now use the same CombatEventRouter.
ArenaTalentEventDispatcher and the standalone Arena talent runtimes remain: synthetic
fighters without a mechanics host still exercise them, including kernel-only tests.
Removing them would change supported execution modes rather than be cleanup.

## Verification

14 new characterization cases passed against the untouched production implementation
before extraction. The final suite adds metadata preservation and ICD boundary cases
(16 cases total), covering crit/direct damage, block/set/Guardian ordering, generic
and class talents, ability completion, DoT/HoT, lethal DoT/OnKill/death dedup, shields,
vampirism, secondary/reflected/proc suppression, record-copy double-dispatch, and
PvE versus hosted-Arena Paladin publication order.

No frontend, content, balance, formula, persistence, reward, scheduler, targeting,
threat implementation, or class-mechanic changes are part of this extraction.

Local verification on the extracted production path:

- Combat/PvP/talent unit filter: 741 passed, zero failures/skips.
- `dotnet build Elyndor.slnx --configuration Release --no-restore`: passed,
  zero warnings/errors, including Server and AppHost.
- `dotnet test Elyndor.slnx --configuration Release`: 1006 unit + 485 integration
  passed, zero failures/skips, with PostgreSQL 18.4 in Testcontainers.
- The first full attempt failed because Docker was stopped. A retry overlapped
  that exiting testhost and hit Windows file locks. After starting the installed
  daemon and waiting for both attempts to exit, the clean full run passed.
- `git diff --check`: clean. No frontend verification is needed for this Core-only
  change; the PR's existing CI still exercises the repository-wide checks.

## Still owned by CombatSession

Authoritative state mutation orchestration, publication/sequencing/statistics,
single-writer lifecycle, active participant selection, encounter callbacks,
reflection execution, threat implementation, death dedup/outcome/kill credit,
ability execution and implementations of started/resolved callbacks, cooldown/resource timing, and
all existing class mechanics remain in the session and its partial files.

## Ability notification routing follow-up

Ability execution remains in the session/AbilityEngine. CombatEventRouter now also
routes started/resolved notifications through CombatAbilityReactionHandlers, bound
to the unchanged class methods. PvE and hosted Arena share this registration; the
Arena fallback without a mechanics host is unchanged.

The authoritative ordering is intentionally preserved:

- Successful Execute: ApplyKernelEvents, then started (Pyromancer, Mage, Archer).
- Non-casted Execute: resolved (Warrior, Pyromancer, Mage, Archer) after started.
- Successful player CompleteCast: ApplyKernelEvents, then the same resolved order.
- Failed Execute and interrupted casts do not dispatch resolved callbacks.
- PvE AbilityUsed publication and Arena interrupt handling keep their old positions.

Started reactions consume primary buffs and must not be wrapped in a proc-origin
scope. Resolved reactions retain their existing per-mechanic RunResolvedProcHooks
scopes. Neither execution events nor actor mutations are replayed by the router.

16 additional real-session characterization cases passed before production edits,
then again after extraction in both PvE and hosted Arena. They cover instant/casted
fire-buff consumption and Clearcasting Afterglow, generic completion before class
resolved reactions, timestamps, duplicate commands, next-attack arming and cooldown,
Archer Exposed Defense consumption/refresh, insufficient resources, and interrupted
casts. The focused combat/PvP/talent suite now passes 757 cases.

The follow-up full Release solution test run passed 1022 unit and 485 integration
tests, with zero failures/skips (Testcontainers PostgreSQL 18.4).
The full Release solution build also passed with zero warnings/errors, and
git diff --check was clean. No frontend or database schema changes were made.

Only callback implementations and execution timing remain in CombatSession; the
class notification graph is no longer duplicated in UseAbility, CompleteReadyCast,
and PlayerMechanics. A next extraction should first characterize ability modifier
composition before separating its routing; do not combine it with formulas or time
advancement.
