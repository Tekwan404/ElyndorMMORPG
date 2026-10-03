# Elyndor UI Kit — production contracts

The UI Kit extends existing `src/ui/components` and `src/styles/tokens.css`.
There is no second theme, request dispatcher or gameplay state store.

## Action feedback

- `UIButton`: `loading` means this action is pending, not an unrelated mutation.
  Use `loadingLabel` for a meaningful action verb; `disabled` has no spinner.
- Use existing store operation keys. Conflicting operations on the same item
  remain mutually exclusive; unrelated game screens must not look busy.
- Success follows an awaited canonical store operation. Never announce purchase,
  rewards or equipment changes optimistically. Never retry mutations automatically.
- `UIToast`: inline by default, `placement="overlay"` for combat feedback that
  must not move layout. Errors use alert/assertive; normal outcomes status/polite.
- `UILoadingState`: mutually exclusive loading/error/empty. Put an explicit retry
  button in its slot for failed reads. Preserve feature-specific error messages.

## Dialogs and review

- `UIModal`: focus capture, keyboard containment, Escape/backdrop close, focus
  restore, scroll ownership and safe-area sizing are common behavior.
- Opening order owns input and stacking, independent of component DOM order.
- Set `busy` while a request is running. Keep a failed action's dialog open and
  show its error inside the dialog, not underneath its overlay.
- `useConfirmation` + `UIConfirmation` review intent only; callers perform the
  operation after acceptance. Unmount cancels outstanding review. No API calls
  or economy logic belong in the confirmation component.
- Custom drawers such as CombatLog are not automatically migrated. Do not replace
  their composition with a generic modal without checking their game contract.

## Items and combat

- `ItemIdentity` composes canonical `ItemIcon`, rarity tokens/labels and existing
  quality stars. Inventory and equipment use the same item heading.
- `ItemSetSummary` counts authoritative equipped items with the same `setId`.
  Current inventory DTO lacks set names/bonus descriptions: do not invent them,
  special-case another set, or calculate activation thresholds on the client.
- Compare and equipment eligibility stay in their existing feature paths.
- Combat feedback is an overlay. Reconnect blocks network commands until manual
  resume succeeds and retains retry on failure; friendly selection remains local.
- Ability inspection supports existing touch hold plus F1/Escape. Cooldowns and
  effects still use server snapshots. No client-side combat calculations added.
- Specialized HUD bars/effect strips/skill buttons keep their production sizing;
  consolidation with generic UIHealthBar/UIEffectBadge remains a later small pass.

## Migration order

1. Combat → Inventory/Equipment → World/Location.
2. Quests → Party/Dungeon.
3. Merchant → Auction → Trade → Mailbox → Professions → Shop → AFK.

`/dev/ui` remains an existing showcase, not a product feature. Verification needs
both component/view states and browser geometry; a mocked preview is not proof
of real multiplayer, economic settlement or dungeon reconnect behavior.
