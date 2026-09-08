# Elyndor Party, Multiplayer Combat, Dungeon and Loot Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add searchable friends, persistent five-player parties, server-authoritative multiplayer combat, the Ancient Mine dungeon, contribution-aware rewards, personal loot, and Need/Greed group rolls without breaking the current single-player combat slice.

**Architecture:** Keep the modular monolith. Friends, parties, dungeon runs, combat sessions, contribution, and loot remain separate domain boundaries connected by application services. Extend the existing combat runtime to support multiple player participants and a session-level single-writer gate; do not model party members as companions. PostgreSQL remains the durable source of truth, while SignalR only publishes authoritative updates. Dungeon membership is a run-level snapshot, but every concrete encounter creates its own immutable combat roster.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs and SignalR, C#, EF Core, PostgreSQL, Vue 3, TypeScript, Vite, Vitest, existing `TimeProvider` and deterministic `IGameRandom` boundaries.

**Spec:** `docs/source-of-truth/gameplay/20_PARTY_SYSTEM.md`, `docs/source-of-truth/gameplay/28_DUNGEON_SYSTEM.md`, `docs/source-of-truth/gameplay/14_LOOT_SYSTEM.md`, `docs/source-of-truth/ui/UI_10_PARTY.md`, `docs/source-of-truth/ui/UI_15_DUNGEON.md`, and the approved requirements in this plan.

## Global Constraints

- Backend is authoritative; clients send intent only and never submit damage, contribution, loot, winner, XP, gold, or item results.
- A party contains at most five characters and a character belongs to at most one party.
- Only the party leader starts a world encounter or creates/starts a dungeon run.
- A world combat roster is frozen when that combat starts. A player who was in the party but in another location may join after arriving; a player added after combat start may not join.
- A fled participant cannot rejoin the same combat. If the last active participant flees, the combat ends in defeat.
- A dungeon run roster is not frozen for the whole run. New party members may join future encounters, but never an encounter already in progress.
- Contribution is not damage-only. The policy must support qualifying actions, effective healing, support effects, mitigation/tanking, threat/taunt, and participation time without requiring one universal damage percentage.
- Ordinary materials, reagents, consumables, and low-value loot use independent personal rolls per eligible participant.
- Rare, Epic, Legendary, and Unique equipment use persisted group loot rolls with `NEED`, `GREED`, and `PASS`. `NEED` is server-authorized by class, slot, weapon/armor category, and level requirements.
- Loot generation is persisted before item granting. Every reward and roll is idempotent across retries, reconnects, server restarts, and duplicate requests.
- Loot-roll timeouts resolve to `PASS` and do not block the next dungeon encounter.
- The first dungeon is Ancient Mine, supports 1–5 players, has four ordinary encounters and one final boss, and a wipe resets only the current encounter.
- Use EF Core migrations; never use `EnsureCreated` for the production schema.
- New gameplay rules require unit or integration tests written before production implementation and observed failing once.

## File Map

### Existing files to modify

- `src/Elyndor.Core/Identity/Account.cs` and its configuration: persist an optional normalized Telegram username used for search.
- `src/Elyndor.Core/Characters/Character.cs` and its configuration: persist a stable public player code and preserve the existing unique normalized character name.
- `src/Elyndor.Infrastructure/Persistence/GameDbContext.cs`: register social, party, dungeon, combat-participant, contribution, and loot entities.
- `src/Elyndor.Infrastructure/DependencyInjection.cs`: register new application services and the loot-roll expiry worker.
- `src/Elyndor.Server/Program.cs`: map social, party, dungeon, and loot endpoints and register the worker.
- `src/Elyndor.Core/Combat/Sessions/CombatSessionModels.cs`, `CombatSession.cs`, and combat partials: add multiple player participants, participant states, contribution events, and flee semantics while retaining the single-player compatibility projection.
- `src/Elyndor.Infrastructure/Combat/CombatSessionRegistry.cs`, `CombatSessionFactory.cs`, `CombatApplicationService.cs`, `CombatSessionFinalizer.cs`, and `src/Elyndor.Server/Combat/CombatHub.cs`: route commands by character participant, use one session gate, publish updates to all participants, and finalize per-participant rewards.
- `src/Elyndor.Core/Items/ItemModels.cs`, `LootRoller.cs`, `PendingLootItem.cs`, and `src/Elyndor.Infrastructure/Progression/CombatRewardService.cs`: split personal loot from group equipment rolls and reuse the existing item/inventory ownership rules.
- `src/Elyndor.Contracts/Combat/CombatContracts.cs`: expose participant strips, contribution-eligible state, loot-roll state, and personal reward updates.
- `src/Elyndor.Core/Content/GameContentPackage.cs`, indexes, validators, content JSON, and content tests: add versioned dungeon definitions, encounter sequences, and the Ancient Mine reward profiles.
- `web/elyndor-web/src/router/index.ts`, `AppShell.vue`, `stores/gameSession.ts`, `stores/combatSession.ts`, API contracts, and combat/world views: add social, party, dungeon, participant, and loot-roll flows.

