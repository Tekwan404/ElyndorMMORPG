Elyndor — Ability System Specification

Document: docs/source-of-truth/gameplay/10_ABILITY_SYSTEM.md
System: Abilities
Status: Foundation / Source of Truth
Version: 0.1

1. Назначение

Ability System определяет правила игровых способностей персонажей, NPC и других боевых сущностей.

Система охватывает:

типы способностей;
условия применения;
стоимость ресурса;
cooldown;
targeting;
взаимодействие с Auto Attack;
Global Cooldown;
очередь способностей;
interrupt;
взаимодействие с Combat System, Effect System, Resource System и Damage and Healing System.

Ability System не определяет:

конкретные значения урона способностей;
конкретные значения лечения способностей;
конкретные коэффициенты масштабирования;
AI-использование способностей;
классовые деревья в деталях;
конкретные таланты;
экономику;
лут;
UI;
визуализацию способностей.

2. Основной принцип

Сервер является единственным авторитетным источником состояния способностей.

Клиент может отправлять запросы на использование способности, но не может:

подтверждать успех использования;
определять стоимость ресурса;
определять cooldown;
определять результат способности;
прерывать или отменять cast самостоятельно.

Итоговое решение — разрешить или отклонить способность — принимает сервер.

3. Ability Entity

Каждая способность концептуально представляется как определение:

AbilityDefinition
  ├── AbilityId
  ├── AbilityType
  ├── Name
  ├── TargetType
  ├── TargetSelectorProfileId, optional
  ├── AllowSelfTarget
  ├── AllowOfflineAutoUse
  ├── ResourceCost
  ├── ResourceCostType
  ├── CastTime
  ├── Cooldown
  ├── GlobalCooldownCategory
  ├── UsesGlobalCooldown
  ├── InterruptedByCast
  ├── InterruptedByStun
  ├── InterruptedBySilence
  ├── CancellableByPlayer
  ├── CanCrit
  ├── IgnoresMiss
  ├── IgnoresDodge
  ├── IgnoresArmor
  ├── IgnoresMagicResistance
  ├── IgnoresShields
  ├── GeneratesThreat
  ├── ThreatMultiplier
  ├── Effects
  └── Metadata

Конкретная техническая реализация будет определена позднее.

Экземпляр активного каста представляется как:

ActiveCast
  ├── CastId
  ├── AbilityId
  ├── CasterId
  ├── TargetId
  ├── StartedAt
  ├── ResolvesAt
  ├── SnapshotData
  └── State

4. Типы способностей

Ability System определяет четыре базовых типа способностей.

4.1. Instant Ability

Instant Ability применяется мгновенно в момент принятия сервером.

Не требует времени каста.

Не блокирует Auto Attack cycle по умолчанию.

Snapshot фиксируется в момент использования.

Пример:

Shield Bash
Instant
Deals physical damage.
Applies Stun 1.5 sec.
Costs 20 Rage.

Пример:

Fireball (Instant version)
Instant
Deals magical damage.
No cast time.
Costs 40 Mana.

4.2. Casted Ability

Casted Ability требует времени каста.

Во время каста персонаж занят.

Casted Ability по умолчанию блокирует Auto Attack на время каста.

Snapshot фиксируется в момент успешного начала каста.

Если Auto Attack становится готовой во время каста, она ожидает завершения каста.

Пример:

Fireball
Casted
Cast Time = 2 sec
Deals magical damage.
Costs 50 Mana.

4.3. Next Attack Modifier

Next Attack Modifier не наносит урон напрямую.

Он модифицирует следующую подходящую Auto Attack.

После применения Auto Attack эффект расходуется.

Snapshot параметры Next Attack Modifier не фиксируются при применении.

Сама Auto Attack разрешается по текущим параметрам в момент её выполнения.

Пример:

Poisoned Blade
Instant
Next Auto Attack:
  - deals bonus physical damage
  - applies Poison effect

Пример:

Aimed Shot
Instant
Next ranged Auto Attack:
  - deals bonus physical damage
  - ignores partial armor

4.4. Taunt Ability

Taunt Ability явно взаимодействует с Threat System.

