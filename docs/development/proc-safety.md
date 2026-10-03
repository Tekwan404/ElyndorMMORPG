# Proc Safety (P0)

Baseline: `origin/main` at `1f80ee35`. Worktree branch: `fix/proc-safety`.

## Admission contract

`ProcGuard` is a single-writer, session-owned Core component. Direct root events
may produce a proc at depth one. Periodic events, reflected/proc-origin events, negative
depth and depth one or greater cannot produce another combat proc. A legacy
`CanTriggerFromProc` flag does not override the depth ceiling.

Each hook/actor observes an event once, before random rolls or EveryNth counters.
Sequenced replays are rejected by a per-hook high-water mark. An internal weak
dispatch token survives record copies and runtime adapters, so adapters cannot
turn one event into a second invocation by assigning a different sequence.
Tokens are not public transport fields and do not require persistent state.

Cooldowns start only after successful admission/roll. They use the existing
explicit content ICD, with exact-boundary readiness. No new cooldown/2 formula
or balance coefficient is inferred. Deep Freeze retains its per-target ICD.

## Integration

- Generic `TalentRuntimeState` uses the guard before chance evaluation and shares
  the session guard when hosted by `CombatSession`.
- Set evaluation shares that guard in production; standalone evaluator callers
  retain their own runtime-owned guard. Existing EveryNth and snapshot fields stay.
- Class event hooks, Guardian block hooks, Paladin hooks
  and Archer auto-attack callbacks pass through guarded execution scopes. Generated
  events carry `IsProc`, `ProcDepth` and origin metadata before nested dispatch.
- Resolved-ability callbacks use admission/deduplication without treating primary
  class ability actions (e.g. traps) as procs. Secondary Warrior/Archer/companion
  attacks and talent-origin resource refunds are explicitly marked. Weapon-hand
  identity is preserved.
- Arena production continues to use `CreatePlayerMechanics`. Kernel-only Arena
  adapters use the same guard implementation, preserve dispatch tokens through
  event conversion, enforce declared ICD and mark generated effect events.

Existing effects, numbers, talent definitions, targeting and damage formulas are
not rewritten. Ordinary effects remain executed by `EffectEngine`.

## Lifecycle boundary

`OnKill` is a terminal kill-credit notification, not another periodic/hit proc.
Existing resource refunds and cooldown resets on kills remain valid when a DoT
or secondary hit is lethal. Enemy death deduplication stays in the existing
session lifecycle. Suppressing this notification caused two characterization
regressions and was deliberately rejected.

The guard is not a replacement for command idempotency or single-writer session
locking. Reset/restart uses existing combat lifecycle policy; no new database,
timer or persistence mechanism is introduced.

## Verification

Regression tests cover periodic/depth rejection, replay, record-copy/adapter
deduplication, actor-scoped ICD boundaries, unchanged EveryNth semantics, Arena
block ICD and secondary off-hand swing provenance. Run:

```powershell
dotnet test Elyndor.slnx --configuration Release
dotnet build Elyndor.slnx --configuration Release --no-restore
```

Frontend and content JSON are unchanged. This is proc admission safety, not a
unified effect engine or a decomposition of the class-specific combat runtime.
