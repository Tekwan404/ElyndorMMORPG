# Elyndor — UI/UX Specification 12 — City Location

**Document:** `UI_12_CITY_LOCATION.md`  
**Status:** Approved foundation  
**Platform:** Telegram Mini App  
**Orientation:** Mobile Portrait First  
**Depends on:**
- `UI_02_WORLD_AND_LOCATION.md`
- `04_WORLD_AND_LOCATIONS_SYSTEM.md`
- `26_CURRENCY_AND_ECONOMY_SYSTEM.md`

---

# 1. Назначение

City is not a global root. It is a special `ЛОКАЦИЯ` state exposing safe services only when the character is physically inside that city.

---

# 2. City Layout

```text
GLOBAL HUD

ЭЛИНДОР
SAFE

[LARGE CITY ART]

TRACKED QUESTS

ГОРОДСКИЕ СЕРВИСЫ
[Merchant] [Auction]
[Guild] [Forge]
[Alchemy] [Cooking]

NPC / Quests
Exits
```

---

# 3. Services

Current:
- Merchant;
- Auction;
- Guild;
- Blacksmithing;
- Alchemy;
- Cooking.

Future:
- Storage;
- Mail;
- Tavern;
- trainer-like content if approved.

---

# 4. Availability

Outside City these service cards do not appear at all.

No disabled global Merchant/Auction shortcut from forest.

---

# 5. Service Card

Large icon/art + short status:
```text
АУКЦИОН
12 новых подходящих лотов
```
Optional badges only for meaningful state.

---

# 6. Safe State

City communicates:
```text
SAFE
```

No normal enemy rows.
No quick-combat buttons.

---

# 7. Visual Reference

Use:
```text
references/07_city_hub.png
references/03_city_trade_guild.png
```

City feels alive, not like a flat admin menu.

---

# 8. Approved Decisions

1. City is Location.
2. Services only physically in City.
3. Large city art.
4. Services as visual cards.
5. Quest/NPC/exits remain part of same location screen.
