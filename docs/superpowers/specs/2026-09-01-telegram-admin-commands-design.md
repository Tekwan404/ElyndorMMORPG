# Telegram Admin Commands Design

**Status:** Approved by the project owner on 2026-09-01
**Scope:** Minimal owner-only administration through the Elyndor Telegram bot
**Owning runtime:** `Elyndor.Server` modular monolith

## Goal

Allow the project owner to inspect and mutate prototype character state and send individual bot messages by writing commands in a private Telegram chat with the Elyndor bot. No browser admin panel, public admin HTTP API, broadcast system, or separate bot worker is introduced.

## Entry flow

```text
Owner private message
→ Telegram Bot API webhook
→ webhook secret validation
→ private-chat and owner allowlist validation
→ update idempotency check
→ typed command parsing
→ authoritative application service
→ PostgreSQL transaction and audit
→ Telegram response
```

Telegram delivers only `message` updates to:

```text
POST /api/v1/administration/telegram/webhook
```

The endpoint does not use player JWT authentication. It requires the exact `X-Telegram-Bot-Api-Secret-Token` configured by the launcher, a private chat, and a sender ID present in the local administration allowlist. Invalid webhook secrets receive `401`; non-owner or non-private messages receive `200` without executing or disclosing admin behavior.

## Runtime configuration

The existing encrypted launcher secrets gain:

- a cryptographically random webhook secret;
- an owner Telegram user ID allowlist containing `732707324` for the current operator environment.

The owner ID is runtime configuration and is not committed as an application default. Existing installations are upgraded through the launcher configuration flow. Bot token, signing key, and webhook secret never enter Git, frontend assets, logs, URLs, or API responses.

On public Start/Restart, after Tailscale Funnel is available and the server is healthy, the launcher calls `setWebhook` with:

- the public HTTPS webhook URL;
- `secret_token`;
- `allowed_updates = ["message"]`;
- `drop_pending_updates = true`.

On Stop, the launcher calls `deleteWebhook` with `drop_pending_updates = true` before disabling the Funnel. Commands sent while Elyndor is offline are intentionally discarded so that stale destructive commands cannot execute after a later restart.

## Command contract

Commands are case-insensitive. IDs and enum-like content identifiers use invariant parsing. Success and failure responses are short Russian messages with stable machine-readable result codes in parentheses.

```text
/help
/char <telegramUserId>
/level <telegramUserId> <1-60>
/restore <telegramUserId>
/location <telegramUserId> <locationId>
/rename <telegramUserId> <new character name>
/class <telegramUserId> <classId>
/race <telegramUserId> <raceId>
/delete <telegramUserId> <exact character name> CONFIRM
/msg <telegramUserId> <text>
```

`/help` documents only supported commands. Unknown commands return `admin_command_unknown`.

`/char` returns account and character identifiers, display name, race, class, level, current/max HP, resource type and current/max value, and current location. It does not return secrets or internal exception details.

`/level` accepts levels 1 through 60. It recalculates authoritative derived stats from current content. Current HP and resource preserve their old fill percentages against the new maxima and are clamped.

`/restore` recalculates authoritative maxima and restores HP and the class action resource to full.

`/location` accepts only a location ID in the loaded content package. It changes the authoritative location directly, increments the location concurrency version, and records the current UTC time. It is an administrative relocation and does not require a normal world transition edge.

`/rename` uses the existing `CharacterNamePolicy` and database unique normalized-name constraint. The remainder after the Telegram user ID is treated as the requested name so supported internal spaces remain possible.

`/class` accepts only an existing class content definition. It recalculates stats and changes the action resource profile. HP and resource preserve their previous fill percentages and are clamped to the new maxima.

`/race` accepts only an existing race content definition. Race currently has no direct stat mutation.

`/delete` requires the target Telegram user ID, the current character name, and the final literal `CONFIRM`. The name must match the current normalized name. The character is deleted in one PostgreSQL transaction; existing cascade rules delete vitals, location, and travel operations. The account remains so the player can create a new character.

