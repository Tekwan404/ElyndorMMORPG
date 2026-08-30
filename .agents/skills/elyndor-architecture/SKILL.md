---
name: elyndor-architecture
description: Review or implement Elyndor backend, infrastructure, EF Core, PostgreSQL, SignalR, scheduling, or persistence changes. Use for architecture decisions, database work, concurrency, reliability, and observability.
---

# Elyndor Architecture

Read `AGENTS.md`, `docs/source-of-truth/architecture/00_DEVELOPMENT_STACK.md`, `docs/source-of-truth/architecture/00_COMPATIBILITY_MATRIX.md`, and the current phase document before changing architecture.

## Architecture check

For every mutation or runtime component, make these decisions explicit:

- Which module owns the authoritative state and rule?
- What is the PostgreSQL transaction boundary?
- What idempotency key or unique constraint prevents duplicate execution?
- What concurrency model prevents races or double spending?
- What state is persistent, cached, or in-memory, and why?
- What happens on retry, disconnect, process restart, partial failure, and cancellation?
- How is an authoritative snapshot restored on reconnect?
- Does dependency direction keep Core free from EF Core, Redis, SignalR, Telegram, and HTTP?
- Which logs, traces, metrics, and correlation identifiers make failures diagnosable?

## Guardrails

- PostgreSQL is permanent truth; Redis is an optional cache/scale helper only.
- Prefer one `GameDbContext` and local transactions while the modular monolith shares one database.
- Combat uses one writer per session and does not keep a DbContext open for the fight.
- Quartz is for durable coarse schedules, not combat timers or a message queue.
- Use outbox only when a durable post-commit effect can otherwise be lost.
- Reject premature microservices, brokers, distributed actors, event sourcing, generic repositories, or abstractions without a present benefit.
