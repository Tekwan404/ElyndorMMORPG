# Ranked Arena 1v1 Design

## Scope

The first PvP slice is a ranked, server-authoritative 1v1 arena with matchmaking, Honor earnings, a seasonal leaderboard, and a mobile game UI. Open-world PvP, 2v2, honor-shop inventory, wagers, and gear loss are excluded. Existing PvE, dungeons, parties, and raids retain their behavior. The arena uses real character level, equipment, class, abilities, talents, and appearance. Matchmaking pairs nearby levels and never starts a match against the same character.

## Combat boundary

`CombatSession` currently requires every hostile actor to be a `Monster` with Monster AI, and its finalizer applies PvE death and reward rules. A player must never be represented as a monster. Arena combat therefore needs an explicit two-player opposition model. The existing `CombatActorState`, `AbilityEngine`, `DamagePipeline`, `EffectEngine`, class/talent modifiers, and combat event contract remain the calculation source. PvP orchestration owns actor sides, commands from both accounts, target authorization, time, disconnect handling, and terminal result. Neither participant can command the other's actor. No monster AI, threat generation, PvE loot, XP, corpse, durability, location relocation, or dungeon hooks run in an arena match.

Do not silently omit class-specific abilities or talent hooks. If a class/build cannot be faithfully executed in PvP, reject queue entry with an explicit unsupported-build error until the missing runtime path is implemented. PvE behavior must remain unchanged. The first release is feature-gated until all playable class/build combinations pass the combat contract tests.

## Match lifecycle

Authenticated characters may queue only when alive, not in combat, travel, AFK, a dungeon encounter, or another arena queue/match. Queue entry is idempotent and cancellation is safe. Matchmaking selects the oldest compatible nearby-level candidate, locks both queue rows in one PostgreSQL transaction, and writes one match with both immutable participant identities and captured build/appearance references. A match starts only after both actors are registered; failure releases both reservations safely. One active match per character is enforced by database constraints and runtime guards.

The match has a bounded duration. Death produces one winner and one loser; simultaneous death or timeout produces a draw. Disconnect permits a short reconnect grace, after which the absent player forfeits. Reconnect resumes the same match with authoritative snapshot and retained event tail. Commands use request ids and the existing per-session single-writer gate; duplicate commands cannot deal extra damage. Process restart either restores a durable match snapshot or cancels the interrupted match without granting rewards or rating changes; never awards a guessed winner.

## Honor and rating

Each completed, eligible match has one durable result. Settlement updates both ratings/win-loss counters and the winner's Honor balance in one PostgreSQL transaction. A unique match settlement key prevents replay and concurrent finalization from awarding twice. Draws and infrastructure cancellations award no Honor or rating. Honor is a character-bound spendable currency with an append-only source/sink ledger; the shop can later consume it through the same transaction-safe currency service. Repeated matchups receive anti-farming limits for Honor and matchmaking rematches. The seasonal leaderboard is read from server-owned rating and match counts; no client-supplied result, damage, rating, or Honor is accepted.

Rating and Honor amounts are server configuration, not UI constants. The starting values and exact formula need balance tests and can change without schema changes. Match records retain the formula/config version used at settlement.

## Client

The menu gets an Arena entry with Queue/Leave, queue status, current Honor, rating, wins/losses, and the top-player list. On match start, the existing BattleScreen receives a PvP variant rather than a second independent combat UI. It shows both character appearances, HP/resources, selected hostile target, skills, combat numbers, log, and surrender. The server validates every command. The UI handles searching, found match, combat, reconnecting, victory/defeat/draw, and error states at narrow Telegram viewports with safe areas.

## Tests and release gate

Tests cover concurrent matching, double queue, active PvE rejection, cross-match command rejection, ability targeting, all supported class/build hooks, death/draw/timeout/disconnect/reconnect, duplicate command/result settlement, Honor/rating replay, leaderboard ordering, no PvE rewards/death effects, migration compatibility, mobile UI and real SignalR/API flows. Run targeted combat/economy/integration tests, the full backend suite, frontend unit/lint/typecheck/build, and mobile Playwright. Keep the feature disabled if any supported build or terminal path remains incomplete.
