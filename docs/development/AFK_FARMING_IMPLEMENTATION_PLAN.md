# AFK / Offline Farming — technical audit and implementation plan

## Scope and invariants

AFK farming is a durable, server-authoritative subsystem. Closing the Telegram WebApp must not stop a session, backend restarts must not lose progress, retries must not duplicate XP/Gold/loot, and no AFK implementation may create a real `CombatSession` per monster or run a per-player background timer.

The implementation reuses the current modular-monolith boundaries and keeps permanent state in PostgreSQL. `TimeProvider` is the authoritative clock. Any randomness affecting rewards or simulation must be deterministic from durable session/interval identity.

## Existing architecture audited

### Character state

- `Character` is the permanent owner of Level, Experience and Gold.
- `CharacterVitals` stores current HP/resource and durable checkpoint timestamps.
- `CharacterLocation` stores current location plus a monotonic version.
- `CharacterTravelState` is durable travel state.
- `CharacterDerivedStateService` already resolves class profile, effective resources, inventory/equipment, item-instance stats, talent ranks/modifiers, calculated `CharacterStats`, known abilities and companion profile. It has an overload accepting an explicit `GameContentSnapshot`, which is the correct source for the AFK start snapshot.

### Combat

- `CombatSessionFactory` is a stateful online-combat factory. It resolves bootstrap state, equipment, dual-wield permission, talents, combat actor state, cooldowns, abilities, party participants and monster runtime. It must not be called by AFK interval simulation.
- `ActiveCombatSession` is durable and is created/removed by `CombatDurabilityService`; therefore AFK start can query PostgreSQL for an ordinary-combat conflict rather than relying only on the in-memory registry.
- `CharacterOperationGuard` is useful as an additional process-local serializer, but its striped `SemaphoreSlim` gates are not a cross-process/restart correctness boundary.
- Core `Combat/Damage` owns the combat damage/healing pipeline. AFK combat math should reuse/extract calculation primitives from this layer instead of cloning formulas into Infrastructure.

### Rewards / progression / inventory

- `CombatRewardService` already serializes permanent rewards with `SELECT ... FOR UPDATE`, an EF execution strategy, a DB transaction, a durable `CombatRewardGrant`, existing loot tables, procedural item generation and inventory persistence.
- `CombatRewardService.ApplyVictoryAsync` is *not* a safe AFK entry point because it also applies quest/legacy-contract kill progression and shared/group-loot behavior. AFK V1 explicitly forbids those side effects.
- Reusable reward primitives should therefore be extracted below the combat-session orchestration layer: monster XP/Gold calculation, loot-table rolling, item generation/rolled affixes and inventory insertion. AFK adds its own durable interval grant boundary.
- `CharacterMutation` already models request-level idempotency with character + mutation id + operation type + request fingerprint. Start/Stop/API mutation requests should follow the same pattern/conventions.

### World / content

- `LocationDefinition` contains level bounds, `RequiredContractId` and weighted encounter definitions.
- `MonsterDefinition` contains rank, stats, attack profile, XP, Gold and LootTableId. Boss filtering is therefore data-driven and does not require a hardcoded monster list.
- `GameContentSnapshot` is immutable for a request and exposes content/balance versions plus indexed world/monster/item definitions.
- The mutable provider keeps only the current snapshot through the public interface. An AFK session therefore persists both content/balance identity and a serialized immutable AFK snapshot of all inputs needed to replay already-started farming. Processing must not silently switch an active session to later character gear/talents/content.

### Dungeon / conflict state

- `DungeonRun`, `DungeonRunMember` and their states are durable. AFK start must reject a character that is an active member of an active run.
- Later integration must make online combat, dungeon start, travel/location changes and AFK start mutually exclusive through durable checks, not only UI disabling.

## Phase 1 design

### Domain

Add `src/Elyndor.Core/Afk/`:

- `AfkFarmSession` — durable lifecycle (`Active`, `Completed`, `Cancelled`, `Dead`, `InventoryFull`, `Invalidated`), UTC timestamps, content/balance versions, serialized character snapshot, processing cursor, stop reason and optimistic version.
- `AfkFarmMode` — starts with `Safe`; later phases extend the same enum/model.
- `AfkCharacterSnapshot` — immutable class/level/stats/resource/equipment/talent/combat inputs captured at Start. V1 stores enough data for later deterministic simulator work; it never points back to mutable live gear for historical calculations.

