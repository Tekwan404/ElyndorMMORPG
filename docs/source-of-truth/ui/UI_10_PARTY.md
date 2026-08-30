# Elyndor — UI/UX Specification 10 — Party

**Document:** `docs/source-of-truth/ui/UI_10_PARTY.md`
**Status:** Approved foundation  
**Platform:** Telegram Mini App  
**Orientation:** Mobile Portrait First  
**Depends on:**
- `docs/source-of-truth/gameplay/20_PARTY_SYSTEM.md`
- `docs/source-of-truth/gameplay/28_DUNGEON_SYSTEM.md`
- `docs/source-of-truth/gameplay/31_RAID_GROUP_SYSTEM.md`

---

# 1. Назначение

Party UI manages the ordinary 1–5 player group and provides compact quick access from the global HUD.

---

# 2. Quick Access

HUD:
```text
👥+
```
or
```text
👥 3/5
```

Tap → Party screen/overlay.

---

# 3. Party Screen

```text
ГРУППА 3/5

[Leader] Player A   HP
Player B            HP
Player C            HP

[ ПРИГЛАСИТЬ ]
[ ПОКИНУТЬ ]
```

---

# 4. Member Card

Shows:
- name;
- level/class;
- HP/resource optional;
- online/disconnected/dead;
- leader marker.

Tap → profile/context actions.

---

# 5. Leader Actions

Leader:
- invite;
- kick;
- transfer leader;
- disband.

No ready check for ordinary Party unless dungeon flow later requests it explicitly; formal ready check lives in Raid Group.

---

# 6. Invites

Incoming invite:
```text
PlayerName приглашает в группу
[ОТКЛОНИТЬ] [ПРИНЯТЬ]
```

Timed and server-authoritative.

---

# 7. Dungeon Integration

Party membership and Dungeon MemberSnapshot are different.

UI warns if party changes after dungeon started:
```text
Новый участник не войдёт в текущий инстанс.
```

---

# 8. Combat Strip

In combat show compact party strip, not full Party screen.

Tap member can target ally if Ability target rules allow.

---

# 9. Approved Decisions

1. Party max 5.
2. Party quick access always in HUD outside combat.
3. Leader controls invite/kick/transfer/disband.
4. Offline member may remain.
5. Dungeon membership snapshot separate.
6. Raid Group is separate system.
