# Elyndor content refresh and inventory reset design

**Status:** Approved by user on 2026-09-17  
**Source branch:** `feat/new-pictures-content-refresh` from `origin/main`  
**Input assets:** `C:\Users\tekwan\Downloads\ELYNDOR\pic\new pictures`

## Goal

Replace the old item/location/monster/resource/world presentation with the authored
1–40 content supplied in `pic/new pictures`, without creating a parallel content
system or replacing gameplay mechanics. Preserve the existing talent, ability, and
character artwork. Preserve player characters and their progression while preparing
a one-time, auditable reset of all character-owned inventory/equipment on the live
server.

This is a data/content refresh, not a rewrite of Combat, World, Location, Loot,
Professions, Talents, or Inventory. The server remains authoritative and the existing
content package validator/runtime pipeline remains the source of truth.

## Current approved implementation slice

The latest user instruction scopes the immediate implementation to:

```text
field locations -> authored monsters -> filled loot tables
three level 20/30/40 raid locations -> raid monsters -> filled loot tables
```

Do not add new combat or raid-specific gameplay logic. Preserve the existing dungeon
and encounter models. Raid mobs use empty ability and AI-priority lists. Raid loot
uses supported material drops because the source's weighted-exclusive set-drop
groups cannot be represented by the current flat loot model without changing runtime
logic. Unsupported item slots/procedural profiles, recipe items, profession-zone
rolls, and set effects remain out of scope and must be reported as remaining work.

The previously proposed inventory reset is a separate operator/data task; it is not
part of this content slice and no production database mutation is authorized here.

## Approaches considered

1. **Replace category content and validate before release (recommended).** Adapt the
   supplied bundles into current `content/<category>/*.json`, import matching art,
   validate the complete package, then separately run a guarded inventory-reset
   operation during a maintenance window. This fits current architecture and gives
   reviewable diffs and rollback preparation.
2. **Load the supplied bundles directly at runtime.** Rejected: it creates a second
   content schema/loader and bypasses the existing category composer, validator,
   Admin, and versioning conventions.
3. **Delete all current content and player data as part of a deployment migration.**
   Rejected: it couples a content release to irreversible production data loss,
   risks foreign-key/runtime breakage, and makes ordinary deployment destructive.

## Content import and compatibility

The supplied JSON files describe 688 item definitions, 60 sets, 227 loot tables,
21 locations, 227 monsters, and profession-zone profiles. The importer/adaptation is
to map these into existing content categories (items, sets, loot, locations,
monsters, professions/resources as supported by current contracts) using stable IDs
and existing DTOs. They are source material, not a new runtime package format.

The supplied Markdown is editorial/design context; JSON and image assets are the
structured inputs. The world map and location backgrounds, enemy art, set sheets,
out-of-set item sheets, and profession-material sheets should be converted/cropped
only as required by existing frontend asset conventions and matching records. Do not
invent resource definitions or gameplay effects from an image alone. Any asset with
ambiguous mapping is listed for review rather than guessed. Keep the current talent,
ability, and character images untouched; talent/ability definitions and runtime
contracts are not bulk-deleted by this content refresh.

IDs referenced by live/static systems (starter location, dungeon entrances, quests,
contracts, recipes, merchant stock, AFK eligibility, encounter tables, profession
zones) must be inventoried before removing or renaming old definitions. Prefer
stable-ID mappings or compatibility aliases when needed. Content must pass the
existing validator and server index construction before it can be published. The
update increments content/balance versions according to existing conventions.

## Player-state preservation and reset boundary

Preserve character records and account links, level/XP, talents, professions and
profession progress, quests/contracts, currencies, friends/party/guild state, and
other progression unless a concrete FK or invalid reference forces a separately
reviewed migration. For every character, clear all owned item instances including
stackables/materials and generated gear, plus equipment bindings. Do not reset
characters or accounts.

Keep immutable audit, ledger, idempotency, and historical combat/reward records.
Before clearing item instances, inspect every FK/reference to item instances and
pending rewards. Resolve the cutover policy for unclaimed loot and operations that
could grant legacy item IDs: drain/finalize them before maintenance or explicitly
expire/cancel only outstanding claims in the same reviewed operation. Do not erase
historical logs to make the reset easier.

Characters located in removed locations must be moved to the designated valid
starter hub as part of the same operation, preserving all other character state.
The concrete hub ID must be verified from the final location package before the
reset command can be enabled.

## Live reset safety

Do not implement this as an automatic EF migration, startup hook, or regular deploy
step. Implement a separately invoked, one-time backend maintenance command using
existing EF Core/domain configuration. It must support:

- read-only preflight/dry-run with counts by affected table and character;
- hard refusal if content validation fails, the designated starter hub is absent,
  unresolved item-instance references exist, or active reward/combat/AFK grant work
  can race the reset;
- explicit live-environment safeguards and a typed confirmation containing the
  target environment plus a unique operation identifier;
- a required verified database backup and documented restore checkpoint before
  execution;
- a transactionally consistent reset where feasible; if data volume makes one
  transaction unsafe, use a resumable, explicitly logged batch protocol with a
  completion marker and no partial-success ambiguity;
- idempotent operation identity, summary counts, and postcondition checks (zero
  character inventory/equipment rows, valid character locations, preserved
  character/progression counts);
- no automatic rollback that silently restores a stale backup over newer player
  activity. Recovery is an operator-led restore from the declared checkpoint.

Production execution is a separate operator action after the exact preflight report
and change window have been reviewed. This implementation does not deploy to or
connect to production.

## Delivery slices

1. **Content and art package:** map/transform authored JSON and art into the existing
   categories, resolve references and compatibility, update content versions, and
   pass the content validator/build/tests. No player data is changed.
2. **Guarded reset utility:** implement the preflight and explicit one-time reset
   command, reference/locking checks, recovery instructions, and focused tests. No
   production reset is run by CI or deployment.
3. **Release handoff:** produce an exact asset/content summary and reset dry-run
   checklist/counts for operator review. Production deploy/reset only after a
   separate explicit go-ahead against the reviewed report.

Each implementation slice is a separate branch/PR from current `main`; do not merge
or push directly to `main`. Synchronize with `main` before final verification and
resolve conflicts by reviewing both sides.

## Verification

- Existing content validator against the composed `content/package.json`.
- Backend build and relevant content/persistence tests.
- Frontend lint, unit tests, and production build for imported assets/map wiring.
- Focused tests for stable-ID references and reset preflight, refusal paths,
  idempotency, counts, character/progression preservation, and no duplicate legacy
  grants.
- Review the final diff and confirm talent/ability/character artwork and unrelated
  gameplay systems are unchanged.

## Explicit non-goals

- No production database access, deletion, or deployment in this design/spec step.
- No account/character wipe, currency reset, talent/profession reset, or broad
  character progression reset.
- No new content loader or parallel item/location/monster architecture.
- No automatic item conversion/compensation unless separately specified and
  approved; inventory reset means items are removed, not translated.
- No inferred mechanics/content solely from artwork.
