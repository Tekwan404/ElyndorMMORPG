# Archer Companion Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let every Archer select one of the three existing physical companion profiles and present that companion in a mobile-first Hero screen.

**Architecture:** Persist one nullable `ActiveCompanionProfileId` on `Character`; physical Archer profiles are content definitions and are all available in this slice, so no duplicate companion-inventory entity is needed. A talent-derived Spirit profile retains priority over the selected physical profile. The server exposes the selection state and performs the out-of-combat mutation; Vue only renders it and sends the chosen profile id.

**Tech Stack:** C#/.NET, EF Core/PostgreSQL migrations, ASP.NET Core minimal APIs, Vue 3/TypeScript, Vite/Vitest.

**Spec:** `docs/source-of-truth/gameplay/21_COMPANION_AND_PET_SYSTEM.md`, `docs/source-of-truth/gameplay/23_ARCHER_TALENT_TREE.md`, `docs/source-of-truth/ui/UI_07_COMPANION.md`.

## Global Constraints

- Only Archer exposes the Companion tab; Warrior and Mage receive no empty UI.
- All three existing physical profiles are available immediately; future taming is out of scope.
- Exactly one physical profile can be selected; selection is rejected during active combat.
- Arcane talent override to `ARCHER_SPIRIT` is authoritative and does not overwrite the stored physical choice.
- Server validates ownership/class/profile and resolves combat participants; client submits intent only.
- Reuse the existing content profiles, combat runtime, SVG glyphs, UI tokens and character art conventions.

---

### Task 1: Persist and resolve the selected physical companion

**Files:**
- Modify: `src/Elyndor.Core/Characters/Character.cs`
- Modify: `src/Elyndor.Infrastructure/Persistence/Configurations/CharacterConfiguration.cs`
- Modify: `src/Elyndor.Infrastructure/Characters/CharacterCreationService.cs`
- Modify: `src/Elyndor.Infrastructure/Characters/CharacterDerivedStateService.cs`
- Create: EF Core migration under `src/Elyndor.Infrastructure/Persistence/Migrations/`
- Test: `tests/Elyndor.IntegrationTests/Characters/CharacterDerivedStateServiceTests.cs`

**Interfaces:**
- Produces `Character.ActiveCompanionProfileId` and `Character.SelectCompanionProfile(string)`.
- `CharacterDerivedState.ActiveCompanionProfile` resolves the selected physical profile unless `TalentModifiers.Profiles.CompanionProfileId` provides the Spirit override.

- [ ] Add a failing derived-state test: an Archer with `ARCHER_GUARDIAN` persisted resolves Guardian; an Arcane override resolves `ARCHER_SPIRIT` without changing the persisted selection.
- [ ] Run the focused test and confirm the selected profile is ignored by the existing resolver.
- [ ] Add the nullable profile id to `Character`, initialize each newly-created Archer with the configured starter id, and persist it as a bounded nullable column.
- [ ] Change the derived resolver to choose `talent override ?? persisted physical selection ?? starting profile`; only accept selected profiles tagged `PHYSICAL_PET`.
- [ ] Generate the additive migration, inspect it for no inventory/character data rewrite, and rerun the focused test.
- [ ] Commit: `feat: persist archer companion selection`.

### Task 2: Add the authoritative companion API

**Files:**
- Create: `src/Elyndor.Infrastructure/Characters/CharacterCompanionService.cs`
- Modify: `src/Elyndor.Contracts/Characters/CharacterContracts.cs`
- Modify: `src/Elyndor.Server/Characters/CharacterEndpoints.cs`
- Modify: `src/Elyndor.Server/Program.cs` only if service registration is not assembly-scanned
- Test: `tests/Elyndor.IntegrationTests/Characters/CharacterCompanionServiceTests.cs`

**Interfaces:**
- Produces `GET /api/v1/character/companion` and `POST /api/v1/character/companion/select`.
- `CompanionSelectionResponse` includes current effective companion, stored physical choice, and all three physical choices with id/name/archetype/art id.
- `SelectCompanionRequest(string CompanionProfileId)` carries intent only.

- [ ] Write failing integration cases for a valid Archer switch, a non-Archer rejection, an unknown/Spirit profile rejection, and an active-combat rejection.
- [ ] Implement `CharacterCompanionService` using current content and `ActiveCombatSessions`; load the account character, validate its class/profile, mutate one row and save once.
- [ ] Map explicit problem codes: `companion_not_available`, `companion_invalid_profile`, and `companion_change_in_combat`.
- [ ] Add endpoints that obtain the account id from JWT claims and map service responses without gameplay rules in the endpoint.
- [ ] Run focused integration tests and commit: `feat: add archer companion selection API`.

### Task 3: Render the Archer-only Companion screen

**Files:**
- Create: `web/elyndor-web/src/game/character/views/CompanionView.vue`
- Modify: `web/elyndor-web/src/api/contracts.ts`
- Modify: `web/elyndor-web/src/stores/gameSession.ts`
- Modify: `web/elyndor-web/src/game/character/views/HeroView.vue`
- Create/Modify: `web/elyndor-web/src/assets/companionArt.ts`
- Test: `web/elyndor-web/src/__tests__/CompanionView.spec.ts`
- Test: `web/elyndor-web/src/__tests__/HeroView.spec.ts`

**Interfaces:**
- Consumes `CompanionSelectionResponse` and `selectCompanion(profileId)` from `gameSession`.
- Produces an Archer-only Hero tab and a responsive companion view.

- [ ] Write a failing component test that shows the active profile, all three selectable cards, and no selector action during a mutation.
- [ ] Add typed API calls and store state with loading/error handling; after a successful selection refresh the bootstrap snapshot so subsequent combat uses the new profile.
- [ ] Add `companionArt` mappings with distinct art/fallback glyph presentation for Predator, Guardian and Trapper; do not reuse three identical wolf images.
- [ ] Implement the screen with active art, name, archetype/role, concise status, and 44px+ selection controls using existing UI tokens.
- [ ] Add the `Спутник` Hero tab only when `character.classId === 'ARCHER'`; do not alter global navigation.
- [ ] Render a read-only Spirit state when the effective profile is Spirit, explaining that the current Arcane build determines it.
- [ ] Run focused Vue tests, typecheck, and commit: `feat: add archer companion screen`.

### Task 4: Verify combat integration and release readiness

**Files:**
- Modify only if verification exposes a real integration defect.
- Test: existing `tests/Elyndor.IntegrationTests/Characters/CharacterDerivedStateServiceTests.cs`
- Test: existing Archer combat tests and new companion tests.

- [ ] Confirm the selected Guardian and Trapper reach `CombatSessionFactory` through `CharacterDerivedState.ActiveCompanionProfile` without changes to combat session ownership or damage logic.
- [ ] Run `dotnet test Elyndor.slnx --configuration Release --no-build` after a Release build.
- [ ] Run frontend lint, unit tests and production build from `web/elyndor-web`.
- [ ] Run `git diff --check`, inspect the migration and ensure no secrets or unrelated UI redesign were introduced.
- [ ] Commit any verification-only correction separately, then open a PR and merge only after green CI.
