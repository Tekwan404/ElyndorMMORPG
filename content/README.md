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
├── balance/
├── bosses/
├── classes/
├── dungeons/
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

Items have exactly one authored definition per ID in `content/items/`. Update the
canonical definition for a new release instead of adding `z`/`zz`/hotfix overlays.
Previous versions belong in Git history. The catalog uniqueness test enforces this
rule; file ordering must not decide which item stats are current.

Do not add feature-named JSON overlays beside `package.json`. If a new content domain is needed,
extend the category composer and validator explicitly so the runtime, importer, and Admin all see
the same package.

Equipment definitions must declare `generationMode` explicitly. Use `Rolled` for equipment backed
by the current affix-pool/count-profile itemization policy and `Fixed` only for equipment whose
explicit definition stats are final. Set membership, trade policy and acquisition source are
independent of generation mode.

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

Combat balance diagnostics are explicit and never rewrite authored monsters:

```powershell
dotnet run --project tools/Elyndor.ContentValidator -- content/package.json --audit-balance
dotnet run --project tools/Elyndor.ContentValidator -- content/package.json --benchmark-balance
```

`--audit-balance` compares Normal/Elite monsters with the versioned `combatBalance`
curve and reports outliers. `--benchmark-balance` runs the authoritative combat simulator
against Weak / Normal / Good equipment benchmarks and prints HP, Armor, EHP, DPS, TTK and TTD.

The validator runs the same `ContentValidationPipeline` used by the server and Admin publish flow,
then forces `GameContentIndexes` construction. Balance-only number changes should normally change
`BalanceVersion`; new definitions/schema-facing content should change `ContentVersion`.
