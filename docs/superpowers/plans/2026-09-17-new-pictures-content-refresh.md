# New Pictures Content Refresh Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Import the authored non-raid open-world locations, their monsters, and their direct loot tables into the existing versioned content pipeline.

**Architecture:** Treat the supplied bundles as authoring input and adapt only the 13 field locations, their listed monsters, and direct loot entries into existing `content/<category>/*.json` fragments. Keep `CategoryContentComposer`, gameplay services, and runtime indexes unchanged; raid and instance content is excluded from this slice.

**Tech Stack:** Versioned JSON content, existing .NET content validator, current monster-art Vite glob, existing backend test/build commands.

**Spec:** `docs/superpowers/specs/2026-09-17-new-pictures-content-refresh-design.md`

## Global Constraints

- Do not work directly on `main`, merge into `main`, push directly to `main`, or run a production deploy.
- Preserve existing talent, ability, and character artwork and keep gameplay systems/runtime contracts intact.
- Backend content and gameplay remain authoritative; reuse the existing content composer and validator.
- Preserve existing dungeon/raid definitions, boss content, player data, and unrelated class/talent/ability content.
- Do not import any raid or instance location in this slice; specifically exclude 20/30/40 raid content identified by the source design.
- Use the existing encounter, loot, item, and art contracts; do not implement new runtime loot/profile mechanics here.
- Keep this slice in its own feature branch/PR and synchronize against latest `main` before final verification.

---

## File and subsystem map

Existing composition and validation remain in:

- `src/Elyndor.Infrastructure/Content/CategoryContentComposer.cs` — discovers category fragments; avoid changing unless a validated existing schema gap blocks this import.
- `src/Elyndor.Infrastructure/Content/GameContentPackageLoader.cs` and `GameContentPackageCodec.cs` — existing runtime load and canonical validation/parity.
- `src/Elyndor.Infrastructure/Content/CategoryContentComposer.cs` — the private `ContentCategoryFragment` and `LocationEncounterFragment` JSON wire contracts; the offline adapter emits their existing serialized shape without changing runtime visibility.
- `src/Elyndor.Core/Content/Validation/*` — existing validation rules.
- `tools/Elyndor.ContentValidator/Program.cs` — canonical package validation entry point.
- `content/items/*.json`, `content/sets/*.json`, `content/loot/*.json`, `content/locations/*.json`, `content/monsters/*.json`, `content/professions/*.json`, `content/resources/*.json` — current source-of-truth fragments to replace/extend after reference mapping.
- `web/elyndor-web/src/assets/{items,monsters,world}/` and `itemArt.ts`, `monsterArt.ts`, `gameArt.ts` — existing asset locations and auto-discovery/mapping conventions.
- `src/Elyndor.Infrastructure/Persistence/GameDbContext.cs`, `Configurations/CharacterItemConfiguration.cs`, `CharacterEquipmentConfiguration.cs`, `PendingLootItemConfiguration.cs`, and related model/configuration files — persistence references to inventory items and active work; inspect before writing reset code.
- `Elyndor.slnx`, `.github/workflows/ci.yml`, and `CONTRIBUTING.md` — project registration, CI and repository workflow.

New files:

- `tools/Elyndor.ContentImport/` — offline, deterministic adapter for source bundle -> current category fragment format; not referenced by production server runtime.
- `tools/Elyndor.Maintenance/` — separately invoked inventory reset preflight/execute command; no registration in server startup or deployment.
- focused test files under the existing content/import and persistence test projects, following their current naming conventions.
- generated/imported assets under existing `web/elyndor-web/src/assets/` folders; add a TypeScript art-map entry only if current assets are not auto-discovered.

## Task 1: Establish open-world source mapping and raid exclusions

**Files:** source bundles in `C:/Users/tekwan/Downloads/ELYNDOR/pic/new pictures/json/`, Markdown in `C:/Users/tekwan/Downloads/ELYNDOR/pic/new pictures/md/`, current category fragments above; create `docs/source-of-truth/content/new-pictures-import-manifest.md`.

- [x] Record source IDs/counts for items, sets, loot tables, locations, monsters, profession zones, and image files; report duplicate IDs and missing source references.
- [x] Compare source location/monster/item/loot shapes with current contracts and document unsupported fields.
- [x] Select 13 non-instance field locations and stable Normal encounter, loot-table, and supported drop-item IDs; preserve current hub and instance definitions.
- [x] Exclude all instance locations and source-designated raids from imported content and field transitions.
- [x] Replace legacy field encounter aliases with authored Normal encounter IDs; do not create an unsupported Elite spawn rule.
- [x] Verify unrelated talent/ability/character artwork remains untouched; no artwork was imported in this slice.
- [x] Add the auditable import manifest.

**Deliverable:** a reviewable non-raid import manifest and no runtime code or player data changes.

## Task 2: Repeatable import tooling (deferred)

**Files:** `tools/Elyndor.ContentImport/Elyndor.ContentImport.csproj`, `tools/Elyndor.ContentImport/Program.cs`, adapter source files under the same project, `Elyndor.slnx`, and focused tests under `tests/Elyndor.UnitTests/Content/`.

