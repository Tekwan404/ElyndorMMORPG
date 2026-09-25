# Elyndor — Raid Group System

**System:** Raid Group  
**Status:** Design contract / not production-complete  
**Updated:** 2026-09-25

## Target model

Raid is a larger group structure than Party.

Target constraints:

- up to 20 players;
- subgroups of up to 5;
- explicit raid leadership/membership identity;
- subgroup identity must be captured authoritatively when combat rules depend on `party-only` effects;
- targeting/effects must distinguish raid-wide recipients from subgroup/party recipients;
- reconnect cannot reconstruct membership from client state.

Raid combat must reuse the normal CombatSession, Ability, Effect, Threat and Reward systems instead of creating a second combat engine.

## Current implementation status

Raid is **not** a finished production gameplay loop in current `main`.

There has been implementation work around raid lifecycle/roster/combat capture, but the unfinished raid branch/PR is not authoritative production state. In particular, subgroup identity/party-only targeting and complete client integration must be solved before raid gameplay is considered ready.

Authored content that was previously presented as raids is currently exposed through the normal **Dungeon 1–5 player pipeline** where applicable.

Therefore:

- UI/content must not advertise unfinished 20-player raid gameplay as available;
- new dungeon/boss content should use the current dungeon flow unless a separate raid milestone is explicitly resumed;
- source code or tests outside `main` do not count as shipped functionality.

## Safety requirements for future raid work

Before enabling Raid in player-facing production flow, require at minimum:

- authoritative subgroup identity;
- party-only vs raid-wide effect regressions;
- ready/start/leave/disconnect/reconnect lifecycle coverage;
- dead/fled participant targeting rules;
- reward idempotency and wipe/finalizer coverage;
- client targeting/formation behavior that scales without changing combat formulas;
- real browser/server/database E2E.