### New files by responsibility

- `src/Elyndor.Core/Social/FriendModels.cs`, `src/Elyndor.Core/Parties/PartyModels.cs`, `src/Elyndor.Core/Dungeons/DungeonModels.cs`, and `src/Elyndor.Core/Combat/Contribution/ContributionModels.cs`: focused domain state and invariants.
- `src/Elyndor.Infrastructure/Social/FriendService.cs`, `PartyService.cs`, `DungeonService.cs`, `ContributionPolicyResolver.cs`, and `LootRollService.cs`: application orchestration and transaction boundaries.
- `src/Elyndor.Infrastructure/Persistence/Configurations/*Social*`, `*Party*`, `*Dungeon*`, `*Contribution*`, and `*LootRoll*`: PostgreSQL mappings, indexes, checks, and unique constraints.
- `src/Elyndor.Server/Social/SocialEndpoints.cs`, `Parties/PartyEndpoints.cs`, `Dungeons/DungeonEndpoints.cs`, and loot endpoints: authenticated transport only; no gameplay rules in endpoints.
- `src/Elyndor.Contracts/Social/*`, `Parties/*`, `Dungeons/*`, and `Loot/*`: strongly typed request/response contracts.
- `tests/Elyndor.UnitTests/Social/*`, `Parties/*`, `Combat/*`, `Dungeons/*`, `Progression/*`, and `Items/*`: domain and service behavior tests.
- `tests/Elyndor.IntegrationTests/Social/*`, `Parties/*`, `Combat/*`, `Dungeons/*`, and `Progression/*`: EF/PostgreSQL transaction, idempotency, and endpoint tests.
- `web/elyndor-web/src/game/social/*`, `game/party/*`, and `game/dungeons/*`: mobile-first views and stores.

---

### Task 0: Update the source of truth with the approved multiplayer and loot rules

**Files:**
- Modify: `docs/source-of-truth/gameplay/20_PARTY_SYSTEM.md`
- Modify: `docs/source-of-truth/gameplay/28_DUNGEON_SYSTEM.md`
- Modify: `docs/source-of-truth/gameplay/14_LOOT_SYSTEM.md`
- Create: `docs/source-of-truth/phases/PHASE_06_PARTY_DUNGEON_MULTIPLAYER.md`

**Interfaces:**
- Consumes: approved user requirements in this plan.
- Produces: one stable implementation contract for all later tasks.

- [ ] **Step 1: Document party-combat lifecycle**

  Add the leader-start rule, per-participant flee state, late arrival rule, frozen world-combat roster, and last-participant-flees defeat rule to the Party source of truth.

- [ ] **Step 2: Document per-encounter dungeon roster**

  Replace any run-wide roster implication with a run membership snapshot plus a new encounter roster snapshot. State that a new member can enter a future encounter but cannot enter an active encounter.

- [ ] **Step 3: Document contribution and loot policy**

  Add `ParticipationPolicy`, independent personal loot rolls, persisted group equipment rolls, `NEED/GREED/PASS`, server-side equipability validation, timeout behavior, and idempotent resolution.

- [ ] **Step 4: Add phase document and review for contradictions**

  Record the implementation order and explicitly mark party group loot as an approved extension of the previous personal-loot-only baseline.

