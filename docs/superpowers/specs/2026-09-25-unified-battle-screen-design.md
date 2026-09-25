# Unified Battle Screen Design

## Goal

Replace the layered combat presentation with one mobile-first battle screen that serves solo and 2–5 player combat, keeps character art readable, and makes server-owned combat state immediately understandable. The battlefield is the visual anchor; controls remain reachable and no gameplay result is calculated on the client.

## Scope and invariants

- One `BattleScreen.vue` renders solo and party combat from the same snapshot and event stream.
- `useCombatSessionStore` remains the only SignalR/API owner. A new `useBattle.ts` composable adapts the store for presentation and commands; it must not create another hub connection, event cursor, or combat state authority.
- Enemy selection, friendly selection, and enemy aggro remain independent concepts.
- Existing server validation for ability targets remains authoritative. The client sends only the requested actor id.
- Existing damage, healing, threat, class mechanics, cooldowns, AI, content, rewards, companions, reconnect, and multi-enemy behavior do not change.
- Character art continues to resolve through the existing `CharacterAppearance`/skin pipeline for local and remote participants.
- Raids remain out of scope. Party and dungeon combat are the largest supported groups.

## Existing state

The current implementation already has the important runtime foundations:

- `useCombatSessionStore` owns SignalR updates, retries, reconnect, commands, events, hostile selection, and local friendly selection.
- `CombatSnapshot.players`, actor ids, appearance fields, per-enemy aggro actor ids, and multi-enemy snapshots are available.
- The server already validates requested ability targets.
- `CombatView.vue` currently mixes projection, event formatting, arena rendering, abilities, consumables, controls, result states, and the combat log.
- `CombatBattlefieldPresentation.vue` teleports a second solo presentation into the battlefield while `combat-layout-v2.css` and `combat-layout-v2-polish.css` override the original layout. This creates competing render paths and makes layout behavior hard to reason about.
- Party combat currently renders only the local player, aggro target, and selected friendly target on the battlefield. The actors use a small horizontal grid rather than a stable depth formation.

## Chosen architecture

### Screen boundary

`BattleScreen.vue` becomes the route-level combat surface used by `AppShell`. It composes focused presentation components and owns only screen-level states such as the open combat-log drawer and flee confirmation.

`CombatView.vue` is removed after migration, or retained temporarily as a zero-logic compatibility wrapper only if an existing import cannot be migrated atomically. `CombatBattlefieldPresentation.vue` and its global Teleport are removed. The two combat-v2 override stylesheets are replaced by component-owned styles and a small shared battle token stylesheet.

### Presentation facade

`composables/useBattle.ts` exposes computed projections and intent methods over the existing stores:

- local actor, allies, enemies, selected enemy, selected friendly, aggro actor ids;
- authoritative casts, effects, cooldowns, resources, participant state, companion, rewards, and log events;
- commands for hostile/friendly selection, ability use, consumables, autoattack, attach, reset, and flee;
- event-to-presentation mapping for log entries and combat numbers.

It does not subscribe to SignalR itself. This preserves one event cursor, one reconnect path, and one command guard.

### Component structure

```text
BattleScreen.vue
├── composables/useBattle.ts
├── composables/useBattleFormation.ts
├── components/BattleHeader.vue
├── components/AlliesStrip.vue
├── components/BattleArena.vue
├── components/CharacterFigure.vue
├── components/AggroIndicator.vue
├── components/TargetSelection.vue
├── components/CombatNumbers.vue
├── components/SkillPanel.vue
├── components/ConsumableBar.vue
├── components/BattleControls.vue
└── components/CombatLog.vue
```

Existing reusable effect, icon, item, cast, reward, and loot-roll components remain in use. Components receive typed data and emit player intent; they do not import SignalR or calculate combat outcomes.

## Layout and responsive behavior

### Mobile composition

At 390×844 the normal active-combat composition targets:

- compact header: approximately 84–96 px;
- arena: `clamp(29rem, 60svh, 34rem)`, preserving the required 58–62% viewport emphasis;
- controls below the arena with four 64 px skill slots per row;
- compact consumable and system-action rows;
- collapsed combat log as one final row.

The baseline case at 390×844 — one skill row containing four abilities, 1–3 allies, ordinary safe-area insets — must fit without vertical scrolling. Vertical scroll is an explicit fallback only when a second skill row, an unusually large device safe area, accessibility text scaling, or a shorter viewport makes the fixed tap targets impossible to preserve. Actor art and controls must never overlap. Bottom navigation stays hidden during combat.