Taunt может:

добавлять фиксированное количество Threat;
устанавливать ForcedTarget на моба;
устанавливать Threat атакующего выше текущего лидера.

Taunt Ability является Instant Ability по умолчанию.

Пример:

Provoke
Instant
+500 Threat on target.
ForcedTarget duration = 3 sec.
Costs 15 Rage.

5. Условия применения

Перед разрешением способности сервер проверяет условия.

Базовые условия:

Персонаж должен быть жив.
Персонаж не должен находиться в состоянии DEAD или RESPAWNING.
Способность должна быть известна персонажу.
Способность не должна быть на Cooldown.
Global Cooldown не должен быть активен, если способность его использует.
Ресурс должен быть достаточен.
Цель должна быть валидной согласно TargetType.
Персонаж не должен быть под действием эффекта, блокирующего использование способностей:
  Stun блокирует все способности.
  Silence блокирует все способности, кроме тех, которые явно разрешены под Silence.
Если способность требует наличия цели, цель должна быть указана.
Если способность требует активного боя, персонаж должен быть IN_COMBAT.

Если любое условие не выполнено:

сервер отклоняет запрос;
клиент получает причину отказа, если это предусмотрено протоколом.

6. Resource Cost

Стоимость ресурса определяется AbilityDefinition.

Стоимость проверяется и списывается согласно правилам Resource System.

6.1. Проверка стоимости

Для Instant Ability:

проверка и списание происходят в момент принятия действия сервером.

Для Casted Ability:

стоимость проверяется в момент начала каста;
стоимость списывается в момент начала каста;
если каст прерывается, ресурс по умолчанию не возвращается.

Для Next Attack Modifier:

стоимость проверяется и списывается в момент применения модификатора.

6.2. Модификаторы стоимости

Стоимость способности может быть изменена эффектами или талантами.

Примеры:

-20% Mana cost of Fire spells;
Next ability costs no resource;
Ability costs +10 Rage;
Free cast: ability costs 0 resource.

Модификаторы стоимости применяются сервером в момент проверки.

6.3. Нулевая стоимость

Способность может иметь нулевую стоимость.

Это не является ошибкой.

Если стоимость равна 0, проверка ресурса всё равно происходит, но всегда проходит.

7. Cooldown

После использования способность может переходить в Cooldown.

Cooldown является временным состоянием способности для конкретного персонажа.

AbilityUsed
  ↓
Cooldown Starts
  ↓
AbilityUnavailable
  ↓
CooldownEndsAt = UsedAt + CooldownDuration
  ↓
AbilityAvailable

7.1. Хранение Cooldown

Cooldown хранится как абсолютная точка завершения:

CooldownEndsAt = UsedAt + CooldownDuration

Не используется countdown.

Это позволяет корректно работать после server restart.

7.2. Cooldown после прерывания

Если Casted Ability была прервана:

cooldown прерванной способности определяется правилами самой способности;
по умолчанию прерванный каст не запускает полный cooldown;
конкретное поведение (no cooldown / partial cooldown / full cooldown) указывается в AbilityDefinition.

7.3. Cooldown Reduction

Cooldown может быть уменьшен эффектами или талантами.

Примеры:

-20% cooldown reduction;
Reset cooldown on kill;
Cooldown reduced by 1 sec on critical hit.

Cooldown Reduction применяется к будущим cooldowns, если эффект не указывает иное.

Конкретная формула Cooldown Reduction определяется отдельно.

8. Global Cooldown

Global Cooldown (GCD) — это короткое общее время восстановления, применяемое после использования большинства способностей.

8.1. Базовые правила GCD

Использование способности с GCD запускает GCD для персонажа.

Пока GCD активен, другие способности с GCD не могут быть использованы.

GCD является отдельным таймером от индивидуального Cooldown способности.

GCD хранится как:

GCDEndsAt = UsedAt + GCDDuration

Current data-driven default:

GCDDuration = 1.5 seconds

8.2. GCD и Cooldown независимы

Индивидуальный Cooldown и GCD тикают параллельно.

Пример:

