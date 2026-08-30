# Phase 1 Identity and Public-Test Launcher Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use Superpowers `executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide a safe Tailscale public-test launcher and the first verified Telegram identity boundary without implementing future Phase 1 persistence prematurely.

**Architecture:** ASP.NET Core serves the built Vue SPA and API from port 5080; Tailscale Funnel terminates public HTTPS and proxies only that port. Telegram `initData` validation lives at the Infrastructure boundary, uses `TimeProvider`, performs constant-time HMAC verification, and returns typed results without creating accounts or sessions.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core/PostgreSQL through Aspire, Vue 3/Vite, PowerShell, Tailscale Funnel, xUnit, Playwright.

**Spec:** `docs/source-of-truth/architecture/PHASE_01_TELEGRAM_IDENTITY_WORLD_IMPLEMENTATION.md`

## Global Constraints

- Keep ASP.NET Core/.NET, EF Core, PostgreSQL, Vue 3, TypeScript, Vite, Aspire, and OpenTelemetry.
- PostgreSQL remains permanent truth; Redis is not introduced.
- Funnel is development/test ingress only and exposes only the ASP.NET single origin.
- Development auth remains disabled in public mode.
- Bot tokens and signing material never enter Git or frontend configuration.
- Phase 2+ is out of scope.

---

### Task 1: Documentation layout and path integrity

**Files:**
- Move: root Source of Truth Markdown into `docs/source-of-truth/architecture`, `gameplay`, and `ui`
- Move: historical audit files into `docs/archive`
- Modify: `AGENTS.md`, `README.md`, `.agents/skills/elyndor-*/SKILL.md`
- Create: `docs/source-of-truth/README.md`

**Interfaces:**
- Produces: stable repo-relative Source of Truth paths consumed by Codex skills and phase plans.

- [x] Move files without overwriting destinations.
- [x] Rewrite bare Markdown references to repo-relative paths.
- [x] Verify every referenced Markdown path exists and no active document points to the removed `references/` tree.

### Task 2: Single-origin server hosting with a regression test

**Files:**
- Modify: `src/Elyndor.Server/Program.cs`
- Modify: `src/Elyndor.Server/Properties/launchSettings.json`
- Modify: `web/elyndor-web/vite.config.ts`
- Test: `tests/Elyndor.IntegrationTests/System/StaticFrontendTests.cs`

**Interfaces:**
- Consumes: `Frontend:DistPath`, defaulting to `web/elyndor-web/dist` from the repository checkout.
- Produces: `GET /world` returning the built SPA from `http://127.0.0.1:5080` while `/api/*` remains server-owned.

- [x] Add an integration test that requests `/world` and expects HTML containing `Elyndor`.
- [x] Run the focused test and verify it fails with 404 before implementation.
- [x] Add static-file/default-file/fallback routing only when the validated dist directory exists.
- [x] Change the standalone HTTP development port and Vite fallback proxy to 5080.
- [x] Build Vue, rerun the focused test, and verify it passes.

### Task 3: Tailscale launcher

**Files:**
- Create: `tools/dev/Elyndor.ps1`
- Create: `Start-Elyndor.cmd`
- Create: `Stop-Elyndor.cmd`
- Modify: `.gitignore`
- Modify: `docs/development/getting-started.md`

**Interfaces:**
- Produces: `Elyndor.ps1 -Action Start|Stop|Status [-Public] [-Open]`.
- Uses: Aspire CLI `start`, `wait`, `describe`, `stop`; Tailscale CLI `funnel --bg --yes 5080`, `funnel status --json`, and `funnel --https=443 off`.

- [x] Validate workspace-contained paths and prerequisite executables before starting anything.
- [x] Build the Vue client for public mode and start Aspire non-interactively.
- [x] Wait on PostgreSQL, game database, and server health without fixed sleeps.
- [x] Configure Funnel only after local HTTP health succeeds and print the stable public URL.
- [x] Stop Funnel and AppHost deterministically without committing runtime metadata.
- [x] Run Start, Status, public curl/browser smoke, and Stop against the real local environment.

### Task 4: Telegram initData validation with TDD

**Files:**
- Create: `src/Elyndor.Infrastructure/Identity/Telegram/TelegramInitDataValidator.cs`
- Create: `src/Elyndor.Infrastructure/Identity/Telegram/TelegramInitDataValidationResult.cs`
- Test: `tests/Elyndor.IntegrationTests/Identity/TelegramInitDataValidatorTests.cs`

**Interfaces:**
- Produces: `TelegramInitDataValidationResult Validate(string initData, string botToken, TimeSpan maxAge, TimeSpan maxFutureSkew)`.
- Returns: validated Telegram user ID and authentication timestamp, or a stable error code.

- [x] Add tests for valid data, invalid hash, expired/future timestamps, duplicate keys, malformed hash/auth date, missing user, invalid JSON, and non-positive user ID.
- [x] Run focused tests and verify compile failure because the validator does not exist.
- [x] Implement strict parsing, Telegram HMAC derivation, fixed-time comparison, JSON user extraction, and `TimeProvider` freshness checks.
- [x] Rerun focused and full backend tests.

### Task 5: Full verification and review

**Files:**
- Modify: `.github/workflows/ci.yml` only if new repository commands require it.
- Review: complete Git diff and Phase 1 checklist.

**Interfaces:**
- Produces: evidence for the completed execution block and a scoped list of remaining Phase 1 work.

- [x] Run Release build, all .NET tests, and content validation.
- [x] Run frontend lint, formatting, unit tests, build, and Playwright E2E.
- [x] Run launcher/Tailscale single-origin smoke in a real browser and inspect console errors/mobile viewport.
- [x] Run Markdown path validation, secret scan, `git diff --check`, and `git status`.
- [x] Review findings by Critical/High/Medium/Low and fix all actionable findings in this block.