The visual language follows Elyndor's existing dark navy, restrained gold, crimson enemy accents, saturated ability art, and `ART > CHROME` rule. The supplied reference informs hierarchy and composition, not copied assets or invented mechanics.

### Header

`BattleHeader` presents the local player on the left and selected/main enemy on the right. Player HP and class resource remain visible; enemy HP and level remain visible. In party combat, `AlliesStrip` occupies a compact secondary header row rather than shrinking the two primary HUD blocks.

`AlliesStrip` uses real character portraits, mini HP bars, aggro and selected states, and at least a 44 px touch target. It shows no role labels. Selecting a portrait invokes the same friendly-selection command as selecting the arena figure.

### Arena

The enemy remains on the right and uses presentation sizing by rank:

- normal: baseline reduced size;
- elite: moderately larger;
- boss: largest, capped so it cannot cover the ally formation.

Allies occupy stable depth slots on the left and center. Actor art uses bottom anchoring, visible upper-body/face regions, explicit z-index, and controlled lower-body overlap. Decorative effects yield before recognizable silhouettes.

The local player is always included. For 1–3 allies, every ally is shown. For 4–5 allies, the initial implementation uses an adaptive depth formation showing everyone. A feature flag supports `frontline-three`: local player, current aggro target, and selected friendly target remain on stage while the rest remain in `AlliesStrip`. The fallback is enabled only if real 390×844 browser captures demonstrate unreadable faces, weapons, or touch targets.

## Formation model

`useBattleFormation.ts` is a pure layout projection. It accepts actor ids, local actor id, aggro actor id, viewport class, fallback mode, and explicit `FormationConstraints`, then returns stable slots containing normalized x/y, scale, z-index, visibility, full bounds, and protected upper-body bounds.

```ts
interface FormationConstraints {
  minUpperBodyClearance: number // normalized arena units; default 0.035
  protectedUpperBodyRatio: number // upper fraction of actor bounds; default 0.34
  minimumTouchTargetPx: number // default 44
}
```

The formation contract expands every protected upper-body rectangle by `minUpperBodyClearance / 2` on each axis. No two expanded protected rectangles may intersect. This protects faces, helmets, upper weapons, and large shoulder silhouettes independently of CSS z-index. Lower-body rectangles may overlap deliberately. A layout that cannot satisfy the constraint at the current viewport is invalid and deterministically selects `frontline-three` before rendering; screenshot review is not the mechanism that discovers invalid geometry.

Base slot assignment is stable by captured participant order. Friendly selection never changes a slot. Aggro temporarily promotes only the aggro actor to the dedicated front slot using a 240 ms ease-out transform; other actors keep their base positions and do not jump. When aggro leaves, the promoted actor returns to its base slot.

Formation changes caused by join/leave preserve existing actor slot assignments where possible and animate only changed actors. Reduced-motion removes movement while preserving immediate state and outline changes.

Unit tests evaluate every valid assignment for 1–5 actors at supported mobile viewport classes and assert pairwise non-intersection of expanded protected upper-body bounds. Separate tests prove that deliberately impossible constraints select the fallback rather than returning overlapping slots.

## Targeting and aggro

The presentation uses distinct values:

- hostile target: authoritative `snapshot.selectedTargetActorId`;
- friendly target: existing `selectedFriendlyTargetActorId`;
- aggro targets: each enemy's `currentAggroTargetActorId`.

Selecting a friendly actor updates only friendly selection. Aggro changes update only formation/aggro presentation. Selecting an enemy updates only hostile selection. A selected-and-aggro ally is rendered once with both states.

`TargetSelection` provides a persistent gold outline that does not depend on hover. `AggroIndicator` provides a compact crimson marker/glow. Dead, inactive, fled, or removed friendly actors cannot remain selected; existing store normalization falls back to the local active player where possible.

## Combat events and numbers

`useBattle.ts` consumes the already deduplicated store event list and derives presentation events without modifying snapshots. `CombatNumbers` maintains short-lived visual entries keyed by combat sequence and target actor id.

Supported number types:

- damage: red/crimson;
- critical damage: larger gold/crimson emphasis;
- healing: green;
- miss/avoidance: muted text;
- periodic damage: smaller than direct damage.

