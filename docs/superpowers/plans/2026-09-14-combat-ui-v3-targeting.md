# Combat UI V3 and Targeting V1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a mobile combat battlefield with independent ally/enemy targets and a stable 12-slot action bar without changing combat balance.

**Architecture:** Reuse the existing per-player authoritative hostile target in CombatPlayerRuntimeState and make every manual ability honor its already-existing explicit target intent. Vue/Pinia adds only ephemeral friendly selection. Reuse PR #156 local per-character hotbar order and extend its active range from 6 to 12.

**Tech Stack:** C#/.NET 10, SignalR, Vue 3, TypeScript, Pinia, xUnit, Vitest, Playwright.

**Spec:** `docs/superpowers/specs/2026-09-14-combat-ui-v3-targeting-design.md`

## Global Constraints

- Never merge or push `main`; work only in `feat/combat-ui-v3-targeting`.
- Do not alter damage, crit, armor, block, shield, threat values, resources, bosses, loot, XP, AFK, dungeon state machine, or database schema.
- Keep server authority, command idempotency, current flee flow and combat session single-writer semantics.
- Reuse `elyndor:combat-hotbar:v1:<characterId>`; no second storage key.
- Keep Autoattack/Escape outside hotbar; use existing art, SVG glyphs and UI tokens.
- Verify 320x568, 360x640, 390x844 and 430x932 without horizontal overflow.

---

### Task 1: Per-participant target intent

**Files:**
- Modify: `src/Elyndor.Core/Combat/Sessions/CombatSession.cs` and its existing player runtime state type.
- Modify: the existing `UseAbilityCommand` file located by `rg "record UseAbilityCommand" src`.
- Modify: `src/Elyndor.Infrastructure/Combat/CombatApplicationService.cs`, `src/Elyndor.Server/Combat/CombatHub.cs`, `src/Elyndor.Contracts/Combat/CombatContracts.cs`, `src/Elyndor.Server/Combat/CombatContractMapper.cs`.
- Test: `tests/Elyndor.UnitTests/Combat/CombatSessionTests.cs`.

**Interfaces:** `UseAbility(sessionId, abilityId, targetActorId, commandId)` reaches the existing `UseAbilityCommand`. `SingleEnemy` validates living enemies and updates the existing requesting-player selection; `SingleAlly` validates living active allies and `AllowSelfTarget`; self/area/companion rules remain unchanged.

- [ ] Write failing tests that an explicit second enemy receives `STRIKE`, and a foreign/dead ally produces `CombatErrorCodes.InvalidTarget`.
- [ ] Run `dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --filter FullyQualifiedName~CombatSessionTests`; confirm current global selection cannot satisfy participant-specific behavior.
- [ ] Implement `ResolvePlayerAbilityTargetIds(AbilityDefinition ability, Guid requestedTargetActorId)`, retaining current resolution for non-selected target kinds. Reuse the existing selected hostile actor on the requesting player runtime; add no persistence.
- [ ] Update the existing selected hostile actor only after an explicit enemy target validates, and preserve command-id duplicate protection and the existing snapshot mapping.
- [ ] Re-run the focused test and `dotnet build Elyndor.slnx --configuration Release --no-restore`; commit `feat: validate participant ability targets`.

### Task 2: Per-enemy aggro projection

**Files:**
- Modify: combat snapshot/session projection under `src/Elyndor.Core/Combat/`.
- Modify: `src/Elyndor.Contracts/Combat/CombatContracts.cs`, `src/Elyndor.Server/Combat/CombatContractMapper.cs`.
- Test: `tests/Elyndor.UnitTests/Combat/GuardianThreatCombatSessionTests.cs`.
- Test: `tests/Elyndor.IntegrationTests/Combat/MultiplayerCombatFlowTests.cs`.

**Interfaces:** optional `currentAggroTargetActorId` on each enemy actor response, derived from the existing ThreatTable current target. No threat-value endpoint or calculation is added.

- [ ] Write a failing test with Enemy A targeting Tank and Enemy B targeting Mage, asserting both distinct IDs in the snapshot.
- [ ] Run `dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --filter FullyQualifiedName~GuardianThreatCombatSessionTests`; confirm projection is missing.
- [ ] Map existing threat target data; return null for non-enemies or absent/dead targets.
- [ ] Run the focused unit test and Docker-backed `MultiplayerCombatFlowTests`; commit `feat: expose enemy aggro targets in combat snapshots`.

### Task 3: Client selection state and target-aware ability command

**Files:**
- Modify: `web/elyndor-web/src/api/contracts.ts`.
- Modify: `web/elyndor-web/src/stores/combatSession.ts`.
- Test: `web/elyndor-web/src/__tests__/CombatSessionStore.spec.ts`.

**Interfaces:** store exposes `selectedHostileTargetId` as the existing authoritative snapshot field, local `selectedFriendlyTargetId`, `selectHostileTarget` through the existing SelectTarget command, `selectFriendlyTarget`, and target-aware `useAbility`.

