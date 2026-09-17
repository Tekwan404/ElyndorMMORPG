# New Pictures Content Refresh Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Import the authored 1–40 Elyndor content and matching artwork into the existing versioned content pipeline, then provide a guarded one-time tool to clear every character inventory/equipment while preserving characters and progression.

**Architecture:** Treat the supplied bundles as authoring input, adapt them into the existing `content/<category>/*.json` fragments, and keep `CategoryContentComposer`, validation, and runtime indexes unchanged. Implement the production inventory reset as a separately invoked maintenance utility with dry-run, explicit safeguards, and no automatic deployment/startup execution.

**Tech Stack:** .NET/C#, System.Text.Json, EF Core/PostgreSQL, existing Elyndor content validator, Vue/Vite asset pipeline, existing frontend test/build commands.

**Spec:** `docs/superpowers/specs/2026-09-17-new-pictures-content-refresh-design.md`

## Global Constraints

- Do not work directly on `main`, merge into `main`, push directly to `main`, or run a production deploy.
- Preserve existing talent, ability, and character artwork and keep gameplay systems/runtime contracts intact.
- Backend content and gameplay remain authoritative; reuse the existing content composer and validator.
- Preserve characters, account links, level/XP, talents, professions, quests, currencies, and other progression.
- Clear all character-owned item instances including stacks and gear, plus equipment bindings; do not convert items or reset accounts/characters.
- Never run the destructive reset against production as part of implementation, tests, CI, or deployment.
- No automatic destructive EF migration, startup hook, or deploy step.
- Keep each implementation slice in its own feature branch/PR and synchronize against latest `main` before final verification.

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

## Task 1: Establish source-to-runtime mapping and protect current artwork

**Files:** source bundles in `C:/Users/tekwan/Downloads/ELYNDOR/pic/new pictures/json/`, Markdown in `C:/Users/tekwan/Downloads/ELYNDOR/pic/new pictures/md/`, current category fragments above; create `docs/source-of-truth/content/new-pictures-import-manifest.md`.

- [ ] Record source IDs/counts for items, sets, loot tables, locations, monsters, profession zones, and image files; report duplicate IDs and missing source references.
- [ ] Compare each source JSON schema with current C# definitions and one current JSON fragment per target category. Write a mapping table for required/ignored/derived fields.
- [ ] Build an explicit stable-ID compatibility table from existing live references: starter hub, locations, dungeons/contracts/quests, merchants, recipes, AFK profiles, monsters, loot, resources, and item sets.
- [ ] Generate an asset inventory (source path, dimensions, intended art ID, target file, retained/replaced) for each supplied set/item/enemy/material/background/map image. Mark ambiguous crops as unresolved; do not guess.
- [ ] Confirm old talent/ability/character art directories and checksums remain unchanged by this content slice.
- [ ] Validate the mapping report against the approved spec and commit this audit artifact with the content import implementation branch.

**Deliverable:** a reviewable import manifest and no runtime code or player data changes.

## Task 2: Build deterministic offline bundle adapter

**Files:** `tools/Elyndor.ContentImport/Elyndor.ContentImport.csproj`, `tools/Elyndor.ContentImport/Program.cs`, adapter source files under the same project, `Elyndor.slnx`, and focused tests under `tests/Elyndor.UnitTests/Content/`.

- [ ] Create tests for representative material, stackable, equipment, set, loot, location, monster, and profession-zone source records using real source samples.
- [ ] Run those tests and confirm they fail because the adapter/CLI does not exist.
- [ ] Implement a CLI that reads `Elyndor_Items_Loot_Professions_1-40.json`, `Elyndor_Locations_1-40.json`, and `Elyndor_Mobs_1-40.json` and emits the exact JSON field shape accepted by the private fragment records in `CategoryContentComposer.cs` (items, sets, loot, monsters, locations/encounter fragments, and profession/resource fields only when current package contracts expose them).
- [ ] Preserve stable IDs and authored values; reject duplicate IDs, unknown fields that would lose gameplay meaning, invalid references, unsupported equipment/category values, and data not representable by existing contracts.
- [ ] Make output deterministic (stable ordering, UTF-8, consistent indentation, no timestamps changing on each run); write to an explicit staging directory and never overwrite production source files unless an explicit `--write` option is passed.
- [ ] Add a `--check` mode that compares generated output to checked-in fragments and returns non-zero on drift.
- [ ] Add unit tests for round-trip/composition parity, malformed input, duplicate IDs, and missing references; run focused tool tests.

