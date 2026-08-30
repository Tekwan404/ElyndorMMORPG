# Elyndor — UI/UX Specification 17 — Menu

**Document:** `docs/source-of-truth/ui/UI_17_MENU.md`
**Status:** Approved foundation  
**Platform:** Telegram Mini App  
**Orientation:** Mobile Portrait First  
**Depends on:**
- `docs/source-of-truth/ui/UI_01_GLOBAL_GAME_SHELL.md`
- `docs/source-of-truth/ui/UI_02_WORLD_AND_LOCATION.md`

---

# 1. Назначение

`МЕНЮ` хранит второстепенные функции, чтобы primary navigation оставалась игровой: Мир / Герой / Локация / Квесты / Меню.

---

# 2. Menu Grid/List

Recommended cards/list:

```text
Друзья
Группа
Достижения
Почта
Рейтинг
Бестиарий / Глоссарий
Новости / Обновления
Помощь
Настройки
```

---

# 3. Friends

Friend list:
- online;
- location summary optional;
- invite party;
- profile.

Friend system gameplay details can remain lightweight until separately expanded.

---

# 4. Achievements

Displays achievement categories/progress/rewards if/when content exists.
Does not affect current combat architecture.

---

# 5. Mail

Mail entry can exist as social/system inbox.
Auction pending delivery does NOT depend on Mail.

---

# 6. Rankings

Future leaderboard categories can be plugged in.
No fake rank metrics before source data exists.

---

# 7. Bestiary / Glossary

Future knowledge base:
- enemies;
- bosses;
- locations;
- known drops;
- abilities/lore.

Known drops can unlock after discovery/kills.
This is where detailed loot knowledge belongs, not enemy rows.

---

# 8. News / Updates

Patch notes / events / announcements.
External/web content should be safely rendered inside app context.

---

# 9. Approved Decisions

1. Menu is secondary systems.
2. Party duplicated here but quick access remains HUD.
3. Guild is not Menu item; it is City service.
4. Bestiary/Glossary belongs here.
5. Settings lives here.
