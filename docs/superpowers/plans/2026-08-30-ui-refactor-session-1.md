# Elyndor UI Refactor Session 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Arcane Minimal design-token foundation, procedural SVG icon factory, atomic Vue UI primitives, and dev-only visual playground required by Session 1.

**Architecture:** Canonical CSS tokens own visual values while compatibility aliases keep existing screens stable until Session 2. A framework-independent icon resolver converts typed presets into safe SVG render models, Vue primitives remain controlled and presentation-only, and a development-only route demonstrates every state without entering production routing.

**Tech Stack:** Vue 3.5, TypeScript 5.9, Vite 8, SCSS/CSS custom properties, Vitest 4, Vue Test Utils, Playwright CLI.

**Spec:** `docs/superpowers/specs/2026-08-30-ui-refactor-session-1-design.md`

## Global Constraints

- Work only on Session 1 from `UI_REFACTOR_PROMPT.md`; do not migrate gameplay screens from Session 2.
- Keep Vue 3, TypeScript, and Vite; add no React, Phaser, Storybook, UI kit, remote font, backend, database, or API change.
- Arcane Minimal uses cold blue/violet as the primary accent and gold only for legendary or exceptional hierarchy.
- Glow is semantic only: selected, dangerous, legendary, or important magical state.
- All new interactive touch targets are at least 44 by 44 CSS pixels.
- Glyphs use pure typed SVG geometry and never emoji, raw HTML, or `v-html`.
- Components own presentation only and never call APIs or duplicate Pinia game state.

---

### Task 1: Canonical Arcane Minimal tokens

**Files:**
- Create: `web/elyndor-web/src/styles/tokens.css`
- Modify: `web/elyndor-web/src/styles/base.scss`
- Modify: `web/elyndor-web/src/main.ts`

**Interfaces:**
- Consumes: Telegram CSS variables exposed by the Mini App host.
- Produces: canonical `--ui-*` tokens and temporary legacy `--color-*` aliases used by current screens.

- [x] **Step 1: Create the canonical token file**

Define Arcane Minimal groups under `:root`: background/surface/border, primary/secondary/danger/warning/success, text and disabled colors; all six rarities; spacing from `--ui-space-1` through `--ui-space-8`; radii; 44px touch target and control heights; display/UI typography; shadows, semantic glows, transition durations, z-index layers; safe-area and Telegram viewport fallbacks.

Use Telegram variables only as bounded fallbacks, for example:

```css
:root {
  --ui-color-background: var(--tg-theme-bg-color, #070911);
  --ui-color-surface-1: #0d1220;
  --ui-color-primary: #7f7bea;
  --ui-color-secondary: #3da7bd;
  --ui-rarity-epic: #8d66d9;
  --ui-rarity-legendary: #b8914f;
  --ui-touch-target: 44px;
  --ui-glow-selected: 0 0 0 1px rgb(127 123 234 / 55%), 0 0 12px rgb(127 123 234 / 22%);
}
```

- [x] **Step 2: Import tokens before global styles and alias legacy variables**

In `main.ts`, import `tokens.css` before `base.scss`. In `base.scss`, remove canonical color declarations and keep compatibility aliases only:

```scss
:root {
  --color-text-primary: var(--ui-color-text-primary);
  --color-text-secondary: var(--ui-color-text-secondary);
  --color-text-muted: var(--ui-color-text-muted);
  --color-gold: var(--ui-rarity-legendary);
  --color-gold-bright: var(--ui-color-warning);
  --color-border: var(--ui-color-border);
}
```

Use canonical tokens for the body background and base focus-visible outline. Do not migrate scoped gameplay styles.

- [x] **Step 3: Run the frontend baseline gate**

Run:

```powershell
npm run type-check --prefix web/elyndor-web
npm run build-only --prefix web/elyndor-web
```

Expected: both commands exit 0 and the existing game still compiles.

- [x] **Step 4: Commit the token foundation**

```powershell
git add web/elyndor-web/src/styles/tokens.css web/elyndor-web/src/styles/base.scss web/elyndor-web/src/main.ts
git commit -m "feat: add Arcane Minimal UI tokens"
```

---

### Task 2: Typed data-driven SVG icon factory

**Files:**
- Create: `web/elyndor-web/src/ui/icons/icon.types.ts`
- Create: `web/elyndor-web/src/ui/icons/glyphs.ts`
- Create: `web/elyndor-web/src/ui/icons/icon-renderer.ts`
- Create: `web/elyndor-web/src/ui/icons/IconGenerator.vue`
- Create: `web/elyndor-web/src/ui/icons/presets/items.ts`
- Create: `web/elyndor-web/src/ui/icons/presets/skills.ts`
- Create: `web/elyndor-web/src/ui/icons/presets/effects.ts`
- Create: `web/elyndor-web/src/ui/icons/__tests__/icon-renderer.spec.ts`

