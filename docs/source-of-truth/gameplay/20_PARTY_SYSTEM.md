# Elyndor — Party System Specification

**Document:** docs/source-of-truth/gameplay/20_PARTY_SYSTEM.md
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

Party membership is not copied into the dungeon automatically after creation:

```text
Party membership change
!=
Dungeon membership change
```

An explicit dungeon entry is required. A player added to the party after an
encounter has started may enter the run, but is eligible only for a later
encounter or a retry after the current encounter is completed or wiped. The
active encounter roster remains frozen, preventing late replacement/reward
abuse.

Party remains the owner of current membership; Dungeon owns the mutable run
membership and the immutable roster of each concrete encounter.

---

# Approved Multiplayer Combat Extension

The party leader starts a shared world combat session. The combat session owns
the authoritative participant roster for that encounter; party membership is
not itself combat participation.

- The roster is frozen when the encounter starts.
- A member who was in the party at start but was in another location may join
  after reaching the encounter location.
- A character added to the party after combat starts cannot join that combat.
- A participant who flees cannot rejoin the same combat.
- Combat remains active when an individual participant flees. If the last
  active participant flees, the session ends in defeat.
- Contribution eligibility is based on authoritative participation events, not
  on a universal damage percentage. Damage, effective healing, support,
  mitigation, threat/taunt, qualifying actions, and participation time may be
  considered by the activity policy.

The combat session must expose participant ownership and state separately from
the existing companion projection. A player must never be represented as a
companion.

## Party and run termination policy

Disbanding a party, including the departure of its last member, abandons its
active dungeon run in the same database transaction. It does not cancel the
already started combat or rewrite that encounter's roster. The encounter may
finish, but an abandoned run cannot advance or start another encounter.
Leader departure while other members remain transfers leadership normally.
Party changes are published to connected members so their group UI refreshes.
