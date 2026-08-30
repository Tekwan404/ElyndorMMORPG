# Elyndor — UI/UX Specification 16 — Crafting & Professions

**Document:** `docs/source-of-truth/ui/UI_16_CRAFTING_AND_PROFESSIONS.md`
**Status:** Approved foundation  
**Platform:** Telegram Mini App  
**Orientation:** Mobile Portrait First  
**Depends on:**
- `docs/source-of-truth/gameplay/29_CRAFTING_AND_PROFESSION_SYSTEM.md`
- `docs/source-of-truth/ui/UI_12_CITY_LOCATION.md`
- `docs/source-of-truth/ui/UI_04_INVENTORY_AND_ITEMS.md`

---

# 1. Назначение

Crafting UI supports Blacksmithing, Alchemy and Cooking, profession progression, recipe discovery and instant/timed craft.

---

# 2. Profession Hub

```text
ПРОФЕССИИ

Кузнечное дело  24
Alchemy          18
Cooking          31
```

Все три доступны одному персонажу.

---

# 3. Profession Screen

```text
КУЗНЕЧНОЕ ДЕЛО
Level 24
XP █████░

[Оружие] [Броня] [Материалы]

Known recipes
Locked recipes
```

---

# 4. Recipe Card

Shows:
- result icon;
- name;
- required profession level;
- materials owned/required;
- Gold fee;
- craft time.

Green/red only for availability, not rarity.

---

# 5. Craft Details

```text
Arcane Blade

Iron 12/8
Crystal Dust 4/2
Gold 250

Время: 02:00

[ СОЗДАТЬ ]
```

---

# 6. Timed Craft

After start:
```text
ИЗГОТОВЛЕНИЕ
01:48
```

Continues offline.
Current default one concurrent operation.

---

# 7. Result

Completed:
```text
ГОТОВО
[EPIC item]

[ЗАБРАТЬ]
```

If inventory full → RESULT_PENDING.

---

# 8. Recipe Unlock

New recipe reveal:
```text
НОВЫЙ РЕЦЕПТ
```

Shows source if relevant.

---

# 9. Station Context

Forge / Alchemy Table / Kitchen are City services.
Future Home Kitchen can reuse same UI.

---

# 10. Approved Decisions

1. Blacksmithing/Alchemy/Cooking.
2. All three learnable.
3. Profession level 1–60.
4. No random fail by default.
5. Timed craft continues offline.
6. Craft uses Item System + Economy.
7. Full inventory does not lose result.