- [ ] Write failing store tests: choosing an ally does not change hostile selection, choosing an enemy does not change friendly selection, a single-enemy ability sends the selected hostile actor id, a mock `SingleAlly` ability sends the selected friendly actor id, and an invalid target response preserves valid UI state.
- [ ] Run `npm run test:unit -- --run src/__tests__/CombatSessionStore.spec.ts`; confirm selections and command argument are absent.
- [ ] Implement only friendly normalization after authoritative snapshots: invalid/dead friendly falls to local player. Reuse server-side hostile normalization. Keep hotbar order unchanged across updates. Preserve the current one-in-flight ability guard and retry command id map.
- [ ] Run focused Vitest and `npm run type-check`; commit `feat: add independent combat target selections`.

### Task 4: Extend existing hotbar settings to twelve active positions

**Files:**
- Modify: `web/elyndor-web/src/game/combat/combatHotbarSettings.ts`.
- Modify: `web/elyndor-web/src/game/combat/CombatHotbarSettings.vue`.
- Test: `web/elyndor-web/src/game/combat/combatHotbarSettings.spec.ts`.

**Interfaces:** same local key and normalization utility; positions 1–12 active, position 13+ reserve.

- [ ] Write failing test with thirteen abilities: first twelve active and exactly one reserve. Add test that fewer abilities still render twelve active settings cells.
- [ ] Run `npm run test:unit -- --run src/game/combat/combatHotbarSettings.spec.ts`; confirm current boundary is six.
- [ ] Use `HOTBAR_SLOT_COUNT = 12`, an `activeSlots` array of 12 including null entries, and `reserveAbilities = orderedAbilities.slice(12)`. Preserve two-tap swap and reset; never include fixed system actions.
- [ ] Run focused tests, lint and build; commit `feat: expand combat hotbar settings to twelve slots`.

### Task 5: Composed Combat UI V3 battlefield

**Files:**
- Create: `web/elyndor-web/src/game/combat/components/CombatPartyPanel.vue`.
- Create: `web/elyndor-web/src/game/combat/components/CombatEnemyPanel.vue`.
- Create: `web/elyndor-web/src/game/combat/components/CombatHotbar.vue`.
- Create: `web/elyndor-web/src/game/combat/components/CombatSystemActions.vue`.
- Modify: `web/elyndor-web/src/game/combat/views/CombatView.vue`.
- Test: `web/elyndor-web/src/__tests__/CombatView.spec.ts`.

**Interfaces:** components receive combat actors, selection IDs and aggro IDs; they emit only target/action intent to the existing store.

- [ ] Write failing UI tests: 12 `[data-combat-hotbar-slot]` elements including empties; ally tap sets only friendly selection; enemy tap sets only hostile selection; AGGRO marker is distinct from both selection markers; Autoattack/Escape exist outside hotbar; fast ability tap produces one command.
- [ ] Run `npm run test:unit -- --run src/__tests__/CombatView.spec.ts`; confirm the current 6-slot battlefield fails expectations.
- [ ] Implement panels with compact full-card touch targets, accessible labels, portrait/name/HP/resource/dead states, per-enemy cast/unblockable telegraphs and current compact log. Implement a 2x6 adaptive hotbar with icon, keyboard number, cooldown/resource/authoritative reactive styling. Use 150–250ms transform/opacity aggro motion with `prefers-reduced-motion`; no deep watchers, polling or per-slot timers.
- [ ] Retain existing escape confirmation and autoattack behavior as fixed controls. Do not touch combat formulas.
- [ ] Run focused UI test, full Vitest, lint and build; commit `feat: build combat ui v3 battlefield`.

### Task 6: E2E, final review and unmerged PR

**Files:**
- Modify: current combat Playwright spec under `web/elyndor-web/e2e/`.

- [ ] Write a failing accessible E2E that finds Party and Enemy regions, 12 hotbar slots, Autoattack, Escape, ally/enemy selection, one ability use, and no horizontal overflow.
- [ ] Run `npm run test:e2e -- --grep "Combat UI V3"`; add only required roles, labels and data attributes. Capture solo, party, tank aggro, switched aggro, hostile selected, friendly selected and 320px screenshots in `output/playwright/combat-ui-v3/` when Docker-backed server/PostgreSQL is available.
- [ ] Run `dotnet restore Elyndor.slnx`, release build, full backend tests, content validation, web `npm ci`, lint, unit tests/build, equivalent admin checks, and real browser E2E. Record unavailable Docker-backed checks as blocked, never passed.
- [ ] Run `git diff --check`, review scope/security/contract compatibility, fetch and rebase on current `origin/main`, push the branch, open `Combat UI V3: battlefield targeting and 12-slot action bar`, wait for green CI, report PR and stop without merging.
