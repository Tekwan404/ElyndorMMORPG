# Elyndor — Guild System Specification

**Document:** `docs/source-of-truth/gameplay/30_GUILD_SYSTEM.md`
**Status:** Foundation / Source of Truth  
**Version:** 1.0

---

# 1. Назначение

Guild System определяет постоянное социальное объединение игроков.

---

# 2. Guild Model

```text
Guild
├── GuildId
├── Name
├── Tag
├── EmblemProfileId
├── Description
├── Level
├── Experience
├── MemberLimit
├── LeaderCharacterId
├── CreatedAt
└── Version
```

Default:

```text
MemberLimit = 50
```

Data-driven.

---

# 3. Creation

Guild is created in City Guild service.

Requirements:
```text
Character not in Guild
valid unique name/tag
Gold creation fee
```

Recommended initial fee is balance-profile driven.

---

# 4. Ranks

Default ranks:

```text
LEADER
OFFICER
VETERAN
MEMBER
RECRUIT
```

Permissions are data-driven.

---

# 5. Permissions

Possible:
```text
INVITE
KICK
PROMOTE
DEMOTE
EDIT_DESCRIPTION
EDIT_EMBLEM
MANAGE_BANK
START_GUILD_EVENT
```

Leader always has all.

---

# 6. Invitations

Invite:
- target not already in guild;
- expires;
- can accept/decline.

Applications can be added later.

---

# 7. Guild Progression

Guild receives Guild XP from approved activities.

Possible sources:
- member dungeon completion;
- world boss;
- guild tasks;
- guild events.

No passive stat bonuses by default.

---

# 8. Guild Perks

Current recommended philosophy:
- social/convenience/cosmetic first;
- avoid mandatory raid-power buffs.

Possible:
- emblem cosmetics;
- guild bank capacity;
- guild task slots;
- banner cosmetics;
- city/guild hall future unlocks.

---

# 9. Guild Bank

```text
GuildBank
├── Tabs
├── ItemSlots
├── GoldBalance, optional
└── Version
```

All deposits/withdrawals audited.
Permissions per rank.
Crystal never stored.

---

# 10. Guild Chat

Guild chat channel exists independently from gameplay combat.

Moderation/admin tooling future.

---

# 11. Guild Events / Tasks

Guild may have weekly tasks:
- kill bosses;
- complete dungeons;
- craft items;
- contribute Gold/materials.

Rewards must avoid infinite economy loops.

---

# 12. Leaving / Kicking

Leaving guild:
- immediate membership removal;
- optional cooldown before joining another guild is content policy.

Leader must transfer leadership or disband.

---

# 13. Disband

Leader confirmation required.
Guild Bank pending assets must have explicit safe policy before disband.

---

# 14. Invariants

1. One character → max one Guild.
2. Guild is persistent.
3. Permissions server-authoritative.
4. Crystal never guild-tradeable.
5. Guild membership alone grants no boss/dungeon reward.
6. Mandatory combat-power perks are not default.
7. Bank operations audited.