**Interfaces:**
- Produces: `GlyphName`, `IconCategory`, `Rarity`, `ModifierName`, `IconState`, `IconConfig`, `ResolvedIcon`, `resolveIcon(config: IconConfig): ResolvedIcon`, `GLYPHS`, and `IconGenerator.vue` props `{ config: IconConfig; label?: string }`.
- Consumes: canonical tokens from Task 1.

- [x] **Step 1: Write failing resolver tests**

Create tests that demand composition without Vue or DOM:

```ts
it('resolves glyph rarity modifier and state into semantic layers', () => {
  const result = resolveIcon({
    id: 'flameblade',
    glyph: 'sword',
    category: 'weapon',
    rarity: 'epic',
    modifier: 'fire',
    state: 'selected',
  })

  expect(result.glyph).toBe(GLYPHS.sword)
  expect(result.modifier).toBe(GLYPHS.fire)
  expect(result.classes).toEqual(['icon--epic', 'icon--fire', 'icon--selected'])
})

it('uses common default rarity and default state', () => {
  expect(resolveIcon({ id: 'ore', glyph: 'ore', category: 'resource' }).classes).toEqual([
    'icon--common',
    'icon--default',
  ])
})
```

- [x] **Step 2: Run tests and verify RED**

Run:

```powershell
npm run test:unit --prefix web/elyndor-web -- src/ui/icons/__tests__/icon-renderer.spec.ts
```

Expected: FAIL because icon types and resolver do not exist.

- [x] **Step 3: Implement typed icon contracts and geometry library**

Define exact unions:

```ts
export type GlyphName =
  | 'sword' | 'greatsword' | 'dagger' | 'axe' | 'bow' | 'staff' | 'shield'
  | 'helmet' | 'armor' | 'boots' | 'ring'
  | 'potion' | 'scroll' | 'chest' | 'ore' | 'herb'
  | 'fire' | 'ice' | 'lightning' | 'poison' | 'holy' | 'shadow'
  | 'skull' | 'star' | 'lock'

export type IconCategory = 'weapon' | 'equipment' | 'consumable' | 'resource' | 'skill' | 'effect' | 'utility'
export type Rarity = 'common' | 'uncommon' | 'rare' | 'epic' | 'legendary' | 'unique'
export type ModifierName = 'fire' | 'ice' | 'lightning' | 'poison' | 'holy' | 'shadow'
export type IconState = 'default' | 'selected' | 'equipped' | 'locked' | 'disabled' | 'new'
export type GlyphDefinition = { readonly paths: readonly string[] }
```

Populate `GLYPHS: Record<GlyphName, GlyphDefinition>` with path geometry for every required name. Keep the `viewBox` consistently `0 0 24 24` and geometry recognizable at 44px.

- [x] **Step 4: Implement the pure resolver and typed presets**

Implement:

```ts
export function resolveIcon(config: IconConfig): ResolvedIcon {
  const rarity = config.rarity ?? 'common'
  const state = config.state ?? 'default'
  return {
    id: config.id,
    glyph: GLYPHS[config.glyph],
    modifier: config.modifier ? GLYPHS[config.modifier] : null,
    rarity,
    state,
    classes: [
      `icon--${rarity}`,
      ...(config.modifier ? [`icon--${config.modifier}`] : []),
      `icon--${state}`,
    ],
  }
}
```

Add representative item, skill, and effect presets, including flameblade, frost staff, poison dagger, healing potion, locked chest, and new ore.

- [x] **Step 5: Implement `IconGenerator.vue` without unsafe markup**

Render SVG paths with `v-for`, modifier geometry in a separate group, a rarity accent, and state overlays. Locked uses the typed lock glyph. Selected/equipped/new use semantic markers. Disabled uses opacity and grayscale. Add `role="img"` only when `label` exists; otherwise use `aria-hidden="true"`.

- [x] **Step 6: Run focused tests and typecheck**

```powershell
npm run test:unit --prefix web/elyndor-web -- src/ui/icons/__tests__/icon-renderer.spec.ts
npm run type-check --prefix web/elyndor-web
```

Expected: resolver tests pass and TypeScript exits 0.

- [x] **Step 7: Commit the icon factory**

```powershell
git add web/elyndor-web/src/ui/icons
git commit -m "feat: add data-driven UI icon factory"
```

---

### Task 3: Controlled atomic Vue components