`/msg` sends a text message through the configured bot to the target Telegram user ID. Text must be 1–4096 characters. The message can be delivered only when Telegram permits the bot to contact the target, normally after the user has started or interacted with the bot. Broadcast is explicitly out of scope.

## Domain mutation rules

Character mutations are expressed through explicit domain methods rather than public setters or raw SQL. The administration application service owns content validation, stat/resource recalculation, transaction boundaries, concurrency handling, and stable result codes.

Level/class changes use the existing `CharacterStatCalculator` and `ResourceProfile`. Percentage preservation uses:

```text
newCurrent = round(newMax × oldCurrent / oldMax)
```

with zero handled explicitly and final values clamped by the owning resource/vitals rules. No client-provided derived stat, max HP, resource maximum, normalized name, or resulting location version is trusted.

## Idempotency and concurrency

Every Telegram `update_id` is globally unique in a new administration audit table. Character mutation and successful audit completion occur in the same PostgreSQL transaction. A duplicate update returns the stored command result without applying the mutation again.

Character and dependent rows are loaded inside the transaction. Location changes use the existing concurrency token. Database uniqueness remains the last guarantee for renames. Concurrency or unique conflicts return stable admin error codes and never expose raw database exceptions.

For `/msg`, the audit row is persisted before the external Telegram send. A duplicate update does not blindly send the same user message twice. If the outbound request has an ambiguous timeout, the audit reports `admin_message_delivery_unknown`; automatic retry is not performed. This chooses at-most-once behavior over accidental duplicate messaging.

## Audit

The PostgreSQL audit record contains:

- Telegram update ID, unique;
- administrator Telegram user ID;
- command name;
- target Telegram user ID when present;
- result code;
- sanitized result summary;
- received/completed UTC timestamps;
- delivery state for outbound messages.

It does not store bot tokens, webhook secrets, signing keys, raw exception details, or `/msg` body text. Audit rows do not reference character/account rows with foreign keys so deletion cannot erase administrative history.

## Telegram response behavior

Mutation results are committed before the success response is sent. If the response to the owner fails, a webhook retry reads the audit result and may resend the owner-facing result without repeating the character mutation.

Malformed commands receive usage guidance. Missing accounts or characters, invalid content IDs, name conflicts, validation failures, concurrency conflicts, and Telegram delivery failures use separate stable codes.

The webhook returns a successful HTTP status for syntactically valid Telegram updates after they have been safely accepted or rejected. It returns an error status only when Telegram should retry due to a transient server/database failure.

## Module boundaries

- `Elyndor.Core`: explicit character/vitals/location mutation methods and administration audit domain record.
- `Elyndor.Infrastructure`: EF configuration/migration and transactional administration service.
- `Elyndor.Server`: webhook DTOs, secret/owner validation, command parser, Telegram Bot API client, endpoint registration, and configuration.
- `tools/dev/Elyndor.ps1`: encrypted administration configuration and webhook registration/removal.
- Vue frontend: no administration code or secrets.

No generic repository, CQRS framework, Redis, message broker, separate service, or public Swagger admin key is introduced.

## Verification scope

Minimum meaningful automated coverage:

- invalid webhook secret cannot execute a command;
- non-owner sender cannot execute a command;
- duplicate update cannot repeat a mutation;
- rename preserves name rules and uniqueness;
- level/class mutations recalculate and clamp authoritative vitals;
- deletion keeps Account and removes Character dependents;
- `/msg` does not store message text in audit;
- launcher-generated webhook secret is not committed or printed.

An actual private Telegram smoke test verifies `/help`, `/char`, one reversible mutation, and one `/msg` delivery. Destructive `/delete` is integration-tested against a disposable account rather than the owner's live character.

## Out of scope

- browser administration UI;
- broadcast or mass messaging;
- arbitrary SQL or arbitrary property mutation;
- economy, item, XP, loot, talent, or combat-session admin commands;
- role hierarchy or multiple permission levels;
- admin commands in group chats;
- processing commands sent while the server is offline;
- separate bot worker or queue infrastructure.
