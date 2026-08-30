# Phase 1 Telegram Identity, Character, and World Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the complete Phase 1 flow from Telegram authentication through persistent character creation and concurrency-safe world travel in the mobile Vue client.

**Architecture:** Elyndor remains a modular monolith. Core owns character-name and world rules, Infrastructure owns EF Core/PostgreSQL transactions, Server owns JWT/HTTP boundaries, and Vue owns only user intent and presentation. PostgreSQL constraints plus idempotency records are the final mutation guarantees; the client never supplies authoritative identity or source location.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core 10, PostgreSQL 18, Npgsql, short-lived JWT, Vue 3, TypeScript, Pinia, Vite, xUnit, Testcontainers PostgreSQL, Playwright.

**Spec:** `docs/source-of-truth/phases/PHASE_01_TELEGRAM_IDENTITY_WORLD.md`

## Global Constraints

- Complete every Phase 1 DoD item before starting Phase 2.
- JWT lifetime is 15 minutes; no refresh token or browser persistence.
- Development auth exists only in `Development` plus an explicit flag and is absent in PublicTest/Production.
- Account, character, location, and idempotency state are permanent PostgreSQL truth.
- Character creation is one transaction and one account owns at most one character.
- Travel validates from the current database location and uses an atomic version check.
- No stats/resources, abilities, combat, Redis, Quartz jobs, or future-phase systems.
- Every mutation uses injected `TimeProvider`, UTC timestamps, stable error codes, and cancellation tokens.

---

### Task 1: PostgreSQL integration harness and Phase 1 persistence model

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `tests/Elyndor.IntegrationTests/Elyndor.IntegrationTests.csproj`
- Create: `tests/Elyndor.IntegrationTests/Postgres/PostgresFixture.cs`
- Create: `tests/Elyndor.IntegrationTests/Postgres/PostgresCollection.cs`
- Create: `src/Elyndor.Core/Identity/Account.cs`
- Create: `src/Elyndor.Core/Characters/Character.cs`
- Create: `src/Elyndor.Core/World/CharacterLocation.cs`
- Create: `src/Elyndor.Core/World/TravelOperation.cs`
- Modify: `src/Elyndor.Infrastructure/Persistence/GameDbContext.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Configurations/AccountConfiguration.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Configurations/CharacterConfiguration.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Configurations/CharacterLocationConfiguration.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Configurations/TravelOperationConfiguration.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Migrations/20260830075709_PhaseOneIdentityWorld.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Migrations/20260830075709_PhaseOneIdentityWorld.Designer.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Migrations/GameDbContextModelSnapshot.cs`
- Test: `tests/Elyndor.IntegrationTests/Postgres/PhaseOneSchemaTests.cs`

**Interfaces:**
- Produces: `PostgresFixture.ConnectionString`, `PostgresFixture.CreateDbContext()`.
- Produces: `GameDbContext.Accounts`, `Characters`, `CharacterLocations`, and `TravelOperations`.
- Constraints: unique `accounts.telegram_user_id`, `characters.account_id`, `characters.creation_request_id`, `characters.normalized_name`, and `travel_operations(character_id, request_id)`.

- [x] **Step 1: Add the PostgreSQL test dependency and fixture**

Add central package version `Testcontainers.PostgreSql` and reference it from IntegrationTests. The fixture starts `postgres:18.4`, creates `GameDbContext` with Npgsql, runs `Database.MigrateAsync`, and disposes the container after the collection.

- [x] **Step 2: Write failing schema tests**

```csharp
[Collection(PostgresFixtureDefinition.Name)]
public sealed class PhaseOneSchemaTests(PostgresFixture postgres)
{
    [Fact]
    public async Task AccountTelegramUserIdIsUnique();

    [Fact]
    public async Task CharacterAccountAndNormalizedNameAreUnique();

    [Fact]
    public async Task CharacterLocationVersionIsPersisted();
}
```

- [x] **Step 3: Run the focused tests and confirm RED**

Run: `dotnet test tests/Elyndor.IntegrationTests/Elyndor.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~PhaseOneSchemaTests`

