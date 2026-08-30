# Elyndor — UI/UX Specification 20 — Guild

**Document:** `docs/source-of-truth/ui/UI_20_GUILD.md`
**Status:** Approved foundation  
**Platform:** Telegram Mini App  
**Orientation:** Mobile Portrait First  
**Depends on:**
- `docs/source-of-truth/gameplay/30_GUILD_SYSTEM.md`
- `docs/source-of-truth/ui/UI_12_CITY_LOCATION.md`
- `docs/source-of-truth/gameplay/26_CURRENCY_AND_ECONOMY_SYSTEM.md`

---

# 1. Назначение

Guild UI turns the previously visual-only concept into a system-backed City social screen.

---

# 2. No Guild State

```text
ГИЛЬДИЯ

[НАЙТИ ГИЛЬДИЮ]
[СОЗДАТЬ ГИЛЬДИЮ]

Creation:
Name
Tag
Emblem
Description
Gold fee
```

---

# 3. Guild Home

```text
[EMBLEM]
Night Wardens [NW]
Level 8
42/50 members

Guild XP █████░

[ЧАТ] [УЧАСТНИКИ] [ЗАДАНИЯ] [БАНК] [НАСТРОЙКИ]
```

---

# 4. Roster

Shows:
- name;
- class/level;
- online;
- rank;
- last active, optional.

Officer actions according to permissions.

---

# 5. Ranks / Permissions

Leader can manage ranks/permissions through dedicated settings sheet.

Do not expose raw permission enum to normal members.

---

# 6. Guild Tasks

Weekly/current tasks:
```text
Complete Dungeons 8/20
World Bosses 2/5
Craft Epic Items 4/10
```

Rewards server-defined.

---

# 7. Guild Bank

Tabs/grid + deposit/withdraw.
Actions require permissions.
All operations confirmed and audited.

---

# 8. Guild Chat

Accessible from Guild Home.
Unread badge on Guild card/service.

---

# 9. Visual Reference

Use:
```text
reference/UI_19-20_SETTINGS_GUILD.png
```

Keep:
- emblem;
- level/progress;
- members;
- officers;
- events/tasks;
- dark-fantasy social identity.

---

# 10. Approved Decisions

1. Guild is City service.
2. Default member limit 50.
3. Ranks: Leader/Officer/Veteran/Member/Recruit.
4. Guild XP/progression.
5. Guild Bank.
6. Guild Chat.
7. Weekly tasks/events.
8. No mandatory combat-power guild perk by default.
