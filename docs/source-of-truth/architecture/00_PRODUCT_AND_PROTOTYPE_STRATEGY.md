# Elyndor — Product and Prototype Strategy

**Status:** Product Source of Truth
**Current validation scope:** Levels 1–10

## Product promise

Elyndor is a Telegram-first MMORPG with near-instant entry from a conversation into a persistent game world. Telegram is part of acquisition, identity, return notifications, and social loops; it is not merely a WebView container.

Retention must come from combat, builds, loot, progression, economy, and social interaction. The returning-player path should be:

```text
Telegram
→ Elyndor
→ authoritative state restored
→ meaningful action
```

Target roughly 30 seconds or less to a meaningful action when technically and product-wise possible. Do not weaken server-side authentication or state restoration to reach that target.

## Prototype question

The first prototype validates whether three compact combat loops feel distinct and worth repeating through levels 1–10. It is not a miniature implementation of every production system.

Playable prototype classes:

- Warrior: Rage, offense versus defense, reactive decisions.
- Archer: Focus, priority/procs, fast actions.
- Mage: Mana, cast timing, sequencing, interrupt risk.

Use approximately 5–7 meaningful active abilities per class if the relevant system document does not define a smaller vertical slice. Do not create production-complete trees and do not add a fourth class before external validation of these three.

## Delivery strategy

Prefer complete vertical slices:

```text
Location → Hunt → Encounter → Combat → Reward → Inventory → Equipment
```

Each slice must be playable through Vue in a Telegram-like mobile viewport, recover authoritative state after reconnect where required, and include tests for rules that can damage progression or the economy.

Do not replace the approved .NET/PostgreSQL/Vue/Telegram stack or add a browser game engine without an explicit product decision.