- [ ] **Step 5: Verify documentation consistency**

  Run: `rg -n "roster|personal loot|group loot|Need|Greed|contribution|late join|encounter" docs/source-of-truth/gameplay/20_PARTY_SYSTEM.md docs/source-of-truth/gameplay/28_DUNGEON_SYSTEM.md docs/source-of-truth/gameplay/14_LOOT_SYSTEM.md docs/source-of-truth/phases/PHASE_06_PARTY_DUNGEON_MULTIPLAYER.md`

  Expected: no paragraph says the whole dungeon roster is immutable or that Need/Greed is out of scope for the approved dungeon slice.

---

### Task 1: Add searchable player identity and friend requests

**Files:**
- Modify: `src/Elyndor.Core/Identity/Account.cs`
- Modify: `src/Elyndor.Core/Characters/Character.cs`
- Create: `src/Elyndor.Core/Social/FriendModels.cs`
- Create: `src/Elyndor.Infrastructure/Social/FriendService.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Configurations/FriendConfiguration.cs`
- Modify: `src/Elyndor.Infrastructure/Persistence/GameDbContext.cs`
- Create: `src/Elyndor.Contracts/Social/FriendContracts.cs`
- Create: `src/Elyndor.Server/Social/SocialEndpoints.cs`
- Modify: `src/Elyndor.Infrastructure/DependencyInjection.cs`
- Modify: `src/Elyndor.Server/Program.cs`
- Create: `tests/Elyndor.UnitTests/Social/FriendRulesTests.cs`
- Create: `tests/Elyndor.IntegrationTests/Social/FriendServiceTests.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Migrations/<timestamp>_PlayerSocialIdentity.cs`

**Interfaces:**
- Consumes: authenticated account ID and current character ownership conventions.
- Produces: `GET /api/v1/social/search`, `GET /api/v1/friends`, `GET /api/v1/friends/requests`, `POST /api/v1/friends/requests`, `POST /api/v1/friends/requests/{id}/accept`, `POST /api/v1/friends/requests/{id}/decline`, and `DELETE /api/v1/friends/{characterId}`.

- [ ] **Step 1: Write failing domain tests**

  Cover self-add rejection, duplicate pending request rejection, reciprocal request convergence, accept/decline transitions, normalized search across name/username/code, and stable public-code generation.

- [ ] **Step 2: Run the focused unit tests and verify RED**

  Run: `dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --filter FullyQualifiedName~Social.FriendRulesTests`

  Expected: compile/test failure because the social model and rules do not exist.

- [ ] **Step 3: Implement identity fields and social aggregates**

  Add a nullable normalized Telegram username to `Account`, a unique public character code to `Character`, a normalized unordered friendship key, and explicit request states. Keep friendship symmetric and prevent duplicates with a database unique constraint.

- [ ] **Step 4: Implement transactional service methods**

  Make request creation, accept, decline, and removal transaction-safe. Lock the involved characters in deterministic ID order before checking existing rows. Search must cap results and never expose Telegram identity for unrelated fields beyond the requested search result contract.

- [ ] **Step 5: Implement authenticated endpoints**

  Endpoints resolve the account from claims, validate input, call `FriendService`, map domain errors to stable problem codes, and never accept a client-provided owner ID.

- [ ] **Step 6: Add migration and integration tests**

  Verify uniqueness, reciprocal request behavior, transaction replay, and search indexes against PostgreSQL.

- [ ] **Step 7: Run the focused green test cycle**

  Run the unit and social integration filters. Expected: all social tests pass, including duplicate and concurrent request cases.

---

### Task 2: Add persistent parties and invitations

**Files:**
- Create: `src/Elyndor.Core/Parties/PartyModels.cs`
- Create: `src/Elyndor.Infrastructure/Parties/PartyService.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Configurations/PartyConfiguration.cs`
- Modify: `src/Elyndor.Infrastructure/Persistence/GameDbContext.cs`
- Create: `src/Elyndor.Contracts/Parties/PartyContracts.cs`
- Create: `src/Elyndor.Server/Parties/PartyEndpoints.cs`
- Modify: `src/Elyndor.Infrastructure/DependencyInjection.cs`
- Modify: `src/Elyndor.Server/Program.cs`
- Create: `tests/Elyndor.UnitTests/Parties/PartyRulesTests.cs`
- Create: `tests/Elyndor.IntegrationTests/Parties/PartyServiceTests.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Migrations/<timestamp>_PartiesAndInvites.cs`

