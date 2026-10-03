# Elyndor UI Kit: baseline audit

Date: 2026-10-03. Baseline: `origin/main` at
`f39d00bd0d7157bdf01cc59d6ad0ed01c7028128`.
Branch: `feat/elyndor-ui-kit`.

This is a static frontend audit and proposed migration order, not a claim that
all gameplay flows have been exercised in a browser. No gameplay, API contracts,
content or production UI have changed in this pass.

## Inventory

Scope: `web/elyndor-web/src`. There are 87 Vue files, including 58 under
`src/game`, and 14 existing Vue components under `src/ui/components`.
Counts below are files containing component tags, not live route counts; some
legacy/test-only views are included. Raw buttons are not automatically duplicates:
ability buttons, actor selection and navigation have different contracts.

| Existing component | Game files using its tag |
| --- | ---: |
| UIButton | 28 |
| UIModal | 12 |
| UILoadingState | 7 |
| UIToast | 3 |
| MoneyAmount | 3 |
| UITabs | 0 |
| UIHealthBar | 0 |
| UIItemSlot | 0 |

32 game Vue files contain native button tags. Inventory/Merchant contain six
`window.confirm` calls. The imported `betaUxPolish.css` has 67 lines containing
`!important`, including selectors coupled to individual game screens.

## Confirmed findings

1. **Existing foundations are underused, not absent.** `tokens.css` already owns
   colors, rarity, spacing, type, touch targets, layers, motion and Telegram/device
   safe areas. Extend it rather than introducing another theme or package.
2. **Parallel buttons, tabs and feedback.** Auction, Mailbox and TradePanel have
   their own native-button CSS. Auction and QuestView implement their own tabs;
   PremiumStore has another tab treatment. Existing UITabs is unused by game views.
3. **Modal behavior is incomplete.** UIModal listens for Escape and supports
   backdrop close, but has no focus capture/trap/restore or scroll lock. Premium
   product/currency sheets and CombatLog separately implement overlay behavior.
   Their distinct game compositions should remain; only common mechanics belong
   in a shared overlay contract. Preserve premium preview affordances.
4. **Tabs lack complete semantics.** UITabs assigns tab roles but has no roving
   focus or arrow/Home/End navigation and no panel linkage contract.
5. **Confirmation differs by screen.** Inventory/Merchant use native confirms;
   Party builds confirmation inside UIModal and closes it before awaiting the
   action. Auction uses an inline second-click confirmation. Financial actions
   need explicit review, pending and error behavior, not automatic retries.
6. **Loading/error/empty can conflict.** Mailbox renders an error paragraph and
   then its empty state if the failed first load left an empty array. It lacks a
   dedicated refresh/retry CTA and explicit successful-claim feedback.
7. **Operation state is inconsistently consumed.** gameSession already exposes
   isMutationPending/isMutationDomainPending. Quest actions and several Inventory
   operations still consume the aggregate mutationPending. Trade serializes its
   protocol through pending; keep that safety, but show which operation is running.
8. **Feedback patterns vary.** UIToast is an inline notice, not a positioned toast
   host. Combat and Arena have separate fixed-error implementations; Auction,
   Mailbox, Profession and Party use local messages. Error classification must
   preserve existing domain messages and avoid exposing raw diagnostics by default.
9. **Item presentation is partly centralized.** ItemIcon is already canonical and
   reusable. Keep it. Rarity label helpers recur in Inventory, Forge, Merchant,
   CharacterOverviewV2, BattleScreen and commerce. Row/card anatomy, amount and
   rarity can be shared without moving item eligibility/business rules into UI.
10. **HUD bars duplicate presentation.** BattleHeader has its own HP/resource markup;
    UIHealthBar is available. Any migration must preserve battle density and actor
    layout instead of substituting a larger generic HUD.
11. **Legacy naming is not evidence of dead code.** WorldViewLegacy remains the
    production wrapper for location systems. HeroView imports CharacterOverviewV2;
    the older CharacterOverviewView and QuestsView need reachability/test review
    before deletion. Do not delete legacy files based only on their names.
12. **Global override debt is active.** App.vue imports betaUxPolish.css. Move
    migrated rules into component ownership before removing corresponding overrides;
    blanket deletion would risk unrelated layouts.

## Flow map and verification boundaries

