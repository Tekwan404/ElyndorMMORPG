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

There are no World PvP currency, rating or kill rewards in this slice. PvP damage
does not count toward monster rewards. A winning survivor needs actual monster
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

## Follow-up design

The Adventurer Guild proposal (independent leaderboard, support credit, bounties,
titles, Field Honor, Blood Tokens and seasons) requires a separate durable event
and anti-farming policy before rewards are enabled. Arena results and rating must
not be reused as World PvP results. PvE contracts keep their existing behavior.