**Files:**
- Create: `web/elyndor-web/src/ui/components/UIButton.vue`
- Create: `web/elyndor-web/src/ui/components/UIPanel.vue`
- Create: `web/elyndor-web/src/ui/components/UICard.vue`
- Create: `web/elyndor-web/src/ui/components/UIHealthBar.vue`
- Create: `web/elyndor-web/src/ui/components/UITabs.vue`
- Create: `web/elyndor-web/src/ui/components/UIModal.vue`
- Create: `web/elyndor-web/src/ui/components/UIToast.vue`
- Create: `web/elyndor-web/src/ui/components/UILoadingState.vue`
- Create: `web/elyndor-web/src/ui/components/UIItemSlot.vue`
- Create: `web/elyndor-web/src/ui/components/index.ts`
- Create: `web/elyndor-web/src/ui/components/__tests__/ui-components.spec.ts`

**Interfaces:**
- Consumes: Task 1 tokens and Task 2 `IconConfig`/`IconGenerator`.
- Produces: named component exports and controlled props/events defined in the design spec.

- [x] **Step 1: Write failing component behavior tests**

Cover behavior rather than CSS snapshots:

```ts
it('disables UIButton while loading and exposes status text', () => {
  const wrapper = mount(UIButton, { props: { loading: true }, slots: { default: 'Travel' } })
  expect(wrapper.get('button').attributes('disabled')).toBeDefined()
  expect(wrapper.get('button').attributes('aria-busy')).toBe('true')
})

it('emits the selected enabled tab value', async () => {
  const wrapper = mount(UITabs, { props: { modelValue: 'items', tabs } })
  await wrapper.get('[data-tab="stats"]').trigger('click')
  expect(wrapper.emitted('update:modelValue')).toEqual([['stats']])
})

it('renders modal dialog semantics and closes from its close control', async () => {
  const wrapper = mount(UIModal, { props: { open: true, title: 'Details' }, attachTo: document.body })
  expect(document.body.querySelector('[role="dialog"]')).not.toBeNull()
  await wrapper.get('[data-modal-close]').trigger('click')
  expect(wrapper.emitted('close')).toHaveLength(1)
})
```

Add one `UIItemSlot` assertion for locked state and accessible item label.

- [x] **Step 2: Run tests and verify RED**

```powershell
npm run test:unit --prefix web/elyndor-web -- src/ui/components/__tests__/ui-components.spec.ts
```

Expected: FAIL because components do not exist.

- [x] **Step 3: Implement buttons, surfaces, bars, and tabs**

Use canonical tokens exclusively. `UIButton` supports only `primary | secondary | ghost | danger`. `UIHealthBar` clamps its displayed percentage to 0–100 without mutating props. `UITabs` does not emit disabled values.

- [x] **Step 4: Implement modal, toast, system state, and item slot**

Use controlled props/events. `UIModal` teleports to body, emits close, and has `aria-modal="true"`. `UIToast` is presentational and has no singleton bus. `UILoadingState` owns loading/empty/error visuals. `UIItemSlot` composes `IconGenerator` and never rebuilds icon logic.

- [x] **Step 5: Export the primitives and run focused tests**

```powershell
npm run test:unit --prefix web/elyndor-web -- src/ui/components/__tests__/ui-components.spec.ts
npm run type-check --prefix web/elyndor-web
```

Expected: component tests pass and TypeScript exits 0.

- [ ] **Step 6: Commit atomic components**

```powershell
git add web/elyndor-web/src/ui/components
git commit -m "feat: add Arcane Minimal UI primitives"
```

---

### Task 4: Development playground and UI guidelines

**Files:**
- Create: `web/elyndor-web/src/ui/playground/UiPlaygroundView.vue`
- Modify: `web/elyndor-web/src/router/index.ts`
- Modify: `web/elyndor-web/src/main.ts`
- Modify: `web/elyndor-web/src/App.vue`
- Modify: `web/elyndor-web/src/app/AppShell.vue`
- Create: `docs/source-of-truth/ui/UI_DESIGN_GUIDELINES.md`
- Modify: `docs/source-of-truth/ui/00_UI_REFERENCE_INDEX.md`

**Interfaces:**
- Consumes: all components and presets from Tasks 2 and 3.
- Produces: development-only route `/dev/ui` and the concise UI Source of Truth.

- [ ] **Step 1: Write a failing production-route guard test**

Add `web/elyndor-web/src/ui/playground/__tests__/playground-route.spec.ts` that imports a small exported `createRoutes(isDevelopment: boolean)` helper from the router module and asserts:

```ts
expect(createRoutes(false).some((route) => route.path === '/dev/ui')).toBe(false)
expect(createRoutes(true).some((route) => route.path === '/dev/ui')).toBe(true)
```

- [ ] **Step 2: Run the route test and verify RED**

```powershell
npm run test:unit --prefix web/elyndor-web -- src/ui/playground/__tests__/playground-route.spec.ts
```

