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
PvE contracts and Faction War. The second section reads the independent top 50
(global or faction-filtered), own victories/assists/deaths, rating and two wallets.
Arena standings and Arena Honor remain untouched.

Balance is centralized in `WorldPvpBalance`:

- Eight-week seasons start at 2026-10-17 00:00 UTC. Ratings start at zero. Wallets
  persist across seasons; no automatic end-of-season prizes are implemented.
- PvP kills count in the seasonal victory statistic as soon as a player is defeated
  by a contributing hostile player (highest damage, excluding departed actors),
  even when the encounter awards no rating or currency. Settlement receipts keep
  these counts idempotent across retries.
- Rewarded progression needs at least 10 seconds of PvP interaction, enemy damage
  worth at least 20% of victim maximum HP, and actual resistance (PvP damage or
  useful healing from the victim). Fast kills and passive victims still award no
  rating, honor, tokens or rewarded assists.
- At least 10% of qualifying damage/support is required. The largest damage
  contributor receives a victory; other qualifying contributors receive assists.
  Companion damage belongs to its owner. Effective healing only consumes recent
  PvP wounds, excludes self-healing, expires after 30 seconds and is consumed by
  one defeat. Departed contributors receive no later credit.
- Each qualifying credit grants 5 Field Honor and 5–15 rating points depending
  on the opponent's rating relative to the recipient. A victory against equal
  or higher rating also grants one Bloody Token.
- Each pair can award progression once per rolling 24 hours in either direction.
  At most 20 credited opponents per character per UTC day. Deaths remain visible
  even when a reward is refused. These limits reduce collusion; they do not claim
  to identify all coordinated alternate accounts.
- Either currency buys existing class-appropriate Arena equipment, using its
  content HonorPrice, or ceil(HonorPrice / 5) tokens. No extra item power tier,
  gold conversion, inter-faction trade or new gear definitions are introduced.

Defeat receipts, reward receipts, standings and wallets live in PostgreSQL.
Settlement locks involved character rows in stable ID order and atomically saves
all receipts and balances for a defeat. The `(session, victim)` primary key makes
replay harmless. Completed battle journals carry immutable contribution evidence,
so restart retries cannot invent or duplicate awards. Interrupted active fights
retain the existing cancellation policy; progress not yet journaled is not saved.
Shop mutations use character locks and canonical mutation IDs; the UI reuses the
same ID after a lost response, with inventory capacity and prices checked server-side.

Bounties, streak titles, cosmetics and season-end prizes remain follow-up scope.
