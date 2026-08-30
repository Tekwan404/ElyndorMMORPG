# Elyndor UI Design Guidelines

## Direction

Elyndor uses **Arcane Minimal**: deep neutral surfaces, cold blue/violet as the primary system accent, cyan for secondary information, and restrained gold only for legendary or exceptional hierarchy. The interface should feel like a game, not a generic dashboard, while remaining quiet enough for dense MMORPG information.

## Canonical tokens

`web/elyndor-web/src/styles/tokens.css` owns all visual values. UI code uses `--ui-*` tokens; the temporary pre-refactor `--color-*` and safe-area aliases were removed after the existing production screens migrated in Session 2A. Telegram theme and safe-area variables are bounded fallbacks rather than the visual source of truth.

- Spacing uses `--ui-space-1` through `--ui-space-8`.
- Interactive targets are at least `--ui-touch-target` (44px) in both dimensions.
- Body text uses `--ui-font-body`; selective headings use `--ui-font-display`.
- Surfaces remain dark and neutral. Rarity appears in borders and small accents, not full-card fills.
- Glow is semantic: selected, dangerous, legendary, or important magical state only.

## Components

Reusable primitives live in `src/ui/components`: `UIButton`, `UIPanel`, `UICard`, `UIHealthBar`, `UITabs`, `UIModal`, `UIToast`, `UILoadingState`, and `UIItemSlot`. They are controlled presentation components: no API calls, gameplay state, singleton buses, or duplicated Pinia state.

## Icons

`src/ui/icons` is the data-driven icon source. Each icon combines typed 24x24 SVG geometry with optional rarity, elemental modifier, and interaction state. Required states are default, selected, equipped, locked, disabled, and new. Do not use emoji, `v-html`, remote icon fonts, or per-screen handcrafted copies of icon logic.

## Verification and migration boundary

In development, `/dev/ui` is the visual playground for component variants, rarity, modifiers, cooldown, feedback, and system states. It must never appear in the production route table or game navigation.

Session 2A migrated the implemented production shell, character creation, character stats, and location flow. Combat and Inventory do not yet have authoritative production flows: Combat belongs to Phase 4 and Inventory to Phase 5. Their UI migration happens with those implementations rather than introducing fake client-only gameplay state.