**Interfaces:**
- Consumes: friend service, character identity, and existing operation/idempotency conventions.
- Produces: party snapshot, leader-only invite/kick/transfer/disband commands, friend and direct invite paths, and incoming invite listing.

- [ ] **Step 1: Write failing party rule tests**

  Cover max five members, one party per character, leader-only commands, friend-vs-direct invite policy, accepting a full-party invite, deterministic leader transfer, leader disband, duplicate command replay, and invite expiry.

- [ ] **Step 2: Run tests and verify RED**

  Run: `dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --filter FullyQualifiedName~Parties.PartyRulesTests`

- [ ] **Step 3: Implement party aggregates and PostgreSQL constraints**

  Use `Party`, `PartyMember`, and `PartyInvite` records/entities. Add a unique active-party membership constraint, leader foreign key, max-size checks in service and database-safe transaction flow, and version fields for optimistic concurrency.

- [ ] **Step 4: Implement service transactions**

  Create, invite, accept, leave, kick, transfer, and disband through one application service. Lock party state and target character deterministically. On leader leave, promote the earliest valid member by `JoinedAtUtc` then `CharacterId`.

- [ ] **Step 5: Add endpoints and contracts**

  Expose `/api/v1/party`, `/api/v1/party/invites`, and command endpoints with replay-safe request IDs. Return current party version in every mutation response.

- [ ] **Step 6: Add integration tests and migration**

  Verify concurrent accept/invite races, duplicate membership prevention, and idempotent leader transfer with PostgreSQL.

---

### Task 3: Build the multi-participant combat kernel and contribution ledger

**Files:**
- Create: `src/Elyndor.Core/Combat/Participants/CombatParticipantModels.cs`
- Create: `src/Elyndor.Core/Combat/Contribution/ContributionModels.cs`
- Modify: `src/Elyndor.Core/Combat/Sessions/CombatSessionModels.cs`
- Modify: `src/Elyndor.Core/Combat/Sessions/CombatSession.cs` and relevant class partials
- Modify: `src/Elyndor.Core/Combat/CombatModels.cs`
- Modify: `src/Elyndor.Infrastructure/Combat/CombatSessionRegistry.cs`
- Modify: `src/Elyndor.Infrastructure/Combat/CombatSessionFactory.cs`
- Modify: `src/Elyndor.Infrastructure/Combat/CombatApplicationService.cs`
- Modify: `src/Elyndor.Infrastructure/Combat/CombatSessionFinalizer.cs`
- Modify: `src/Elyndor.Core/Combat/ActiveCombatSession.cs`
- Modify: `src/Elyndor.Infrastructure/Persistence/Configurations/ActiveCombatSessionConfiguration.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Configurations/CombatParticipantConfiguration.cs`
- Modify: `src/Elyndor.Infrastructure/Persistence/GameDbContext.cs`
- Modify: `src/Elyndor.Contracts/Combat/CombatContracts.cs`
- Modify: `src/Elyndor.Server/Combat/CombatHub.cs`
- Create: `tests/Elyndor.UnitTests/Combat/CombatParticipantTests.cs`
- Create: `tests/Elyndor.UnitTests/Combat/ContributionPolicyTests.cs`
- Create: `tests/Elyndor.IntegrationTests/Combat/MultiplayerCombatTests.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Migrations/<timestamp>_MultiplayerCombatParticipants.cs`

**Interfaces:**
- Consumes: party snapshot, existing ability/damage/effect runtime, `TimeProvider`, `IGameRandom`, and current SignalR contract.
- Produces: one authoritative session with many player participants, participant-owned commands, immutable encounter roster, flee state, contribution context, and broadcasts to all participant accounts.