Expected: compile failure because Phase 1 entities/DbSets do not exist.

- [x] **Step 4: Implement focused entities and EF configurations**

Use UUID keys, `timestamptz`, required varchar IDs, cascade only from Account to its single Character, and restrict unrelated deletes. `CharacterLocation.Version` is a `long` concurrency token incremented only by a successful travel update. `TravelOperation` stores request ID, requested target, resulting location/version, and completion timestamp.

- [x] **Step 5: Generate and inspect the migration**

Run with an explicit local design connection string and keep EF Core's generated UTC migration ID so the migration history matches the generated artifact:

`dotnet tool run dotnet-ef migrations add PhaseOneIdentityWorld --project src/Elyndor.Infrastructure --startup-project src/Elyndor.Server --context GameDbContext --output-dir Persistence/Migrations`

Inspect SQL/model snapshot for unique constraints, FKs, schema `game`, UTC column types, and unintended cascade paths.

- [x] **Step 6: Run schema tests and confirm GREEN**

Run the focused test command from Step 3. Expected: all schema tests pass against PostgreSQL 18.4.

- [x] **Step 7: Commit the persistence foundation**

`git commit -m "feat: add phase 1 persistence schema"`

---

### Task 2: Versioned location content and transition rules

**Files:**
- Modify: `src/Elyndor.Core/Content/GameContentPackage.cs`
- Modify: `src/Elyndor.Core/Content/GameContentPackageValidator.cs`
- Create: `src/Elyndor.Core/World/LocationDefinition.cs`
- Create: `src/Elyndor.Core/World/WorldMap.cs`
- Modify: `content/package.json`
- Modify: `tests/Elyndor.UnitTests/Content/GameContentPackageValidatorTests.cs`
- Create: `tests/Elyndor.UnitTests/World/WorldMapTests.cs`
- Modify: `tests/Elyndor.IntegrationTests/Content/GameContentPackageLoaderTests.cs`

**Interfaces:**
- `LocationDefinition(string Id, string DisplayName, string DangerLevel, int RecommendedLevel, IReadOnlyList<string> Transitions)`.
- `WorldMap.GetRequired(string locationId)` and `WorldMap.CanTravel(string sourceId, string targetId)`.
- Produces location IDs `STARTER_TOWN`, `WHISPERING_FOREST`, `DEEP_FOREST`.

- [x] **Step 1: Write failing content and transition tests**

Cover duplicate location IDs, missing transition targets, self-transition, non-positive recommended level, invalid danger category, valid bidirectional chain, and rejection of direct Starter Town → Deep Forest.

- [x] **Step 2: Run focused unit/content tests and confirm RED**

Run: `dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --configuration Release --filter "FullyQualifiedName~WorldMapTests|FullyQualifiedName~GameContentPackageValidatorTests"`

- [x] **Step 3: Implement location content and pure WorldMap**

Extend the strict JSON package with `locations`. Validate canonical IDs, unique IDs, all references, allowed danger values `SAFE|ADVENTURE|DANGEROUS`, and the exact three-location prototype chain.

- [x] **Step 4: Add the production content records**

Add Starter Town, Whispering Forest, and Deep Forest to `content/package.json` with the names, danger levels, recommended levels, and transitions from the Phase 1 spec.

- [x] **Step 5: Run unit, loader, and content-validator checks**

Run:

- `dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --configuration Release`
- `dotnet test tests/Elyndor.IntegrationTests/Elyndor.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~GameContentPackageLoaderTests`
- `dotnet run --project tools/Elyndor.ContentValidator --configuration Release -- content/package.json`

- [x] **Step 6: Commit world content**

`git commit -m "feat: add phase 1 world content"`

---

