# Elyndor — UI/UX Specification 09 — World Boss / Raid Combat

**Document:** `docs/source-of-truth/ui/UI_09_WORLD_BOSS_COMBAT.md`
**Status:** Approved foundation  
**Platform:** Telegram Mini App  
**Orientation:** Mobile Portrait First  
**Depends on:**
- `docs/source-of-truth/gameplay/18_BOSS_AND_WORLD_EVENT_SYSTEM.md`
- `docs/source-of-truth/gameplay/31_RAID_GROUP_SYSTEM.md`
- `docs/source-of-truth/ui/UI_08_NORMAL_COMBAT.md`

---

# 1. Назначение

World Boss UI расширяет Normal Combat для больших encounters и организованного RaidGroup до 20 игроков, сохраняя mobile readability.

---

# 2. Raid Layout

```text
BOSS HUD / PHASE

RAID FRAMES (compact)

BOSS ART

MECHANIC WARNING
BOSS CAST BAR

PLAYER HUD

ABILITY ROW
```

Bottom nav hidden.

---

# 3. Boss HUD

Показывает:
- Boss name;
- HP;
- phase;
- enrage/important timer if BossProfile exposes it;
- current major debuff/phase state.

Не показывать fake timers, которых нет в gameplay data.

---

# 4. Raid Frames

До 20 игроков.

Mobile:
```text
4 subgroups × up to 5
```

Compact cell:
```text
name short
HP
role/state icon
dead/disconnected marker
```

Own subgroup визуально заметнее остальных.

---

# 5. Raid Management

Outside active combat Raid Leader может открыть:
- roster;
- subgroup assignment;
- assistants;
- ready check;
- invites.

В активном combat management actions минимизируются.

---

# 6. Ready Check

Visual:
```text
READY 14
NOT READY 3
NO RESPONSE 3
```

Leader sees completion/timeout.

---

# 7. Mechanic Warnings

Major mechanic:
```text
⚠ ТЕНЕВОЙ РАЗЛОМ
через 3.2 сек
```

Warnings come from encounter state, not client guesses.

---

# 8. Threat / Tank

If boss threat UI is available:
- current tank marker;
- own threat warning if near top.

No full desktop threat table by default.

---

# 9. Contribution

Live DPS/healing meter is not mandatory.

Optional compact personal contribution:
```text
Damage 12.4%
Healing 4.1%
```

Only if server exposes safe summary.

Reward eligibility never inferred client-side.

---

# 10. Deaths

Dead raid members remain visible as dimmed cells.
Alive/dead count optional:

```text
16 / 20 alive
```

---

# 11. Boss Result

Expanded reward result:
- boss defeated;
- personal loot;
- Gold/XP;
- contribution summary;
- quest updates;
- first-kill/new unlock.

---

# 12. Visual Reference

Use:
```text
reference/UI_09-10_RAID_PARTY.png
reference/UI_03-04_HERO_INVENTORY.png
```

Boss must dominate center; UI must not become spreadsheet.

---

# 13. Approved Decisions

1. Organized RaidGroup exists.
2. Default max 20.
3. Subgroups of 5.
4. Ready Check supported.
5. Leader/Assistant/Member roles.
6. Raid membership does not guarantee reward.
7. Party-only effects remain subgroup-only unless explicitly raid-wide.
8. Boss/Combat systems remain state owners.