- [ ] **Step 1: Write failing combat participant tests**

  Cover one session for multiple characters, commands accepted only for the owning participant, one participant fleeing while the session stays active, no rejoin after flee, late arrival from the same starting party joining before victory, post-start party member rejection, and last-active-participant defeat.

- [ ] **Step 2: Write failing contribution tests**

  Assert that damage, effective healing, shield/mitigation, taunt/threat, support effects, qualifying actions, and participation time can independently make a participant eligible according to policy. Assert that a single damage percentage is not the universal gate and that a valid participant who dies remains eligible.

- [ ] **Step 3: Run focused tests and verify RED**

  Run: `dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --filter FullyQualifiedName~Combat.CombatParticipantTests|FullyQualifiedName~Combat.ContributionPolicyTests`

- [ ] **Step 4: Introduce participant ownership without breaking the solo projection**

  Add a participant collection and owner-character ID to the session. Keep `Player` in the snapshot as the requesting participant for existing single-player clients, and add a `Players` collection for new clients. Do not reuse `Companion` for another player.

- [ ] **Step 5: Make the registry session-centric**

  Replace account-only ownership with account-to-session participant bindings and one per-session gate. Ensure timers tick once per combat session, not once per participant, and all participant accounts receive the same sequence-ordered update.

- [ ] **Step 6: Add contribution ledger hooks**

  Record authoritative combat events into participant contribution state. Include event source/target ownership, effective amounts, action counts, join time, flee time, death state, and disconnect-independent participation.

- [ ] **Step 7: Implement flee and late join rules**

  Add a participant-level `Flee` command. Freeze the encounter roster at session creation, permit only eligible initial roster members to attach before the encounter ends, and reject any participant that has fled or was added after start.

- [ ] **Step 8: Persist active session participants and migrate**

  Store one session root plus participant rows, with unique active combat per character and a version/concurrency token. Preserve restart recovery for the current solo session model while enabling multi-character sessions.

- [ ] **Step 9: Update SignalR and contracts**

  Route abilities, consumables, target selection, auto-attacks, and flee through the authenticated participant. Publish updates to each account’s combat group without trusting client character IDs.

- [ ] **Step 10: Run combat tests and all existing combat regressions**

  Run focused multiplayer tests, then `dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --filter FullyQualifiedName~Combat` and the integration combat filters.

---

### Task 4: Make world encounters party-startable

**Files:**
- Modify: `src/Elyndor.Infrastructure/World/WorldEncounterService.cs`
- Modify: `src/Elyndor.Server/World/WorldEndpoints.cs`
- Modify: `src/Elyndor.Infrastructure/Combat/CombatApplicationService.cs`
- Modify: `src/Elyndor.Infrastructure/Combat/CombatSessionFactory.cs`
- Modify: `src/Elyndor.Contracts/World/WorldContracts.cs`
- Modify: `web/elyndor-web/src/stores/combatSession.ts`
- Modify: `web/elyndor-web/src/game/world/views/WorldView.vue`
- Modify: `web/elyndor-web/src/game/combat/views/CombatView.vue`
- Create: `tests/Elyndor.IntegrationTests/Combat/PartyWorldEncounterTests.cs`

**Interfaces:**
- Consumes: party leader authorization and the multi-participant combat kernel.
- Produces: leader-only world combat start, location-aware late attach, party participant strip, flee action, and synchronized victory/defeat state.

- [ ] **Step 1: Write failing integration tests**

  Cover leader-only start, same-location eligibility, late arrival by an original party member, rejection of a member added after start, one member fleeing, and defeat after the last participant flees.

- [ ] **Step 2: Verify RED**

  Run: `dotnet test tests/Elyndor.IntegrationTests/Elyndor.IntegrationTests.csproj --filter FullyQualifiedName~PartyWorldEncounterTests`

- [ ] **Step 3: Implement leader start and encounter roster creation**

  Resolve the party from the authenticated character, validate the leader, capture the party member IDs and location/encounter context, and create the shared combat session once.

- [ ] **Step 4: Implement late attach**

  When an original roster member enters the location, expose the active session and attach them only if they have not fled, died irrecoverably, or already completed the encounter.

