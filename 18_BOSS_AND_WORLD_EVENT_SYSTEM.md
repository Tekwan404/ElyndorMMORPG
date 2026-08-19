Elyndor — Boss and World Event System Specification

Document: 18_BOSS_AND_WORLD_EVENT_SYSTEM.md
System: Bosses / World Events
Status: Foundation / Source of Truth
Version: 0.1

1. Назначение

Boss and World Event System определяет жизненный цикл мировых боссов и событий, которые создают ограниченное по времени или условию encounter.

Система отвечает за:

BossDefinition;
BossInstance;
Boss lifecycle;
spawn/activation;
schedule;
manual/condition triggers;
single-instance protection;
encounter state;
phase context;
wipe/reset;
completion;
cooldown;
reward eligibility context;
restart recovery;
world event state.

Система не определяет:

Damage formulas;
Ability mechanics;
Monster AI decision rules;
Loot roll;
Quest progress;
Item stats;
Progression XP formulas.

2. Основной принцип

Boss System оркестрирует encounter.

Monster AI управляет действиями boss внутри боя.

Combat System управляет CombatSession.

Loot System рассчитывает награду.

Quest System наблюдает подтверждённый Boss result.

3. Boss Definition

BossDefinition
  ├── BossDefinitionId
  ├── MonsterDefinitionId
  ├── LocationId
  ├── ActivationProfile
  ├── EncounterProfile
  ├── PhaseProfile
  ├── ResetProfile
  ├── RewardProfile
  ├── CooldownProfile
  ├── Version
  └── Metadata

4. Boss Instance

BossInstance
  ├── BossInstanceId
  ├── BossDefinitionId
  ├── State
  ├── SpawnedAt
  ├── ActivatedAt
  ├── CombatSessionId, optional
  ├── ActivePhaseId
  ├── CompletedAt, optional
  ├── NextAvailableAt, optional
  ├── StateVersion
  └── RuntimeContext

5. Boss State

core states:

INACTIVE
SCHEDULED
AVAILABLE
ACTIVE
DEFEATED
FAILED
COOLDOWN

6. Lifecycle

INACTIVE
  ↓ trigger/schedule configured
SCHEDULED
  ↓ activation time reached
AVAILABLE
  ↓ encounter starts
ACTIVE
  ├── boss killed → DEFEATED
  └── wipe/timeout/reset → FAILED
          ↓
       COOLDOWN
          ↓
       AVAILABLE / SCHEDULED

7. Available vs Active

AVAILABLE:
boss encounter может быть запущен.

ACTIVE:
существует активный BossInstance и CombatSession/encounter.

Это разделение предотвращает двойной запуск.

8. Activation Profile

core trigger types:

SCHEDULED_TIME
PLAYER_INTERACTION
SCRIPTED
ADMIN_DEBUG

Будущее:
ITEM_CONSUME
QUEST_CONDITION
WORLD_EVENT_CHAIN

9. Scheduled Boss

Time System является владельцем времени.

Boss System хранит:

ScheduledAt

Когда Server Time >= ScheduledAt:
state → AVAILABLE или происходит automatic spawn по ActivationProfile.

10. Player Started Boss

Player Interaction trigger:

игрок взаимодействует с world object/NPC;
World System подтверждает interaction;
Boss System проверяет availability;
atomic lock;
создаёт BossInstance;
state → ACTIVE.

11. Single Active Instance

Для одного BossDefinition в одном world scope по умолчанию допускается:

MaxActiveInstances = 1

Операция activation должна быть atomic.

Два одновременных запроса не должны создать двух одинаковых boss.

12. Boss Encounter Scope

core Boss имеет LocationId.

Все participants должны находиться в совместимом location/encounter context.

Без Position System proximity внутри боя не вычисляется.

13. Participants

Boss System получает participants из CombatSession.

Participant может подключиться через правила Combat/World reinforcements, если encounter это допускает.

