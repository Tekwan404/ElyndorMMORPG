# World PvP rewards and guild integration

Finish PRs 386 and 383 as one coherent release. Keep the actual hosted battle,
leaderboard, faction filters and replay-safe shop from 386; integrate the guild
navigation and preview presentation from 383 without fake production values.

Add optional `worldPvpRewards` to published game content with `goldPerKill` and
`goldPerAssist`, in integer bronze units. Existing content defaults to zero.
The admin form edits gold denominations (1 gold = 10,000 bronze), validates exact
precision/range, and uses the existing content draft/revision/publish workflow.

The qualifying main damage contributor receives kill gold, other qualifying
contributors receive assist gold. Ordinary gold is generated as a reward, using
the same pair cooldown and daily cap as other World PvP awards. The existing
receipt gains a backward-compatible GoldEarned column; character balance and
receipt commit together under stable-order character locks. Recovery uses the
captured content revision, so publishing new prices cannot reprice a completed
battle. No client-supplied amounts or additional reward-claim endpoint.

Verify concurrent settlement, replay, pair restrictions, disabled rewards,
admin precision/range, publication validation, UI money display, shop retries,
full build/tests and phone browser flows. Admission tests use full derived HP
and controlled time so immediate level-60 attacks do not kill the fixture while
testing membership and durable journal invariants.