**Interfaces:** CLI takes explicit `--items`, `--locations`, `--mobs`, and `--output` paths; emits files under existing category directory names and returns a non-zero exit code on any lossy/invalid mapping.

**Deliverable:** repeatable adapter without runtime/schema fork.

## Task 3: Import authored gameplay categories and ensure world references stay valid

**Files:** staged output copied to `content/items/`, `content/sets/`, `content/loot/`, `content/monsters/`, `content/locations/`, plus `content/professions/`/`content/resources/` only for supported contracts; `content/package.json`; relevant compatibility fragments/quests/dungeons/contracts/merchants.

- [ ] Add tests or validator fixtures that detect any old-to-new ID reference lost by replacement (locations, encounter rosters, loot tables, items, recipes, professions, dungeons, and starter hub).
- [ ] Run adapter output into a disposable staging folder and compare entity counts and stable IDs with source bundle summaries before copying anything into canonical content.
- [ ] Resolve location and loot compatibility mappings so every referenced ID exists; do not keep obsolete content merely to satisfy runtime references when a deliberate mapping can be made.
- [ ] Keep talent/ability/class/quest mechanics outside this content replacement unless the source package explicitly contains a compatible replacement and the spec is revised.
- [ ] Replace only the relevant canonical fragments; do not blanket-delete unrelated folders or package definitions.
- [ ] Update `ContentVersion`/`BalanceVersion` per current policy and run `dotnet run --project tools/Elyndor.ContentValidator -- content/package.json`.
- [ ] Run backend content tests and `dotnet build Elyndor.slnx --configuration Release`; fix every validation/build regression before proceeding.
- [ ] Review full JSON diff to ensure no unrelated talent, class, ability, economy, or dungeon changes slipped in.

**Deliverable:** complete composed content package validates and preserves required game entry points.

## Task 4: Integrate artwork and world map

**Files:** `web/elyndor-web/src/assets/items/`, `monsters/`, `world/`; `itemArt.ts`, `monsterArt.ts`, `gameArt.ts` only where required; frontend tests for mappings.

- [ ] Create/verify crop boundaries for the supplied item-set and out-of-set sheets; no neighboring item may bleed into a crop. Prefer existing project image tooling; do not add a runtime image dependency.
- [ ] Convert supported raster files to the existing production format (WebP only where alpha/quality and current browser conventions allow) and retain source-to-output mapping in the manifest.
- [ ] Import enemy and profession-material art, location backgrounds, and `Карта мира.png` into existing directories and wire stable `artId`s through existing asset helpers.
- [ ] Keep current talent/ability/character artwork byte-identical; verify with a scoped file diff/checksum list.
- [ ] Add/adjust focused art-helper tests for new/unknown IDs and fallback behavior; run frontend unit tests, lint, and production build.
- [ ] Inspect generated contact sheets/previews at full size to catch white backgrounds, bad alpha, wrong class/item mapping, and crop bleed before accepting the art import.

**Deliverable:** authored art is discoverable through existing UI art helpers and the world view can display the new map/location art.

## Task 5: Audit reset persistence graph and implement read-only preflight

**Files:** `src/Elyndor.Infrastructure/Persistence/GameDbContext.cs`; `Configurations/CharacterItemConfiguration.cs`, `CharacterEquipmentConfiguration.cs`, `PendingLootItemConfiguration.cs`, active-combat/AFK/loot entities and configurations; `tools/Elyndor.Maintenance/Elyndor.Maintenance.csproj`; `tools/Elyndor.Maintenance/Program.cs`; `Elyndor.slnx`; `tests/Elyndor.IntegrationTests/`.