0.0 — Fireball used
0.0 — GCD starts (ends at 1.5)
0.0 — Fireball Cooldown starts (ends at 8.0)

1.5 — GCD ends → другие способности с GCD доступны
8.0 — Fireball Cooldown ends → Fireball доступен

8.3. GCD Categories

Не все способности используют единый GCD.

AbilityDefinition содержит:

GlobalCooldownCategory
UsesGlobalCooldown

Базовые категории:

STANDARD — обычный GCD 1.5 sec;
SHORT — сокращённый GCD для некоторых способностей;
NONE — способность не использует GCD.

Примеры:

Обычные способности → STANDARD GCD;
Racial abilities или Off-GCD → NONE;
Некоторые instant способности → SHORT GCD.

Конкретный список категорий может быть расширен.

8.4. Off-GCD способности

Способности с UsesGlobalCooldown = false не запускают и не блокируются GCD.

Примеры Off-GCD:

боевые крики танка;
некоторые реактивные способности;
racial abilities.

8.5. GCD и Auto Attack

GCD не блокирует Auto Attack cycle.

Auto Attack продолжает тикать независимо от GCD.

9. Targeting

TargetType определяет допустимые цели для способности.

9.1. Authoritative TargetTypes

```text
SELF
SINGLE_ENEMY
SINGLE_ALLY
ALL_ENEMIES_IN_COMBAT
N_ENEMIES_IN_COMBAT
SELF_AND_PARTY_MEMBERS_IN_COMBAT
ACTIVE_COMPANION
OWNER
```

`N_ENEMIES_IN_COMBAT` выбирает до N целей deterministic selector'ом способности (например lowest HP, highest Threat, random-seeded). Понятие «ближайший» не используется, потому что position/distance отсутствуют.

9.2. Правила выбора цели

### SELF
Цель всегда caster.

### SINGLE_ENEMY
Одна валидная hostile цель в том же CombatSession/Encounter.

### SINGLE_ALLY
Одна валидная allied цель. Разрешение выбрать самого caster задаётся `AllowSelfTarget`.

### ALL_ENEMIES_IN_COMBAT
Все валидные hostile targets текущего encounter, с optional target cap из AbilityDefinition.

### N_ENEMIES_IN_COMBAT
До N hostile targets по явному SelectorProfile.

### SELF_AND_PARTY_MEMBERS_IN_COMBAT
Caster + валидные члены его Party в том же CombatSession. Случайные союзники encounter не включаются.

### ACTIVE_COMPANION
Текущий active Companion caster'а.

### OWNER
OwnerCharacterId для companion ability.

Ни один TargetType не требует position, distance, facing или pathfinding.

9.3. Союзники и враги

Способность может быть разрешена для:

враждебных целей (атакующие способности);
союзных целей (лечение, бафф);
любых целей (включая себя);
конкретного типа сущности.

Допустимая сторона определяется в AbilityDefinition.

9.4. Мёртвые цели

По умолчанию мёртвые цели не могут быть атакованы.

По умолчанию мёртвые цели не могут быть вылечены.

Если конкретная способность разрешает взаимодействие с мёртвой целью, это указывается явно.

10. Взаимодействие с Auto Attack

10.1. Instant Ability и Auto Attack

Instant Ability по умолчанию не прерывает Auto Attack cycle.

Пример:

0.0 — Auto Attack hits
0.7 — Instant Ability used
1.4 — Auto Attack hits again

Если конкретная Instant Ability должна вмешиваться в Auto Attack, это указывается в AbilityDefinition.

10.2. Casted Ability и Auto Attack

Casted Ability блокирует Auto Attack на время каста.

Если Auto Attack становится готовой во время каста, она ожидает завершения каста.

После завершения каста Auto Attack цикл возобновляется.

Пример:

0.0 — Fireball cast starts
0.5 — Auto Attack becomes ready (waits)
2.0 — Fireball resolves
2.0+ — Auto Attack fires immediately

Точная политика — немедленная или отложенная Auto Attack после завершения каста — определяется отдельно.

10.3. Next Attack Modifier и Auto Attack

Next Attack Modifier специально разработан для взаимодействия со следующей Auto Attack.