### Persistence

Add:

- `DbSet<AfkFarmSession>` to `GameDbContext`.
- `AfkFarmSessionConfiguration` under `Persistence/Configurations`.
- Migration creating `game.afk_farm_sessions` with FK to character, status/end-time indexes and a PostgreSQL partial unique index on CharacterId for `Status = Active`.
- Optimistic concurrency field (`Version`) and an explicit transaction/row-lock boundary for Start/Stop.

The DB uniqueness constraint is the authoritative protection against two Active sessions. The process-local operation guard is only a latency/UX optimization.

### Start service

Add `Infrastructure/Afk/AfkFarmService`.

Start transaction:

1. Acquire the existing per-account operation guard and DB execution strategy.
2. Lock/load the character row.
3. Verify character and `CharacterVitals.CurrentHp > 0`.
4. Reject durable `ActiveCombatSession`.
5. Reject `CharacterTravelState`.
6. Reject active dungeon membership.
7. Load current immutable `GameContentSnapshot`.
8. Validate requested location exists and equals the character's authoritative current location.
9. Validate level bounds and `RequiredContractId` completion.
10. Validate location allows AFK and has at least one eligible non-boss encounter.
11. Resolve `CharacterDerivedState` using the exact content snapshot and serialize an AFK snapshot.
12. Insert the session. The partial unique index is the final duplicate-session guard.
13. Commit.

Boss encounters are never eligible in V1. Contract completion/progression rewards are not part of Phase 1.

### Stop service

Stop runs in a transaction. If the current session is Active it moves to Cancelled with an authoritative UTC completion timestamp/reason. Repeating Stop for an already terminal session returns the same terminal state and performs no new mutation.

### Location content marker

Extend `LocationDefinition` with an AFK permission flag while preserving existing JSON compatibility. A location must also contain at least one non-boss encounter, so starter/training/dungeon-only spaces cannot become farmable accidentally.

## Phase 2–9 implementation direction

- Phase 2: pure deterministic `AfkFarmSimulator` in Core using common combat calculation primitives, weighted location encounters and deterministic seed `session + interval + content version`; no DB writes.
- Phase 3: `AfkFarmIntervalGrant`/operation table with unique `(SessionId, IntervalIndex)`, row-locking and one transaction covering simulation result, XP/Gold, generated loot/affixes, inventory mutation and cursor advancement. Extract safe reward primitives from `CombatRewardService`; never invoke quest/contract progression from AFK.
- Phase 4: authoritative start/state/stop/preview APIs, Bootstrap AFK state and durable conflict checks wired into normal combat/dungeon/travel/location mutations.
- Phase 5: game-like mobile location UI with backend-derived preview/risk, active state, reconnect and terminal summary; Playwright 320–430px coverage.
- Phase 6: data-driven Safe/Dangerous/Targeted modes using the same simulator.
- Phase 7: passive modifiers from `DerivedState`; abstract average active-ability/resource/sustain model; idempotent consumable reservations.
- Phase 8: exploit/economy/load audit including parallel Start/Stop/Process, restart/crash/retry and 100/1,000/10,000 durable sessions without per-player timers.
- Phase 9: full build/test/content/frontend/Playwright verification and Russian-localization audit before merge readiness.

## Transaction / replay boundaries

- Start/Stop: request-level idempotency + character row lock + AFK partial unique index.
- Process interval: unique durable interval key and transaction; retry returns the committed interval result.
- Item RNG: deterministic item-generation key derived from the durable interval/reward identity, never `Guid.NewGuid()`/`Random.Shared` on retry.
- Content: active session calculations use the persisted immutable AFK snapshot/content identity; live equipment/talents cannot retroactively affect it.
- Completion: `LastProcessedAt` never advances outside the same transaction that commits the corresponding rewards.

## Phase gates

No phase is considered complete until its migrations/model compile, relevant tests pass, and regressions introduced by the phase are fixed. A later phase must not be started while the current phase is red.