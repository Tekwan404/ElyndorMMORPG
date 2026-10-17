# Dead Reaches open-world PvPvE

`OpenWorldPvpRules` enables combat between HUMAN and UNDEAD only in
`DEAD_REACHES` at level 60. Shared progression locations remain PvE. Auctions,
ordinary parties, direct trade and arena ranking retain their existing rules.

## Entry and combat

- `/api/v1/world/pvp/players` observes authenticated presence for 45 seconds and
  returns up to 50 players currently in the location. Active open-world fights
  remain discoverable while their participants are fighting.
- `/api/v1/world/pvp/enter` accepts a target character and an attack/entry intent.
  The server checks faction, level, location, travel, AFK, dungeon and arena state.
  Admission locks serialize both accounts against travel and other combat starts.
- Attacking an idle opponent creates one `CombatSession` with two player actors
  and no placeholder monster. Entering an existing fight adds a player under its
  existing writer gate. Two already active fights cannot be merged.
- A session holds at most 20 captured player identities. Each participant retains
  class state, gear, cooldowns, captured party ID and their own companion. Dynamic
  joins preserve existing actors, effects, HP, threat and event sequence.
- Monsters oppose both factions; players oppose the other faction. Friendly
  healing stays within a faction; party buffs require actual captured membership.
  Rostered players outside the location are not hostile targets. Player controls
  reuse the existing crowd-control diminishing-returns policy.

## Exit and rewards

Death causes no equipment/currency loss. A dead participant can leave and respawn
in their own city while other participants continue fighting. Fleeing ends only
that participant's involvement; abandoned actors cannot be attacked or rejoin.

World PvP has independent seasonal standings and wallets. PvP damage does not
count toward monster rewards. A winning survivor needs actual monster
damage, effective support or tanking contribution to receive PvE rewards. Eligible
surviving contributors share one valuable-loot eligibility group for the session;
different captured parties cannot duplicate the same shared drop. Open-world
participants do not receive a party XP bonus merely for joining a fight.

The combat ends when no participating players remain, or when all monsters are
dead and only one faction remains active. The existing UI projects enemies and
allies separately for each participant and receives updates through CombatHub.

## Durability

Before terminal settlement, the registry journals every pending participant's own
snapshot in one PostgreSQL transaction, pinned to the captured content revision.
An opponent's HP, cooldowns or defeat state are never reconstructed from the
winner's faction-only view. Recovery replays individual snapshots; unfinished
active fights retain the existing interruption/cancellation policy. Completed
journals cannot replay PvP vitals or grant monster rewards a second time.

## World PvP progression and economy

The guild has one registrar (Celia or Morvena) and two menu sections: existing
PvE contracts and Faction War. Arena standings and Arena Honor stay independent.
World PvP starts at zero seasonal earned honor, not a competitive rating.

## Seasonal honor and ranks

- Eight-week seasons start at 2026-10-17 00:00 UTC. Seasonal honor cannot be spent
  or lost on death. Field Honor is the separate shop wallet; wallets persist across
  seasons. No automatic season-end prizes or new rank-gated items are implemented.
- Base honor is 20 for the largest qualifying damage contributor. Qualifying assists
  receive ceil(base / contributor count). At least 10% of damage plus effective
  support is required. Companion damage belongs to its owner. Effective healing
  consumes recent allied PvP wounds, excludes self-healing and expires after 30s.
- For each recipient -> victim -> UTC day, successive qualifying contributions
  grant 100%, 75%, 50%, 25%, then 0%, rounded down after assist scaling. Revenge has
  its own counter. Assists consume a repeat credit too, preventing support farming.
- Seasonal honor and Field Honor receive the same award. Spending Field Honor does
  not change seasonal honor. There is no total daily cap on either honor award.
- Last-hit kills, qualifying assists and deaths count independently of reward size.
  Death resets the current kill streak. The original journal order is preserved
  for same-tick defeats. Receipts also record zero-honor participation.
- Gold and tokens use a separate economic gate: only the first directed target
  credit per UTC day, at most 20 economic grants per recipient/day. A primary
  victory grants one token; assistants may receive configured assist gold. Repeat
  targets cannot mint extra gold/tokens even while earning diminished honor.
- The leaderboard orders the top 50 by seasonal earned honor, globally or by faction.
  Ranks depend on personal honor thresholds, never leaderboard position.

Initial balance in `WorldPvpBalance` (provisional):

| Rank | Name | Earned honor |
|---|---|---:|
| I | Новобранец | 0 |
| II | Боец | 200 |
| III | Ветеран | 500 |
| IV | Сержант | 1000 |
| V | Страж | 1800 |
| VI | Рыцарь | 2800 |
| VII | Командир | 4000 |
| VIII | Воевода | 5000 |
| IX | Завоеватель | 8000 |
| X | Чемпион | 12000 |
| XI | Генерал | 18000 |
| XII | Маршал | 26000 |
| XIII | Полководец | 36000 |
| XIV | Верховный полководец | 50000 |

Compared with the previous base of 5 Field Honor, the new base of 20 buys existing
gear four times faster per first qualifying victory. Existing gear prices are
unchanged. Highest rank requires 2500 full-credit solo victories; repeat reductions
and assists change this pace. Rank reward catalogs, cosmetics, bounties and
season-end prizes remain follow-up scope, not simulated live features.

## Persistence and migration

Receipts, standings and wallets live in PostgreSQL. Stable-order character locks
serialize settlement, counters and all balances; `(session, victim)` makes replay
harmless. Captured terminal journals survive restart; interrupted active fights
retain their existing cancellation policy. Client-provided rewards are never used.

The migration reconstructs `SeasonHonor` from SUM(FieldHonor) on immutable receipts,
not from the spent wallet or old competitive rating. Legacy Rating columns are
retained as archives and no longer updated. Historical zero-reward assists/streaks
cannot be recovered because they were not stored; new receipts capture participation.
Schema rollback retains nonnegative receipt checks so new zero-honor rows survive.
Rolling old/new settlement writers are unsupported: deploy as one server release.

The guild displays server rank progress, honor, a separate economic payout counter,
14 rank thresholds and the latest 20 personal participation/death records. Historical
receipts without a role display 'Участие'; the old death journal does not name the
killer. Death-history lookups have a victim/time/session index.

Shop purchases retain stable mutation IDs, inventory capacity checks, server prices
and transactional currency spending. Existing Arena gear remains the shop catalog.

## Configurable gold

Admin -> Content -> World PvP Rewards edits gold per kill/assist. Values are stored
in integer bronze (1 gold = 10000 bronze), with zero disabling money rewards.
The maximum per credit is 100000 gold. Save a revision and publish it; new battles
capture the profile and restart recovery keeps that original version. All separate
first-target/day and daily economic limits apply before any gold is granted.
