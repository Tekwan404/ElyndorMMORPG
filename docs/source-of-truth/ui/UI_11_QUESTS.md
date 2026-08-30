# Elyndor — UI/UX Specification 11 — Quests

**Document:** `docs/source-of-truth/ui/UI_11_QUESTS.md`
**Status:** Approved foundation  
**Platform:** Telegram Mini App  
**Orientation:** Mobile Portrait First  
**Depends on:**
- `docs/source-of-truth/gameplay/17_QUEST_SYSTEM.md`
- `docs/source-of-truth/ui/UI_01_GLOBAL_GAME_SHELL.md`
- `docs/source-of-truth/ui/UI_02_WORLD_AND_LOCATION.md`

---

# 1. Назначение

Quest UI manages active/available/completed tasks while a compact tracker remains visible on gameplay screens.

---

# 2. Root Structure

```text
КВЕСТЫ

[АКТИВНЫЕ] [ДОСТУПНЫЕ] [ИСТОРИЯ]

Tracked 2/3
```

History can be lightweight in first version.

---

# 3. Quest Card

```text
ОХОТА НА ВОЛКОВ
Dark Forest

Волки: 3/8
Награда:
850 XP · 120 Gold

[ОТКРЫТЬ]
```

---

# 4. Quest Details

Shows:
- title/lore;
- objectives;
- progress;
- destination;
- rewards;
- requirements;
- track/untrack.

No hidden technical IDs.

---

# 5. Tracker

Maximum 2–3 tracked quests.
Player can manually choose tracked quests.
Auto-track newly accepted only if free slot.

---

# 6. Map / Location Link

Objective with known destination:
```text
[ ПОКАЗАТЬ НА КАРТЕ ]
```

Opens World with selected location/route preview.

---

# 7. Completion

Ready-to-turn-in visually distinct.

If remote completion allowed by quest definition:
```text
[ ЗАВЕРШИТЬ ]
```

Otherwise show required NPC/location.

---

# 8. Objective Types

UI supports:
- kill;
- collect;
- boss;
- world event;
- dungeon;
- craft;
- profession level;
- recipe learning;
- travel/visit.

Quest System remains owner progress.

---

# 9. Approved Decisions

1. Root tab Quests exists.
2. Tracker 2–3 quests.
3. World/Location integrate quest markers.
4. Rewards show XP/Gold/items.
5. Dungeon/Crafting quest objectives supported.
6. QuestProtected items cannot be destroyed/sold.
