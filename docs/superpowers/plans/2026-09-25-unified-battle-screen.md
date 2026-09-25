# Unified Battle Screen Implementation Plan

> **For Codex:** REQUIRED SUB-SKILL: Use `executing-plans` to implement this plan task-by-task with review checkpoints.

**Goal:** Replace the competing combat render paths with one mobile-first Vue battle screen for solo and 1–5-player dungeon combat, while preserving the existing server-authoritative combat store and gameplay behavior.

**Architecture:** `useCombatSessionStore` remains the only API/SignalR state owner. A thin `useBattle.ts` presentation facade projects actors, commands, combat numbers, and log entries into focused components. Formation is a pure deterministic function with an explicit upper-body-clearance contract and a deterministic three-actor fallback when five readable figures cannot fit.

**Tech Stack:** Vue 3 Composition API, TypeScript, Pinia, Vitest/Vue Test Utils, CSS container/media queries, Playwright, Vite.

---

## Task 1: Lock formation geometry behind a tested contract

**Files:**
- Create: `web/elyndor-web/src/game/combat/composables/useBattleFormation.ts`
- Create: `web/elyndor-web/src/game/combat/composables/useBattleFormation.spec.ts`

- [ ] Write failing table tests for 1–5 actors at compact/mobile/tablet widths. Assert stable actor order, one frontline slot, unique slots, touch size, and pairwise non-intersection of expanded protected upper-body rectangles.
- [ ] Add a failing impossible-geometry test that expects the deterministic `frontline-three` fallback and keeps local, aggro, then selected actors without duplicates.
- [ ] Implement the smallest pure API:

```ts
export interface FormationConstraints {
  minUpperBodyClearance: number
  protectedUpperBodyRatio: number
  minimumTouchTargetPx: number
}

export interface BattleFormationSlot {
  actorId: string
  slotId: string
  x: number
  y: number
  scale: number
  zIndex: number
  visible: boolean
  bounds: NormalizedRect
  protectedUpperBodyBounds: NormalizedRect
}

export function buildBattleFormation(input: BattleFormationInput): BattleFormationResult
export function protectedBoundsOverlap(left: NormalizedRect, right: NormalizedRect): boolean
```

- [ ] Keep selection out of position calculation; only local/aggro identity may affect the frontline overlay. Use defaults `0.035`, `0.34`, and `44`.
- [ ] Run `npm test -- --run src/game/combat/composables/useBattleFormation.spec.ts`, inspect failure then green result, and commit.

## Task 2: Build one presentation facade over the existing combat store

**Files:**
- Create: `web/elyndor-web/src/game/combat/composables/useBattle.ts`
- Create: `web/elyndor-web/src/game/combat/composables/useBattle.spec.ts`
- Create: `web/elyndor-web/src/game/combat/battleEventPresentation.ts`
- Create: `web/elyndor-web/src/game/combat/battleEventPresentation.spec.ts`
- Read/Reuse: `web/elyndor-web/src/stores/combatSession.ts`

- [ ] Write failing tests proving hostile target, selected friendly target, and per-enemy aggro remain independent across snapshot refreshes and aggro changes.
- [ ] Write failing tests for event projection: damage, critical, heal, miss, skill use, join, and leave create normalized visual/log entries tied to `targetActorId` without calculating gameplay values.
- [ ] Implement `useBattle()` as computed projections and intent methods only; it must call the current store for `selectTarget`, `selectFriendlyTarget`, abilities, consumables, auto-attack, flee, reconnect, and training commands.
- [ ] Deduplicate party actors by actor id, exclude companion actors from party formation, and preserve backend ordering where possible.
- [ ] Run the new tests plus `src/__tests__/CombatSessionStore.spec.ts` and commit.

## Task 3: Implement reusable actor visuals and responsive art loading

**Files:**
- Create: `web/elyndor-web/src/game/combat/components/TargetSelection.vue`
- Create: `web/elyndor-web/src/game/combat/components/AggroIndicator.vue`
- Create: `web/elyndor-web/src/game/combat/components/CharacterFigure.vue`
- Create: `web/elyndor-web/src/game/combat/components/CharacterFigure.spec.ts`
- Reuse: `web/elyndor-web/src/assets/characterArt.ts`