- [ ] **Step 5: Update the web combat flow**

  Add participant status cards and a flee action. Ensure reconnect resumes the participant’s authoritative session and never starts a second session.

- [ ] **Step 6: Run backend and frontend tests**

  Run the focused integration tests, `npm run test:unit -- --runInBand` from `web/elyndor-web`, and the existing combat/world suites.

---

### Task 5: Add dungeon definitions, Ancient Mine, and per-encounter membership

**Files:**
- Create: `src/Elyndor.Core/Dungeons/DungeonModels.cs`
- Create: `src/Elyndor.Infrastructure/Dungeons/DungeonService.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Configurations/DungeonConfiguration.cs`
- Modify: `src/Elyndor.Infrastructure/Persistence/GameDbContext.cs`
- Create: `src/Elyndor.Contracts/Dungeons/DungeonContracts.cs`
- Create: `src/Elyndor.Server/Dungeons/DungeonEndpoints.cs`
- Modify: `src/Elyndor.Infrastructure/DependencyInjection.cs`
- Modify: `src/Elyndor.Server/Program.cs`
- Modify: `src/Elyndor.Core/Content/GameContentPackage.cs` and related indexes/validators
- Add: `content/*` Ancient Mine definitions following current content conventions
- Create: `tests/Elyndor.UnitTests/Dungeons/DungeonRulesTests.cs`
- Create: `tests/Elyndor.IntegrationTests/Dungeons/DungeonServiceTests.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Migrations/<timestamp>_DungeonRuns.cs`

**Interfaces:**
- Consumes: party service, world location, combat session factory, content versioning, and participation rules.
- Produces: dungeon preview, leader-created run, member entry/rejoin, four encounters plus boss, checkpoints, wipe reset, completion state, and per-encounter roster snapshots.

- [ ] **Step 1: Write failing dungeon rule tests**

  Cover 1–5 player bounds, leader-only creation, run membership snapshot, a new party member entering the run after the current encounter, rejection while an encounter is active, admission to the next encounter, checkpoint preservation after wipe, and completion idempotency.

- [ ] **Step 2: Verify RED**

  Run the dungeon unit filter and confirm failures are caused by missing dungeon behavior.

- [ ] **Step 3: Implement versioned dungeon content**

  Define Ancient Mine with four normal encounters and one boss, level/entry requirements from the approved design, encounter monster IDs, checkpoint sequence, reward profile, and content version. Extend validators so invalid encounter references fail startup/content publication.

- [ ] **Step 4: Implement durable run and encounter state**

  Persist `DungeonRun`, `DungeonRunMember`, `DungeonEncounter`, and encounter member snapshots. Store party membership at run creation separately from each encounter’s immutable combat roster.

- [ ] **Step 5: Implement enter/continue/wipe/complete transactions**

  Lock the run and current encounter, reject duplicate entry, do not add a new participant to an active encounter, and permit the new party member at the next encounter. A wipe creates a fresh roster for the same encounter while retaining completed checkpoint IDs.

- [ ] **Step 6: Add endpoints and mobile UI**

  Add dungeon preview, create, enter, current-run, restart-current-encounter, and exit commands. Build a dungeon screen with progress, member state, checkpoints, and a non-blocking combat/loot transition.

- [ ] **Step 7: Run dungeon unit/integration/frontend tests**

  Include restart/reconnect cases and verify no duplicate run membership or completion rewards.

---

### Task 6: Implement contribution-aware personal loot and persisted group equipment rolls

