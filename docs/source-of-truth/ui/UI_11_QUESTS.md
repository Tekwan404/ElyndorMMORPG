# Elyndor — UI/UX Specification 11 — Adventure Journal

**Document:** `docs/source-of-truth/ui/UI_11_QUESTS.md`
**Status:** Approved implementation baseline
**Platform:** Telegram Mini App
**Orientation:** Mobile Portrait First
**Depends on:**
- `docs/source-of-truth/gameplay/17_QUEST_SYSTEM.md`
- `docs/source-of-truth/ui/UI_01_GLOBAL_GAME_SHELL.md`
- `docs/source-of-truth/ui/UI_02_WORLD_AND_LOCATION.md`

---

# 1. Player-facing principle

The unified Quest System is an internal server-authoritative engine.

The player-facing game must not present Elyndor as one global list of available quests.

New work is discovered through the world:

```text
Location / NPC / discovery
→ Story or Errand

Adventurer Guild representative / expedition post
→ Contract

Adventure Journal
→ active work, ready-to-claim work, completed history
```

The bottom navigation label is **Журнал**, not a universal quest board.

---

# 2. Journal structure

```text
ИСТОРИЯ ПРИКЛЮЧЕНИЙ

[СЮЖЕТ] [ПОРУЧЕНИЯ]
[КОНТРАКТЫ] [ЗАВЕРШЕНО]
```

The journal reads `/api/v1/quests/`.

`AVAILABLE` and `LOCKED` remain server-derived states, but are not a global player-facing browse list.

---

# 3. Story and errands

Available Story and Side/Errand definitions appear in the current Location UI when their server availability conditions are met.

Story presentation includes:
- title;
- source/issuer when relevant;
- region;
- description;
- level;
- reward preview;
- action to continue/start the story.

Errand presentation includes:
- issuer;
- local context;
- objective;
- reward;
- action **Принять поручение**.

Side errands must not automatically become mandatory gates for the main story unless explicitly designed as such.

---

# 4. Adventurer Guild contracts

Contracts are official registered work and are presented through an Adventurer Guild surface.

Current implementation supports:
- Starter Town Guild representation;
- field/expedition post where content provides a local contract;
- registrar;
- contract number;
- issuer/customer;
- region;
- threat level;
- required level;
- reward;
- availability/active/ready/completed state.

The player action is **Принять контракт**, not “take quest”.

---

# 5. Active card

Active Story / Errand / Contract cards show:
- title;
- category;
- source;
- region;
- objectives;
- progress;
- rewards;
- unlocks;
- ready-to-claim state.

No hidden technical IDs.

---

# 6. Completion

Ready-to-claim remains a first-class authoritative state.

Claim uses an idempotent mutation and grants XP, Gold, and items exactly once.

```text
[ ПОЛУЧИТЬ НАГРАДУ ]
```

Completed entries remain in journal history.

---

# 7. Tracker

Future tracker:
- maximum 2–3 tracked active entries;
- active work only;
- never becomes a replacement for world discovery.

---

# 8. Map / location integration

World and Location are the primary discovery surfaces.

Future supported links:
```text
[ ПОКАЗАТЬ НА КАРТЕ ]
```

World events and discovery chains are added only after their runtime systems exist; UI must not fake them as working content.

---

# 9. Current 1–20 implementation

The level 1–20 content intentionally interleaves:
- Story;
- Errands;
- Contracts.

Errands are optional branches where possible.

Contracts periodically gate/bridge major story progression.

The Broodmother contract is registered through the Adventurer Guild and remains the authoritative gate that opens Blighted Grove after the confirmed boss kill.