### Task 3: Idempotent Account resolution and short-lived JWT authentication

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/Elyndor.Server/Elyndor.Server.csproj`
- Create: `src/Elyndor.Infrastructure/Identity/AccountResolver.cs`
- Create: `src/Elyndor.Server/Identity/AuthenticationOptions.cs`
- Create: `src/Elyndor.Server/Identity/JwtTokenIssuer.cs`
- Create: `src/Elyndor.Server/Identity/AuthenticationEndpoints.cs`
- Create: `src/Elyndor.Contracts/Identity/AuthenticationContracts.cs`
- Modify: `src/Elyndor.Server/Program.cs`
- Modify: `src/Elyndor.Server/appsettings.json`
- Modify: `src/Elyndor.Server/appsettings.Development.json`
- Test: `tests/Elyndor.IntegrationTests/Identity/AccountResolverTests.cs`
- Test: `tests/Elyndor.IntegrationTests/Identity/AuthenticationEndpointsTests.cs`

**Interfaces:**
- `Task<Account> AccountResolver.ResolveAsync(long telegramUserId, CancellationToken cancellationToken)`.
- `IssuedAccessToken JwtTokenIssuer.Issue(Guid accountId)` with a 15-minute expiry from injected `TimeProvider`.
- `POST /api/v1/auth/telegram` and Development-only `POST /api/v1/auth/development`.

- [x] **Step 1: Write failing concurrent Account tests**

Start two separate DbContexts resolving the same Telegram user ID simultaneously. Assert one database row, identical Account IDs, and monotonic `LastSeenAtUtc`.

- [x] **Step 2: Write failing authentication API tests**

Cover valid signed fixture, invalid hash, expired data, missing Bot Token failure, 15-minute JWT claims, development endpoint enabled in Development, and 404 outside Development/when flag is false.

- [x] **Step 3: Run focused tests and confirm RED**

Run: `dotnet test tests/Elyndor.IntegrationTests/Elyndor.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~AccountResolverTests|FullyQualifiedName~AuthenticationEndpointsTests"`

- [x] **Step 4: Implement Account resolution transaction**

Insert by unique Telegram ID and handle only PostgreSQL unique-violation `23505` for the named constraint by reloading the winner. Do not catch arbitrary database exceptions. Update `LastSeenAtUtc` using `TimeProvider`.

- [x] **Step 5: Configure JWT bearer authentication**

Add `Microsoft.AspNetCore.Authentication.JwtBearer`. Validate issuer, audience, signature, and lifetime with zero hidden clock authority; use configured 30-second validation skew. Signing key must be at least 32 UTF-8 bytes and configuration validation fails closed.

- [x] **Step 6: Map Telegram and Development endpoints**

Telegram endpoint passes raw data to the existing `TelegramInitDataValidator`, resolves Account, and returns the token. Development endpoint reads only `Authentication:Development:TelegramUserId`; it has no request identity field and is mapped only in the permitted environment/flag combination.

- [x] **Step 7: Run focused and security-boundary tests**

Also rerun `PublicTestEnvironmentTests` to prove PublicTest has no development endpoint or Development OpenAPI.

- [x] **Step 8: Commit authentication**

`git commit -m "feat: add Telegram account authentication"`

---

### Task 4: Formal name policy and atomic character creation

**Files:**
- Create: `src/Elyndor.Core/Characters/CharacterNamePolicy.cs`
- Create: `src/Elyndor.Infrastructure/Characters/CharacterCreationService.cs`
- Create: `src/Elyndor.Contracts/Characters/CharacterContracts.cs`
- Create: `src/Elyndor.Server/Characters/CharacterEndpoints.cs`
- Modify: `src/Elyndor.Server/Program.cs`
- Test: `tests/Elyndor.UnitTests/Characters/CharacterNamePolicyTests.cs`
- Test: `tests/Elyndor.IntegrationTests/Characters/CharacterCreationServiceTests.cs`
- Test: `tests/Elyndor.IntegrationTests/Characters/CharacterEndpointsTests.cs`

**Interfaces:**
- `CharacterNameValidationResult CharacterNamePolicy.Validate(string value)` returns display/normalized names or a stable error code.
- `Task<CharacterCreationResult> CharacterCreationService.CreateAsync(Guid accountId, CreateCharacterCommand command, CancellationToken cancellationToken)`.
- `POST /api/v1/character` requires JWT and accepts `requestId`, `name`, `raceId`, `genderId`, `classId`.