**Files:**
- Create: `src/Elyndor.Core/Progression/ParticipationPolicy.cs`
- Create: `src/Elyndor.Core/Items/LootRollModels.cs`
- Create: `src/Elyndor.Infrastructure/Progression/ContributionEligibilityService.cs`
- Create: `src/Elyndor.Infrastructure/Progression/LootRollService.cs`
- Create: `src/Elyndor.Infrastructure/Progression/LootRollExpiryWorker.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Configurations/LootRollConfiguration.cs`
- Modify: `src/Elyndor.Infrastructure/Progression/CombatRewardService.cs`
- Modify: `src/Elyndor.Core/Items/PendingLootItem.cs`
- Modify: `src/Elyndor.Infrastructure/Persistence/GameDbContext.cs`
- Create: `src/Elyndor.Contracts/Loot/LootContracts.cs`
- Create: `src/Elyndor.Server/Loot/LootEndpoints.cs`
- Modify: `src/Elyndor.Server/Program.cs`
- Create: `tests/Elyndor.UnitTests/Progression/ContributionEligibilityTests.cs`
- Create: `tests/Elyndor.UnitTests/Items/GroupLootRollTests.cs`
- Create: `tests/Elyndor.IntegrationTests/Progression/PartyRewardIdempotencyTests.cs`
- Create: `tests/Elyndor.IntegrationTests/Items/LootRollPersistenceTests.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Migrations/<timestamp>_PartyRewardsAndLootRolls.cs`

**Interfaces:**
- Consumes: completed encounter snapshot, participant contribution ledger, versioned loot tables, item equipability rules, `TimeProvider`, and `IGameRandom`.
- Produces: one independent personal reward resolution per eligible participant and one persisted group roll per valuable equipment item.

- [ ] **Step 1: Write failing contribution eligibility tests**

  Cover support-only healer eligibility, tank/taunt eligibility, mitigation eligibility, damage eligibility, dead-after-valid-participation eligibility, no-action spectator rejection, fled-before-kill rejection, and late-join exclusion from the earlier encounter.

- [ ] **Step 2: Write failing group-loot tests**

  Cover `NEED` priority over `GREED`, random roll among same-priority choices, no winner when all pass, server rejection of invalid `NEED`, timeout-as-pass, duplicate-choice idempotency, and a single winner grant after reconnect.

- [ ] **Step 3: Run tests and verify RED**

  Run the focused unit filters and confirm they fail before implementation.

- [ ] **Step 4: Implement contribution policy**

  Resolve an activity policy containing minimum participation time, qualifying action rules, damage/healing/support/tanking weights, optional join cutoff, and eligibility mode. Keep policy evaluation independent of UI and item rarity.

- [ ] **Step 5: Implement independent personal loot resolution**

  For every eligible participant, create a unique reward resolution, perform an independent server roll against the encounter loot table, persist the result and loot-table version atomically, then grant items through the existing inventory/pending-loot path. Empty personal rolls are valid and persisted as resolved.

- [ ] **Step 6: Implement valuable equipment group rolls**

  Create normalized `LootRoll` and `LootRollChoice` rows with `DungeonRunId`, `CombatSessionId`, item definition/version, rolled instance data, eligible character IDs, state, `EndsAtUtc`, and concurrency version. Only `ItemType.Equipment` with rarity `Rare` or higher enters this flow.

- [ ] **Step 7: Implement server equipability validation**

  Reuse the canonical inventory equipment checks for class, level, slot, weapon category, armor category, and off-hand restrictions. The client may render disabled `NEED`, but the service must reject a forged invalid choice.

- [ ] **Step 8: Implement timeout and non-blocking resolution**

  Add a hosted expiry worker using `TimeProvider`. Resolve expired open rolls as `PASS`, grant the winner in one idempotent transaction, and ensure dungeon progression never awaits an open roll.

- [ ] **Step 9: Expose loot choices and updates**

  Add authenticated GET/POST endpoints and SignalR notifications. A repeated choice returns the existing result; it never rerolls or creates a second item.

- [ ] **Step 10: Run persistence and concurrency tests**

  Verify duplicate HTTP requests, concurrent finalization, restart recovery, full inventory pending loot, and one-and-only-one equipment instance.

---

### Task 7: Add social, party, dungeon, participant, and loot UI

