Elyndor — Monster and AI System Specification

Document: 15_MONSTER_AND_AI_SYSTEM.md
System: Monsters / AI
Status: Foundation / Source of Truth
Version: 0.1

1. Назначение

Monster and AI System определяет игровые сущности противников и серверное принятие ими решений.

Система отвечает за:

MonsterDefinition;
MonsterInstance;
combat AI state;
выбор действий;
использование abilities;
AI priorities;
cooldown/resource validation через существующие системы;
death/despawn hooks;
reset;
server-side randomness.

Система не определяет:

общие Combat rules;
Threat formula;
Damage formula;
Ability mechanics;
Effect lifecycle;
loot generation;
boss event lifecycle;
World encounter chance.

2. Основной принцип

Monster AI не имеет собственного «второго Combat Engine».

Монстр является Combat participant и использует те же:

Ability System;
Resource System;
Effect System;
Damage and Healing System;
Threat System;
Time System.

AI только решает:

что попытаться сделать следующим.

3. Monster Definition

MonsterDefinition
  ├── MonsterDefinitionId
  ├── Name
  ├── MonsterRank
  ├── Level
  ├── BaseStatProfileId
  ├── ResourceProfileId
  ├── AutoAttackProfile
  ├── AbilityIds
  ├── AIProfileId
  ├── AggressionProfile
  ├── LootTableId, optional
  ├── RespawnProfileId
  ├── Tags
  ├── Version
  └── Metadata

4. Monster Rank

core ranks:

NORMAL
ELITE
BOSS

Boss lifecycle определяется Boss System, но Boss всё равно использует MonsterDefinition/AI.

5. Monster Instance

MonsterInstance
  ├── MonsterInstanceId
  ├── MonsterDefinitionId
  ├── LocationId
  ├── LifeState
  ├── CombatSessionId, optional
  ├── CurrentTargetId, optional
  ├── SpawnedAt
  ├── StateVersion
  └── RuntimeContext

HP, Resources, Effects и Threat хранятся их соответствующими системами/context.

6. AI Profile

AIProfile
  ├── AIProfileId
  ├── DecisionRules
  ├── FallbackAction
  ├── ReevaluatePolicy
  ├── Randomization
  └── Version

7. AI State

core AI State:

IDLE
ENGAGED
IN_COMBAT
DEAD
RESETTING

Boss может иметь дополнительный Encounter Phase context из Boss System.

8. No Movement

Combat System не использует позицию/дистанцию.

Поэтому Monster AI не делает:

pathfinding;
chase;
kite;
move-to-range;
facing;
dash.

World aggression решает, начнётся ли encounter.

После начала Combat цель считается достижимой по правилам текущей модели.

9. Target Selection

Monster AI не рассчитывает Threat.

Threat System выбирает valid target.

AI получает CurrentAggroTarget.

Если ForcedTarget active:
используется он.

10. Action Decision

Когда Monster готов совершить действие:

AI оценивает DecisionRules.

Пример:

if HP < 30% and DefensiveAbility available
  → use DefensiveAbility

else if HighPriorityAbility available
  → use it

else if SecondaryAbility available
  → use it

else
  → Auto Attack / wait

11. Decision Timing

AI не должен polling каждую миллисекунду.

Decision evaluation выполняется на значимых событиях:

Combat started;
Auto Attack ready;
GCD ended;
cast completed/interrupted;
resource became sufficient;
target changed;
important effect applied/expired;
Boss phase changed.


Если несколько decision triggers срабатывают в одном server processing window или относятся к одному logical moment:

они объединяются в одну pending AI decision evaluation.

Для одного MonsterInstance не выполняются несколько параллельных decision evaluations одновременно.

После завершения текущей evaluation накопленные новые triggers могут инициировать следующую evaluation, только если состояние действительно изменилось.

12. Ability Validation

AI может выбрать AbilityId, но окончательное решение принимает Ability System.

Если ability rejected:

AI получает reason;
может выбрать fallback action;
не обходит cooldown/resource/control rules.

13. AI Rule