Each entry anchors to the target actor's formation slot or enemy slot. Entries are capped per actor and expire after a short animation to prevent group-combat spam. They use a bold readable UI font and respect reduced motion.

`CharacterFigure` receives the computed slot width and exposes responsive image sizing through explicit intrinsic dimensions and a `sizes` value derived from the mobile/tablet slot contract. Frontline/local actors load eagerly with high fetch priority; rear actors decode asynchronously with normal/low priority. The component does not request a desktop-sized render box for a 70–85% rear slot, and adding real `srcset` variants later will not require changing the formation or component API.

## Skills, consumables, and controls

`SkillPanel` renders local-player abilities only. Mobile uses four columns and 64×64 px minimum buttons; additional abilities create another row. Ready, cooldown, insufficient-resource, invalid-target, queued, casting, and proc states remain visually distinct. Cooldown combines numeric time with dimming/desaturation rather than relying on text alone.

`ConsumableBar` is a compact horizontal row using existing item icons and server-owned cooldown/eligibility. `BattleControls` contains autoattack and flee/leave actions. Existing confirmation and server validation are preserved.

## Combat log

`CombatLog` is collapsed by default to the latest meaningful event and event glyph. Tapping opens an accessible bottom drawer occupying about 45svh, with recent entries, scroll containment, safe-area padding, Escape/backdrop dismissal, and focus restoration. It does not compete with the arena while collapsed.

## Companion and multi-enemy behavior

Companions retain their existing actor kind and targeting semantics. They are displayed as a compact companion unit and never enter the party formation or ally roster merely because friendly targeting exists.

The selected/main enemy is shown as the large right-side actor. Existing multi-enemy target selection remains available as a compact target strip. Combat numbers and casts resolve by actor id, so secondary enemies remain supported without introducing a single-enemy-only UI model.

## Failure and recovery behavior

- Reconnect reuses the current store snapshot and event cursor; no second combat loop or event replay mechanism is introduced.
- Invalid target and command failures use the current sanitized combat error path.
- Missing character or monster art uses existing fallbacks without breaking layout.
- Join/leave/death re-normalizes formation and selection from current snapshot data.
- A completed session does not retain interactive actor controls.
- No client-side damage, hit, threat, cooldown, or reward calculation is added.

## Verification strategy

### Unit and component tests

- stable formation for 1–5 allies;
- pairwise upper-body clearance for every valid 1–5 actor slot assignment;
- deterministic `frontline-three` fallback when clearance cannot be satisfied;
- aggro promotion moves only the aggro actor;
- selected target does not change formation;
- selected and aggro states can coexist on one actor without duplication;
- roster and battlefield selection share one state;
- dead/removed selection normalization;
- companion exclusion from party formation;
- hostile and friendly target independence;
- ability target intent uses the existing server-authoritative path;
- combat-number classification, actor anchoring, cap, and expiry;
- four-column 64 px skill layout and cooldown visual state;
- no vertical overflow at 390×844 for four skills and 1–3 allies under normal safe-area insets;
- responsive figure sizing/fetch priority for frontline and rear actors;
- collapsed and expanded combat log behavior.

### Browser verification

Add a deterministic development/test-only battle preview that uses production components with fixture snapshots but cannot be enabled in a production build by default. Capture and inspect:

- solo at 390×844;
- party of 3 at 390×844;
- party of 5 at 390×844;
- aggro transfer;
- selected ally distinct from aggro ally;
- reduced-motion mode;
- 360 px narrow viewport;
- tablet/desktop sanity layouts.

Playwright assertions cover overflow, touch-target sizes, actor uniqueness, visible selected/aggro states, log drawer, and skill/control separation. Screenshots are reviewed for face/weapon overlap before choosing the five-actor default or `frontline-three` fallback.

### Regression commands

- affected Vitest suites;
- full frontend unit suite;
- lint, typecheck, and production build;
- relevant combat store/backend targeting tests if contracts change;
- Playwright battle preview tests and real combat smoke test;
- final diff review for balance/content/backend drift.

## Non-goals

- No new abilities, combat formulas, threat rules, AI, balance, loot, or content.
- No raid UI or raid-specific participant abstraction.
- No canvas/WebGL/Phaser renderer or heavy particle system.
- No new persistence for selected friendly targets.
- No replacement of the existing SignalR store.
- No equipment-compositing system beyond the current character appearance art.
