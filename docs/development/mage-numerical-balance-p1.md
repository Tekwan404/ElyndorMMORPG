# Mage P1 numerical balance — 8 October 2026

Gameplay 0.46.0 / balance 0.36.0. Conservative authored-coefficient tuning after PR #340. **This does not establish measured equality of Mage rotations**: .NET could not be executed in this authoring environment. Run the CI and scenario-based simulations before merging.

| Branch | Spell | Previous | Tuned | Design intent |
| --- | --- | --- | --- | --- |
| Fire | Flamestrike | 1.05 SP; 6s cooldown | 1.15 SP; 3s cooldown | Expensive repeatable AoE burst |
| Arcane | Arcane Spark | 0.75 SP | 0.82 SP | Stronger instant fallback |
| Arcane | Arcane Missiles | 0.60 SP per tick | 0.72 SP per tick | Reward completing the 4s channel |
| Arcane | Arcane Explosion | 0.90 SP; 6s cooldown | 1.05 SP; 4s cooldown | Costly burst against groups |
| Frost | Blizzard | 23.75 + 0.55 SP/tick | 22 + 0.48 SP/tick | Reduce long AoE dominance while retaining control |
| Frost | Ice Lance | 3x frozen / 1.75x boss Deep Chill | 2.5x frozen / 1.65x boss | Reduce combo spikes alongside Frost survivability |

Unchanged: all 1–60 Mana-cost anchors, channel tick timings, abilities/talent IDs and rank prerequisites, Core kit, equipment and other classes.

## Illustrative raw numbers (not measured DPS)

Level 60, Spell Power 400, no crits, talents, mitigation, procs or targets beyond one. Direct hit formula: flat + 60 × per-level + 400 × SP coefficient. Blizzard and Missiles are shown **per tick** (4 ticks over 4 seconds).

| Spell | Old direct hit | New direct hit |
| --- | ---: | ---: |
| Flamestrike | 684 | 724 |
| Arcane Spark | 555 | 583 |
| Arcane Missiles tick | 386.25 | 434.25 |
| Arcane Explosion | 638 | 698 |
| Blizzard tick | 351.75 | 322 |
| Ice Lance against frozen normal target | 2,061 | 1,717.5 |
| Ice Lance against boss Deep Chill | 1,202.25 | 1,133.55 |

Rough baseline AoE throughput **per affected target**, with cast repetition idealized and excluding talents: Flamestrike with baseline burn (724 + 3 × 0.04 × 400) / 3s = 257.3/s; Blizzard 322 per tick = 322/s; Arcane Explosion 698 / 4s = 174.5/s. These are *not* comparable full DPS rotations: Arcane can weave spells between explosions while a channeled Blizzard prevents other casts, Fire has Ignite and Blast Wave, and enemy count and incoming control change results.

## P1 acceptance plan

- Run Release unit/integration suites, frontend lint/tests and the content validator. The existing Mage PvE/PvP channel parity and Ice Lance regression tests must pass.
- Run `dotnet run --project tools/Elyndor.ContentValidator -- --audit-mage-mana --export-analysis=.elyndor/mage-mana` and compare Fire/Arcane/Frost Mana budgets to the baseline table in `mage-core-mana-channels.md`.
- Simulate legal specialized and hybrid builds with Weak/Normal/Good equipment at levels 18/35/45/60: sustained boss ST 30/60/120s; 12–15s burst; 3- and 5-enemy AoE at 15/45s; elite survival, movement/interrupts and 1v1 Arena. Rerun with L60 PvE/PvP sets and Uniques.
- Use real measured damage/TTK/resource and deaths to iterate. Desired identities: Fire sustained ST + burst AoE, Arcane timed burst at high Mana pressure, Frost sustained group control and survival with a modest ST tradeoff. Flag >10% same-gear long-ST deviations for review; never declare a branch balanced solely from this raw-hit table.

`MageNumericalBalanceTests` locks authored coefficients and indicative raw ratios. It is not a runtime DPS benchmark. Existing Frost combat-session testing covers the frozen Ice Lance multiplier.
