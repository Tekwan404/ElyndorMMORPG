# Phase 1 — Telegram Identity, Character Creation, and First World

**Status:** Approved for implementation
**Owner:** Phase 1 execution contract
**Exit gate:** Phase 2 cannot begin until every applicable item in this document is verified.

## Outcome

A player authenticates through Telegram, receives a short-lived Elyndor session, creates one Warrior, Archer, or Mage, enters Starter Town, travels only through valid world links, and restores the same authoritative state after reload or reconnect.

## Source of Truth

- `docs/source-of-truth/phases/ELYNDOR_PHASES_0-5.md`
- `docs/source-of-truth/architecture/00_DEVELOPMENT_STACK.md`
- `docs/source-of-truth/gameplay/01_TIME_SYSTEM.md`
- `docs/source-of-truth/gameplay/04_WORLD_AND_LOCATIONS_SYSTEM.md`
- `docs/source-of-truth/gameplay/05_CHARACTER_SYSTEM.md`
- `docs/source-of-truth/gameplay/12_CLASS_SYSTEM.md`
- `docs/source-of-truth/gameplay/19_CLASS_ROSTER_AND_CHARACTER_CREATION.md`
- `docs/source-of-truth/ui/UI_01_GLOBAL_GAME_SHELL.md`
- `docs/source-of-truth/ui/UI_02_WORLD_AND_LOCATION.md`
- `docs/source-of-truth/ui/UI_19_SETTINGS_AND_SYSTEM_STATES.md`

This phase document owns execution boundaries and API/persistence decisions. The linked system documents continue to own gameplay rules.

## Scope

Phase 1 includes:

- Telegram `initData` exchange and Development-only test authentication;
- idempotent Account resolution;
- short-lived JWT access tokens and frontend re-authentication after `401`;
- atomic creation of one character per account;
- Human and Undead races, Male and Female genders, and Warrior/Archer/Mage classes;
- three versioned locations and server-authoritative travel;
- persistent bootstrap/reconnect state;
- mobile-first Vue flows and real Playwright verification.

Phase 1 excludes stats/resources beyond identity-level class selection, abilities, combat, inventory, loot, quests, parties, Redis, and background scheduling.

## Authentication and session

```text
Telegram Mini App
→ raw initData
→ POST /api/v1/auth/telegram
→ server validates hash and auth_date
→ Account lookup/create
→ short-lived Elyndor JWT
```

- Telegram identity is accepted only from validated raw `initData`.
- The Bot Token exists only in environment variables or .NET user secrets.
- JWT lifetime is 15 minutes. There is no refresh token in Phase 1.
- JWT contains the Elyndor Account ID as subject; Telegram profile data is not treated as authorization state.
- Frontend stores JWT only in runtime memory and never in `localStorage`, `sessionStorage`, cookies, or IndexedDB.
- A `401` clears the in-memory token, repeats the Telegram exchange, and retries the original safe bootstrap request once. A repeated `401` enters an explicit authentication error state.
- Development authentication is registered only when the environment is `Development` and `Authentication:Development:Enabled` is explicitly true. It is disabled in PublicTest and Production.

### Authentication API

```text
POST /api/v1/auth/telegram
Request:  { initData: string }
Response: { accessToken: string, expiresAtUtc: string }

POST /api/v1/auth/development
Request:  {}
Response: { accessToken: string, expiresAtUtc: string }
```

The development endpoint uses a configured positive Telegram test ID and never accepts an identity supplied by the browser.

## Account persistence

```text
Account
- Id: uuid
- TelegramUserId: bigint, unique
- CreatedAtUtc: timestamptz
- LastSeenAtUtc: timestamptz
```

Account resolution uses a PostgreSQL unique constraint on `TelegramUserId`. Concurrent first logins converge on one Account. `LastSeenAtUtc` uses injected `TimeProvider` and is updated inside the resolution transaction.

## Character creation

One account owns at most one prototype character.

```text
Character
- Id: uuid
- AccountId: uuid, unique FK
- CreationRequestId: uuid, unique
- Name: varchar(16)
- NormalizedName: varchar(16), unique
- RaceId: HUMAN | UNDEAD
- GenderId: MALE | FEMALE
- ClassId: WARRIOR | ARCHER | MAGE
- Level: 1
- CreatedAtUtc: timestamptz
```

Creation and initial location are committed in one PostgreSQL transaction. An exact retry with the same `CreationRequestId` and payload returns the existing character. Reuse of the key with different payload returns `idempotency_conflict`. A second creation key for an account that already owns a character returns `character_already_exists`.

### Formal character-name contract

The submitted name must already be in its intended display form; the server does not silently trim it.

- Normalize with Unicode Normalization Form KC before validation and persistence.
- Length is 3–16 Unicode scalar values, including separators.
- Letters must all belong to either the Latin script or the Cyrillic script; mixed scripts are rejected.
- The only separators are U+0020 SPACE and U+002D HYPHEN-MINUS.
- A separator cannot be first or last.
- Two separators cannot be adjacent, including different separator types.
- Digits, emoji, control characters, punctuation other than the hyphen, combining-only sequences, and other scripts are rejected.
- `NormalizedName` is the Form-KC value converted with invariant uppercase.
- PostgreSQL has a unique index on `NormalizedName`; application checks are for friendly errors, not the final uniqueness guarantee.

