# Inventory organization

The inventory browser uses the existing character snapshot, canonical spatial
capacity API, item renderer, details/compare sheet and mutation operations.
No ownership, equipment, economy or content rules are changed.

- Four categories: All; Equipment (including spatial artifacts); Supplies
  (consumables and loot containers); Materials. Unknown future types remain
  visible in All under Other items.
- Default ordering groups items by purpose, rarity and name, with stable instance
  ID tie-breaking. Explicit sorts span the whole result; Received restores server order.
- Sort and presentation preferences are saved per character in browser storage.
  Filters/search are temporary; clearing them never resets the saved sort.
  Contextual equipment picking does not overwrite normal bag preferences.
- Search is case-insensitive, trims whitespace and treats Russian е/ё equally.
- Icon-only Grid is the compact default: four columns on phones, more on wider
  screens. An optional large-icon mode uses two columns. Names remain accessible
  to assistive technology; inspect shows full descriptions and comparison.
  Switching icon density preserves filters and selection.
- Category counts describe the bag, not stack quantities or filtered results.
  Capacity remains authoritative and is never inferred from rendered cells.
- Empty capacity slots are not rendered. Overflow items remain accessible.
- New and Wearable are independent filters, not item categories. Active filters
  stay visible; sorting is not counted as a filter.
- Contextual equipment picking still respects the original slot and compatibility
  rules. Search and presentation never broaden that contextual result.

`InventoryToolbar` owns presentation and emits model changes; `InventoryView`
owns filtering, selection and existing game operations. The organization helper
only classifies existing item types. Do not use it for gameplay eligibility.

Verification: inventory component regressions, full Vitest, typecheck/build,
lint and browser checks at 360/390px and desktop. Browser fixtures and screenshots
live in ignored `output/playwright`, not production content.
