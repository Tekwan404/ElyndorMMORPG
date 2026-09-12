# Unified AFK Farming Design

## Status

Approved design for the AFK Farming Phase 6 rework.

## Goal

AFK Farming is one predictable, server-authoritative offline progression flow:

`send character to a location -> simulate farming -> collect durable rewards`

It is not an alternative combat mode or a survival system. A character never dies during AFK farming.

## Session model

`AfkFarmSession` remains the canonical persistent session and keeps its existing timing, content-version, balance-version, snapshot, status, and idempotency boundaries.

It contains:

- `CharacterId`
- `LocationId`
- `TargetMonsterId` (nullable)
- `StartedAtUtc`
- `EndsAtUtc`
- `LastProcessedAtUtc`
- `Status`

`TargetMonsterId = null` means the normal weighted encounter table of the selected location. A non-null target means the simulator farms only that ordinary monster. The server validates that the target is present in the selected location's encounter table.

`AfkFarmMode` and the persisted `Mode` column are removed. `Dead` is not an AFK terminal state. Old active Safe sessions migrate to ordinary location farming with a null target.

## Location and eligibility

`allowAfk` is the explicit content permission for a location. `DangerLevel` remains world metadata and does not select or block an AFK mode.

A start or preview is valid when the character owns and currently occupies an unlocked location with `allowAfk`, has an eligible normal encounter, is not in active combat, travel, dungeon, or another active AFK session, and has a valid target when requested.

Current HP does not block an AFK start. Active-combat death, dungeon death, and boss rules remain outside AFK farming.

## Simulation and efficiency

The existing deterministic simulator remains the source of combat timing, encounter selection, incoming-damage estimates, XP candidates, gold candidates, and loot candidates. It does not mutate vitals or create an online combat session.

Incoming damage is an internal combat-efficiency input only. It never causes death, a survival score, a chance of death, an HP threshold, an AFK penalty, or an early AFK stop.

An encounter the character cannot finish consumes simulated time. The simulator continues across the interval, so a weak character earns fewer successful kills rather than losing the remaining AFK duration.

`EfficiencyPercent` is not a universal character-power score. It is the percentage of the AFK interval's base farming opportunity for the chosen location and target that became successful completed kills. It is derived from the same deterministic interval simulation that produces XP, gold, and loot. No standalone HP formula or hidden death/survival rule participates in it.

Rewards remain server-calculated from completed kills, then flow through the existing AFK reward multipliers, loot roller, inventory insertion, pending-loot handling, and `AfkFarmIntervalGrant` transaction.

## Time

Session processing continues to use server UTC and durable interval timestamps. The simulator already receives each interval's start and end timestamps.

No second day/night clock or speculative schedule format is introduced here. When the content pipeline supplies encounter schedules, the simulator can use its existing interval timestamp to select applicable encounter data without changing the AFK session model.

## API and UI

Start and preview accept only:

- `locationId`
- `durationMinutes`
- `targetMonsterId?`

State and preview return the target when present and `efficiencyPercent`. They do not expose a mode or describe incoming damage as a player death risk.

The World AFK modal presents a location, an optional target with `Any enemies` as the default, duration choices, efficiency, and server previewed rewards. It contains no Safe/Dangerous/Targeted switch.

The completion presentation reports location, elapsed duration, encounters, victories, XP, gold, loot, and efficiency. It has no death or survival messaging.

## Persistence and compatibility

The migration drops the mode column and adds the nullable target column. Existing session rows preserve all timing and reward history; legacy mode values are not needed after migration. `AfkFarmIntervalGrant`, its unique session/interval constraint, and current reward provenance remain unchanged.

All gameplay mutations remain atomic and replay-safe: a retry cannot create a second interval grant, duplicate loot, XP, or gold.

## Verification

Focused coverage proves:

- null target uses weighted location encounters;
- a selected target restricts encounters to that ordinary location mob;
- a target outside the location is rejected;
- `DANGEROUS` locations with `allowAfk` are eligible;
- low HP does not create a death/HP stop condition;
- weaker characters produce lower efficiency and fewer rewards without ending the interval;
- interval reward retry remains idempotent;
- the frontend emits no mode and presents the optional target flow.
