Elyndor — Quest System Specification

Document: 17_QUEST_SYSTEM.md
System: Quests
Status: Foundation / Source of Truth
Version: 0.1

1. Назначение

Quest System определяет задания, условия доступности, progress, completion и reward orchestration.

Quest System проектируется последним из текущего слоя, потому что использует подтверждённые события:

Progression;
Class;
Items;
Loot;
Monster/Combat;
Boss/World Events;
World;
Abilities;
Effects.

Quest System отвечает за:

QuestDefinition;
QuestState;
Objectives;
availability;
prerequisites;
event-driven progress;
completion;
reward claim;
quest chain;
abandon;
persistence;
idempotency.

2. Основной принцип

Quest System не заставляет другие системы писать quest-specific logic.

Другие системы эмитят факты.

Quest System интерпретирует факты.

Пример:

Combat:
EnemyKilled(Wolf)

Quest:
objective expects Wolf kill
→ progress +1

Combat ничего не знает о квесте.

3. Quest Definition

QuestDefinition
  ├── QuestId
  ├── Name
  ├── Description
  ├── AvailabilityConditions
  ├── Prerequisites
  ├── Objectives
  ├── RewardProfile
  ├── Repeatability
  ├── FailureConditions
  ├── Version
  └── Metadata

4. Character Quest State

CharacterQuest
  ├── CharacterId
  ├── QuestId
  ├── State
  ├── AcceptedAt
  ├── CompletedAt, optional
  ├── RewardClaimedAt, optional
  ├── ObjectiveProgress
  ├── QuestVersion
  └── StateVersion

5. Quest State

core states:

AVAILABLE
ACTIVE
COMPLETED
REWARD_CLAIMED
FAILED
ABANDONED

LOCKED может быть derived availability, не обязательно persisted.

6. State Flow

LOCKED/Unavailable
  ↓ conditions met
AVAILABLE
  ↓ accept
ACTIVE
  ├── objectives completed → COMPLETED
  ├── failure → FAILED
  └── abandon → ABANDONED

COMPLETED
  ↓ claim reward
REWARD_CLAIMED

7. Availability

Quest availability может зависеть от:

Level;
ClassId;
completed quests;
current Location;
World Event state;
Server Time condition;
required item possession;
other explicit server conditions.

8. Prerequisite Graph

Quest может требовать другие QuestId со State = REWARD_CLAIMED или COMPLETED по definition.

Content validation должна запрещать cyclic quest chains.

9. Accept Quest

Server validates:

quest exists;
quest available;
not already active;
not already permanently completed if Repeatability = ONCE;
quest slot limit, если такой limit будет введён.

Для текущей системы рекомендуется отсутствие низкого active quest cap.

10. Objective

QuestObjective
  ├── ObjectiveId
  ├── ObjectiveType
  ├── TargetDefinitionId / TargetTag
  ├── RequiredAmount
  ├── Conditions
  ├── AfkProgressAllowed
  ├── SharedProgressPolicy
  └── Metadata

11. core Objective Types

KILL
COLLECT
VISIT_LOCATION
INTERACT_NPC
INTERACT_OBJECT
USE_ABILITY
APPLY_EFFECT
REACH_LEVEL
DEFEAT_BOSS
COMPLETE_WORLD_EVENT
ENTER_DUNGEON
COMPLETE_DUNGEON
DEFEAT_DUNGEON_BOSS
CRAFT_ITEM
REACH_PROFESSION_LEVEL
LEARN_RECIPE

12. Kill Objective

Источник:

Combat EnemyKilled.

Проверки:

EnemyDefinitionId / tag;
Location, optional;
Ability used, optional;
RequiredAmount.

Kill objective не увеличивается от AFK bonus по умолчанию.

13. Collect Objective

Есть два режима:

EVENT_COUNT
OWNERSHIP_COUNT

EVENT_COUNT:
считает ItemObtained events.