AIRule
  ├── Priority
  ├── AbilityId
  ├── Conditions
  ├── Weight
  ├── InternalRuleCooldown, optional
  └── FallbackBehavior

14. Conditions

core conditions:

SelfHPPercentBelow
SelfHPPercentAbove
TargetHPPercentBelow
HasEffect
MissingEffect
TargetHasEffect
ResourceAtLeast
AbilityReady
TargetIsValid
CombatTimeElapsed
BossPhaseIs
RandomChance

15. Priority

Rules сортируются по Priority.

Если несколько rules одного priority валидны:

может использоваться Weight/random.

16. Randomness

AI randomness выполняется сервером.

Randomness должна быть ограниченной.

Главные boss mechanics не должны зависеть от неконтролируемого 1% roll, если это ломает encounter.

17. Fallback Action

Fallback для большинства melee monsters:

Auto Attack.

Для caster monster может быть:

basic instant/cast ability.

Fallback никогда не нарушает Ability System.

18. Normal Monster Archetype

Basic Melee:

Auto Attack;
1 simple ability;
иногда 1 buff/debuff;
простая priority list.

19. Caster Monster Archetype

Caster:

1 basic spell;
1 longer cooldown spell;
optional defensive/control ability.

Silence/Interrupt должны влиять на cast согласно Ability/Effect System.

20. Elite Monster

Elite:

выше Stats;
2–4 abilities;
может иметь более сложный AIProfile;
DR на control effects согласно Effect System.

Elite не требует отдельного Combat Engine.

21. Boss Monster

Boss использует:

MonsterDefinition Rank = BOSS;
Boss System encounter lifecycle;
Monster AI decision rules;
Boss phase context;
boss-specific Effect DR.

22. Aggression

World System определяет Aggression Model:

Passive;
Defensive;
Aggressive;
Predatory;
Territorial.

Monster AI не делает world encounter rolls.

После World System создаёт encounter:
Monster AI вступает в Combat.

23. Combat Start

При Combat start:

AI State = IN_COMBAT;
Threat table initialized Combat System;
target selected Threat System;
AI decision scheduler activated.

24. Combat End

При Combat end:

если monster dead:
DEAD.

если reset/encounter cancelled:
RESETTING → IDLE/despawn согласно World/Spawn rules.

25. Reset

Reset очищает transient combat state:

target;
Threat table через Combat;
temporary combat Effects по их правилам;
cast;
queued ability;
combat-only resource state, если профиль требует.

HP reset policy определяется encounter/spawn profile.

Для обычного monster core:
reset → full HP.

26. Leash Without Position

Так как Movement отсутствует, geometric leash отсутствует.

Reset может произойти если:

нет valid hostile targets;
Combat System завершил encounter;
scripted encounter timeout;
Boss System объявил wipe/reset.

27. Death

Monster death:

LifeState = DEAD;
AI stops;
casts interrupted;
Combat publishes EnemyKilled;
Loot/Progression reward pipeline может быть запущен;
respawn/despawn решается World/Spawn rules.

28. Respawn

Monster AI не определяет world spawn schedule.

World System/Spawn profile создаёт новый MonsterInstance.

29. Resource

Monster может использовать:

Mana;
Rage;
Energy;
или no Action Resource, если AbilityDefinition не требует cost.

Resource System остаётся владельцем состояния.

30. Effects

Stun:
AI не действует.

Silence:
AI не может использовать запрещённые abilities.

DoT/HoT:
разрешаются Effect System.

AI может учитывать HasEffect в decisions.

31. Threat

AI не меняет ThreatValue напрямую.

Damage/Healing/Taunt генерируют threat по Combat rules.

AI использует выбранную Threat System цель.

32. Offline Characters

Offline Character остаётся обычной потенциальной Combat target, если World/Combat создали encounter.

AI не различает target по UI connection status, если combat rules не указывают иное.

33. AFK Farming

AFK Farming по умолчанию не создаёт реальный MonsterInstance/CombatSession.

Monster AI в AFK bonus mode не запускается.

34. AI and Boss Phase

Boss System может установить:

ActivePhaseId

AI conditions могут использовать BossPhaseIs.

