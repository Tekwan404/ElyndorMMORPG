# Elyndor — Dungeon System Specification

**Document:** docs/source-of-truth/gameplay/28_DUNGEON_SYSTEM.md
**System:** Instanced PvE / Dungeon  
**Status:** Foundation / Source of Truth  
**Version:** 1.0

---

# 1. Назначение

Dungeon System определяет инстансовый PvE-контент для одного игрока или Party.

Dungeon объединяет уже существующие системы:

```text
World
Party
Character
Combat
Monster AI
Boss
Loot
Progression
Quest
Economy
```

Dungeon System не создаёт второй Combat/Boss/Loot engine.

---

# 2. Главный принцип

```text
DungeonDefinition
→ create DungeonInstance
→ snapshot eligible members
→ enter instance
→ encounters
→ checkpoint
→ boss
→ completion
→ rewards
→ exit
```

---

# 3. Party Size

Global maximum:

```text
MaxPartySize = 5
```

DungeonDefinition определяет:

```text
MinPlayers
MaxPlayers <= 5
```

Поддерживаются:

```text
solo dungeon
small-party dungeon
full 5-player dungeon
```

---

# 4. Dungeon Definition

```text
DungeonDefinition
├── DungeonId
├── Name
├── EntranceLocationId
├── RequiredLevel
├── MinPlayers
├── MaxPlayers
├── DifficultyProfileId
├── EntryRequirementProfile
├── EncounterIds[]
├── CheckpointIds[]
├── FinalBossEncounterId
├── CompletionRewardProfile
├── LockoutProfileId
├── InstanceDuration
├── RespawnPolicy
├── Version
└── Metadata
```

---

# 5. Difficulty

Dungeon engine не hardcode'ит Normal/Heroic.

Используется:

```text
DifficultyProfileId
```

Текущий первый content может иметь:

```text
NORMAL
```

Позже без изменения engine:

```text
HEROIC
MYTHIC
CHALLENGE
```

если они будут нужны.

---

# 6. Entry Requirements

```text
DungeonEntryRequirementProfile
├── RequiredCharacterLevel
├── RequiredQuestFlags[]
├── RequiredItemDefinitionId, optional
├── ConsumeEntryItem
├── RequiredPartySize, optional
├── RequiredLocationId
└── Metadata
```

Не все dungeon требуют key/item.

---

# 7. Dungeon Instance

```text
DungeonInstance
├── DungeonInstanceId
├── DungeonDefinitionId
├── State
├── MemberSnapshot[]
├── CreatedAt
├── StartedAt
├── ExpiresAt
├── CurrentEncounterId
├── CurrentCheckpointId
├── CompletedEncounterIds[]
├── CompletionId, optional
├── InstanceVersion
└── RuntimeContext
```

State:

```text
CREATED
ACTIVE
COMPLETED
FAILED
ABANDONED
EXPIRED
```

---

# 8. Member Snapshot

При создании instance фиксируется:

```text
DungeonMember
├── CharacterId
├── PartyIdAtCreation, optional
├── JoinedAt
├── ParticipationState
├── LastKnownDungeonState
└── Metadata
```

MemberSnapshot определяет, кто имеет право входить/re-enter этот instance.

---

# 9. Party Membership vs Dungeon Membership

Party и Dungeon Instance — разные сущности.

После создания:

```text
Party change
!=
automatic Dungeon member change
```

Если игрок вышел из Party:

- Dungeon membership не исчезает мгновенно;
- Dungeon policy решает, может ли он продолжить;
- новый игрок Party не получает автоматический доступ в уже начатый instance.

Текущий default:

```text
MemberSnapshot immutable after first encounter begins
```

---

# 10. No Replacement Exploit

После начала первого encounter нельзя:

- пригласить сильного игрока;
- заменить участника;
- дать ему только boss reward.

Для replacement нужен новый DungeonInstance, если future content явно не разрешит иное.

---

# 11. Instance Location

Dungeon использует instanced world scope.

```text
WorldLocationId
+ DungeonInstanceId
```

Два Party могут находиться в одном DungeonDefinition, но в разных DungeonInstance и не видят/не влияют друг на друга.

---

# 12. Enter Dungeon

Pipeline:

```text
request entry
→ validate character state
→ validate Party/member snapshot
→ validate level/requirements
→ validate lockout
→ create/join DungeonInstance
→ set Character activity/location context
```

Нельзя войти:

- DEAD;
- IN_COMBAT;
- during travel;
- в другой incompatible instance.

---

# 13. Rejoin

Если участник disconnect:

- membership сохраняется;
- DungeonInstance остаётся;
- после reconnect player может rejoin, пока instance ACTIVE и не expired.

Rejoin не создаёт новый reward eligibility entry.

---

# 14. Encounter

```text
DungeonEncounterDefinition
├── EncounterId
├── EncounterType
├── MonsterGroups[]
├── BossDefinitionId, optional
├── StartCondition
├── CompletionCondition
├── ResetProfile
├── RewardProfile, optional
└── Metadata
```

EncounterType:

```text
TRASH
ELITE
BOSS
SCRIPTED
```

---

# 15. Encounter Start

Dungeon System подтверждает, что encounter доступен.

Combat System создаёт CombatSession.

Dungeon не управляет:

- hit;
- crit;
- cooldown;
- threat;
- ability.

---

# 16. Ordered Progression

DungeonDefinition может задавать граф/sequence.

Первый content рекомендуется делать понятным:

```text
Encounter 1
→ Encounter 2
→ Elite
→ Final Boss
```

Но engine допускает optional branch encounter через dependency ids.

---

# 17. Checkpoint

```text
DungeonCheckpoint
├── CheckpointId
├── UnlockAfterEncounterId
├── RespawnLocationContext
└── Metadata
```

После подтверждённого encounter completion:

```text
CurrentCheckpointId = unlocked checkpoint
```

---

# 18. Death

Character System остаётся владельцем DEAD/RESPAWN.

Dungeon задаёт respawn context.

Current default:

```text
RespawnPolicy = CHECKPOINT_OUT_OF_COMBAT
```

Во время активного encounter бесплатный самостоятельный respawn не происходит.

После окончания/сброса encounter погибший участник может respawn на CurrentCheckpoint.

---

# 19. Resurrection Abilities

Если class/ability в будущем умеет resurrect ally in combat:

- это Ability/Effect content;
- Dungeon не содержит class-specific revive logic.

---

# 20. Wipe

Wipe:

```text
нет живого/валидного participant, способного продолжать encounter
```

Текущий default:

```text
Reset current encounter
keep DungeonInstance ACTIVE
keep completed encounters
respawn members at CurrentCheckpoint
```

Dungeon не сбрасывается полностью из-за одного wipe.

---

# 21. Encounter Reset

Reset:

- active monsters/AI runtime очищаются;
- boss encounter использует Boss ResetProfile;
- HP/resource персонажей обрабатываются Character/Resource rules;
- награда за незавершённый encounter не выдаётся.

---

# 22. Final Boss

Final Boss использует:

```text
18_BOSS_AND_WORLD_EVENT_SYSTEM
```

Dungeon передаёт:

```text
DungeonInstanceId
DungeonEncounterId
MemberSnapshot
activity context
```

Boss System остаётся owner boss lifecycle внутри encounter.

---

# 23. Dungeon Boss Scope

Dungeon boss отличается от world boss scope:

```text
World Boss:
open world participation

Dungeon Boss:
DungeonMemberSnapshot participation
```

Оба используют один Boss/Combat/Loot engine.

---

# 24. Encounter Rewards

Optional encounter RewardProfile может выдавать:

- XP;
- Item/Loot;
- Gold.

Используются owner systems:

```text
XP → Progression
Item → Loot/Item
Gold → Economy
```

---

# 25. Completion

После Final CompletionCondition:

```text
State = COMPLETED
CompletedAt = ServerTime
Create DungeonCompletionId
```

`DungeonCompletionId` — idempotency anchor.

---

# 26. Completion Reward

```text
DungeonCompletionReward
├── XP profile
├── LootTableId, optional
├── CurrencyRewardProfile, optional
├── Quest/Unlock events
└── Metadata
```

Dungeon не создаёт ItemInstance/Gold напрямую.

---

# 27. Reward Eligibility

Присутствие в MemberSnapshot само по себе не всегда гарантирует reward.

Completion reward использует ParticipationPolicy.

Учитываются:

- participation time;
- qualifying actions;
- damage;
- healing;
- support;
- tanking;
- encounter participation.

AFK/offline member, не участвовавший в dungeon, не получает автоматическую награду.

---

# 28. Personal Loot

Dungeon equipment reward по умолчанию использует Personal Loot.

Один participant не забирает drop другого, если RewardProfile явно не использует другую будущую модель.

---

# 29. Quest Integration

Quest objectives могут использовать:

```text
ENTER_DUNGEON
COMPLETE_DUNGEON
DEFEAT_DUNGEON_BOSS
COMPLETE_DUNGEON_ENCOUNTER
```