| Flow | Current surfaces | Next UX checks |
| --- | --- | --- |
| World / Location / AFK | WorldMapView, LocationOverview, WorldViewLegacy | Request feedback, AFK preview/result, domain pending, retry |
| Combat / Loot | BattleScreen, SkillPanel, CombatLog | Preserve arena, fixed errors, claim/roll pending and reconciliation |
| Inventory / Equipment | InventoryView, CharacterOverviewV2 | Selection toolbar, protected actions, equip feedback, native confirms |
| Quest / Turn-in | QuestView, AdventurerGuildBoard | Per-action pending, reward outcome, retry, shared tabs |
| Party / Invite | PartyView, FriendsView | Invite/accept pending, confirmation remains until outcome, reconnect |
| Dungeon / Wipe / Reward | DungeonLocationCard and dungeon/combat stores | Existing retry, run return, membership, wipe/reconnect, reward feedback |
| Auction | AuctionView | Shared tabs/buttons, quote/confirmation, pending row, stale-lot/error outcome |
| Trade | TradePanel and tradeStore | Offer/lock/confirm state, pending operation, disconnect reconciliation |
| Mailbox | MailboxView | Mutually exclusive load/error/empty, retry, claim outcome |
| Shop / Skins | PremiumStoreView, MerchantShop, CharacterSkinStoreView | Shared overlay lifecycle, purchase/equip feedback, eligibility reasons |
| Professions / Forge | ProfessionView, SkinningAftermath, ForgeView | Existing pending keys, confirmation, success/error, item selection |
| PvP | ArenaView, ArenaBattlefield, ArenaInvitations | Queue/invite/ability pending, reconnect, error overlay |
| World boss / Other | WorldBossView, MenuView, CompanionView, talents | Reuse components without changing gameplay or frozen raid mechanics |

Browser tests for these flows remain outstanding. Static presence of an error
label or disabled prop does not establish a complete UX flow.

## Recommended incremental architecture (approval pending)

- **Foundations:** keep current tokens and game art; reconcile semantic type,
  control, state and layer usage. No new UI dependency.
- **Primitives:** improve UIButton, UITabs, UIModal, UILoadingState and UIToast
  backwards-compatibly. Add purpose-specific APIs only where existing components
  cannot reasonably cover a repeated need.
- **Shared patterns:** common overlay lifecycle, action feedback, confirmation,
  page/section header, item/list rows, rarity and selection toolbar. Feature stores
  remain authoritative for requests/results; primitives never call gameplay APIs.
- **First migration (approved):** Combat, Inventory/Equipment, World/Location.
  Preserve combat composition and use /dev/ui only as an existing showcase.
- **Following passes:** Quests, Party/Dungeon, then Merchant, Auction, Trade,
  Mailbox, Professions, Shop, AFK and other secondary screens.
- **Cleanup:** remove proven replaced CSS/mappings/components only after consumer
  and test migration, recording adoption and remaining debt after each pass.

Action contract: `idle -> pending -> success | error`; pending is scoped to an
operation/entity, not the entire game. Success means server-confirmed outcome.
Read errors offer retry. Ambiguous mutation failures reconcile canonical state
before presenting a retry; UI refactoring must not introduce duplicate economic
requests or optimistic reward/purchase success.

Accessibility/mobile contract: visible focus/selection, keyboard navigation,
44px minimum action hit areas, readable text, reduced motion, safe areas,
long Russian labels and narrow 360/390/480px layouts. Keep desktop/tablet behavior.

Verification per code pass: component state tests, migrated-view tests, full
frontend unit tests, lint, typecheck/build and focused Playwright flow/layout
checks. Backend verification is required if backend behavior/contracts change.

## Pass 0 result

Audit complete enough to select the first migration slice. No duplicate removed,
no screen migrated, and no tests/build run in this documentation-only pass.
This baseline was followed by user approval of the core-loop-first order above.

## Pass 1: production core loop

### Unified

- UIButton loading labels, focus and reduced-motion behavior.
- UIModal focus/scroll ownership, opening-order stacking, busy dismissal guard
  and sticky mobile actions. Nested review cannot hide behind an item dialog.
- UIToast inline/overlay placement and alert/status semantics; UILoadingState
  defaults are Russian. UITabs supports roving focus and arrow/Home/End keys.
- UIConfirmation/useConfirmation replace four Inventory native confirms.
- ItemIdentity/ItemSetSummary unify Inventory and equipment item headings,
  canonical icons, rarity, quality and equipped set-piece count.

### Migrated surfaces and outcomes

| Surface | Changes | Preserved |
| --- | --- | --- |
| BattleScreen | Fixed shared feedback, explicit reconnect retry, commands blocked through resume, failed flee stays open | Arena composition, actor art/sizing, target policies, effect/cooldown runtime |
| InventoryView | Shared item identity/review, action-specific pending, conflicting item-operation guard, equip/use feedback, consumable failure stays open, pending-loot read error/retry | Compare, restrictions, quantity/selection and economy paths |
| CharacterOverviewV2 | Shared identity/set summary, unequip feedback/pending | Paperdoll, full stats and slot navigation |
| WorldViewLegacy | Shared errors and operation-specific busy feedback for explore/training/join | Production location wrapper, AFK and dungeon flows |
| LocationOverview | Shared loading/error/empty/retry and latest-request guard | Artwork/layout, residents/loot and primary actions |