**Files:**
- Create: `web/elyndor-web/src/game/social/views/FriendsView.vue`
- Create: `web/elyndor-web/src/game/social/socialStore.ts`
- Create: `web/elyndor-web/src/game/party/views/PartyView.vue`
- Create: `web/elyndor-web/src/game/party/partyStore.ts`
- Create: `web/elyndor-web/src/game/dungeons/views/DungeonView.vue`
- Create: `web/elyndor-web/src/game/dungeons/dungeonStore.ts`
- Create: `web/elyndor-web/src/game/loot/LootRollModal.vue`
- Modify: `web/elyndor-web/src/api/contracts.ts`
- Modify: `web/elyndor-web/src/router/index.ts`
- Modify: `web/elyndor-web/src/app/AppShell.vue`
- Modify: `web/elyndor-web/src/stores/combatSession.ts`
- Modify: `web/elyndor-web/src/game/combat/views/CombatView.vue`
- Create: `web/elyndor-web/src/__tests__/FriendsView.spec.ts`
- Create: `web/elyndor-web/src/__tests__/PartyView.spec.ts`
- Create: `web/elyndor-web/src/__tests__/DungeonView.spec.ts`
- Create: `web/elyndor-web/src/__tests__/LootRollModal.spec.ts`

**Interfaces:**
- Consumes: generated/handwritten contracts from Tasks 1–6 and SignalR authoritative updates.
- Produces: mobile-first flows for search, friend requests, party management, synchronized combat, dungeon progression, and loot choices.

- [ ] **Step 1: Write failing component/store tests**

  Cover search result actions, accept/decline requests, leader-only controls, party refresh after reconnect, participant flee, dungeon encounter transition, `NEED` disabled when the server says invalid, and timeout display.

- [ ] **Step 2: Verify RED**

  Run: `npm run test:unit -- src/__tests__/FriendsView.spec.ts src/__tests__/PartyView.spec.ts src/__tests__/DungeonView.spec.ts src/__tests__/LootRollModal.spec.ts`

- [ ] **Step 3: Implement stores and API calls**

  Use existing replay-safe mutation conventions for every state-changing request. Stores must refresh authoritative snapshots after mutation and preserve duplicate-request safety.

- [ ] **Step 4: Implement views and combat participant strip**

  Add HUD access to friends/party, incoming invite UI, party leader controls, compact participant state in combat, dungeon progress, and the three loot buttons with server-error handling.

- [ ] **Step 5: Run frontend typecheck/build/tests**

  Run: `npm run test:unit`, `npm run type-check`, and `npm run build` from `web/elyndor-web`.

---

### Task 8: Full verification and final architecture review

**Files:**
- Review: all files changed by Tasks 0–7
- Modify: documentation only where implementation evidence exposes drift

- [ ] **Step 1: Run backend unit tests**

  Run: `dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj`

- [ ] **Step 2: Run backend integration tests**

  Run: `dotnet test tests/Elyndor.IntegrationTests/Elyndor.IntegrationTests.csproj`

- [ ] **Step 3: Run content validation and migration checks**

  Run the repository’s content validator and PostgreSQL integration fixture. Confirm migration SQL contains all unique constraints and no production path uses `EnsureCreated`.

- [ ] **Step 4: Run frontend verification**

  Run: `npm run lint`, `npm run type-check`, `npm run test:unit`, and `npm run build` from `web/elyndor-web`.

- [ ] **Step 5: Review security and concurrency**

  Inspect `git diff` for client-trusted IDs/results, missing authorization, duplicate reward paths, non-idempotent commands, transaction scope errors, and accidental secrets.

- [ ] **Step 6: Run browser smoke checks**

  Use the repository Playwright workflow to verify friend search, invitation acceptance, party creation, leader combat start, late join, flee, dungeon progression, and loot-roll timeout behavior.

- [ ] **Step 7: Report evidence and limitations**

  Report exact commands and results. Explicitly list any checks blocked by unavailable Telegram authentication, PostgreSQL, browser environment, or production-only dependencies.

## Delivery Order

Implement and review in this order:

1. Task 0 documentation contract.
2. Tasks 1–2 social and party vertical slice.
3. Task 3 combat kernel with tests and migration.
4. Task 4 world party combat.
5. Task 5 Ancient Mine run/encounter lifecycle.
6. Task 6 contribution and loot resolution.
7. Task 7 UI integration.
8. Task 8 full verification and architecture review.

Each task must leave its own tests green before the next task begins. Do not deploy to production until Task 8 is complete and the migration has been applied and verified against the production schema backup/rollback procedure.