Boss System не выбирает конкретную кнопку каждую секунду.

Monster AI выбирает действие внутри правил текущей phase.

35. AI Cooldown vs Ability Cooldown

Ability cooldown — реальный gameplay cooldown.

AI InternalRuleCooldown — только ограничение decision rule, если нужно избежать spam-паттерна.

Нельзя использовать AI cooldown для обхода Ability cooldown.

36. Server Load

AI evaluation event-driven.

Нельзя создавать бесконечный tight loop:

while monster alive:
  check all rules continuously

Сервер планирует следующее значимое решение.

37. Persistence

Обычные NORMAL/ELITE monster AI runtime state не обязан полностью переживать server restart В текущей системе.

Content definitions persist.

Boss persistence определяется Boss System.

38. Restart Recovery for Normal Monsters

policy:

обычные незавершённые monster encounters после server restart не восстанавливаются как точная AI simulation;
MonsterInstance пересоздаётся/respawn по World rules;
незавершённый ActiveCast отменяется согласно Ability System;
зафиксированные смерти не откатываются.

39. Boss Restart

Boss encounter recovery не определяется Monster AI.

Boss System является владельцем этой policy.

40. Events

Monster AI System эмитит:

MonsterAIActivated
MonsterActionSelected
MonsterActionRejected
MonsterTargetContextChanged
MonsterResetStarted
MonsterResetCompleted

Combat/Ability уже эмитят фактические gameplay results.

AI event не заменяет CastCompleted или EnemyKilled.

41. Debugability

Для текущей системы крайне полезно логировать:

MonsterInstanceId;
AIProfileId;
evaluated rules;
selected rule;
rejection reason;
target;
timestamp.

42. Monster Content

Рекомендуется:

6–8 NORMAL monster definitions;
2 ELITE;
2 BOSS definitions.

43. Monster AI Invariants

INVARIANT-01
AI решения принимает сервер.

INVARIANT-02
AI не обходит Ability validation.

INVARIANT-03
AI не рассчитывает Damage самостоятельно.

INVARIANT-04
AI не рассчитывает Threat самостоятельно.

INVARIANT-05
Target выбирается Threat System.

INVARIANT-06
World System является владельцем encounter generation.

INVARIANT-07
Loot System является владельцем loot roll.

INVARIANT-08
Stun блокирует действия monster через существующие systems.

INVARIANT-09
Silence влияет на abilities согласно Effect/Ability rules.

INVARIANT-10
AI не требует Movement/Position System.

INVARIANT-11
AI evaluation должна быть event-driven.

INVARIANT-12
Monster death останавливает AI.

INVARIANT-13
AFK Farming не запускает Monster AI по умолчанию.

INVARIANT-14
Boss lifecycle принадлежит Boss System.

INVARIANT-15
Одновременные AI decision triggers coalesce в одну evaluation; параллельные evaluation одного MonsterInstance запрещены.

44. Out of Scope

Этот документ пока не определяет:

pathfinding;
navigation mesh;
movement;
distance;
facing;
stealth detection;
pet/companion AI rules (owner: Companion System);
advanced behavior tree editor;
machine learning AI;
PvP bots;
procedural boss AI;
dynamic difficulty;
final monster roster;
final boss mechanics;
spawn density;
world encounter chance;
UI.

---

# Source of Truth Revision v2

- Monster AI и Companion AI используют общий Combat/Ability/Effect/Damage engine.
- Companion имеет отдельный CompanionAIProfile и owner context; он не маскируется под hostile Monster.
- No Movement остаётся базовым правилом текущего Combat.
- AI decision trigger coalescing сохраняется для защиты от нескольких одновременных reevaluate.

---

# Source of Truth Revision v5 — Instanced Monster Context

MonsterInstance may contain:

```text
WorldScopeId
DungeonInstanceId, optional
DungeonEncounterId, optional
```

Spawn uniqueness and encounter cleanup must include scope.

The same MonsterDefinition may exist simultaneously in many DungeonInstance values.

Monster AI only mutates its own MonsterInstance/CombatSession and never queries another dungeon instance by definition id alone.
