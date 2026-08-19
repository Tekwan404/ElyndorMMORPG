# Elyndor — Raid Group System Specification

**Document:** `31_RAID_GROUP_SYSTEM.md`  
**Status:** Foundation / Source of Truth

---

# 1. Назначение

Raid Group System вводит организованную группу больше обычного Party для world-boss/raid encounters.

Current Party max remains:

```text
5
```

Raid Group does not replace Party.

---

# 2. Model

```text
RaidGroup
├── RaidGroupId
├── LeaderCharacterId
├── Subgroups[]
├── MaxMembers
├── State
├── CreatedAt
└── Version
```

Default:

```text
MaxMembers = 20
Subgroup size = 5
```

---

# 3. Membership

Character can be:
- in one Party OR
- in one RaidGroup context that internally contains subgroup assignments.

Raid Group UI may represent 4 subgroups × 5.

Party effects that are explicitly `Party-only` apply only to subgroup unless ability/effect says `Raid-wide`.

---

# 4. Roles / Permissions

Raid permissions:
```text
LEADER
ASSISTANT
MEMBER
```

Leader:
- invite;
- kick;
- move subgroup;
- promote assistant;
- ready check;
- disband.

Assistant:
- invite;
- ready check;
- mark targets if enabled.

---

# 5. Ready Check

```text
READY
NOT_READY
NO_RESPONSE
```

Ready Check has stable instance id and timeout.

Does not start combat automatically.

---

# 6. World Boss Integration

World Boss may accept:
- ordinary Party;
- organized RaidGroup;
- unaffiliated participants if encounter policy allows.

Reward eligibility remains participation-based, not raid membership alone.

---

# 7. Disconnect

Offline member may remain for grace period.
No automatic reward for disconnected/non-participating member.

---

# 8. Invariants

1. Party max stays 5.
2. Raid max default 20, data-driven.
3. Subgroup size 5.
4. Party-only effects do not silently become raid-wide.
5. Raid membership does not grant reward by itself.
6. Combat/Boss remain authoritative owners of encounter state.
