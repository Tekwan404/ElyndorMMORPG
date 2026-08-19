# Elyndor — Party System Specification

**Document:** 20_PARTY_SYSTEM.md  
**System:** Party / Group  
**Status:** Foundation / Source of Truth  
**Version:** 1.0

---

# 1. Назначение

Party System определяет постоянную группу игроков для совместного PvE, поддержки, XP/reward context и групповых эффектов.

Party не является CombatSession.

```text
Party = социально/игровая группа
CombatSession = конкретный бой
```

Игроки могут состоять в Party вне боя.

---

# 2. Размер Party

```text
MaxPartySize = 5
```

World Boss может одновременно содержать участников нескольких Party.

Buff/Heal/Resource эффекты Командира, если ability не говорит иное, распространяются только на его собственную Party.

---

# 3. Party Entity

```text
Party
├── PartyId
├── LeaderCharacterId
├── Members[1..5]
├── CreatedAt
├── State
├── Version
└── Metadata
```

Member:

```text
PartyMember
├── CharacterId
├── JoinedAt
├── ConnectionState
└── MemberVersion
```

---

# 4. Membership

Персонаж одновременно может состоять максимум в одной Party.

Authoritative membership хранит Party System.

Combat/Ability/Effect/Quest/Loot получают membership context только через подтверждённый PartyId.

---

# 5. Party Ally

`Party Ally` для боевой механики:

```text
same PartyId
AND allied combat side
AND same CombatSession
AND valid target state
```

Случайный игрок в том же encounter не является Party Ally.

---

# 6. Party Targeting

Основной target context:

```text
SELF_AND_PARTY_MEMBERS_IN_COMBAT
```

Он:
- включает caster;
- включает валидных членов его Party в CombatSession;
- не включает других дружественных игроков encounter;
- не использует distance/proximity.

Это **не spatial Aura**.

---

# 7. Lifecycle

```text
Create Party
→ Invite
→ Accept
→ Member Added
→ Play / Combat
→ Leave / Kick / Disconnect
→ Leadership Transfer if needed
→ Disband
```

---

# 8. Invite

Invite содержит:

```text
PartyInviteId
PartyId
InviterCharacterId
TargetCharacterId
CreatedAt
ExpiresAt
State
```

Игрок не может принять invite:
- если уже состоит в другой Party;
- если Party заполнена;
- если invite истёк;
- если target/invite context больше невалиден.

---

# 9. Leader

Leader может:
- приглашать;
- исключать участника;
- передавать лидерство;
- распустить Party.

Если Leader выходит:
1. лидерство передаётся следующему валидному member по deterministic policy;
2. если участников не осталось — Party disband.

---

# 10. Combat

Вход в Combat не создаёт Party.

Party membership snapshot/context передаётся Combat System.

Join/leave Party во время активного Combat:
- не должен ретроактивно переписывать уже подтверждённые combat events;
- новые Party-targeted effects используют актуальный authoritative membership;
- reward eligibility использует собственный participation timeline.

---

# 11. XP

Last Hit не определяет XP ownership.

Party member получает group XP, если:
- состоит в Party;
- находится в eligible CombatSession/activity context;
- проходит ParticipationPolicy.

Конкретный XP multiplier/split является Progression balance data.

---

# 12. Loot

Personal Loot остаётся основной моделью.

Party membership сама по себе:
- не гарантирует boss loot;
- не заменяет ParticipationPolicy;
- не даёт reward отсутствующему/неучаствующему игроку.

---

# 13. Quest Credit

Objective сам определяет sharing policy.

По умолчанию:

```text
KILL / BOSS / WORLD_EVENT → shareable when eligible
COLLECT / USE_ITEM / DIALOGUE / CRAFT → personal
```

---

# 14. Offline

Offline member может оставаться в Party.

Он:
- не получает Party combat buffs вне CombatSession;
- не получает activity reward без eligibility;
- может быть исключён leader;
- membership сохраняется через reconnect.

---

# 15. World Boss

Несколько Party могут участвовать в одном World Boss.

```text
Party A: up to 5
Party B: up to 5
Party C: up to 5
...
```

Commander buff Party A не баффает Party B/C.

Boss reward рассчитывается персонально через ParticipationPolicy.

---

# 16. Events

```text
PartyCreated
PartyInviteCreated
PartyInviteAccepted
PartyMemberJoined
PartyMemberLeft
PartyMemberKicked
PartyLeaderChanged
PartyDisbanded
```

---

# 17. Invariants

1. MaxPartySize = 5.
2. Character имеет не больше одного PartyId.
3. Party != CombatSession.
4. Party Ally требует same Party + same CombatSession для боевого targeting.
5. Party membership не равен reward eligibility.
6. Party effects не требуют spatial distance.
7. Все membership mutations серверно-авторитетны.

# 18. Dungeon Integration

Party can create/join a DungeonInstance.

At instance creation Dungeon System stores MemberSnapshot.

After first encounter begins:

```text
Party membership change
!=
Dungeon membership change
```

This prevents late replacement/reward abuse.

Party remains owner current membership; Dungeon remains owner instance membership snapshot.