OWNERSHIP_COUNT:
проверяет текущий Inventory count.

Для «принеси 10 шкур» рекомендуется OWNERSHIP_COUNT.

14. Visit Location Objective

Источник:

LocationEntered / CharacterEnteredLocation.

Проверяет LocationId/Tag.

15. Interact Objective

Источник:

NpcInteracted
WorldObjectInteracted

World System должен подтвердить interaction.

16. Use Ability Objective

Источник:

CastCompleted / ability resolved event.

Не считать AbilityUseRequested.

Иначе rejected cast мог бы дать progress.

17. Apply Effect Objective

Источник:

EffectApplied.

Пример:

Apply Poison to 5 enemies.

EffectTick не используется, если objective явно не требует tick-level tracking.

18. Reach Level Objective

Источник:

CharacterReachedLevel.

Также при принятии quest сервер должен проверить current Level, чтобы уже выполненное условие засчиталось.

19. Defeat Boss Objective

Источник:

BossDefeated.

Использует BossCompletionId для deduplication.

20. Complete World Event Objective

Источник:

WorldEventCompleted.

21. Event-Driven Progress

Quest System подписывается на authoritative domain events.

Quest System не должен polling весь мир каждую секунду.

22. Event Idempotency

Каждое progress-relevant событие должно иметь EventId или deduplication key.

CharacterQuest сохраняет обработку достаточным способом, чтобы retry одного события не дал двойной progress.

23. Progress Clamp

ObjectiveProgress не может превышать RequiredAmount, если objective не требует overflow tracking.

24. Multi-Objective Quest

Quest может содержать несколько objectives.

Quest Completed только если:

все обязательные objectives выполнены.

Optional Objectives не входят в core.

25. Ordered Objectives

Для текущей системы поддерживаются два режима:

PARALLEL
SEQUENTIAL

SEQUENTIAL:
следующий objective активируется только после предыдущего.

26. Quest Completion

Когда все required objectives выполнены:

State = COMPLETED
CompletedAt = Server Time
Emit QuestCompleted

Reward автоматически не обязан выдаваться в тот же момент.

27. Reward Profile

QuestRewardProfile может содержать:

ExperienceGrant amount/profile;
Item rewards;
LootTable reward;
Currency rewards;
unlock flag;
scripted world progression hook.

Quest System orchestrates, но делегирует:

XP → Progression System
Item → Item System
Loot table → Loot System
Currency → Economy System

28. Reward Claim

Для текущей системы используется explicit server operation:

ClaimQuestReward

Авторитетный порядок Reward Claim:

1. Validate Quest State = COMPLETED.
2. Create/lock CompletionInstance reward context.
3. Apply Experience grants через Progression System.
4. Apply direct Item grants через Item System.
5. Resolve LootTable rewards через Loot System, если они есть.
6. Verify all mandatory reward entries are confirmed or safely persisted as pending.
7. Only then set Quest State = REWARD_CLAIMED.
8. Emit QuestRewardClaimed.

Каждый шаг использует idempotent GrantId.

Если XP уже успешно выдан, а Item reward остался pending из-за полного Inventory:

повторный Claim не выдаёт XP второй раз;
Item Grant повторяется безопасно;
Quest остаётся COMPLETED до подтверждения всех обязательных reward entries.

29. Reward Atomicity

Полная распределённая transaction между системами может быть сложной.

Поэтому каждый reward component должен иметь stable GrantId derived from:

CharacterId + QuestId + CompletionInstanceId + RewardEntryId

Retry безопасно повторяет незавершённые grants.

Quest переходит REWARD_CLAIMED только когда обязательные grants подтверждены.

30. Inventory Full

Если quest reward item не помещается:

Quest остаётся COMPLETED;
reward entry остаётся pending;
XP component не должен выдаваться дважды при retry;
игрок может повторить Claim после освобождения Inventory.

31. Quest Completion Instance

Для non-repeatable quest:

один CompletionInstanceId.