- [x] **Step 1: Write exhaustive failing name-policy theory tests**

Valid examples: `Arthas`, `Артас`, `Анна-Мария`, `Dark Wolf`. Invalid examples: trimmed names, two separators, leading/trailing separators, digits, emoji, Greek, mixed `Aртас`, fewer than 3 or more than 16 Unicode scalar values, and names changed by disallowed normalization behavior.

- [x] **Step 2: Run name tests and confirm RED**

Run: `dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --configuration Release --filter FullyQualifiedName~CharacterNamePolicyTests`

- [x] **Step 3: Implement the Unicode policy using `System.Text.Rune`**

Normalize Form KC, count runes, classify Latin/Cyrillic ranges explicitly, enforce separator adjacency, and create invariant-uppercase `NormalizedName`.

- [x] **Step 4: Write failing PostgreSQL creation tests**

Cover exact request retry, request-key payload mismatch, concurrent same normalized name across accounts, concurrent different requests for one account, invalid roster values, and rollback leaving neither Character nor CharacterLocation.

- [x] **Step 5: Implement one-transaction creation service**

Persist Character and initial `CharacterLocation(STARTER_TOWN, Version=1)` in one transaction. Map named PostgreSQL constraints to `character_name_taken`, `character_already_exists`, or idempotent result. Never implement uniqueness with only a pre-insert SELECT.

- [x] **Step 6: Add JWT-protected GET/POST character endpoints**

Read Account ID only from validated JWT subject. Return RFC problem details plus stable `code` and correlation identifier for failures.

- [x] **Step 7: Run focused unit/integration/API tests**

Expected: all creation and policy tests pass against real PostgreSQL.

- [x] **Step 8: Commit character creation**

`git commit -m "feat: add atomic character creation"`

---

### Task 5: Bootstrap snapshot and concurrency-safe travel

**Files:**
- Create: `src/Elyndor.Infrastructure/World/BootstrapService.cs`
- Create: `src/Elyndor.Infrastructure/World/TravelService.cs`
- Create: `src/Elyndor.Contracts/World/WorldContracts.cs`
- Create: `src/Elyndor.Server/World/WorldEndpoints.cs`
- Modify: `src/Elyndor.Server/Program.cs`
- Test: `tests/Elyndor.IntegrationTests/World/BootstrapServiceTests.cs`
- Test: `tests/Elyndor.IntegrationTests/World/TravelServiceTests.cs`
- Test: `tests/Elyndor.IntegrationTests/World/WorldEndpointsTests.cs`

**Interfaces:**
- `Task<BootstrapSnapshot> BootstrapService.GetAsync(Guid accountId, CancellationToken cancellationToken)`.
- `Task<TravelResult> TravelService.TravelAsync(Guid accountId, Guid requestId, string targetLocationId, CancellationToken cancellationToken)`.
- JWT-protected `GET /api/v1/bootstrap`, `GET /api/v1/world/locations`, and `POST /api/v1/world/travel`.

- [x] **Step 1: Write failing bootstrap tests**

Assert no-character snapshot, created-character snapshot, current content/balance versions, server UTC, actual location, and only server-derived outgoing transitions.

- [x] **Step 2: Write failing travel/idempotency/concurrency tests**

Cover valid Town ↔ Forest ↔ Deep transitions, direct Town → Deep rejection, unknown target, exact retry, request ID reused for another target, and two DbContexts racing Forest vs Deep from Town. Assert only the valid transition from the winning authoritative row commits.

- [x] **Step 3: Run focused tests and confirm RED**

Run: `dotnet test tests/Elyndor.IntegrationTests/Elyndor.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~BootstrapServiceTests|FullyQualifiedName~TravelServiceTests|FullyQualifiedName~WorldEndpointsTests"`

- [x] **Step 4: Implement bootstrap projection**

Project only the authenticated Account/Character row, join current location state, and resolve display/transitions from `WorldMap`. Do not serialize EF entities.

- [x] **Step 5: Implement atomic travel and operation replay**