- [ ] Write failing component tests for combined selected+aggro state, click/tap selection, accessible label, dead state, and no duplicated overlay.
- [ ] Implement a single figure component using the existing appearance resolver (`classId`, `genderId`, `skinId`) and actor art identity.
- [ ] Compute `sizes` from viewport/formation slot. Load local/frontline art eagerly with high priority and rear actors lazily with lower priority; expose a source-candidate contract that can accept `srcset` when generated variants exist.
- [ ] Ensure touch target is at least 44px and state is visible without hover; decorative effects must remain behind the protected upper-body area.
- [ ] Run the component tests and commit.

## Task 4: Build arena, enemy presentation, and actor-bound combat numbers

**Files:**
- Create: `web/elyndor-web/src/game/combat/components/BattleArena.vue`
- Create: `web/elyndor-web/src/game/combat/components/CombatNumbers.vue`
- Create: `web/elyndor-web/src/game/combat/components/BattleArena.spec.ts`
- Reuse: `web/elyndor-web/src/game/combat/CombatEnemyTargetList.vue`
- Reuse: `web/elyndor-web/src/game/combat/CombatBlockFeedback.vue`

- [ ] Write failing tests for solo, three-player, and five-player rendering; aggro+selection on one actor must render one figure, not two.
- [ ] Write failing tests that damage/heal/crit/miss numbers anchor to the target actor slot and expire after animation without mutating combat state.
- [ ] Render allies from formation slots and the current primary enemy on the right. Apply rank-aware enemy scale (`normal`, `elite`, `boss`) without allowing the enemy to cover the ally protected region.
- [ ] Keep existing multi-enemy hostile selection available in a compact layer and render companion separately without treating it as a party member.
- [ ] Add 200–300ms transform/scale transitions for aggro, independent selection outline animation, and reduced-motion behavior. Run tests and commit.

## Task 5: Build compact header and one shared friendly target surface

**Files:**
- Create: `web/elyndor-web/src/game/combat/components/BattleHeader.vue`
- Create: `web/elyndor-web/src/game/combat/components/AlliesStrip.vue`
- Create: `web/elyndor-web/src/game/combat/components/BattleHeader.spec.ts`
- Reuse: `web/elyndor-web/src/game/combat/CombatEffectStrip.vue`

- [ ] Write failing tests showing roster taps and battlefield taps call the same selection intent and both reflect one selected actor id.
- [ ] Implement local HP/resource and enemy HP summaries, with a 42–50px ally portrait strip only for party combat.
- [ ] Use existing actor appearance data and HP/resource values; do not add role labels or a separate target store.
- [ ] Preserve selection on harmless rerenders and show dead/unavailable actors without offering invalid selection.
- [ ] Run tests and commit.

## Task 6: Replace controls with mobile-first skill, consumable, and action components

**Files:**
- Create: `web/elyndor-web/src/game/combat/components/SkillPanel.vue`
- Create: `web/elyndor-web/src/game/combat/components/ConsumableBar.vue`
- Create: `web/elyndor-web/src/game/combat/components/BattleControls.vue`
- Create: `web/elyndor-web/src/game/combat/components/SkillPanel.spec.ts`
- Reuse: `web/elyndor-web/src/game/combat/combatAbilityGroups.ts`
- Reuse: `web/elyndor-web/src/game/combat/combatHotbarSettings.ts`
- Reuse: `web/elyndor-web/src/game/combat/CombatHotbarSettings.vue`

- [ ] Write failing tests for four 64px skill buttons per mobile row, clear ready/cooldown states, inspected ability behavior, and forwarding the existing resolved target id.
- [ ] Migrate existing hotbar ordering, grouping, long-press/details, cast, cooldown, global cooldown, and disabled-reason behavior without duplicating combat rules.
- [ ] Render consumables as a compact row and auto-attack/flee as reachable controls; keep training controls where applicable.
- [ ] Add a layout contract test/class asserting the baseline 390×844 case with four skills and 1–3 allies uses the non-scroll layout. Allow scroll only for two skill rows, large safe area/text scaling, or short viewport.
- [ ] Run hotbar/settings/new component tests and commit.