14. Join Policy

core рекомендует:

OPEN_WHILE_ACTIVE

Игроки в той же location могут присоединиться, пока boss alive, если World/Combat разрешают.

Для instanced boss можно позже использовать CLOSED_AFTER_START.

Participation Rule:

`OPEN_WHILE_ACTIVE` разрешает позднее присоединение, но само присоединение не даёт reward eligibility.

Boss System сохраняет participation timeline.
Loot System применяет `ParticipationPolicy`.

Игрок, вошедший в бой перед смертью boss без qualifying contribution, не получает полный reward.


15. Boss Combat

Boss — MonsterInstance Rank = BOSS.

Он использует:

Stats;
Resources;
Abilities;
Effects;
Damage/Healing;
Monster AI;
Threat.

Boss System не реализует собственную автоатаку.

16. Boss Phase

Boss System хранит ActivePhaseId.

Phase может меняться по condition.

core condition types:

HP_BELOW_PERCENT
COMBAT_TIME_ELAPSED
EVENT_TRIGGERED

17. Phase Definition

BossPhase
  ├── PhaseId
  ├── EntryConditions
  ├── AIProfileOverride, optional
  ├── AbilityEnableTags
  ├── EffectOnEnter, optional
  ├── EffectOnExit, optional
  └── Metadata

18. Phase Transition

При выполнении condition:

Boss System atomically changes ActivePhaseId;
emits BossPhaseChanged;
Monster AI reevaluates decision rules.

19. No Hidden Damage Formula in Phase

Phase может:

изменить AI profile;
применить Effect;
разрешить abilities.

Phase не должна вручную считать damage.

20. Enrage

Enrage для core реализуется как:

scheduled/time condition
→ apply Effect

Например:

Combat time 180 sec
→ apply Enrage effect
→ +damage / +attack speed

Effect System остаётся владельцем modifier lifecycle.

21. Wipe

Wipe считается произошедшим, если:

нет валидных живых player participants;
или Combat System завершил encounter failure;
или encounter timeout.

22. Reset After Wipe

При wipe:

Boss State → FAILED;
Combat ends;
Boss AI stops;
boss temporary state очищается;
reward не создаётся;
после ResetDelay → COOLDOWN/AVAILABLE согласно profile.

23. Boss HP Reset

Для core после failed encounter:

Boss HP восстанавливается до MaxHP перед следующим attempt.

24. Boss Defeat

Boss считается defeated только после подтверждённой смерти Boss MonsterInstance.

Pipeline:

Boss HP = 0
  ↓
Combat confirms death
  ↓
Boss System receives BossDefeated
  ↓
State = DEFEATED
  ↓
Create CompletionId
  ↓
Build reward eligibility context
  ↓
Loot/Progression reward systems invoked
  ↓
Cooldown scheduled

25. Completion Id

Каждая победа имеет уникальный:

BossCompletionId

Он используется для idempotency:

Loot;
XP;
Quest events;
analytics.

26. Boss Rewards

Boss System не roll loot.

Он создаёт RewardSource:

SourceType = BOSS_KILL
SourceId = BossCompletionId
Participants = eligible participants
LootTableId from RewardProfile

Loot System делает roll.

27. Boss XP

Reward pipeline может создать ExperienceGrant:

SourceType = BOSS_KILL
SourceId = BossCompletionId

Progression System остаётся владельцем XP.

28. Boss Quest Event

После подтверждения победы:

BossDefeated event

Quest System решает, относится ли он к objective.

29. Cooldown

После defeat:

NextAvailableAt = CompletedAt + CooldownDuration

Time System используется для проверки.

30. World Event

WorldEventDefinition
  ├── WorldEventDefinitionId
  ├── LocationId
  ├── TriggerProfile
  ├── Duration
  ├── Stages
  ├── BossDefinitionIds, optional
  ├── Version
  └── Metadata

31. World Event State

