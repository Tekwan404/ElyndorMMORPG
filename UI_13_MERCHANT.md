# Elyndor — UI/UX Specification 13 — Merchant

**Document:** `UI_13_MERCHANT.md`  
**Status:** Approved foundation  
**Platform:** Telegram Mini App  
**Orientation:** Mobile Portrait First  
**Depends on:**
- `26_CURRENCY_AND_ECONOMY_SYSTEM.md`
- `UI_04_INVENTORY_AND_ITEMS.md`
- `UI_12_CITY_LOCATION.md`

---

# 1. Назначение

Merchant UI supports NPC buy/sell/buyback in City and uses authoritative Economy/Item prices.

---

# 2. Tabs

```text
ТОРГОВЕЦ
[КУПИТЬ] [ПРОДАТЬ] [ВЫКУП]
```

Buyback optional but supported.

---

# 3. Buy List

Rows/cards show:
- item icon;
- rarity;
- name;
- required level;
- price;
- stock/requirement;
- compare marker.

Tap → item details.

---

# 4. Purchase

```text
[ КУПИТЬ — 120 Gold ]
```

Quantity selector for stackable offers.
Server returns final price/result.

---

# 5. Sell Mode

Uses Inventory selection mode.
Eligible items only.
Multi-select supported.

Bottom:
```text
Выбрано: 6
Получите: 342 Gold
[ ПРОДАТЬ ]
```

---

# 6. Protected Items

QuestProtected/UserProtected/Equipped/TransactionLocked/AuctionEscrow excluded.

---

# 7. Buyback

Shows recent sold items with:
- sold time;
- buyback price;
- expiry.

No guarantee of infinite history.

---

# 8. Visual Reference

Use:
```text
references/08_merchant.png
```

NPC portrait/art should remain visible; merchant must feel like character interaction, not generic shop table.

---

# 9. Approved Decisions

1. Merchant only in City.
2. Buy/Sell/Buyback.
3. Sell uses Inventory multi-select.
4. Final prices server-authoritative.
5. Compare equipment available.
6. User protection respected.