Within one transaction, first resolve an existing operation. Otherwise load actual location/version, validate through `WorldMap`, insert the request record, and execute a conditional update matching `(CharacterId, Version)`. Commit result and operation together. Convert a zero-row update to `travel_conflict` after rollback/reload.

- [x] **Step 6: Map protected endpoints and stable errors**

The body contains no source location or version. Unknown/invalid transition is 422, idempotency mismatch is 409, concurrency conflict is 409, and unauthenticated access is 401.

- [x] **Step 7: Run focused and full backend tests**

Run: `dotnet test Elyndor.slnx --configuration Release`

- [x] **Step 8: Commit world bootstrap/travel**

`git commit -m "feat: add persistent world travel"`

---

### Task 6: Vue authentication, re-authentication, and bootstrap state

**Files:**
- Create: `web/elyndor-web/src/telegram/telegramWebApp.ts`
- Create: `web/elyndor-web/src/api/apiClient.ts`
- Create: `web/elyndor-web/src/api/contracts.ts`
- Create: `web/elyndor-web/src/stores/gameSession.ts`
- Modify: `web/elyndor-web/src/main.ts`
- Modify: `web/elyndor-web/src/App.vue`
- Create: `web/elyndor-web/src/__tests__/gameSession.spec.ts`
- Create: `web/elyndor-web/src/__tests__/apiClient.spec.ts`

**Interfaces:**
- `getTelegramInitData(): string | null` reads `window.Telegram?.WebApp.initData`, never `initDataUnsafe`.
- `apiClient.request<T>(path, init)` attaches runtime bearer token and retries one safe request after delegated re-authentication.
- Pinia `useGameSessionStore()` exposes `state`, `snapshot`, `authenticate()`, `bootstrap()`, `createCharacter()`, and `travel()`.

- [x] **Step 1: Write failing store/client tests**

Cover Telegram exchange, Development fallback only in dev build, token never written to Web Storage, one `401` re-auth/retry, second `401` terminal error, bootstrap states, mutation disabling, and offline error.

- [x] **Step 2: Run focused frontend tests and confirm RED**

Run: `npm run test:unit -- --run src/__tests__/gameSession.spec.ts src/__tests__/apiClient.spec.ts`

- [x] **Step 3: Implement Telegram adapter and runtime-only client**

Keep the token in a closure/Pinia ref. Never call `localStorage` or `sessionStorage`. Re-authenticate only once per request chain and never automatically retry unsafe POST mutations unless their stable request ID is unchanged.

- [x] **Step 4: Implement the session state machine**

On app mount: authenticate, fetch bootstrap, then select character-creation or world state. Repeating bootstrap after reload reconstructs state entirely from the server response.

- [x] **Step 5: Run unit, lint, typecheck, and build**

Run:

- `npm run test:unit`
- `npm run lint`
- `npm run format:check`
- `npm run build`

- [x] **Step 6: Commit frontend session flow**

`git commit -m "feat: add Telegram frontend session flow"`

---

### Task 7: Mobile character creation and world travel UI

**Files:**
- Create: `web/elyndor-web/src/game/character/views/CharacterCreationView.vue`
- Modify: `web/elyndor-web/src/game/world/views/WorldView.vue`
- Modify: `web/elyndor-web/src/app/AppShell.vue`
- Modify: `web/elyndor-web/src/router/index.ts`
- Create: `web/elyndor-web/src/__tests__/CharacterCreationView.spec.ts`
- Create: `web/elyndor-web/src/__tests__/WorldView.spec.ts`
- Modify: `web/elyndor-web/e2e/game-shell.spec.ts`
- Modify: `web/elyndor-web/playwright.config.ts`
- Create: `tools/dev/Test-Elyndor.ps1`

**Interfaces:**
- Character form emits the exact Phase 1 request using a generated stable UUID retained for retries.
- World view renders `snapshot.character.location` and only `availableTransitions`; buttons call `store.travel(targetId)`.

- [ ] **Step 1: Write failing component tests**

Cover approved options, formal name validation hints, submit disabled while invalid/pending, server name conflict, all bootstrap states, transition disabled while pending, travel conflict refresh, and no arbitrary target input.

