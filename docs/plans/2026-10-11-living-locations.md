# Living locations

## Audit and scope

- Baseline: main at 4bcb6537, including ambient aggro PR #364.
- WorldMapView owns travel; WorldView/LocationOverview own field locations.
- CityView already has interactive services. Preserve it.
- WorldEncounterRegistry issues account-bound, single-use five-minute tokens.
- CombatApplicationService consumes tokens; CombatSessionFactory rechecks location,
  captures content and configures OpenWorldAggroConfigurator. Keep these paths.
- Locations, encounter rosters, monsters, quests and loot are composed content.
- Durable progression/rewards remain in PostgreSQL; no schema change is needed.

## Implementation

1. Test scheduled encounter availability at exact UTC boundaries and validation.
2. Add optional content schedules/points and a read-only current-location scene.
3. Test authenticated manual selection, foreign/absent targets, travel, token replay,
   stale location and unchanged progress. Issue tokens through the existing registry.
4. Build a compact Vue scene with paged markers and one interaction panel. Load
   dynamic state separately from the cached static catalog. Keep AFK, aftermath,
   city services, contracts and dungeon entry.
5. Add authored inspection points and three shared rare-elite schedules. NPC quest
   markers open real existing quest actions. Do not ship resource/chest reward buttons.
6. Verify server/unit/integration, content, Vue tests/lint/build and phone layouts.
7. Review diff, document limitations and open a PR without merging.

## Failure and state boundaries

- Never accept client damage, loot, rank, coordinates or world state as rules.
- Selection must match both the player's persisted location and its current roster.
- Rare availability is derived from explicit UTC content schedules, identical for
  all accounts and stable across restart. Tokens reserve entry for their usual TTL.
- Invalid selection must not discard a corpse or replace a valid pending token.
- No rewards or character progression are mutated by reading/choosing a scene object.
- Travel, active combat and AFK retain existing authoritative admission gates.
- Late scene responses must not replace newer location/content data. Refresh on
  expiry, focus/reconnect and retry; never choose rare enemies locally.

## Deferred

Consumable global spawns, chest/gathering rewards, dynamic invasions/corruption and
local-event lifecycles require durable state, transactional claiming and balancing.
This slice defines typed state/point boundaries without an event engine.
