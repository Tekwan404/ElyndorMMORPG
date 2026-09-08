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

Quest UI manages available, active, ready-to-turn-in, and completed tasks while a compact tracker remains visible on gameplay screens.

---

# 2. Root Structure

```text
КВЕСТЫ

[ДОСТУПНЫЕ] [АКТИВНЫЕ]
[ГОТОВЫ К СДАЧЕ] [ВЫПОЛНЕННЫЕ]

В работе: 2
```

The journal is server-authoritative and reads `/api/v1/quests/`.
Locked quests remain hidden from the four player-facing lists until their
level, prerequisite, and offer-location requirements are satisfied.

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

Ready-to-turn-in is a first-class journal state and is visually distinct.

Current 1–20 progression supports remote turn-in after the server confirms
all objectives. Claim uses an idempotent mutation and grants XP, gold, and
items exactly once:

```text
[ ПОЛУЧИТЬ НАГРАДУ ]
```

Active quests may be abandoned; completed quests remain in history.

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
7. Four journal states are player-facing: Available, Active, Ready to Claim, Completed.
8. The level 1–20 story chain and the Broodmother contract use the same Quest System API.