### Character API

```text
GET  /api/v1/me
GET  /api/v1/character
POST /api/v1/character

POST body:
{
  "requestId": "uuid",
  "name": "string",
  "raceId": "HUMAN|UNDEAD",
  "genderId": "MALE|FEMALE",
  "classId": "WARRIOR|ARCHER|MAGE"
}
```

## First world content

Locations are versioned static content. Permanent player location is PostgreSQL state.

```text
STARTER_TOWN       SAFE       recommended level 1
WHISPERING_FOREST  ADVENTURE  recommended level 1
DEEP_FOREST        DANGEROUS  recommended level 3

STARTER_TOWN ↔ WHISPERING_FOREST ↔ DEEP_FOREST
```

The server returns current location and outgoing transitions from its loaded content version. The client submits only the target and a stable request ID; it never submits the authoritative source location.

## Travel concurrency and idempotency

```text
POST /api/v1/world/travel
{ "requestId": "uuid", "targetLocationId": "string" }
```

`CharacterLocation` contains `CharacterId`, `LocationId`, `Version`, and `UpdatedAtUtc`. Travel executes in a transaction:

1. Load the actual current location and current version.
2. Check the target against outgoing transitions from that actual location.
3. Record a `TravelOperation` identified by `(CharacterId, RequestId)`.
4. Apply an atomic conditional update for the loaded version and increment `Version`.
5. Commit the operation result and location together.

If another request changed the row first, the loser reloads authoritative state and returns `travel_conflict`; it does not revalidate against client history. Repeating a completed request ID returns its stored result. Reusing a request ID with a different target returns `idempotency_conflict`.

The two-tab case `Starter Town → Forest` racing with `Starter Town → Deep Forest` permits only the transition validated against the state that wins the conditional update. Deep Forest is never reachable directly from Starter Town.

## Bootstrap and reconnect

```text
GET /api/v1/bootstrap
```

The authenticated snapshot contains Account ID, optional Character identity, current location, allowed transitions, server UTC time, and content/balance versions. Reload repeats Telegram authentication, obtains a new JWT, and fetches this snapshot. PostgreSQL is the permanent source of truth.

SignalR may be added only for connection-state and snapshot delivery required by the final reconnect flow; HTTP bootstrap remains the recovery baseline.

## Frontend states

The Vue application has explicit states:

```text
booting
authenticating
authentication error
character missing
creating character
character validation error
world ready
travel pending
travel conflict/error
re-authenticating
offline/reconnect
```

The character-creation screen exposes only the approved race, gender, and class options. The world screen renders current location and server-provided transitions. Controls are disabled while their mutation is pending.

## Persistence boundary for later combat

```text
Permanent character state → PostgreSQL
Active combat runtime state → authoritative single-writer CombatSession
Persistence → defined checkpoints, combat completion, and recovery policy
```

Authoritative state does not imply writing HP, Rage, Focus, or Mana to PostgreSQL on every combat action. Phase 1 introduces no combat runtime state.

## Failure and security cases

- Invalid, stale, future-dated, duplicated, or malformed Telegram data is rejected with stable error codes.
- Missing signing configuration fails closed; secrets never appear in logs or API responses.
- Expired JWT returns `401` and triggers the bounded re-auth flow.
- Duplicate Account, character name, account-character, creation request, and travel request races are resolved by database constraints/transactions.
- Invalid class/race/gender/location identifiers are rejected server-side.
- Cancellation before commit changes nothing; a committed mutation remains discoverable through its request ID.
- API errors expose stable codes and correlation IDs, not internal exceptions.

## Testing contract

- Unit: character-name policy and content/world transition rules.
- PostgreSQL integration: migrations, unique constraints, concurrent Account resolution, concurrent names, character idempotency, and travel races.
- API: Telegram/development auth boundaries, JWT authorization/expiry, character creation, bootstrap, and travel errors.
- Frontend: auth retry, creation validation, loading/disabled/error states, and bootstrap restoration.
- Playwright: Telegram-like mobile viewport from first boot through character creation, travel, reload, and restored location.

## Definition of Done

- [ ] Telegram auth endpoint validates real protocol fixtures and issues a 15-minute JWT.
- [ ] Invalid `initData` is rejected and Development auth is absent outside Development.
- [ ] Expired JWT performs one clean Telegram re-authentication attempt without persistent token storage.
- [ ] Account creation is idempotent under concurrency.
- [ ] Character creation is atomic, idempotent, and protected by PostgreSQL uniqueness.
- [ ] Human/Undead, Male/Female, and Warrior/Archer/Mage are available.
- [ ] Formal character-name policy is covered by tests.
- [ ] Starter Town, Whispering Forest, and Deep Forest are versioned content.
- [ ] Travel is server-authoritative, idempotent, and concurrency-safe.
- [ ] Reload/reconnect restores the authoritative character and location.
- [ ] Backend build/tests and PostgreSQL integration tests pass.
- [ ] Frontend lint/typecheck/unit/build checks pass.
- [ ] The complete mobile flow passes in a real Playwright browser.
- [ ] Diff review finds no secrets, development-auth exposure, or Phase 2 scope creep.
