# Elyndor — UI/UX Specification 15 — Dungeon

**Document:** `docs/source-of-truth/ui/UI_15_DUNGEON.md`
**Status:** Approved foundation  
**Platform:** Telegram Mini App  
**Orientation:** Mobile Portrait First  
**Depends on:**
- `docs/source-of-truth/gameplay/28_DUNGEON_SYSTEM.md`
- `docs/source-of-truth/gameplay/20_PARTY_SYSTEM.md`
- `docs/source-of-truth/ui/UI_08_NORMAL_COMBAT.md`

---

# 1. Назначение

Dungeon UI covers preview, entry, instance progress, checkpoints, encounters and completion for 1–5 players.

---

# 2. Dungeon Preview

```text
ДРЕВНИЕ ШАХТЫ
Ур. 20–25
3–5 игроков
NORMAL

4 encounters
Final Boss: ???

Rewards: Rare–Epic

[ВОЙТИ / СОЗДАТЬ ИНСТАНС]
```

---

# 3. Requirements

Show:
- required level;
- quests/keys;
- party size;
- lockout/reward state.

Locked requirement explains exact reason.

---

# 4. Instance Header

Inside:
```text
Ancient Mine
Encounter 2/4
Checkpoint 1
Party 4/5
```

---

# 5. Progress

Vertical/horizontal compact progress:
```text
✓ Encounter 1
● Encounter 2
○ Elite
○ Boss
```

No full giant dungeon map required initially.

---

# 6. Encounter Entry

Current encounter card:
```text
ELITE GUARD
[НАЧАТЬ]
```

Only valid members can trigger according to Dungeon policy.

---

# 7. Wipe

Wipe result:
```text
ГРУППА ПОБЕЖДЕНА
Checkpoint: Old Lift
[ВОЗРОДИТЬСЯ]
```

Current encounter resets; completed progress remains.

---

# 8. Disconnect / Rejoin

Rejoin banner:
```text
Активный инстанс найден
[ВЕРНУТЬСЯ]
```

---

# 9. Completion

Expanded result:
- XP;
- Gold;
- personal loot;
- quest updates;
- lockout changes;
- return/exit button.

---

# 10. Approved Decisions

1. 1–5 players.
2. MemberSnapshot separate from Party.
3. Checkpoints.
4. Wipe resets current encounter.
5. Every server restart closes unfinished runs; the next entry starts a fresh run. Completed history and earned rewards remain saved.
6. Personal loot.
7. Rewards participation-based.


## Current dungeon navigation (2026-10-15)

Encounters are completed in authored order. There is no floor grouping, floor selector
or floor-based loot filter. The card shows current encounter progress and all possible
dungeon loot. Existing run state, encounter retries and rewards remain authoritative.


## Restart behavior (2026-10-15)

After any server restart, including crashes, resume clears the retired battle and
current-run refresh returns no active run. The player may start a new dungeon run.
A normal wipe without a server restart continues to reset only the current encounter.
