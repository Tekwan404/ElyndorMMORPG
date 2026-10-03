# Elyndor UI Kit: core loop implementation plan

**Goal:** Make World -> Combat -> Loot -> Inventory -> Equipment share reliable
UI behavior without changing gameplay, backend contracts or battlefield geometry.
**Architecture:** Extend existing UI components backwards-compatibly. Feature
stores own canonical operation outcomes; shared components only present them.
**Stack:** Vue 3 Composition API, TypeScript, existing tokens, Vitest, Playwright.
**Spec:** User-approved order supersedes the migration order in
`docs/development/ui-kit-audit.md`.

## Constraints

- Keep mobile-first, 44px actions, safe areas, desktop/tablet layouts, game art.
- Never retry mutations automatically or infer successful rewards/equipment.
- Preserve compare data, actor IDs, effects, cooldowns and targeting.
- No dependencies, content edits, backend changes, main edits or merge.

## Component map

- UIButton: action appearance and busy/disabled feedback; optional loading label.
- UIModal + useModalLayer: shared focus, Escape, scroll lock, safe-area sheet;
  `open`, `title`, `busy`, `close` remain presentation inputs/events.
- UIToast: inline or fixed feedback; explicit success/error semantics.
- UIConfirmation + useConfirmation: local confirmation promise, no API calls.
- ItemIdentity: existing ItemIcon, rarity and quality composition; no stat math.
- BattleScreen: render store connection/error state through shared feedback.
- Inventory/CharacterOverviewV2: retain feature rules; reuse confirmation and
  item identity, report canonical equip/unequip outcomes.
- World/Location: same action/loading/error patterns, store pending keys.

## Execution

1. [x] Baseline frontend suite; add failing tests for modal focus/stack/scroll,
   busy dismissal, tabs keyboard navigation and overlay notifications.
2. [x] Extend primitives; run ui component tests.
3. [x] Add failing Combat tests for reconnect/recovery feedback and accessible
   ability inspection; migrate feedback without moving arena or changing skills.
4. [x] Add failing Inventory tests for confirmation, canonical equip feedback
   and real set identity; migrate shared presentation/confirmation.
5. [x] World/Location feedback migration and view tests (disjoint worker scope).
6. [x] Full frontend tests, lint, typecheck/build; existing battle E2E and browser
   inspection at 360/390px plus tablet/desktop. Review diff, report each pass and
   remaining limitations. Quest/Party/Dungeon and secondary migrations are the
   subsequent passes, not prerequisites for this first core-loop deliverable.