INACTIVE
SCHEDULED
ACTIVE
COMPLETED
FAILED
COOLDOWN

32. Event Stage

World Event может иметь простые stages.

текущая система рекомендует максимум 1–3 stages.

Пример:

Stage 1: event starts
Stage 2: boss available
Stage 3: boss defeated → event complete

Не нужен универсальный visual scripting engine.

33. Event Trigger

core:

scheduled time;
admin/debug;
scripted server condition.

34. Event Duration

WorldEvent может иметь:

StartsAt
EndsAt

Time System определяет наступление временных границ.

35. Event Failure

Если EndsAt достигнут до completion:

state → FAILED.

Boss encounter может:

быть завершён;
или получить grace period,

в зависимости от EventDefinition.

36. World Integration

World System сообщает:

Location context;
WorldEventOccurred;
interactions.

Boss/WorldEvent System сообщает World:

event active;
boss available/active;
world state markers.

World System не хранит Boss reward state.

37. Quest Integration

Quest System может использовать:

WorldEventStarted
WorldEventCompleted
WorldEventFailed
BossEncounterStarted
BossPhaseChanged
BossDefeated

38. Loot Integration

Loot System получает только подтверждённый RewardSource после completion.

Нельзя выдавать boss loot при:

spawn;
pull;
phase change;
wipe.

39. Participant Eligibility

core eligible participant:

был зарегистрирован Combat participant Boss encounter;
не является spectator/debug invalid entity.

Умерший participant сохраняет eligibility.

Disconnected player сохраняет eligibility, если Character участвовал.

ParticipationPolicy применяется обязательно.

Она может учитывать MinimumParticipationTime и qualifying actions, но healer/support/tank не обязаны выполнять damage threshold для eligibility.

40. Repeated Farming

Cooldown ограничивает повторный запуск BossDefinition.

Если позже нужен per-character weekly lockout:
это отдельное Reward Lockout extension.

Для текущей системы не требуется.

41. Spawn Collision

Activation operation должна использовать logical lock по:

WorldScope + BossDefinitionId

Проверка `state != ACTIVE` без atomic lock недостаточна.

42. Persistence

BossInstance и WorldEvent state должны быть persisted.

Минимум:

DefinitionId;
State;
timestamps;
ActivePhaseId;
CompletionId;
NextAvailableAt;
StateVersion.

43. Restart Recovery

Scheduled/Available/Cooldown:
восстанавливаются по Server Time.

DEFEATED:
completion не должен повторно выдать reward.

44. Active Boss Restart Policy

Активный boss fight намеренно не реконструируется через server restart: применяется deterministic fail policy.

core policy:

если server restart обнаруживает BossInstance.State = ACTIVE:
encounter помечается INTERRUPTED;
boss reward не выдаётся;
Combat session не считается победой;
active cast отменяется;
Boss State → FAILED;
после короткого RecoveryDelay boss становится AVAILABLE согласно ResetProfile.

Зафиксированные до crash player deaths не откатываются автоматически.
Не зафиксированная смерть не должна создаваться задним числом.

Эта policy является официальным operational rule и должна быть отражена в logs/admin tooling.

45. Idempotency

BossCompletionId генерируется только один раз.

Повторная обработка Defeat event:

не создаёт второй completion;
не выдаёт повторный loot;
не выдаёт повторный XP.

46. Events

Boss System:

BossScheduled
BossAvailable
BossEncounterStarted
BossPhaseChanged
BossWipe
BossDefeated
BossEncounterFailed
BossCooldownStarted
BossAvailableAgain

World Event:

WorldEventScheduled
WorldEventStarted
WorldEventStageChanged
WorldEventCompleted
WorldEventFailed

47. Admin / Debug

Для текущей системы обязательны безопасные admin operations:

spawn boss;
despawn/reset boss;
force available;
force cooldown end;
start world event;
stop world event;
inspect BossInstance state.

Admin operations должны логироваться.

48. Boss Scope