- [ ] Enumerate every FK and durable queue/grant record that can reference `CharacterItem`, definitions, loot rolls, or a character's current location. Include PostgreSQL FK delete behavior and operation/idempotency tables.
- [ ] Add tests with a disposable PostgreSQL/test database proving preflight counts characters, item instances/stacks, equipment rows, pending item grants, active combat/AFK rewards, and characters in removed locations without mutating any row.
- [ ] Implement `reset-inventory --dry-run` as the default behavior; it validates content, starter hub ID, FK/reference conditions, and quiescence, then emits counts and a stable operation fingerprint.
- [ ] Refuse execute mode when running in production without an explicit environment confirmation, backup checkpoint metadata, operation ID, no-active-grant proof, or reviewed content package.
- [ ] Verify tests prove dry-run leaves every row unchanged and all refusal conditions produce actionable errors.

**Deliverable:** operator can safely inspect impact without mutation.

## Task 6: Implement guarded one-time reset execution

**Files:** `tools/Elyndor.Maintenance/Program.cs`, `tools/Elyndor.Maintenance/InventoryResetService.cs`, reset tests under `tests/Elyndor.IntegrationTests/`, and `docs/development/inventory-reset-runbook.md`.

- [ ] Write failing tests for clearing owned item instances/equipment, preserving characters/progression/currencies/audit, relocating characters from removed locations, and cancelling only explicitly selected outstanding claims.
- [ ] Run the focused tests to verify they fail before implementation.
- [ ] Implement a separately invoked `reset-inventory --execute --environment <name> --operation-id <id> --backup-checkpoint <id> --confirm <typed-value>` command; never wire it to server startup, migrations, or release scripts.
- [ ] In one retry-safe transaction where supported: refuse unresolved pending/active grants, resolve only the approved outstanding-claim policy, move invalid-location characters to the verified starter hub, delete equipment bindings and all character-owned item instances, and record immutable operation summary/idempotency state.
- [ ] If one transaction is not safe for realistic data volume, stop and revise the spec before replacing it with batch deletion; do not ship partially resumable deletion without an explicit progress ledger and recovery contract.
- [ ] Add postcondition checks for zero character item/equipment rows, unchanged character/account/progression/currency counts, valid locations, no unresolved new/legacy item grants, and exactly one reset operation record.
- [ ] Add a recovery runbook: required backup validation, maintenance window, stop API/workers, run dry-run, inspect report, execute once, run postchecks, re-enable workers; restore only by operator from the recorded checkpoint.
- [ ] Test retry/idempotency, wrong confirmation/environment, missing backup, failed transaction rollback, duplicate operation ID, pending loot, active AFK/combat, and preservation invariants against a disposable database.

**Deliverable:** safe reset executable exists, but no production operation has been run.

## Task 7: Final regression, PR and release handoff

**Files:** changed files from Tasks 1–6; PR description.

- [ ] Fetch latest `origin/main`; rebase/merge only on the feature branch and manually analyze any content or persistence conflicts.
- [ ] Run `dotnet build Elyndor.slnx --configuration Release`.
- [ ] Run `dotnet test Elyndor.slnx --configuration Release` and focused PostgreSQL integration tests.
- [ ] Run `dotnet run --project tools/Elyndor.ContentValidator -- content/package.json`.
- [ ] Run frontend lint, format check, unit tests, build, and relevant E2E commands from `CONTRIBUTING.md`.
- [ ] Review asset previews and full `git diff`; verify unchanged talent/ability/character art and no secrets or production data artifacts.
- [ ] Open a content-import PR to `main` containing Tasks 1–4 and a separate reset-utility PR containing Tasks 5–6. Each PR states its own safety boundary and verification and explicitly says no production reset/deploy was executed. Do not merge either without the user's explicit merge request.
- [ ] Provide the operator a dry-run checklist; require a separate explicit go-ahead after the exact production dry-run report is reviewed before any reset is run.

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