Expected: FAIL because `createRoutes` and the playground route do not exist.

- [ ] **Step 3: Build the playground**

Display all required variants and states in clear sections: buttons; panel/card; health/resource bars; tabs; toast; modal; loading/empty/error; cooldown; rarity; fire/ice/poison; selected/equipped/locked/new. Add one interactive modal toggle and controlled tab example so behavior is testable in the browser.

- [ ] **Step 4: Add the development-only route**

Export `createRoutes(isDevelopment: boolean): RouteRecordRaw[]`. Make `/world` lazily render `AppShell`, append the lazy `/dev/ui` route only when `isDevelopment` is true, and create the router with `createRoutes(import.meta.env.DEV)`. Install the router in `main.ts`, replace the direct shell in `App.vue` with `RouterView`, and move Telegram initialization plus `session.start()` into the mounted lifecycle of `AppShell`. Do not add the playground to game navigation. This removes the existing dead-router condition without creating a second routing mechanism.

- [ ] **Step 5: Write concise UI guidelines**

Document canonical token ownership, Arcane Minimal palette, typography, spacing, 44px rule, rarity and glow semantics, component list, icon composition, modifiers/states, Telegram fallbacks, and the Session 2 migration boundary. Link the file from `00_UI_REFERENCE_INDEX.md`.

- [ ] **Step 6: Run route test, all unit tests, lint, format, and build**

```powershell
npm run test:unit --prefix web/elyndor-web -- src/ui/playground/__tests__/playground-route.spec.ts
npm run test:unit --prefix web/elyndor-web
npm run lint --prefix web/elyndor-web
npm run format:check --prefix web/elyndor-web
npm run build --prefix web/elyndor-web
```

Expected: all commands exit 0.

- [ ] **Step 7: Commit playground and guidelines**

```powershell
git add web/elyndor-web/src/ui/playground web/elyndor-web/src/router/index.ts web/elyndor-web/src/main.ts web/elyndor-web/src/App.vue web/elyndor-web/src/app/AppShell.vue docs/source-of-truth/ui/UI_DESIGN_GUIDELINES.md docs/source-of-truth/ui/00_UI_REFERENCE_INDEX.md
git commit -m "feat: add UI development playground"
```

---

### Task 5: Browser verification and Session 1 review

**Files:**
- Create artifacts only under ignored `output/playwright/`.
- Modify implementation files only for defects reproduced during this task.

**Interfaces:**
- Consumes: `/dev/ui` from Task 4 and the repository Playwright CLI wrapper.
- Produces: browser evidence, final reviewed diff, and Session 1 DoD report.

- [ ] **Step 1: Start Vite development mode for the playground**

```powershell
npm run dev --prefix web/elyndor-web -- --host 127.0.0.1
```

Keep it in a managed terminal session and use the actual printed port.

- [ ] **Step 2: Open and inspect `/dev/ui` in a real browser**

Use the Playwright CLI wrapper after confirming `npx` exists. Open the route, take a snapshot, interact with tabs and modal by current snapshot refs, then re-snapshot.

Verify:

- no page or console errors;
- all required states are visible;
- controls and modal work;
- no emoji are used as glyphs;
- ordinary slots remain dark while rarity is restrained;
- selected/legendary glow is semantic;
- focus-visible and disabled state are distinguishable.

- [ ] **Step 3: Verify Telegram-like and narrow mobile viewports**

Use 390 by 844 and 320 by 568 viewports. Capture screenshots under `output/playwright/`. Verify no horizontal overflow, clipped controls, unreadable labels, or touch targets below 44px.

- [ ] **Step 4: Verify the production route boundary**

Run the production preview and request `/dev/ui`. Confirm the development component is not present in the production route table and normal production navigation remains unchanged.

- [ ] **Step 5: Run the final automated gate**

```powershell
npm run lint --prefix web/elyndor-web
npm run format:check --prefix web/elyndor-web
npm run test:unit --prefix web/elyndor-web
npm run build --prefix web/elyndor-web
git diff --check
```

Expected: all commands exit 0.

- [ ] **Step 6: Review complete diff**

Review for correctness, unsafe SVG markup, missing states, hardcoded colors in new UI files, accessibility regressions, duplicated icon logic, production dev-route leakage, backend/API changes, Session 2 scope creep, secrets, and unrelated modifications.

- [ ] **Step 7: Commit verification fixes if required**

If browser verification produced a reproduced defect, commit only its tested fix:

```powershell
git add web/elyndor-web/src/ui web/elyndor-web/src/styles web/elyndor-web/src/router/index.ts web/elyndor-web/src/main.ts docs/source-of-truth/ui
git commit -m "fix: resolve UI playground verification findings"
```

Then stop. Report Session 1 results and do not start Session 2.
