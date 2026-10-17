# Seasonal World PvP honor implementation

User specification: arena stays competitive; World PvP starts at zero, earns
non-spendable seasonal honor and spendable field honor, and has 14 honor ranks.

1. Add centralized honor/rank balance, directed UTC-day repeat scaling
   (100/75/50/25/0), and regression tests for death and rank boundaries.
2. Preserve legacy rating columns for rolling compatibility; add long SeasonHonor
   and backfill from immutable earned FieldHonor receipts, never current wallet.
   Persist zero-honor participation receipts so repeat accounting survives restart.
3. Under existing ordered character locks, record stats independently, award honor
   without a global cap, and gate gold/tokens separately (first directed target
   credit per UTC day, at most 20 economic grants). Receipt and balances are atomic.
4. Read service supplies authoritative ranks, next threshold, honor leaders and
   the latest 20 personal contribution records. Guild components only render data;
   store purchases cannot reduce seasonal honor. Keep arena paths unchanged.
5. Verify directed revenge, fifth repeat, UTC reset, beyond-20 honor, replay,
   concurrent settlement, migration/backfill, wallet spending and mobile UI.
   Run build, focused/full tests, typecheck/build, CI and final review before merge.

Initial rank thresholds: 0, 200, 500, 1000, 1800, 2800, 4000, 5000, 8000,
12000, 18000, 26000, 36000, 50000. Base honor 20; provisional balance.
Historical rating stays archived; historical earned honor is reconstructed from
receipts. Existing gold/token receipts count toward the separate daily cap.
