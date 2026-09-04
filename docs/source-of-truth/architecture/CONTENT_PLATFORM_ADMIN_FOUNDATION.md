# Content Platform and Admin Foundation

Status: source of truth for the current Elyndor content administration foundation.

## Runtime model

Gameplay reads content only through `IContentSnapshotProvider`. A published change is built and
validated as a complete `GameContentPackage`, then activated by an atomic snapshot swap.

A combat session captures the concrete content snapshot used at creation. Publishing a newer
balance never changes an already-running combat.

## Revision model

Elyndor currently uses immutable whole-package revisions intentionally.

```text
edit
  -> validate
  -> immutable ContentRevision
  -> ContentRelease
  -> atomic runtime activation
```

A revision payload is canonical JSON with SHA-256 integrity metadata. Revisions are never edited in
place. A release points at one revision. Rollback creates a new release that points at an older
revision; it never deletes history.

Whole-package revisions are preferred for the current single-instance game because they make
cross-reference validation, deterministic rollback, and version-pinned combat simple. Per-entity
storage must not be introduced until scale or collaboration requirements justify the additional
composition complexity.

## Concurrency contract

Admin writes use optimistic live-content guards.

- Creating a draft requires the SHA-256 of the live package it was based on.
- Publishing requires the SHA-256 of the live package the administrator expects.
- The publish check runs inside the single publication coordinator gate immediately before release
  creation and runtime activation.
- A stale editor receives HTTP 409 and must review/rebase instead of overwriting newer live content.

This is separate from player `MutationId` idempotency. Admin publication is append-only and guarded
by expected state; gameplay mutations are retry-safe per character.

## Authorization

The normal short-lived account JWT is also the Admin authentication token. Telegram users in
`Administration:Telegram:AllowedUserIds` receive the `SUPER_ADMIN` role claim.

`/api/v1/admin/content/*` requires the server-side `elyndor-content-admin` authorization policy.
Frontend visibility is convenience only and never an authorization boundary.

Audit actors are derived from authenticated claims, preferring the Telegram user id.

## Admin API

The supported content lifecycle endpoints are:

```text
GET  /api/v1/admin/content/current
POST /api/v1/admin/content/validate
POST /api/v1/admin/content/revisions
GET  /api/v1/admin/content/revisions/{revisionId}
GET  /api/v1/admin/content/history
POST /api/v1/admin/content/revisions/{revisionId}/publish
POST /api/v1/admin/content/releases/{releaseId}/rollback
```

There is deliberately no endpoint that mutates live content directly.

## Admin UI

`/admin` is a minimal operational UI, not a second game client. It provides:

- live content/balance identity and payload hash;
- structured form editing for common Monster, Ability, and Item balance fields;
- creation of new Monster and Item entities inside the local draft, including a dedicated basic AI profile for new monsters;
- category/entity JSON editing for advanced or not-yet-structured fields;
- full-package JSON fallback;
- server validation errors;
- immutable draft creation;
- read-only revision detail and an entity-aware diff against current LIVE before publish confirmation;
- revision publishing;
- release history and confirmed rollback.

The UI may become richer later, but it must continue to use the same API and publication invariants.

## Content files

The repository content source uses only:

```text
content/package.json
content/<category>/*.json
```

Feature-named root overlays and bespoke per-feature loader methods are prohibited. The file loader,
PostgreSQL importer, validator, Admin, and runtime must converge on the same composed package.

## Scaling boundary

Redis, microservices, distributed invalidation, and per-entity revision databases are intentionally
out of scope for the current single-instance modular monolith. If Elyndor becomes multi-instance,
publish propagation must gain a durable version notification mechanism before horizontal scaling is
considered safe.