### Removed duplicates

- Four Inventory `window.confirm` calls (Merchant's two remain deferred).
- Separate BattleScreen error-overlay and WorldViewLegacy error CSS.
- LocationOverview loading/quiet-state CSS.
- Duplicate Inventory/equipment item-heading markup/CSS and unused rarity helpers.
- False hardcoded tracker-set hint; actual set membership is no longer mislabeled.

### Remaining UX debt / verification boundary

- Set bonus descriptions/names are not supplied by inventory DTO. Counts are
  shown honestly; a later presentation-contract change is needed for full bonuses.
- Generic HUD bars/effects/cooldowns are not yet substituted into specialized
  battle widgets. Their geometry/production behavior must be preserved in a
  separate small migration, not a battle redesign.
- Pending-loot read failures now offer explicit retry without disabling inventory;
  broad loot-state/reconciliation and real-server flow tests need a later pass.
- Quests/Party/Dungeon state migration follows next; all commerce, professions,
  Shop and AFK redesign work remains deferred per the approved priority.
- Real Telegram clients and complete authenticated multiplayer/economy flows
  were not manually tested. Mocked battle preview is only layout verification.
- Existing 13 ESLint E2E warnings, large bundle warning and npm audit findings
  were not addressed by this UI-only scope. No dependency upgrade was performed.

### Verification

- Final `npm run test:unit -- --maxWorkers=2`: 89 files, 404 tests passed.
  An earlier unrestricted run hit an existing content-art test's 5-second
  timeout under simultaneous browser/build load; no test timeout was relaxed.
- Targeted `npm run test:e2e -- e2e/battle-screen.spec.ts`: 5 passed using the
  development preview, including narrow/reduced-motion, solo/party and 16 skills.
- `npm run lint`: zero errors, 13 existing warnings in unrelated E2E files.
- `npm run build` (includes `vue-tsc --build`): passed; large chunk warning remains.
- Browser CLI inspection: /dev/ui at 390x844 and 360x780 has no horizontal
  overflow; modal captures focus/locks scroll and Escape closes it. Five-player
  preview screenshot reviewed; error preview confirms fixed notification at 360px.
- `git diff --check`: clean. No backend/content/API/dependency changes; backend
  tests not run. This does not certify real authenticated multiplayer flows.

## Pass 2: Quest journal, Party and Dungeon run actions

Production screens now share UITabs, UILoadingState, UIToast, UIButton and
UIModal/UIConfirmation instead of local tab/error/loading treatments.

- QuestView: accessible shared tabs, load retry (including generic network
  errors), scoped quest pending keys, abandon review and truthful reward feedback.
  A null claim response never displays success; tab changes made while loading
  are not overwritten by initial-tab selection.
- PartyView: initial membership loading/error cannot masquerade as an empty
  group; panel titles use the existing slot contract. Destructive confirmation
  stays open/busy through execution and failure. Group mutations show progress,
  result/error; conflicting view actions are guarded and polling pauses during
  confirmation/execution. Mobile member actions wrap, with shared 44px targets.
- DungeonLocationCard: command errors no longer replace known run progress.
  Initial membership/load failures remain blocking and retryable; cached preview
  alone does not authorize a new run. Reconciliation preserves known progress.
  Create/enter/restart/return/city-exit/start/leave show scoped pending feedback.
  Permanent leave explicitly differs from temporary city exit and is confirmed.
- Removed quest-local tab/loading/empty/error markup and tab/error CSS, party and
  dungeon bespoke error styling, unsupported party panel title attributes and
  sub-44px party button overrides.
- Shell E2E close-button locators now match the shared Russian accessible label.

No new backend contract, gameplay, content, dependencies or combat layout.
This bounded pass does not claim all quest offer/NPC flows or all game screens
are migrated: AdventurerGuildBoard/other offer panels and real authenticated
party/dungeon wipe/reconnect validation remain follow-up work. Commerce,
professions, Shop and AFK remain deferred. Existing set-bonus DTO limitation and
specialized HUD consolidation from pass 1 remain unchanged.

### Pass 2 verification

- Final full unit run: 89 files, 419 tests passed (`--maxWorkers=2`).
- Local full Playwright run: 7 passed, 1 skipped (real-database professions test).
- Final build/typecheck passed; lint zero errors and 13 pre-existing warnings.
- Browser production PartyView with mock stores at 360x780: no horizontal
  overflow, all action buttons 44px; failed confirmation remains open and its
  bottom-sheet screenshot was reviewed. Dungeon browser mount was inspected,
  but full real-server dungeon flows remain unverified locally.
- Failure-first regressions reproduced hidden progress, premature dismissal,
  unknown initial membership and reconciliation replacement before fixes.
- Final `git diff --check` clean. Backend unchanged; required CI still gates merge.