## Task 7: Implement the collapsed combat log and accessible drawer

**Files:**
- Create: `web/elyndor-web/src/game/combat/components/CombatLog.vue`
- Create: `web/elyndor-web/src/game/combat/components/CombatLog.spec.ts`
- Reuse: `web/elyndor-web/src/game/combat/BossCombatLogReporter.vue`

- [ ] Write failing tests for one-line collapsed latest event, opening the 40–50svh drawer, scrolling recent entries, backdrop/Escape close, and focus restoration.
- [ ] Implement event icons and readable text from the normalized presentation entries; do not duplicate the store event cursor.
- [ ] Preserve boss log reporting/telemetry behavior outside the visible drawer.
- [ ] Run tests and commit.

## Task 8: Compose and adopt the single BattleScreen

**Files:**
- Create: `web/elyndor-web/src/game/combat/views/BattleScreen.vue`
- Create: `web/elyndor-web/src/__tests__/BattleScreen.spec.ts`
- Modify: `web/elyndor-web/src/app/AppShell.vue`
- Modify: `web/elyndor-web/src/App.vue`
- Modify: `web/elyndor-web/src/main.ts`
- Modify: `web/elyndor-web/src/__tests__/AppShell.spec.ts`
- Delete after migration: `web/elyndor-web/src/game/combat/views/CombatView.vue`
- Delete after migration: `web/elyndor-web/src/game/combat/CombatBattlefieldPresentation.vue`
- Delete after migration: `web/elyndor-web/src/styles/combat-layout-v2.css`
- Delete after migration: `web/elyndor-web/src/styles/combat-layout-v2-polish.css`

- [ ] Port existing active, terminal, result, pending-loot, training, reconnect, error, and leave behavior into one screen-level composition; first make migrated `CombatView` tests fail against `BattleScreen`.
- [ ] Compose header, arena, controls, and log using `min-height: 100svh`, Telegram safe-area variables, and an arena target of 58–62svh.
- [ ] Replace the `AppShell` import, remove the global Teleport presenter and legacy override stylesheet imports, then delete the obsolete render path only after all migrated tests pass.
- [ ] Verify the baseline no-scroll contract for 390×844 / four skills / 1–3 allies and prevent ability bar overlap at 360px.
- [ ] Run all combat/component tests and commit.

## Task 9: Add deterministic browser fixtures and visual verification

**Files:**
- Create: `web/elyndor-web/e2e/battle-screen.spec.ts`
- Modify or Create: a development/test-only battle preview entry discovered from the current router/bootstrap conventions

- [ ] Add production-safe deterministic fixtures for solo, three allies, and five allies; they must be unavailable in production builds.
- [ ] Add Playwright checks at 390×844 for three and five allies, verifying arena height, no baseline page scroll, no ability overlap, actor touch targets, and independent target/aggro classes.
- [ ] Add checks at 360px and tablet width, reduced-motion mode, aggro transfer, friendly selection, log drawer, and the deterministic three-actor fallback.
- [ ] Capture and inspect screenshots for face/weapon readability. Treat screenshots as visual regression evidence after geometry tests, not as the geometry contract.
- [ ] Fix every reproducible UI issue found and rerun the browser suite.

## Task 10: Complete regression verification and review

**Files:**
- Review all files changed by this plan.

- [ ] Run targeted Vitest suites for formation, facade, actors, arena, header, skills, log, store targeting/reconnect, companion, and BattleScreen.
- [ ] Run frontend unit tests, lint, typecheck, and production build using repository scripts.
- [ ] Run relevant backend combat targeting/reconnect tests only if the existing transport contract changed; otherwise document that backend code was untouched.
- [ ] Use `elyndor-review`, `requesting-code-review`, and `verification-before-completion` to inspect the final diff for gameplay drift, duplicate state/event ownership, accessibility, safe areas, stale CSS/imports, and untracked files.
- [ ] Commit only after fresh verification evidence is green. Report changed systems, exact commands/results, visual fallback behavior, and remaining limitations; do not merge or push `main`.