Quest System слушает подтверждённые Dungeon events.

---

# 30. Lockout

Dungeon может иметь lockout.

```text
DungeonLockoutProfile
├── Mode
├── ResetPeriod
├── ResetAnchor
├── AppliesToRewardOnly
└── Metadata
```

Mode:

```text
NONE
DAILY
WEEKLY
CUSTOM
```

Рекомендуется различать:

```text
entry lockout
reward lockout
```

Большинство обычных dungeon могут не иметь entry lockout.

---

# 31. Reward Lockout

Можно разрешать повторное прохождение, но ограничивать конкретный boss/completion reward.

Это позволяет:

- помогать друзьям;
- не создавать бесконечный rare reward farm.

Lockout проверяется сервером по CompletionId/Profile.

---

# 32. Instance Expiration

DungeonInstance имеет:

```text
ExpiresAt
```

Recommended default:

```text
2 hours
```

Если instance пуст и долго не используется, он может expire раньше по cleanup profile.

Число data-driven.

---

# 33. Exit Dungeon

Игрок может выйти, если activity rules разрешают.

Выход:

```text
Dungeon Location
→ ReturnLocationId
```

ReturnLocation обычно:

```text
EntranceLocationId
```

Нельзя использовать Exit как бесплатный combat escape, если персонаж IN_COMBAT.

---

# 34. Abandon

Если все участники покинули instance и recovery window прошёл:

```text
ACTIVE → ABANDONED
```

Награда за незавершённый dungeon не выдаётся.

---

# 35. Server Restart

DungeonInstance сохраняется.

Если restart произошёл **вне encounter**:

- instance восстанавливается;
- members могут rejoin.

Если restart произошёл **во время CombatSession/encounter**:

```text
CombatSession → INTERRUPTED
no encounter reward
current encounter → RESET
DungeonInstance remains ACTIVE
members return/rejoin at CurrentCheckpoint
```

Не реконструировать бой посередине.

---

# 36. Boss Restart Inside Dungeon

Если server restart произошёл во время dungeon boss:

```text
Boss ACTIVE → FAILED
no reward
encounter reset
DungeonInstance remains ACTIVE
```

После recovery boss может быть запущен снова.

---

# 37. Concurrency

Один DungeonInstance не может:

- завершить один encounter дважды;
- создать два Final CompletionId;
- одновременно иметь два active экземпляра одного exclusive encounter.

Использовать InstanceVersion + transaction.

---

# 38. Idempotency

Stable ids:

```text
DungeonInstanceId
DungeonEncounterCompletionId
DungeonCompletionId
DungeonRewardResolutionId
```

Retry event не даёт duplicate reward/quest progress.

---

# 39. UI Contract

Dungeon screen показывает:

- Dungeon name;
- difficulty;
- Party members;
- progress;
- current encounter;
- completed encounters;
- boss status;
- checkpoint;
- optional lockout;
- Exit.

Перед входом:

- required level;
- party size;
- reward preview;
- lockout state.

---

# 40. First Dungeon Content Pattern

Рекомендуемый первый testable dungeon:

```text
Entrance
→ 2 normal encounters
→ 1 elite encounter
→ checkpoint
→ final boss
→ completion chest/reward
```

Party:

```text
1–5
```

Для проверки Party gameplay лучше балансировать основной вариант на:

```text
3–5 players
```

но solo entry может быть разрешён конкретным DungeonDefinition.

---

# 41. Analytics

Собирать:

```text
DungeonStarted
DungeonCompleted
DungeonAbandoned
CompletionTime
WipeCount
BossWipeCount
PartySize
ClassComposition
RewardEligibilityRate
RejoinRate
```

---

# 42. Events

```text
DungeonInstanceCreated
DungeonEntered
DungeonMemberRejoined
DungeonEncounterStarted
DungeonEncounterCompleted
DungeonCheckpointUnlocked
DungeonWiped
DungeonCompleted
DungeonAbandoned
DungeonExpired
DungeonExited
```

---

# 43. Invariants

1. Dungeon не реализует второй Combat engine.
2. Party max 5 сохраняется.
3. Dungeon membership snapshot не равен текущей Party membership.
4. Новый Party member не получает автоматический доступ в начатый instance.
5. Wipe не выдаёт reward.
6. Server restart не реконструирует encounter mid-fight.
7. Completion имеет один DungeonCompletionId.
8. Rewards выдаются owner systems.
9. Quest progress приходит только из confirmed Dungeon events.
10. Lockout проверяется сервером.
11. DungeonInstance scope изолирует группы друг от друга.