- [ ] **Step 2: Run component tests and confirm RED**

Run: `npm run test:unit -- --run src/__tests__/CharacterCreationView.spec.ts src/__tests__/WorldView.spec.ts`

- [ ] **Step 3: Implement the character creation screen**

Use the existing old-browser-MMORPG visual language and mobile safe areas. Present race/gender/class as explicit choices, show the exact name policy, preserve values after API errors, and prevent double submission.

- [ ] **Step 4: Replace the foundation placeholder with the real world screen**

Render danger, recommended level, server state, and transition actions. Handle pending, empty, conflict, offline, and re-authenticating states without pretending a travel succeeded.

- [ ] **Step 5: Update Playwright flow**

`Test-Elyndor.ps1` generates a positive per-run development Telegram ID, enables Development auth only for the child AppHost process, builds the Vue client, starts Aspire, waits for PostgreSQL/server health, sets `ELYNDOR_E2E_BASE_URL=http://127.0.0.1:5080`, runs Playwright, and stops AppHost in `finally`. The test creates a unique character, travels Town → Forest → Deep, reloads, and asserts Deep Forest is restored. Assert no console/page errors and use a Telegram-like mobile viewport.

- [ ] **Step 6: Run frontend checks and browser test**

Run all package scripts defined by `package.json`, then `npm run test:e2e` against the configured test runtime.

- [ ] **Step 7: Commit the playable Phase 1 UI**

`git commit -m "feat: add character and world frontend flow"`

---

### Task 8: Phase 1 verification, documentation, and gate

**Files:**
- Modify: `docs/source-of-truth/phases/PHASE_01_TELEGRAM_IDENTITY_WORLD.md`
- Modify: `docs/development/getting-started.md`
- Modify: `.github/workflows/ci.yml` if PostgreSQL Testcontainers requires explicit runner configuration
- Review: all Phase 1 code, migrations, tests, content, and frontend diff

**Interfaces:**
- Produces: verified Phase 1 DoD evidence and a clean entry gate for Phase 2 planning.

- [ ] **Step 1: Run the complete backend verification**

Run:

- `dotnet build Elyndor.slnx --configuration Release`
- `dotnet test Elyndor.slnx --configuration Release --no-build`
- `dotnet run --project tools/Elyndor.ContentValidator --configuration Release --no-build -- content/package.json`

- [ ] **Step 2: Verify migrations on an empty PostgreSQL database**

Start the actual Aspire/PostgreSQL runtime, apply migrations from zero, stop/restart, and confirm bootstrap restores the same character/location.

- [ ] **Step 3: Run complete frontend verification**

Run `npm ci`, `npm run lint`, `npm run format:check`, `npm run test:unit`, `npm run build`, and `npm run test:e2e` from `web/elyndor-web`.

- [ ] **Step 4: Run real-browser public smoke**

Use `tools/dev/Elyndor.ps1 -Action Start -Public`, Playwright with a 390×844 viewport, the public Tailscale URL, and inspect load, navigation, disabled/loading/error states, reload restoration, network failures, and console/page errors. PublicTest must not expose Development auth, OpenAPI, Vite, PostgreSQL, or Aspire Dashboard.

- [ ] **Step 5: Review security and concurrency evidence**

Review Critical/High/Medium/Low findings for Telegram validation, JWT configuration, log redaction, account/name uniqueness, transaction rollback, request replay, travel races, source-location trust, cancellation, and secrets.

- [ ] **Step 6: Verify repository hygiene**

Run Markdown link validation, secret scan, `git diff --check`, `git diff`, and `git status`. Remove only verified generated artifacts; do not remove PostgreSQL data volumes.

- [ ] **Step 7: Mark Phase 1 complete only if every DoD item has evidence**

If any item is blocked by missing Bot credentials, record it as blocked and keep Phase 1 in progress. Otherwise check each DoD item, update AGENTS current phase to Phase 2, and create the separate Phase 2 implementation plan.

- [ ] **Step 8: Commit Phase 1 gate evidence**

`git commit -m "docs: verify phase 1 definition of done"`