После применения модификатора следующая Auto Attack использует его эффект.

Модификатор расходуется после одной Auto Attack.

Если персонаж имеет несколько Next Attack Modifiers одновременно:

каждый модификатор применяется к следующей соответствующей Auto Attack;
порядок применения нескольких модификаторов к одной атаке определяется отдельно.

11. Cast Process

Процесс каста для Casted Ability:

11.1. Начало каста

Player sends UseAbility request
  ↓
Server validates conditions
  ↓
If valid:
  Resource cost deducted
  Snapshot taken
  ActiveCast created
  CastStarted event emitted
  ResolvesAt = StartedAt + CastTime

11.2. Во время каста

Во время каста персонаж находится в состоянии CASTING.

Auto Attack заблокирован.

GCD запускается в момент начала каста.

Другие Casted Ability не могут начаться.

Instant Ability по умолчанию не может использоваться во время каста.

Исключение: Off-GCD Instant способности могут использоваться во время каста, если это явно разрешено.

11.3. Завершение каста

Когда Server Time >= ResolvesAt:

AbilityEffect разрешается;
результат применяется к цели;
ActiveCast удаляется;
Cooldown запускается;
CastCompleted event emitted.

11.4. Прерывание каста

Каст может быть прерван.

Базовые причины прерывания:

Stun прерывает каст.
Silence прерывает каст.
Явная отмена игроком, если CancellableByPlayer = true.
Смерть кастера прерывает каст.
Смерть цели прерывает каст, если способность требует живой цели.
Scripted event.

При прерывании каста:

ActiveCast удаляется;
эффект не разрешается;
ресурс по умолчанию не возвращается;
cooldown по умолчанию не запускается (или запускается частично, если указано в AbilityDefinition);
CastInterrupted event emitted.

11.5. Отмена каста игроком

Если CancellableByPlayer = true:

игрок может отменить каст вручную;
каст прерывается согласно правилам прерывания.

Если CancellableByPlayer = false:

игрок не может отменить каст;
каст может быть прерван только внешними условиями.

12. Interrupt

Interrupt — это принудительное прерывание активного каста.

12.1. Interrupt способности

Некоторые способности могут прерывать каст цели.

Пример:

Kick
Instant
Interrupts target's current cast.
If interrupted successfully:
  interrupted ability goes on lockout for 3 sec.
Costs 15 Energy.

12.2. Lockout

После успешного Interrupt целевая способность или школа способностей может быть заблокирована на определённый период.

Lockout Duration = X seconds (определяется Interrupt способностью).

Пример:

Caster interrupted while casting Fireball.
Fire School locked out for 3 sec.
Caster cannot use Fire abilities for 3 sec.

Lockout является временным состоянием для конкретного персонажа.

Lockout хранится как:

LockoutEndsAt = InterruptedAt + LockoutDuration

12.3. Spell School Lockout

Lockout может применяться к конкретной School способностей.

Примеры школ:

Physical;
Fire;
Frost;
Arcane;
Shadow;
Holy;
Nature.

Конкретные школы определяются отдельно при проектировании классов.

Для core достаточно:

Physical;
Magical (без разделения на элементы).

12.4. Interrupt иммунитет

Некоторые способности не могут быть прерваны.

Это указывается в AbilityDefinition:

InterruptedByCast = false
InterruptedByStun = false
InterruptedBySilence = false

Примеры:

боссовые способности, которые не прерываются;
channeled ability без возможности interrupt;
специальные scripted abilities.

13. Ability Queue

Ability Queue позволяет игроку поставить следующую способность в очередь во время каста или GCD.

13.1. Базовое правило

Игрок может поставить в очередь одну следующую способность.

Если следующая способность поставлена в очередь:

сервер запоминает запрос;
как только текущий cast или GCD завершается, следующая способность автоматически применяется, если условия выполнены.

13.2. Queue Window

Queue принимает запрос только в пределах Queue Window.

Current data-driven default:

QueueWindow = 0.5 seconds до завершения cast или GCD.

Запросы вне Queue Window отклоняются как преждевременные.

13.3. Queue Invalidation

Очередь инвалидируется если:

