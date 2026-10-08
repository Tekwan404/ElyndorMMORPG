# Mage talent quality pass — 8 October 2026

Scope: all **96 Mage talents** reviewed for low-value point spends, gameplay identity, prerequisites, passive redundancy and unusual incentives. This is **not** a numerical DPS rebalance: spell damage, Ice Lance multipliers, enemy scaling, gear budgets, stat/resource formulas and core channel behavior remain unchanged.

Versions: authored content **0.45.1**, balance **0.35.1**.

## Small live tuning

| Talent | Previously | Now | Reason |
| --- | --- | --- | --- |
| F-6-4 Elemental Heat | 3 / 6 Mana on Combustion crit | **12 / 24 Mana** | A two-rank late Fire talent now meaningfully offsets its burst window's spell costs |
| A-3-4 Counterspell | 1s cooldown reduction | **3s cooldown reduction** | Core spell starts at 18s; one point now matters without changing the base ability |
| A-6-3 Deep Meditation | After 3s no Mana spend, +15 / 30 / 45% combat regen | **After 1s, +50 / 100 / 150% combat regen** | Becomes relevant in Mage's four-second channels; a modest bonus to the base regeneration, not 150% of total Mana |
| A-7-4 Magic Echo | 8 / 16% trigger | **15 / 30% trigger** | Conditional 12s Arcane Power window should visibly proc; echoed damage remains 30%, proc policy unchanged |
| I-6-4 Defensive Reaction | Recover 3 / 6 Mana on barrier break | **12 / 24 Mana** | Utility investment now returns meaningful Mana once barrier breaks; Nova cooldown reduction stays 3 / 6s |
| I-7-4 Ice Economy | 2 / 4 Mana on Frost crit, 1s ICD | **8 / 16 Mana**, same ICD | Late-tree crit-recovery talent is less invisible without changing proc frequency |

All stable talent IDs, ranks, tier placements, prerequisites, 32-per-branch node counts, signatures, and supported hooks remain unchanged. No database migration.

## Retained as good identity

- **Fire:** Ignite rolling remainder, Hot Streak free instant Pyroblast, Scorch vulnerability stacks, Combustion burst ceiling and sustained direct DPS.
- **Arcane:** Clearcasting and two charges, one-use Presence of Mind, Arcane Power's Mana tradeoff, Evocation over four ticks.
- **Frost:** Shatter + normal freeze versus boss Deep Chill distinction, controlled Ice Lance amplification, Ice Barrier, Cold Snap and distinct area-control identity.

## Watchlist only — no premature changes

- **I-5-1 Winter's Chill:** five points earn up to five group-visible crit stacks; also gates a late damage upgrade. Could be weak in solo play, but it is not clearly useless in parties.
- **F-1-3 Burning Soul:** threat reduction may have little solo value, but Mana discount is useful; revisit when party threat is widely used.
- **F-8-2 Eternal Burning:** overlaps with Living Flame; stacking is intentional but needs real-player burst/DoT uptime data.
- **Frost defensive layering:** Barrier, Ice Block, Cold Snap and Shatter can feel oppressive in Arena despite respectable boss DPS. Monitor outcomes instead of imposing an arbitrary flat nerf.
- **Arcane accuracy:** A-1-1 grants up to +10 Accuracy, possibly overshooting equal-level accuracy caps. Check level-difference data before reworking.
- **Cross-class balance:** outside this targeted Mage talent review.

## Practical validation

Keep existing Release unit/integration and content checks; the modified Arena tests cover Counterspell and idle regen, and the shared runtime already has parity tests for Echo and Frost effects. No broad synthetic DPS benchmark suite, no new combat formula, no large integration-test expansion is required for this pass.

After more players arrive, capture anonymized combat snapshots at fixed level/item-power brackets: branch, talents, abilities used, DPS/TTK (ST + AoE), Mana spent/returned and OOM time, PvP win rate versus opponent branch, and control uptime/deaths. Compare sample sizes and matchups before rebalancing damage formulas.
