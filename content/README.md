# Elyndor game content

This directory contains versioned static game data. Player state must never be stored here.

`package.json` is the active versioned package entry point. It carries `ContentVersion`, `BalanceVersion`, an explicit UTC publication time, typed character profiles, and the base content definitions. The loader composes the approved JSON overlays next to it before validation. IDs use uppercase ASCII letters, digits, and underscores. References are resolved by `(type, id)`.

## Adding a normal monster

Normal world encounters are data-driven. Do not add monster IDs to `CombatSessionFactory` or to a Vue encounter array.

1. Add the `MonsterDefinition` and AI profile to the relevant monster content overlay. For an encounter-visible monster also set `displayName`, `description`, and `artId`.
2. Add `{ "monsterId": "...", "weight": ... }` to the location overlay under `content/locations/*.json`.
3. Add the visual asset under `web/elyndor-web/src/assets/monsters/<artId>.<ext>`. Vite discovers monster art automatically; no TypeScript map entry is required.
4. Add or reference a loot table when the monster grants loot.
5. Increase `ContentVersion` for new content and run validation/tests.

`POST /api/v1/world/explore` performs the authoritative encounter roll on the server and returns a short-lived opaque encounter id. Combat can only start by consuming that id, so the client cannot request an arbitrary monster.

Validate the package from the repository root:

```powershell
dotnet run --project tools/Elyndor.ContentValidator -- content/package.json
```

The server validates the composed package before accepting traffic. Balance-only number changes should normally change `BalanceVersion`; new definitions/schema-facing content should change `ContentVersion`.
