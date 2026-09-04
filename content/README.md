# Elyndor game content

This directory contains versioned static game data. Player state must never be stored here.

`package.json` is the base package entry point. Additional content is composed only from
`content/<category>/*.json`.

The loader executes:

```text
package.json
  -> content/<category>/*.json scan
  -> modular validation
  -> immutable lookup indexes
```

IDs use uppercase ASCII letters, digits, and underscores. References are resolved by `(type, id)`.

## Category directories

Content belongs under the matching directory:

```text
content/
├── abilities/
├── bosses/
├── classes/
├── items/
├── locations/
├── loot/
├── merchants/
├── monsters/
├── progression/
├── resources/
├── sets/
└── talents/
```

Category files may carry `contentVersion`, `balanceVersion`, `publishedAtUtc` plus the typed
collection or profile they contribute. Composition is deterministic by file path and entities are
merged by stable id.

Do not add feature-named JSON overlays beside `package.json`. If a new content domain is needed,
extend the category composer and validator explicitly so the runtime, importer, and Admin all see
the same package.

## Adding a normal monster

Normal world encounters are data-driven. Do not add monster IDs to `CombatSessionFactory` or to a
Vue encounter array.

1. Add the `MonsterDefinition` and AI profile under `content/monsters/*.json`. For an
   encounter-visible monster also set `displayName`, `description`, and `artId`.
2. Add `{ "monsterId": "...", "weight": ... }` to the location content under
   `content/locations/*.json`.
3. Add the visual asset under `web/elyndor-web/src/assets/monsters/<artId>.<ext>`. Vite discovers
   monster art automatically; no TypeScript map entry is required.
4. Add or reference a loot table when the monster grants loot.
5. Increase `ContentVersion` for new content and run validation/tests.

`POST /api/v1/world/explore` performs the authoritative encounter roll on the server and returns a
short-lived opaque encounter id. Combat can only start by consuming that id, so the client cannot
request an arbitrary monster.

Validate the composed snapshot from the repository root:

```powershell
dotnet run --project tools/Elyndor.ContentValidator -- content/package.json
```

The validator runs the same `ContentValidationPipeline` used by the server and Admin publish flow,
then forces `GameContentIndexes` construction. Balance-only number changes should normally change
`BalanceVersion`; new definitions/schema-facing content should change `ContentVersion`.