Рекомендуется:

2 boss definitions;
1 scheduled/world boss;
1 player-triggered boss;
2–3 phases максимум;
3–5 abilities на boss;
no complex geometry;
no adds unless Combat Reinforcements already stable.

49. Boss Invariants

INVARIANT-01
Boss actions выполняются через Monster AI + Ability System.

INVARIANT-02
Boss System не рассчитывает Damage.

INVARIANT-03
Boss loot рассчитывает Loot System.

INVARIANT-04
Boss XP применяет Progression System.

INVARIANT-05
Boss quest progress определяет Quest System.

INVARIANT-06
Один BossCompletionId не обрабатывается дважды.

INVARIANT-07
Boss activation является atomic.

INVARIANT-08
По умолчанию один BossDefinition имеет не более одного Active instance в world scope.

INVARIANT-09
Wipe не выдаёт reward.

INVARIANT-10
Boss phase change не выдаёт reward.

INVARIANT-11
Cooldown использует Server Time.

INVARIANT-12
Disconnect игрока не удаляет Character из participation автоматически.

INVARIANT-13
Active boss restart в core завершается безопасным FAILED, а не псевдо-победой.

50. Out of Scope

Этот документ пока не определяет:

raid groups;
party matchmaking;
instanced raid lockouts;
weekly lockouts;
cross-server bosses;
position mechanics;
ground hazards requiring coordinates;
cinematics;
cutscenes;
world shards;
dynamic scaling by participant count;
advanced contribution ranking;
leaderboards;
seasonal bosses;
финальный boss roster;
финальные schedules;
UI event calendar.

---

# Source of Truth Revision v2

- OPEN_WHILE_ACTIVE remains allowed, but reward eligibility requires ParticipationPolicy.
- Late join without qualifying contribution does not receive full reward.
- Damage, healing, support and tanking actions may all count as qualifying contribution.
- Server restart during ACTIVE encounter: ACTIVE → FAILED, no rewards, short recovery, then encounter can become available again.
- BossCompletionId/RewardResolution remain idempotency anchors.


## Authoritative Restart Policy

```text
ACTIVE
↓ server restart / unrecoverable process loss
FAILED
↓
No rewards generated
↓
RecoveryCooldown
↓
AVAILABLE / SCHEDULED
```

Активный бой не пытается реконструироваться из частичного runtime state.

## Authoritative Participation

Boss System фиксирует participant timeline/context.
Loot System применяет ParticipationPolicy.

Eligibility не может основываться только на:
- присутствии в CombatSession;
- Last Hit;
- нанесённом damage без учёта healer/support/tank.

## Boss Scope Key

`MaxActiveInstances = 1` применяется **внутри scope**, а не глобально ко всему BossDefinition.

```text
BossScopeKey
WORLD:<WorldScopeId>
DUNGEON:<DungeonInstanceId>
EVENT:<WorldEventInstanceId>
```

Uniqueness:

```text
(BossDefinitionId, BossScopeKey)
```

Следствие:

- один world boss не спавнится дважды в одном world scope;
- две разные DungeonInstance могут одновременно иметь свой экземпляр одного Dungeon BossDefinition;
- boss одной группы не блокирует boss другой группы.

`BossInstance` должен хранить:

```text
BossScopeKey
DungeonInstanceId, optional
DungeonEncounterId, optional
```

---

# Source of Truth Revision v5 — Dungeon / Economy

- Boss reward may include CurrencyRewardProfile; Economy System owns Wallet.
- Boss System never grants Gold directly.
- BossInstance may have optional `DungeonInstanceId` / `DungeonEncounterId` context.
- World Boss participation remains open-world policy.
- Dungeon Boss eligibility is additionally limited by Dungeon MemberSnapshot.
- Restart during a dungeon boss follows:
  `Boss ACTIVE → FAILED`, no reward, Dungeon encounter resets, DungeonInstance remains ACTIVE.