Для будущих repeatable quests понадобятся разные completion instances.

Repeatable поддерживается архитектурой, но конкретный QuestDefinition должен явно включить repeat policy.

32. Repeatability

Базовое правило:

ONCE

Не поддерживать daily/weekly до проверки базовой модели.

33. Abandon

ACTIVE quest может быть abandoned, если QuestDefinition разрешает.

При abandon:

progress удаляется или reset;
reward отсутствует;
quest может снова стать AVAILABLE, если conditions выполнены.

Quest-protected items:
policy определяется QuestDefinition/Item integration.

34. Failed Quest

FAILED используется только если QuestDefinition имеет explicit FailureConditions.

Обычные quests рекомендуется делать без fail state.

Timed quest может:

EndsAt reached
→ FAILED

Time System предоставляет время.

35. AFK Progress

Default:

AfkProgressAllowed = false

AFK System уже сообщает результат как AFK_BONUS.

Конкретный objective может явно разрешить AFK.

36. AFK Kill Semantics

AFK Farming не эмитит обычный EnemyKilled per simulated kill.

Поэтому KILL objective не должен случайно прогрессировать.

Если objective разрешает AFK:

Quest System использует explicit AFK reward/result event с согласованным target context.

37. Class Conditions

Quest availability может содержать:

RequiredClassId.

Quest System читает Class State.

Class System не хранит quest flags.

38. Item Conditions

Quest может требовать:

HaveItem(ItemDefinitionId, Quantity)
EquippedItemTag, если действительно нужно.

Item System является source of truth.

39. Talent Conditions

В текущей системе не рекомендуется делать quest availability зависимой от конкретного Talent.

Технически Quest System может читать Talent event/state позже.

40. Boss Integration

BossDefeated payload должен содержать:

BossDefinitionId;
BossCompletionId;
Participants;
LocationId;
CompletedAt.

Quest System прогрессирует только eligible Character quest.

41. World Event Integration

Quest может:

стать available во время event;
требовать event completion;
требовать boss spawned by event.

Availability должна пересчитываться при relevant WorldEvent event, а не глобальным polling.

42. Combat Integration

Quest System может слушать:

EnemyKilled
CombatResult
ReinforcementJoined, редко

Для текущей системы основное:
EnemyKilled.

43. Progression Integration

Quest System слушает:
CharacterReachedLevel

Quest rewards создают ExperienceGrant.

44. Ability Integration

Quest System слушает только подтверждённые ability results, например:

CastCompleted;
AbilityInterruptApplied.

45. Effect Integration

По умолчанию:

EffectApplied;
EffectExpired;
EffectRemoved;
EffectDispeled.

EffectTick только для objective, который явно требует tick-level.

46. Quest Log

Character может иметь набор ACTIVE/COMPLETED quests.

UI Quest Log является display layer.

Server state остаётся source of truth.

47. Quest Chain

QuestDefinition может иметь:

PrerequisiteQuestIds

После RewardClaimed/Completed предыдущего:
следующий quest может стать AVAILABLE.

48. Narrative

Quest System хранит text/content references, но не требует отдельного dialogue engine для текущей системы.

NPC interaction может открыть quest accept/turn-in UI.

49. Server Restart

После restart:

ACTIVE quests восстанавливаются;
progress сохраняется;
timed conditions проверяются по Server Time;
COMPLETED reward не теряется;
частично выданный reward безопасно продолжается через idempotent GrantId;
обработанный EventId не считается повторно.

50. Persistence

Persist:

CharacterQuest;
ObjectiveProgress;
State timestamps;
QuestVersion;
CompletionInstanceId;
reward grant state;
deduplication state/keys.

51. Versioning

QuestDefinition имеет Version.

Если content patch меняет objective:

активные quests нельзя молча преобразовывать без policy.

Рекомендация:

active CharacterQuest keeps AcceptedQuestVersion;
breaking change → reset/compensate explicitly.

52. Events

