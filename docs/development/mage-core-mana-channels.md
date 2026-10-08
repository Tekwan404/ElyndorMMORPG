# Mage Core, channels and Mana economy

Gameplay release 0.45.0 / balance 0.35.0 completes the Mage Core/channel/runtime
slice. The earlier 0.42.1 release rewrote all 96 RU talent descriptions without
changing mechanics; this release updates the six descriptions affected by Core
and channels. Ability and talent IDs remain stable; no database migration is needed.

## Core and composition

All Mages know Fireball, Arcane Spark, Ice Shard and Counterspell at level 1.
First-row talents retain their improvements. A-3-4 replaces Counterspell's unlock
with a one-second cooldown reduction. Missiles, Blizzard and other specialist
abilities still require their talents. Arcane Spark is a repeatable filler.

Authored `resourceCostByLevel` anchors live with each Mage ability in
`content/abilities/mage-pyromancer.json`. Linear interpolation runs before talent
discounts and temporary effects. Bootstrap previews, PvE and production Arena use
the same cost resolver. Fixed-cost abilities in other classes keep their behavior.
Offline farm also applies the cost curve and advances supported single-target
channels per tick, cancelling the captured cast when an encounter ends.

## Sustained Mana benchmark

Run from the repository root:

```powershell
dotnet run --project tools/Elyndor.ContentValidator -- --audit-mage-mana --export-analysis=.elyndor/mage-mana
```

The report includes Mana pool, first unaffordable scheduled rotation spell,
elapsed seconds, cast count, spending/refunds, remaining Mana and selected ranks.
`SecondsToOom = null` means no rotation failure within the observation window,
not zero seconds. Mana can remain when the next required spell is unaffordable.

The deterministic reference uses production stat/resource calculation, legal
sustained talent builds, actual item templates and procedural quality profiles,
and CombatSession against a durable passive target. It excludes autoattacks,
potions, Evocation and burst cooldowns. Fire maintains Scorch and Fire Blast and
uses Hot Streak Pyroblast; Arcane uses Missiles once learned, otherwise Spark;
Frost uses Ice Lance and Ice Shard. This is a sustained single-target reference,
not an optimal burst or AoE simulator.

Mana-oriented equipment is selected after rolling actual values and compares
two-handed weapons against a one-handed weapon plus offhand. The same item keeps
the same roll across levels. Normal uses ELITE quality with a two-level equipment
lag; Good uses BOSS quality at the current level. Unique, item procs, PvP Honor
gear and PvE T1 are excluded from this baseline. Existing set rules still apply.
The reference intentionally rewards choosing Intellect; it does not model an
average random drop loadout. Bare characters are also reported as a stress case.

| Level | Target sustained rotation, seconds |
| --- | ---: |
| 1 | 45 |
| 9–10 | 60 |
| 18 | 90 |
| 29 | 105 |
| 35 | 120 |
| 45 | 135 |
| 55 | 150 |
| 59 | 165 |
| 60 | 180 |

These are reference targets, not guaranteed timers. Crits, Clearcasting, talent
allocation and loot rolls change the result. Cost anchors do not decrease at
higher levels; discrete spell timings and talent/gear breakpoints can move the
first failure below the nominal target. Good equipment should extend sustain;
Unique/T1 resource effects and recovery tools are measured separately.

Release reference results (Normal equipment, seed 1337), seconds to first failure:

| Level | Fire | Arcane | Frost |
| --- | ---: | ---: | ---: |
| 1 | 45.7 | 46.5 | 45.0 |
| 18 | 88.6 | 93.0 | 91.5 |
| 35 | 119.4 | 120.0 | 120.0 |
| 45 | 135.0 | 132.0 | 136.5 |
| 55 | 149.5 | 148.0 | 150.0 |
| 59 | 163.5 | 164.0 | 166.5 |
| 60 | 169.6 | 168.0 | 166.5 |

Good L60 equipment extends these runs to approximately 303–332 seconds; bare
characters last approximately 24–27 seconds. This intentionally leaves a large
benefit for equipped Intellect. The full CLI report includes all ten sampled
levels and all three equipment states; the regression suite checks the level
budgets with a 20% tolerance rather than pinning individual random proc times.

## Channel and runtime contract

Missiles and Blizzard deliver four damage ticks, with their former total direct
damage spread across those ticks. Evocation restores 10% maximum Mana on each
of four ticks. Mana and cooldown commit at start. Interrupts, silence, stun,
fear, death and target loss cancel future ticks. Completed ticks remain applied.
Evocation has no separate completion refund. Duplicate timestamps cannot replay
ticks; snapshots carry channel state and the completed/total tick cursor.
Both battle screens show a draining channel bar.

Arena commits simultaneous due actions as before: a due channel tick can resolve
with control at the same timestamp, but remaining ticks are cancelled. PvE stops
the channel when control is applied, including short effects between ticks.
Target debuffs are recomputed at each tick in both modes. Clearcasting rolls once
on completed offensive channels; Echo uses their accumulated direct damage.
Per-tick Frost interactions still use the individual hit results.

`FireMageRuntime`, `ArcaneMageRuntime` and `FrostMageRuntime` own school state,
including crit streaks, Arcane Power counters, delayed refunds and Cold Blood.
`MageCombatRuntime` coordinates the three; a fixed owner context routes effects,
resources and proc execution through the session's existing shared services.
Party callbacks restore the active owner in `finally`, so switching the viewed
player cannot move another Mage's pending refunds or counters.

This adds no persistence mechanism for in-flight casts. Reconnect uses current
authoritative snapshots; process restart retains the existing combat/session
recovery policy and does not replay channel ticks from a new durable ledger.
