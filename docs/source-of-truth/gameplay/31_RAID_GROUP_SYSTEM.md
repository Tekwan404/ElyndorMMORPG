# Elyndor — Raid Group System

**Document:** `docs/source-of-truth/gameplay/31_RAID_GROUP_SYSTEM.md`
**Status:** Approved foundation

---

## 1. Purpose

`RaidGroup` is one organised group for raid and world-boss encounters. It does not replace the existing five-player `Party` used by ordinary party content and dungeons.

A raid has one roster. If ten characters join, the raid has ten members; it is not represented as two parties and does not require filling empty slots.

## 2. Model

```text
RaidGroup
├── RaidGroupId
├── LeaderCharacterId
├── Members[]
├── MaxMembers
├── State
├── CreatedAt
└── Version
```

Default `MaxMembers` is 20. The maximum is encounter/content-defined and must be validated by the server.

There are no combat subgroups and no subgroup assignment in the first raid implementation.

## 3. Membership and permissions

A character is in either one ordinary `Party` or one `RaidGroup` context. Joining a raid must use an explicit server-authoritative transition; it must not silently preserve a second, independently managed party membership.

Raid roles are:

```text
LEADER
ASSISTANT
MEMBER
```

The leader may invite, remove members, promote assistants, begin a ready check, and disband the raid. An assistant may invite and begin a ready check. Member and capacity changes are versioned and validated atomically.

## 4. Combat and effect scope

An encounter started for a `RaidGroup` creates one combat roster containing all eligible raid members who join that encounter, up to the encounter maximum. It does not split the encounter into five-member combats.

During a raid encounter:

- self effects affect only their owner;
- explicitly targeted effects affect their selected valid target;
- ally, party, group, and raid-wide effects affect every eligible active participant in the same raid combat roster.

Thus a raid buff is shared by all present raid participants whether the roster contains 2, 10, or 20 characters. A member who is offline, has not joined the encounter, has fled, or is otherwise no longer an active participant is not a valid recipient.

The rules above are a raid-context mapping of existing group effects, not a second parallel buff system. Ordinary Party and dungeon combat retain their existing maximum of five and their existing effect semantics.

## 5. Ready check and lifecycle

Ready check states are:

```text
READY
NOT_READY
NO_RESPONSE
```

A ready check has a stable instance id and server timeout. It is organisational only and never starts combat by itself.

Members may disconnect for a grace period. Reconnect restores the authoritative raid snapshot. Disconnected or non-participating characters do not receive rewards merely because their membership remains in the raid.

## 6. Reward eligibility

Raid membership alone grants no reward. Boss and reward systems determine eligibility from the combat roster and the existing participation/contribution rules. Reward mutations remain server-authoritative, idempotent, and independent of the client connection.

## 7. Invariants

1. Ordinary Party and ordinary dungeon maximum remain 5.
2. Raid has one roster with a default maximum of 20.
3. A raid may start with any positive member count accepted by the encounter policy; no padding is required.
4. No combat subgroup or subgroup-only buff rule exists in the initial raid system.
5. Group/ally effects in an active raid combat apply to all valid members of that raid combat roster.
6. Combat and boss systems remain authoritative owners of encounter state, targets, participation, and rewards.
