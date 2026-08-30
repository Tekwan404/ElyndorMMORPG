# Elyndor — UI/UX Specification 14 — Auction House

**Document:** `docs/source-of-truth/ui/UI_14_AUCTION.md`
**Status:** Approved foundation  
**Platform:** Telegram Mini App  
**Orientation:** Mobile Portrait First  
**Depends on:**
- `docs/source-of-truth/gameplay/27_TRADE_AND_AUCTION_SYSTEM.md`
- `docs/source-of-truth/gameplay/26_CURRENCY_AND_ECONOMY_SYSTEM.md`
- `docs/source-of-truth/ui/UI_04_INVENTORY_AND_ITEMS.md`

---

# 1. Назначение

Auction UI implements the current fixed-price buyout-only player market in City.

---

# 2. Tabs

```text
АУКЦИОН
[ПОИСК] [МОИ ЛОТЫ] [ВЫСТАВИТЬ]
```

---

# 3. Search

Search/filter:
- name;
- item type;
- slot;
- rarity;
- required level;
- class compatibility;
- price;
- affix/stat tags.

Sort:
```text
Цена ↑
Цена ↓
Время
Новые
Уровень
```

---

# 4. Listing Card

```text
[icon] Arcane Staff
EPIC · Lv 28

Key stats
Цена: 2 400 Gold
Осталось: 12:42:18

[КУПИТЬ]
```

---

# 5. Buy

Tap Buy:
- details/compare;
- confirm total price;
- server purchase.

No bidding UI.

---

# 6. Create Listing

```text
ВЫБРАТЬ ПРЕДМЕТ
→ eligible Inventory mode
→ quantity
→ price
→ duration
→ fee preview
→ [ВЫСТАВИТЬ]
```

Fee/tax clearly shown.

---

# 7. My Listings

States:
```text
ACTIVE
SOLD
EXPIRED
RETURN_PENDING
PAYOUT_PENDING
```

Active can cancel if not purchase-pending.

---

# 8. Full Inventory

Purchased item can become pending delivery.
UI shows pending item safely; no fake mail dependency.

---

# 9. Visual Reference

Use Auction reference from city/trade concept:
- dark list;
- item icons;
- price/time readability;
- strong tab bar.

---

# 10. Approved Decisions

1. Buyout-only.
2. Gold only.
3. No self-buy.
4. Listing fee + sale tax.
5. Auction only in City.
6. Bound/Quest items excluded.
7. Full inventory → pending delivery.