кастер умирает;
текущий cast прерывается;
поставленная в очередь способность стала недоступна (недостаточно ресурса, цель стала невалидной).

Если очередь инвалидирована, запрос отклоняется без применения.

13.4. Одна способность в очереди

В очереди может быть только одна способность.

Если игрок ставит новую способность в очередь, она заменяет предыдущую.

14. Rage-специфичное поведение

Rage Archetype имеет особое взаимодействие со способностями.

14.1. Rage и использование способностей

Rage расходуется при использовании способности.

Проверка Rage происходит в момент использования согласно правилам Resource System.

14.2. Rage и Taunt

Taunt способности могут иметь Rage стоимость.

Если Rage недостаточно, Taunt не применяется.

14.3. Rage генерация от способностей

Некоторые способности могут генерировать Rage.

Пример:

Battle Shout
Instant
Generates 20 Rage.
No resource cost.

Генерация Rage является частью эффекта способности.

15. AFK Farming Interaction

AFK Farming по умолчанию не использует Ability System.

AFK Farming является passive bonus mode и не создаёт реальных UseAbility запросов.

Следовательно:

cooldowns не расходуются во время AFK;
ресурс не расходуется во время AFK;
GCD не активируется во время AFK;
casts не происходят во время AFK.

16. Offline Combat Interaction

Если персонаж участвует в offline combat:

сервер может применять способности согласно offline combat rules;
Ability System применяет те же правила проверки условий;
ресурс расходуется согласно Resource System;
cooldowns обрабатываются согласно Server Time.

Конкретные правила offline combat определяются Combat System.

17. Persistence

Cooldown состояния должны быть сохраняемыми.

Для персонажа сохраняются:

CooldownEndsAt для каждой способности с активным cooldown;
GCDEndsAt;
Lockout состояния;
ActiveCast, если каст активен в момент сохранения.

Persistence не должен зависеть от клиента.

18. Restart Recovery

После server restart:

истёкшие cooldowns считаются завершёнными;
активные cooldowns проверяются по Server Time;
GCD проверяется по Server Time;
активный каст:
  если ResolvesAt уже прошёл → каст завершается или отменяется согласно политике;
  если ResolvesAt не прошёл → каст может быть продолжен или отменён;
  конкретная политика восстановления активного каста определяется отдельно.

Базовое правило Базовое правило:

Активный каст на момент restart отменяется без применения эффекта и без cooldown.

Ресурс не возвращается.

19. Events

Ability System эмитит события:

AbilityUseRequested
AbilityUseValidated
AbilityUseRejected
CastStarted
CastCompleted
CastInterrupted
CastCancelled
AbilityCooldownStarted
AbilityCooldownEnded
GCDStarted
GCDEnded
AbilityInterruptApplied
SpellSchoolLockoutApplied
SpellSchoolLockoutExpired
NextAttackModifierApplied
NextAttackModifierConsumed

19.1. Event Delivery Rules

Combat System и Resource System могут получать все ability события.

Quest System по умолчанию получает только:

CastCompleted;
AbilityInterruptApplied;
другие события, если objective явно требует tracking.

Analytics и debug tools могут получать расширенный лог.

20. Interaction с другими системами

20.1. Combat System

Combat System использует Ability System для:

обработки UseAbility запросов в контексте боя;
управления Auto Attack и cast взаимодействием;
определения боевого состояния персонажа.

Ability System не определяет логику боя.

20.2. Damage and Healing System

Ability System создаёт DamageRequest или HealingRequest при разрешении эффекта способности.

Ability System передаёт:

SnapshotContext;
CanCrit, CanMiss, CanBeDodged, IgnoresArmor и другие флаги из AbilityDefinition;
AbilityId для аудита.

Damage and Healing System рассчитывает итоговый результат.

20.3. Effect System

Ability System применяет эффекты через Effect System.

Эффекты могут быть:

stat modifiers;
DoT / HoT;
control effects (Stun, Silence);
shields;
Next Attack Modifier effects;
resource modifiers.

Effect System отвечает за жизненный цикл эффекта.

Ability System лишь инициирует применение.

20.4. Resource System

