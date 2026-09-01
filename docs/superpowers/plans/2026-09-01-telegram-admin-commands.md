# Telegram Admin Commands Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add owner-only Telegram commands for typed character administration and individual bot messages.

**Architecture:** Telegram posts signed updates to one webhook in `Elyndor.Server`. A small parser creates typed commands, an Infrastructure service performs content-validated PostgreSQL mutations with an idempotent audit record, and a typed Bot API client sends replies. The launcher owns encrypted webhook configuration and webhook lifecycle.

**Tech Stack:** ASP.NET Core/.NET, EF Core, PostgreSQL, `TimeProvider`, Telegram Bot API, PowerShell launcher.

**Spec:** `docs/superpowers/specs/2026-09-01-telegram-admin-commands-design.md`

## Global Constraints

- Only private messages from configured owner Telegram IDs may execute commands.
- Every state mutation is server-authoritative, transactional, content-validated, and idempotent by Telegram `update_id`.
- Secrets remain in local encrypted launcher configuration and environment variables.
- No frontend admin surface, broadcast, Redis, worker service, generic repository, or arbitrary property mutation.
- Existing uncommitted Talent UI files are preserved and excluded from backend commits.

---

### Task 1: Typed command parser and webhook security

**Files:**
- Create: `src/Elyndor.Server/Administration/TelegramAdminOptions.cs`
- Create: `src/Elyndor.Server/Administration/TelegramAdminModels.cs`
- Create: `src/Elyndor.Server/Administration/TelegramAdminCommandParser.cs`
- Test: `tests/Elyndor.UnitTests/Administration/TelegramAdminCommandParserTests.cs`

**Interfaces:**
- Produces `TelegramAdminCommandParser.Parse(string)` returning `AdminCommandParseResult` with a typed `AdminCommand` or stable error code.
- Produces options containing webhook secret and allowed owner IDs.

- [ ] Write a parser test proving names/message bodies preserve spaces and `/delete` requires final `CONFIRM`.
- [ ] Run the focused test and confirm it fails because parser types are missing.
- [ ] Implement command records, validation limits, stable codes, and parser.
- [ ] Run the focused test and confirm it passes.

### Task 2: Authoritative mutations, audit, and migration

**Files:**
- Modify: `src/Elyndor.Core/Characters/Character.cs`
- Modify: `src/Elyndor.Core/Characters/CharacterVitals.cs`
- Modify: `src/Elyndor.Core/World/CharacterLocation.cs`
- Create: `src/Elyndor.Core/Administration/AdminCommandAudit.cs`
- Create: `src/Elyndor.Infrastructure/Administration/TelegramAdministrationService.cs`
- Modify: `src/Elyndor.Infrastructure/Persistence/GameDbContext.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Configurations/AdminCommandAuditConfiguration.cs`
- Generate: `src/Elyndor.Infrastructure/Persistence/Migrations/*TelegramAdministration.cs`
- Test: `tests/Elyndor.IntegrationTests/Administration/TelegramAdministrationServiceTests.cs`

**Interfaces:**
- Consumes parsed `AdminCommand`, content package, `GameDbContext`, and `TimeProvider`.
- Produces `ExecuteAsync(updateId, administratorId, command, cancellationToken)` with stable result code/text and duplicate replay behavior.

- [ ] Write focused PostgreSQL integration coverage for duplicate mutation and delete-account preservation.
- [ ] Run it and confirm the missing service/audit failure.
- [ ] Add explicit domain mutation methods, audit entity/configuration, and transactional service dispatch.
- [ ] Generate the EF Core migration and inspect cascade/index/UTC fields.
- [ ] Run the focused integration coverage and confirm it passes.

### Task 3: Telegram webhook and Bot API client

**Files:**
- Create: `src/Elyndor.Server/Administration/TelegramBotClient.cs`
- Create: `src/Elyndor.Server/Administration/TelegramAdminEndpoints.cs`
- Modify: `src/Elyndor.Server/Program.cs`
- Modify: server/infrastructure DI registration file used by the repository.
- Test: `tests/Elyndor.IntegrationTests/Administration/TelegramAdminEndpointTests.cs`

**Interfaces:**
- `POST /api/v1/administration/telegram/webhook` accepts Bot API message updates.
- `TelegramBotClient.SendMessageAsync(chatId, text, cancellationToken)` is the only outbound message boundary.

- [ ] Write endpoint tests proving a wrong secret and non-owner sender cannot call the administration service.
- [ ] Run them and confirm the endpoint is absent.
- [ ] Implement constant-time secret validation, private-owner validation, parsing, dispatch, and short Russian replies.
- [ ] Run focused endpoint tests and confirm they pass.

### Task 4: Launcher lifecycle and verification

**Files:**
- Modify: `tools/dev/Elyndor.ps1`
- Modify locally only: `.elyndor/launcher-secrets.json`
- Modify: `docs/development/getting-started.md`

**Interfaces:**
- Public Start/Restart registers the webhook after Funnel health.
- Stop removes the webhook before Funnel shutdown.
- Existing encrypted secret files are upgraded without printing secret values.

- [ ] Add webhook secret/owner configuration with backward-compatible local secret upgrade.
- [ ] Register `setWebhook` with only message updates and remove it with pending-update cleanup on Stop.
- [ ] Run focused unit/integration tests, `dotnet build`, migration/content validation, and PowerShell syntax validation.
- [ ] Restart Elyndor, call `getWebhookInfo`, and smoke-test `/help`, `/char`, and a harmless owner message through Telegram.
- [ ] Review `git diff`, secret scan, and audit the changed files before completion.
