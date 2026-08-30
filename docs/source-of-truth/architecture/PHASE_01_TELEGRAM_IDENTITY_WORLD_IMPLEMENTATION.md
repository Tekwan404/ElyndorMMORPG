# Phase 1 — Telegram Identity, Character, Time, and World

**Status:** In progress
**Implementation:** Phase 0 Definition of Done is verified. Execute this phase as small tested vertical slices; do not begin Phase 2+.

## Current execution block

1. [x] Provide a one-command public-test launcher using the approved ASP.NET single-origin topology and Tailscale Funnel.
2. [x] Implement and verify the server-side Telegram `initData` validation boundary.
3. [x] Do not issue Elyndor sessions or create accounts until the next persistence slice defines their transaction and token policies.

The launcher was verified on the real local toolchain with ASP.NET serving the Vue production build, Tailscale Funnel proxying only port 5080, and Playwright using a 390 x 844 public HTTPS browser viewport. The identity boundary is library-level until the next persistence slice can create accounts and sessions atomically.

## Source of Truth

- `docs/source-of-truth/architecture/00_DEVELOPMENT_ROADMAP.md`
- `docs/source-of-truth/architecture/00_DEVELOPMENT_STACK.md`
- `docs/source-of-truth/gameplay/01_TIME_SYSTEM.md`
- `docs/source-of-truth/gameplay/04_WORLD_AND_LOCATIONS_SYSTEM.md`
- `docs/source-of-truth/gameplay/05_CHARACTER_SYSTEM.md`
- `docs/source-of-truth/gameplay/12_CLASS_SYSTEM.md`
- `docs/source-of-truth/gameplay/19_CLASS_ROSTER_AND_CHARACTER_CREATION.md`
- `docs/source-of-truth/ui/UI_01_GLOBAL_GAME_SHELL.md`
- `docs/source-of-truth/ui/UI_02_WORLD_AND_LOCATION.md`
- `docs/source-of-truth/ui/UI_19_SETTINGS_AND_SYSTEM_STATES.md`

## Planned vertical slice

```text
Telegram initData
→ server-side validation
→ Account resolution
→ character creation
→ persistent starting location
→ world bootstrap
→ travel
→ reconnect and restored authoritative snapshot
```

## Required safeguards

- Validate Telegram `initData` signature/hash and `auth_date` on the backend using the official protocol.
- Never accept Telegram user identity from a normal frontend field.
- Keep Bot Token and signing material in environment/user secrets only.
- Register development auth only in `Development` and behind an explicit flag; never enable it for Tailscale Funnel or Production.
- Make character creation atomic and define a database-backed duplicate-name policy.
- Use UTC and injected `TimeProvider`; the client never owns time.
- Persist location and restore it after reconnect/restart.
- Return stable machine-readable API errors.
- Test retries, duplicate character creation, invalid/stale Telegram data, database constraints, reconnect, and restart.

### Telegram validation policy

- Bot-token HMAC validation follows the official Telegram Mini Apps protocol.
- The default accepted age is five minutes; configuration may shorten it but must not disable freshness checks.
- Timestamps more than 30 seconds in the future are rejected as clock-skew/replay risk.
- Duplicate query keys, malformed hashes, invalid `auth_date`, missing/invalid user JSON, and non-positive Telegram user IDs are rejected.
- Hash comparison is constant-time. Bot tokens are supplied only through environment variables or .NET user secrets.

## Local public-test launcher

Public Telegram testing uses one origin:

```text
Vue production build
→ ASP.NET Core static files + /api on http://127.0.0.1:5080
→ Tailscale Funnel HTTPS
→ Telegram Mini App URL
```

The launcher must:

- verify .NET, Node/npm, Docker, Aspire, and Tailscale prerequisites;
- build the Vue client before public exposure;
- start the AppHost and wait for PostgreSQL and Server health;
- expose only port 5080 through Funnel;
- never expose PostgreSQL, Aspire Dashboard, Vite HMR, secrets, or development-auth endpoints;
- print local and public URLs and provide deterministic Start, Stop, and Status actions;
- turn Funnel off on normal Stop unless explicitly told to keep it.

## Exit condition

A player can authenticate through Telegram, create one valid Warrior/Archer/Mage character, enter the starting location, travel to an allowed location, reload/reconnect, and see the same authoritative state in a Telegram-like mobile browser flow.