- [ ] Create tests for representative location, normal/elite monster, direct item loot, and missing-reference source records using real source samples.
- [ ] Run those tests and confirm they fail because the adapter/CLI does not exist.
- [ ] Implement a CLI that reads the location, mob, and item/loot bundles and emits the exact JSON field shape accepted by the private fragment records in `CategoryContentComposer.cs` for the 13 field locations, their authored monsters, direct loot tables, and required direct-drop item definitions.
- [ ] Preserve stable IDs and authored values; reject duplicate IDs, missing location/monster/item references, unsupported rank/enum values, and data not representable by existing contracts. Exclude `randomEquipmentProfile` metadata because it is not part of the existing `LootTableDefinition` contract and report it as remaining work.
- [ ] Make output deterministic (stable ordering, UTF-8, consistent indentation, no timestamps changing on each run); write to an explicit staging directory and never overwrite production source files unless an explicit `--write` option is passed.
- [ ] Add a `--check` mode that compares generated output to checked-in fragments and returns non-zero on drift.
- [ ] Add unit tests for round-trip/composition parity, malformed input, duplicate IDs, and missing references; run focused tool tests.

**Interfaces:** CLI takes explicit `--items`, `--locations`, `--mobs`, and `--output` paths; emits existing category fragments and returns non-zero on any invalid reference or lossy gameplay field other than the explicitly reported unsupported random-equipment profile.

**Status:** deferred. This bounded, one-off content slice was adapted into existing
category JSON directly; no second runtime loader or import CLI was introduced. Add a
repeatable tool only if future authored content refreshes justify maintaining it.

## Task 3: Import field locations, monsters, and direct loot

**Files:** staged output copied to `content/items/`, `content/loot/`, `content/monsters/`, `content/locations/`, `content/package.json`; compatibility fragments only when an existing reference must be preserved.

- [x] Add a focused unit test for field locations, encounter and loot references, item references, and raid exclusions.
- [x] Import 13 locations, 134 Normal encounters/mobs, 134 direct loot tables, and 121 supported Material/Consumable item definitions.
- [x] Preserve existing dungeon/raid locations and remove raid routes from the imported field-location graph.
- [x] Exclude unsupported Elite roster entries, all equipment drops requiring invalid/unavailable itemization contracts, and Recipe items without a runtime `ItemType`; every imported table retains at least one supported entry.
- [x] Update content/balance versions and validate the composed package.
- [x] Run backend build, unit tests, and the focused content integration test.
- [x] Review the change scope; no gameplay service/runtime schema or player data was changed.

**Deliverable:** composed direct-loot field content package validates and preserves existing instances and game entry points.

## Task 4: Integrate field-location and monster artwork

**Files:** `web/elyndor-web/src/assets/monsters/`, `world/`; existing `monsterArt.ts`/world art resolver only if the current auto-discovery path cannot consume the new asset IDs.

- [ ] Import source enemy art and location backgrounds for the 13 field locations, using stable `artId`s and existing Vite discovery conventions.
- [ ] Import `Карта мира.png` only if the current world map UI consumes a static map image; preserve existing navigation if it does not.
- [ ] Keep current talent/ability/character artwork byte-identical; verify with a scoped file diff/checksum list.
- [ ] Add/adjust focused art-helper tests for new/unknown monster art IDs and fallback behavior; run frontend unit tests, lint, and production build.
- [ ] Inspect generated contact sheets/previews at full size to catch white backgrounds, bad alpha, wrong class/item mapping, and crop bleed before accepting the art import.

**Deliverable:** authored art is discoverable through existing UI art helpers and the world view can display the new map/location art.

## Remaining work

- A complete inventory reset tool and the production data reset.
- Rare random-equipment bonus profiles attached to the source field-mob tables; current `LootTableDefinition` does not model `randomEquipmentProfile`.
- Raid content, including the source-designated level 20/30/40 raids; none is imported here.
- The 35 authored Elite field roster entries need an encounter/spawn contract before they can enter normal location encounter pools.
- Authored equipment and recipe drops: 26 equipment definitions need compatible slot/category/procedural affix data; 3 Recipe definitions need an existing runtime representation. Their loot entries are omitted for now.
- Field-location/enemy artwork, the authored world map, location presentation mapping, and item art have not been imported.
- Profession zone rolls, set-effect implementations, and remaining authored itemization not required by direct field-mob loot.
- The separately requested guarded inventory reset utility and any production reset/deployment; neither is part of this slice.

## Task 5: Final regression, PR and release handoff

**Files:** changed files from Tasks 1–6; PR description.

- [ ] Fetch latest `origin/main`; rebase/merge only on the feature branch and manually analyze any content or persistence conflicts.
- [ ] Run `dotnet build Elyndor.slnx --configuration Release`.
- [ ] Run `dotnet test Elyndor.slnx --configuration Release` and focused PostgreSQL integration tests.
- [ ] Run `dotnet run --project tools/Elyndor.ContentValidator -- content/package.json`.
- [ ] Run frontend lint, format check, unit tests, build, and relevant E2E commands from `CONTRIBUTING.md`.
- [ ] Review asset previews and full `git diff`; verify unchanged talent/ability/character art and no secrets or production data artifacts.
- [ ] Open a content PR to `main` describing the 13 field locations, imported mob/loot coverage, excluded raids, and exact validation/build/test results. Do not merge without the user's explicit merge request.

## Verification commands

```powershell
dotnet build Elyndor.slnx --configuration Release
dotnet test Elyndor.slnx --configuration Release
dotnet run --project tools/Elyndor.ContentValidator -- content/package.json
npm run lint --prefix web/elyndor-web
npm run format:check --prefix web/elyndor-web
npm run test:unit --prefix web/elyndor-web
npm run build --prefix web/elyndor-web
npm run test:e2e --prefix web/elyndor-web
```