Quest System эмитит:

QuestAvailable
QuestAccepted
QuestObjectiveProgressed
QuestObjectiveCompleted
QuestCompleted
QuestRewardClaimStarted
QuestRewardClaimed
QuestFailed
QuestAbandoned

53. Analytics

Analytics полезно собирать:

accept count;
completion count;
abandon count;
time to complete;
objective bottlenecks;
reward claim failures;
inventory-full reward blocks.

54. Quest Scope

Рекомендуется:

10–15 quests;
1–2 short chains;
Kill;
Collect;
Visit;
Interact;
ReachLevel;
DefeatBoss;
CompleteWorldEvent.

Не нужен сложный branching narrative до проверки core loop.

55. Example Quest Chain

Quest 1:
Talk to Guard.

Quest 2:
Kill 5 Wolves.

Quest 3:
Collect 3 Wolf Pelts.

Quest 4:
Reach Old Mine.

Quest 5:
Defeat Elite Miner.

Quest 6:
Reach Level 5.

Quest 7:
Defeat Boss.

Этот chain проверяет почти весь vertical slice.

56. Quest Invariants

INVARIANT-01
Quest progress изменяет только Quest System.

INVARIANT-02
Другие системы эмитят facts, но не меняют quest progress.

INVARIANT-03
Progress events должны быть server-authoritative.

INVARIANT-04
Повторная доставка одного event не даёт двойной progress.

INVARIANT-05
Quest reward grant должен быть idempotent.

INVARIANT-06
Quest System не создаёт ItemInstance напрямую.

INVARIANT-07
Quest System не изменяет XP напрямую.

INVARIANT-08
Boss objective использует подтверждённый BossDefeated.

INVARIANT-09
Ability objective использует подтверждённый ability result, не request.

INVARIANT-10
AFK progress запрещён по умолчанию.

INVARIANT-11
Quest chain graph не должен содержать cycles.

INVARIANT-12
COMPLETED quest не считается REWARD_CLAIMED до подтверждения reward grants.

INVARIANT-13
Server restart не сбрасывает active progress.

INVARIANT-14
Quest availability проверяется сервером.

57. Out of Scope

Этот документ пока не определяет:

branching dialogue;
moral choices;
daily quests;
weekly quests;
repeatable quests;
procedural quests;
quest sharing;
party-shared quest progress;
escort quests requiring Movement;
cutscenes;
voiceover;
cinematic scripting;
account-wide quests;
achievements;
battle pass;
seasonal quest lines;
dynamic scaling;
финальный narrative;
полный quest text;
UI quest tracker.

---

# Source of Truth Revision v2

- Quest architecture supports optional objectives, repeatability and failure conditions even if individual content does not use them.
- Party shared credit is objective-specific: kill/boss/event objectives may share; collect/use/dialogue objectives are personal by default.
- Quest reward may orchestrate XP, items and currency through owner systems.
- Quest System observes confirmed events and never injects quest-specific logic into Combat/AI.

---

# Source of Truth Revision v5 — Dungeon / Crafting / Economy

## Dungeon objectives

Authoritative sources:

```text
ENTER_DUNGEON → DungeonEntered
COMPLETE_DUNGEON → DungeonCompleted
DEFEAT_DUNGEON_BOSS → BossDefeated with DungeonInstanceId context
```

Quest System only consumes confirmed events.

## Profession objectives

```text
CRAFT_ITEM → CraftCompleted
REACH_PROFESSION_LEVEL → ProfessionLevelReached
LEARN_RECIPE → RecipeLearned
```

Crafting/Profession System never mutates quest progress directly.

## Currency rewards

QuestRewardProfile may include:

```text
CurrencyRewardEntry
├── CurrencyId
├── Amount / ProfileId
└── RewardEntryId
```

GrantId derives from:

```text
CharacterId
+ QuestId
+ CompletionInstanceId
+ RewardEntryId
```

Economy System is Wallet owner.
