# Living locations

## Implemented slice

World remains the map/travel tab. Location uses existing background and monster art with up to eight markers per page. Enemy panels show level, rank, rare status, rewards and per-monster possible loot. NPC markers use existing quest offers, journal statuses, acceptance and reward APIs. Authored landmarks provide inspection text without inventing rewards. Contracts, AFK, skinning, cities and dungeon entry retain their existing flows.

`GET /api/v1/world/scene` is authenticated and uncached. The server reads persisted location and returns content version, server UTC time, availability boundary and scene objects. Presentation coordinates are mapped separately from content and gameplay rules. Vue contains no enemy roster or spawn RNG.

`POST /api/v1/world/select-encounter` accepts `{ locationId, monsterId }` as intent. Under the existing character operation guard it validates current location, travel, combat activity, roster membership, safe/dungeon restrictions and availability. A valid selection creates the existing account-scoped, single-use encounter token; SignalR combat startup revalidates location and roster. Invalid selections preserve pending encounters and skinnable corpses. Reading the scene grants nothing and does not reset progress. No migration, Redis or new combat engine is introduced.

## Shared rare availability

Content may define `availability: { periodSeconds, durationSeconds, offsetSeconds }` on an encounter. UTC windows derive from Unix epoch; all clients read the same server result. Start is inclusive, end exclusive. Content validation rejects invalid periods/durations/offsets.

Three existing elite opponents are rare during a ten-minute window each hour: forest leader at minute 0, meadow queen at minute 20, old-road captain at minute 40. Other elites retain continuous availability. Existing random exploration filters the same schedule, so access to these three elites decreases outside their windows. Selection remains repeatable during a window; these are shared appearances, not globally consumable spawn instances. Existing five-minute encounter reservations can enter combat after the window ends. AFK balance is unchanged.

Scene refresh occurs at a server-provided boundary, at least once per minute, and on focus/reconnection. Failed reads expose retry; stale responses cannot replace another location. Clients never create or remove appearances independently. Pending tokens retain the existing process-local registry limitation and are lost on server restart.

## Compatibility and deferred work

Combat creation, ambient aggression, rewards, contracts and dungeon combat are reused. No files implementing extra-mob aggression are changed. Persistence and reward formulas are unchanged.

The state response defines Calm/Invasion/Corruption, but current runtime only emits Calm. Durable local events, state transitions, resource gathering, chests and their transactional/idempotent rewards, elite subterritories and globally consumable spawn instances are deferred. No action buttons for those unfinished systems are displayed. Content-authoring forms preserve new JSON fields but do not yet provide dedicated schedule/landmark editors.

## Verification

Unit tests cover schedule boundaries and invalid content. PostgreSQL integration tests exercise selected combat startup, token replay, stale location, invalid targets, shared rare availability, travel/combat rejection and unchanged progress on scene/selection. Frontend tests cover server selection, failures, stale responses, pagination, party restrictions and quest interaction. Browser layout coverage uses deterministic API fixtures at widths 320/360/390/430; actual combat startup is covered by server integration tests rather than the fixture browser test.
