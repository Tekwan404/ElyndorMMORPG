# Elyndor UI Refactor Session 1 Design

**Status:** Approved visual direction; implementation pending  
**Scope owner:** `UI_REFACTOR_PROMPT.md`, Session 1  
**Visual direction:** Arcane Minimal

## Goal

Create a reusable UI foundation and procedural icon factory for the existing Vue 3 Telegram Mini App without changing gameplay behavior, backend contracts, or Phase 3 systems. Session 1 ends after its own Definition of Done and does not migrate the world, character, inventory, or combat screens.

## Visual language

- Near-black and deep navy backgrounds establish the base hierarchy.
- Cold blue and violet are the primary interaction and magical accents.
- Gold is reserved for legendary rarity, rare status cues, and exceptional hierarchy.
- Glow communicates selected, dangerous, legendary, or magical state; ordinary surfaces do not glow.
- Panels remain dark and neutral. Rarity affects the frame, a small accent, the item name, and a restrained semantic glow.
- Display typography uses the existing serif stack; dense UI text uses the existing readable system sans stack. Session 1 adds no remote font dependency.
- Touch targets are at least 44 by 44 CSS pixels.

## Token architecture

Create `src/styles/tokens.css` and import it before `base.scss`.

The token groups are:

- semantic colors and Telegram theme fallbacks;
- rarity colors and restrained rarity glows;
- spacing, radius, control height, touch target, and content width scales;
- display and UI typography scales;
- shadows, semantic glows, transitions, and z-index layers;
- safe-area and Telegram viewport fallbacks.

Canonical tokens use a `--ui-*` prefix. Existing `--color-*` variables become temporary aliases to canonical tokens so current screens remain stable until Session 2. Session 1 components must use only canonical tokens. Existing one-off screen styles are not migrated or deleted in Session 1.

## Icon system

Create `src/ui/icons/` with typed configuration, pure geometry definitions, a pure resolver, a Vue renderer, and presets.

```text
src/ui/icons/
├── glyphs.ts
├── icon.types.ts
├── icon-renderer.ts
├── IconGenerator.vue
└── presets/
    ├── items.ts
    ├── skills.ts
    └── effects.ts
```

`glyphs.ts` stores reusable SVG path geometry for the required weapon, equipment, item, modifier, and utility glyphs. It contains no emoji, raw HTML, or per-item SVG files.

`icon-renderer.ts` is a framework-independent resolver. It validates a typed `IconConfig`, applies safe defaults, resolves glyph and modifier definitions, and returns a render model containing semantic class names and layers. It does not access the DOM.

`IconGenerator.vue` renders the resolved model as one SVG composition:

1. neutral dark slot background;
2. base glyph paths;
3. optional modifier paths and restrained accent;
4. rarity frame and corner accent;
5. selected, equipped, locked, disabled, or new state treatment.

The renderer never uses `v-html`. New ordinary items require only a typed preset entry.

## Atomic components

Create `src/ui/components/` with controlled, presentation-focused primitives:

- `UIButton`: primary, secondary, ghost, danger; default, pressed, disabled, loading.
- `UIPanel`: semantic surface with optional title and actions slots.
- `UICard`: static or interactive card with selected and disabled semantics.
- `UIHealthBar`: bounded value/max rendering with HP and resource tones.
- `UITabs`: controlled `modelValue`, typed tab options, disabled tab support.
- `UIModal`: controlled open state, accessible dialog semantics, close event, body teleport.
- `UIToast`: presentational success, warning, danger, and info state; no global event bus.
- `UILoadingState`: loading, empty, and error presentation.
- `UIItemSlot`: composes `IconGenerator`, item name, quantity, rarity, and icon state.

Components do not own game state, call APIs, or introduce a parallel application store. They expose props, slots, and explicit events.

## Dev UI playground

Add `src/ui/playground/UiPlaygroundView.vue` and register `/dev/ui` only when `import.meta.env.DEV` is true. The route is absent from production routing and never appears in game navigation.

The existing router is currently not installed in `main.ts`. Session 1 completes that existing integration rather than adding a second routing mechanism: `App.vue` becomes the router outlet, `/world` renders `AppShell`, and `/dev/ui` renders the standalone playground only in development. Telegram initialization and session startup move with the game lifecycle into `AppShell`, so opening the playground never triggers authentication or game API calls.

The playground displays every required component variant and state, including rarity, modifiers, cooldown, loading, empty, disabled, selected, equipped, locked, and new. It is the visual verification surface for Session 1.

## Documentation

Create `docs/source-of-truth/ui/UI_DESIGN_GUIDELINES.md` as the concise UI foundation owner and link it from the UI index. It records tokens, typography, spacing, rarity, glow rules, component ownership, icon composition, states, mobile constraints, and Telegram fallbacks.

`UI_REFACTOR_PROMPT.md` remains the execution brief. Existing detailed UI specifications continue to own individual screens.

## Testing and verification

Keep tests focused:

- unit-test the pure icon resolver and important invalid/default combinations;
- component-test the critical semantics for button loading/disabled state, controlled tabs, modal dialog behavior, and item-slot state composition;
- run existing frontend unit tests, lint, format check, typecheck, and production build;
- use Playwright in a real browser against `/dev/ui` at Telegram-like and narrow mobile viewports;
- verify zero console errors and capture screenshots under `output/playwright/`;
- confirm `/dev/ui` is absent from the production build route table.

No backend or database verification is required unless implementation unexpectedly changes those layers.

## Failure and compatibility boundaries

- Missing or unknown glyph/preset values fail through TypeScript or the resolver's explicit fallback, never through unsafe markup.
- Values are clamped for visual bars; authoritative game state remains unchanged.
- Modal and toast primitives do not introduce global state or side effects.
- Current player flows remain on their existing components until Session 2.
- No React, Phaser, Storybook, external UI kit, remote font, or new game mechanic is added.

## Session 1 completion gate

Session 1 is complete only when every Session 1 item in `UI_REFACTOR_PROMPT.md` is implemented, the playground demonstrates all states, new code uses canonical tokens, focused and existing checks pass, browser console errors are zero, and the diff contains no gameplay/API changes or Session 2 migration.
