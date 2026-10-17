# Dead Reaches open-world PvPvE

Implement the agreed shared fight model only in DEAD_REACHES at level 60.
Level 1–40 homelands remain separated; OUTER_REACHES 41–60 is shared PvE.
Auction, direct trading, parties and the arena retain their existing boundaries.

Use the existing CombatSession, hosted registry, command/tick serialization,
class handlers, contribution ledger, durable activity journal and reward pipeline.
A second independent PvP match beside an existing PvE fight would violate the
requested behavior and is not an acceptable implementation.

1. Add an explicit open-world session mode. Capture race and actual party identity
   in player definitions. Determine hostility per actor: monsters oppose both
   factions; players oppose the other faction; healing cannot cross factions.
   Party effects remain scoped to actual captured party membership.
2. Admit new players under the session gate and existing account admission locks.
   Revalidate location, level, life, travel/AFK/arena state and target availability.
   Joining preserves session ID, HP, cooldowns, effects, threat and event sequence.
   Bound participant count and make retries harmless. Do not merge two live fights.
3. Discover online actors in this location with a short server-observed presence
   lifetime. Attack an idle opponent to start PvP, or join an existing fight to
   attack its opponent, help a same-faction ally, or attack its monster.
4. Publish the existing combat snapshots to all authenticated participant accounts.
   Reuse the normal combat UI with perspective-correct hostile/friendly rosters.
   Expose explicit location discovery/entry actions and readable server errors.
5. Keep death free of equipment/currency loss, using the existing home-city respawn.
   No honor, arena rating or currency is awarded for killing players. Monster
   rewards require PvE contribution and use the existing idempotent settlement.
   Player actors must never be interpreted as monster loot templates.
6. Reconnect attaches to the same active session. Process interruption follows the
   existing durable combat recovery: interrupted active fights are cancelled;
   committed terminal rewards and aftermath are recovered without duplicate grants.

Failure cases: simultaneous attack/start/join; forged foreign-location or same-race
target; joining an ended/full fight; duplicate commands; death/exit while others
continue; PvP-only contribution; late target death; disconnect; restart; full bag.
Existing solo, party, dungeon and arena behavior must remain unchanged.

Verification: deterministic kernel tests, PostgreSQL/API admission and settlement
tests, frontend targeting/discovery tests, mobile browser smoke, full build and CI.