Ability System проверяет и расходует ресурсы через Resource System.

Resource System возвращает результат проверки.

Ability System не хранит текущее значение ресурса.

20.5. Threat System

Ability System передаёт GeneratesThreat и ThreatMultiplier в Combat System.

Threat рассчитывается Combat System и Damage and Healing System.

Ability System не хранит Threat состояние.

21. Ability Invariants

INVARIANT-01
Сервер является источником истины для состояния способностей.

INVARIANT-02
Клиент не может подтверждать успех использования способности.

INVARIANT-03
Способность не может быть использована если условия не выполнены.

INVARIANT-04
Resource Cost проверяется сервером до разрешения способности.

INVARIANT-05
Ресурс по умолчанию не возвращается при прерывании каста.

INVARIANT-06
Cooldown хранится как абсолютная точка завершения CooldownEndsAt.

INVARIANT-07
GCD является отдельным таймером от индивидуального Cooldown.

INVARIANT-08
GCD не блокирует Auto Attack cycle.

INVARIANT-09
Instant Ability по умолчанию не прерывает Auto Attack cycle.

INVARIANT-10
Casted Ability блокирует Auto Attack на время каста.

INVARIANT-11
Snapshot для Casted Ability фиксируется в момент успешного начала каста.

INVARIANT-12
Snapshot для Instant Ability фиксируется в момент использования.

INVARIANT-13
Next Attack Modifier не фиксирует snapshot. Атака разрешается по текущим параметрам.

INVARIANT-14
Stun прерывает активный каст.

INVARIANT-15
Silence прерывает активный каст.

INVARIANT-16
Смерть кастера прерывает активный каст.

INVARIANT-17
GCD запускается в момент начала использования способности.

INVARIANT-18
Off-GCD способности не запускают и не блокируются GCD.

INVARIANT-19
В очереди способностей может быть только одна способность.

INVARIANT-20
Queue принимает запрос только в пределах QueueWindow до завершения cast или GCD.

INVARIANT-21
AFK Farming по умолчанию не создаёт UseAbility запросов.

INVARIANT-22
Lockout хранится как абсолютная точка завершения LockoutEndsAt.

INVARIANT-23
Cooldown, GCD и Lockout корректно восстанавливаются после server restart по Server Time.

INVARIANT-24
Прерванный каст по умолчанию не запускает полный Cooldown.

INVARIANT-25
Ability System не хранит квестовый прогресс.

22. Default Balance Values

GCDDuration = 1.5 seconds
QueueWindow = 0.5 seconds
Default ThreatMultiplier = 1.0
Default LockoutDuration = 3.0 seconds (определяется Interrupt способностью)

Значения являются текущими defaults и меняются versioned Balance Profile.

23. Out of Scope

Этот документ пока не определяет:

конкретные способности классов;
конкретные значения урона и лечения способностей;
конкретные Cast Time значения;
конкретные Cooldown значения;
конкретные Resource Cost значения;
конкретные Threat Multiplier значения для способностей;
Channeled Ability (не входит в core);
AoE с ground targeting (требует Position System);
Charge / Dash (требует Movement System);
новые Companion ability families сверх правил Companion System;
Mount abilities;
Crafting abilities;
Gathering abilities;
конкретные Spell Schools для core классов;
конкретный список Off-GCD способностей;
формулы Cooldown Reduction;
очередь из нескольких способностей;
Combo Point система;
proc-based ability triggers в деталях;
анимационные состояния;
UI;
визуализацию каста и cooldown.

---

# Source of Truth Revision v2

- Authoritative TargetTypes: SELF, SINGLE_ALLY, SINGLE_ENEMY, ALL_ENEMIES_IN_COMBAT, N_ENEMIES_IN_COMBAT, SELF_AND_PARTY_MEMBERS_IN_COMBAT, ACTIVE_COMPANION, OWNER.
- Companion commands являются обычными AbilityDefinition и проходят общий validation/resource/cooldown pipeline.
- Party-targeted ability не требует spatial Aura.
- Channeled Ability может быть добавлена позже отдельным AbilityType; существующие Casted abilities не симулируют channel скрыто.
