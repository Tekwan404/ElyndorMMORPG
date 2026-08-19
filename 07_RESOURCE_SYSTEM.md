Elyndor — Resource System Specification

Document: 07_RESOURCE_SYSTEM.md
System: Resource
Status: Foundation / Source of Truth
Version: 0.3

1. Назначение

Resource System определяет правила жизненно важных игровых ресурсов персонажа и других боевых сущностей.

В текущей версии система охватывает:

Health / здоровье;
Action Resource / ресурс для использования способностей.

Система определяет:

текущее и максимальное значение ресурса;
изменение ресурса;
восстановление ресурса;
расход ресурса;
ограничения;
связь с Combat System, Character System, Effects System и Time System;
сохранение и восстановление состояния ресурсов.

Resource System не определяет:

валюту;
лут;
инвентарь;
экономику;
предметы;
consumables в деталях;
durability;
AFK-награды;
классовые деревья;
конкретные формулы регенерации;
конкретные формулы урона;
конкретные формулы лечения.

2. Основной принцип

Все ресурсы являются серверными данными.

Клиент может отображать текущее значение ресурса, но не является источником истины.

Сервер определяет:

текущее значение ресурса;
максимальное значение ресурса;
расход;
восстановление;
момент, когда ресурс считается потраченным;
момент, когда ресурс считается восстановленным;
состояние недостатка ресурса.

3. Типы ресурсов

В базовой модели выделяются две категории ресурсов:

Health Resource
Ресурс жизни персонажа.

Action Resource
Ресурс, используемый для применения способностей.

Другие ресурсные системы, такие как валюта, материалы, очки гильдии или AFK-бонусы, не входят в Resource System.

4. Health Resource

Health Resource представляет здоровье персонажа.

Базовые поля:

CurrentHP
MaxHP

CurrentHP

Текущее здоровье персонажа.

Если CurrentHP достигает 0, персонаж переходит в состояние DEAD.

MaxHP

Максимальный запас здоровья персонажа.

MaxHP может зависеть от:

Stamina;
уровня;
экипировки;
эффектов;
других разрешённых источников.

Конкретная формула MaxHP определяется отдельно.

5. Правила Health

CurrentHP не может быть меньше 0.

CurrentHP не может превышать MaxHP.

Если CurrentHP становится больше MaxHP, значение должно быть ограничено до MaxHP.

Если MaxHP уменьшается и CurrentHP превышает новое значение MaxHP, CurrentHP должен быть ограничен новым MaxHP.

Если MaxHP увеличивается, поведение CurrentHP по умолчанию:

CurrentHP сохраняется как есть;
автоматическое лечение не происходит, если источник изменения явно не указывает другое.

6. Изменение Health

Health может изменяться в результате:

получения урона;
лечения;
эффектов;
DoT/HoT;
респауна;
скриптовых событий;
других разрешённых систем.

Все изменения Health должны быть серверно-авторитетными.

Клиент не может напрямую заявить:

я вылечился;
я получил урон;
у меня теперь столько HP.

7. Death from Health Depletion

Если CurrentHP достигает 0:

персонаж считается мёртвым;
Life State переходит в DEAD;
Activity State очищается;
AFK Farming останавливается;
бой с участием персонажа разрешается согласно Combat System;
запускается процесс респауна.

По умолчанию:

смерть не уничтожает экипировку;
смерть не уменьшает уровень;
смерть не отнимает опыт;
смерть не уничтожает предметы.

После респауна персонаж возвращается с неполными ресурсами согласно правилам раздела 22.

Конкретные значения восстановления после смерти определяются отдельно.

8. Action Resource

Action Resource — это ресурс, который персонаж использует для применения способностей.

Конкретный тип ресурса зависит от класса, билда или архетипа персонажа.

Базовые поля:

CurrentResource
MaxResource
ResourceType

9. Resource Archetypes

Action Resource может иметь несколько архетипов с разным поведением генерации, расхода и decay.

В текущей модели утверждены четыре Action Resource archetype:

### Mana Archetype

Базовый профиль:

```text
MaxMana = 100
StartingMana = 100
RespawnMana = 100
CombatRegen = 4 Mana / sec
OutOfCombatRegen = 12 Mana / sec
```

Базовое поведение:

StartValue = MaxResource
PassiveRegen = true
OutOfCombatDecay = false
CombatRegen = true
Generation: пассивная регенерация по времени

Пример использования:

магические классы;
лечащие классы;
классы, использующие заклинания.

### Rage Archetype

Базовый профиль:

```text
MaxRage = 100
StartingRage = 0
RespawnRage = 0
AutoAttackHitGeneration = 10 Rage
DirectDamageTakenGeneration = 5 Rage
OutOfCombatDecay = 5 Rage / sec after OutOfCombatDelay
```

DoT tick по умолчанию не генерирует Rage от получения урона, если ability/effect явно не говорит обратное.

Базовое поведение:

StartValue = 0 (персонаж начинает без ресурса)
PassiveRegen = false (не регенерирует пассивно)
OutOfCombatDecay = true (спадает вне боя)
CombatRegen = false (не регенерирует пассивно в бою)
Generation: активная генерация от боевых действий

Генерация Rage может происходить от:

auto attack hit → +10 Rage по базовому profile
direct damage taken → +5 Rage по базовому profile
specific abilities → varies by ability
specific talents → varies by talent

Decay вне боя:

после OutOfCombatDelay Rage начинает спадать;
скорость decay по текущему profile = -5 Rage per second после OutOfCombatDelay.

Пример использования:

воин;
берсерк;
другие классы, основанные на ярости.

### Focus Archetype

Focus используется Лучником до перехода в Тайного стрелка.

Базовый профиль:

```text
MaxFocus = 100
StartingFocus = 100
RespawnFocus = 100
CombatRegen = 8 Focus / sec
OutOfCombatRegen = 12 Focus / sec
```

Focus:
- пассивно восстанавливается в бою;
- восстанавливается быстрее вне боя;
- не требует получения урона, как Rage;
- не является копией Energy: базовая скорость ниже и сильнее взаимодействует с выстрелами/талантами.

Talent `Тайны Магии` может заменить Action Resource персонажа `FOCUS → MANA` через валидируемый class/talent combat profile override.

### Energy Archetype

Базовый профиль будущего Rogue:

```text
MaxEnergy = 100
StartingEnergy = 100
RespawnEnergy = 100
CombatRegen = 10 Energy / sec
OutOfCombatRegen = 10 Energy / sec
```

Базовое поведение:

StartValue = MaxResource (персонаж начинает с полным ресурсом)
PassiveRegen = true (регенерирует быстро)
OutOfCombatDecay = false (не спадает вне боя)
CombatRegen = true (регенерирует в бою с той же скоростью)
Generation: быстрая пассивная регенерация

Пример использования:

разбойник;
монах;
другие классы, использующие быстрые действия.

Другие архетипы могут быть добавлены позднее, если это потребуется для конкретных классов.

10. CurrentResource

CurrentResource — текущее количество доступного ресурса.

CurrentResource используется при проверке возможности применить способность.

Если CurrentResource меньше требуемой стоимости:

способность не может быть использована;
сервер должен отклонить действие;
клиент может показать причину отказа, если это предусмотрено UI.

11. MaxResource

MaxResource — максимальное количество ресурса.

MaxResource может зависеть от:

класса;
уровня;
основных характеристик;
экипировки;
эффектов;
других разрешённых источников.

Конкретные формулы определяются отдельно.

12. Правила Action Resource

CurrentResource не может быть меньше 0.

CurrentResource не может превышать MaxResource.

Если CurrentResource становится больше MaxResource, значение должно быть ограничено до MaxResource.

Если MaxResource уменьшается и CurrentResource превышает новое значение MaxResource, CurrentResource должен быть ограничен новым MaxResource.

Если MaxResource увеличивается, поведение CurrentResource по умолчанию:

CurrentResource сохраняется как есть;
автоматическое восстановление не происходит, если источник изменения явно не указывает другое.

13. Resource Cost

Способности могут иметь стоимость.

Resource Cost проверяется сервером.

Проверка происходит в момент, когда сервер принимает действие.

Для instant-способностей:

проверка и списание происходят в момент принятия действия.

Для casted-способностей:

стоимость проверяется в момент начала каста;
ресурс списывается или резервируется в момент начала каста, если способность не определяет другое.

Поведение при прерывании каста:

Для Mana Archetype:
потраченный ресурс по умолчанию не возвращается.

Для Rage Archetype:
потраченный ресурс по умолчанию не возвращается.
Однако конкретные способности могут явно указывать refund при прерывании, если это требуется для баланса.

Для Focus Archetype:
потраченный ресурс по умолчанию не возвращается.

Для Energy Archetype:
потраченный ресурс по умолчанию не возвращается.

Если способность явно поддерживает refund при прерывании, это указывается в самой способности.

14. Resource Generation

Action Resource может восстанавливаться или генерироваться несколькими способами.

Возможные источники зависят от Resource Archetype:

### Mana Generation

пассивная регенерация по времени;
эффекты;
способности;
скриптовые события;
мировые условия.

### Rage Generation

auto attack hit;
taking damage;
specific abilities;
specific talents;
скриптовые события.

### Focus Generation

Focus имеет базовую временную регенерацию согласно Focus profile.

Способности и таланты могут:
- восстановить Focus;
- увеличить/уменьшить стоимость;
- временно изменить regeneration rate.

### Energy Generation

быстрая пассивная регенерация по времени;
эффекты;
способности;
скриптовые события.

Конкретные формулы генерации определяются отдельно.

15. Resource Regeneration

Регенерация ресурса должна использовать Server Time.

Регенерация может быть:

непрерывной;
tick-based;
событийной;
зависящей от состояния персонажа.

### Out-of-Combat Detection

Combat System определяет, находится ли персонаж в бою.

Для целей Resource System используется:

OutOfCombatDelay = 5 seconds

Если с момента последнего боевого события прошло больше OutOfCombatDelay, персонаж считается out-of-combat.

Последнее боевое событие может включать:

получение урона;
нанесение урона;
использование боевой способности;
участие в Combat Session.

### Rage Decay Rate

Для Rage Archetype после OutOfCombatDelay:

Rage Decay Rate = -5 Rage per second

Значение является current data-driven default и может меняться versioned Balance Profile.

### Регенерация в зависимости от состояния

Для Mana Archetype:

IN_COMBAT: регенерация может быть замедлена или отключена
OUT_OF_COMBAT: полная регенерация

Для Rage Archetype:

IN_COMBAT: нет пассивной регенерации, только активная генерация
OUT_OF_COMBAT: decay после OutOfCombatDelay с скоростью Rage Decay Rate

Для Focus Archetype:

IN_COMBAT: 8 Focus / sec
OUT_OF_COMBAT: 12 Focus / sec

Для Energy Archetype:

IN_COMBAT: регенерация с той же скоростью
OUT_OF_COMBAT: регенерация с той же скоростью

Указанные значения являются current versioned defaults. Class/talent/effect content может модифицировать их через Resource System.

16. Health Regeneration

Health также может регенерировать.

Health Regeneration является системным параметром, а не отдельной утверждённой боевой характеристикой.

Он может зависеть от:

уровня;
Stamina;
состояния персонажа;
эффектов;
зоны;
времени вне боя.

### Out-of-Combat Health Regeneration

Health использует тот же OutOfCombatDelay:

OutOfCombatDelay = 5 seconds

Если персонаж out-of-combat:

Health может регенерировать.

Если персонаж in-combat:

Health регенерация может быть замедлена или отключена, если это не определено эффектом или способностью.

По умолчанию:

регенерация Health не должна автоматически лечить персонажа до полного значения в бою;
регенерация не должна отменять смерть;
регенерация не должна нарушать правила опасных территорий.

17. Spirit

Spirit не является активной характеристикой Elyndor.

Resource System:
- не читает Spirit;
- не использует Spirit в Mana/Focus regeneration;
- не создаёт Spirit modifiers.

Если когда-либо Spirit будет возвращён, это потребует отдельной revision Stats + Resource Systems.

18. Resource State Changes

Изменение ресурса должно быть атомарным и проверяемым.

Пример:

Ability requests use
  ↓
Check resource cost
  ↓
If sufficient:
    deduct resource
    allow ability
  ↓
If insufficient:
    reject ability

Нельзя сначала разрешить способность, а затем постфактум обнаружить недостаток ресурса.

19. Resource Snapshot

Для способностей может использоваться snapshot состояния ресурсов.

Для casted ability по умолчанию:

проверка стоимости происходит в момент начала каста;
состояние ресурса фиксируется или списывается в момент начала каста.

Для instant ability:

проверка и списание происходят в момент использования.

Если эффект изменяет стоимость способности, стоимость проверяется по правилам этого эффекта в момент применения.

20. Offline Behavior

Resource System должна корректно работать в offline-состоянии.

Если регенерация разрешена для offline-персонажа:

она продолжается по Server Time;
результат применяется при восстановлении состояния или при входе игрока.

Если персонаж находится в AFK Farming:

AFK Farming по умолчанию не расходует ресурсы;
AFK Farming по умолчанию не создаёт боевой расход ресурсов;
AFK Farming не использует Action Resource как часть своей бонусной модели.

Если персонаж участвует в реальном offline combat:

расход ресурсов следует обычным Ability/Resource rules для действий, которые разрешил Offline Combat Controller;
offline status сам по себе не создаёт отдельную формулу расхода.

21. AFK Farming Interaction

AFK Farming является пассивным бонусным режимом.

По умолчанию AFK Farming:

не расходует CurrentResource;
не расходует CurrentHP;
не ломает экипировку;
не использует consumables;
не приводит к смерти;
не изменяет ресурсы персонажа напрямую.

AFK-награды не являются частью Resource System.

Если AFK Farming выдаёт опыт, валюту, предметы или другие бонусы, это обрабатывается AFK Farming, Loot/Reward и Economy системами.

22. Resources After Death

После смерти и респауна ресурсы персонажа восстанавливаются согласно правилам Resource Archetype.

### Health After Death

CurrentHP после респауна:

CurrentHP = 50% of MaxHP

Конкретное значение может зависеть от:

зоны респауна;
типа смерти;
эффектов;
других систем.

### Mana After Death

CurrentResource для Mana Archetype после респауна:

CurrentResource = MaxResource (полная Мана)

Логика:

персонаж возвращается в безопасную зону;
Мана полностью восстанавливается;
персонаж готов к использованию способностей.

### Rage After Death

CurrentResource для Rage Archetype после респауна:

CurrentResource = 0 (нулевая Ярость)

Логика:

персонаж возвращается в безопасную зону;
Ярость полностью теряется;
персонаж должен снова накопить Ярость в бою.

### Focus After Death

После respawn:

```text
CurrentFocus = 100
```

если другой explicit respawn profile не определён.

### Energy After Death

CurrentResource для Energy Archetype после респауна:

CurrentResource = MaxResource (полная Energy)

Логика:

персонаж возвращается в безопасную зону;
Energy полностью восстанавливается;
персонаж готов к использованию способностей.

Конкретные значения могут быть изменены для баланса или специальных механик.

23. Combat Interaction

Combat System использует Resource System для:

проверки стоимости способностей;
списания ресурса;
восстановления ресурса;
нанесения урона по Health;
лечения;
смерти персонажа;
определения out-of-combat состояния для регенерации и decay.

Combat System эмитит события:

InCombatEntered — персонаж вошёл в бой
OutOfCombatEntered — персонаж вышел из боя (после OutOfCombatDelay)

Resource System подписывается на эти события и меняет поведение регенерации и decay согласно Resource Archetype.

Combat System не должна напрямую менять MaxHP или MaxResource без разрешённого источника.

Пример:

Ability uses 30 Mana
  ↓
Resource System checks CurrentResource
  ↓
If enough:
    deduct 30 Mana
    ability proceeds
  ↓
If not enough:
    ability rejected

Combat System определяет последнее боевое событие для OutOfCombatDelay и эмитит соответствующие события.

24. World Interaction

World System может предоставлять контекст, влияющий на ресурсы.

Примеры:

Safe Territory может разрешать ускоренное восстановление;
Dangerous Territory может ограничивать восстановление;
конкретная зона может иметь resource modifier;
мировое событие может влиять на регенерацию.

Однако World System не должна напрямую хранить CurrentHP или CurrentResource.

Она предоставляет контекст, а Resource System применяет изменения.

25. Character System Interaction

Character System использует Resource System для определения:

жив ли персонаж;
находится ли персонаж в состоянии смерти;
может ли персонаж выполнять действия;
какие ресурсы доступны после респауна.

Если CurrentHP = 0:

Character System переводит персонажа в DEAD.

Если персонаж мёртв:

ресурсы не могут использоваться для активных действий;
регенерация может быть приостановлена или изменена;
AFK Farming останавливается;
Travel останавливается или становится недоступным;
Exploration останавливается.

26. Effects Interaction

Buffs, debuffs, DoT и HoT могут изменять ресурсы.

Примеры:

HoT восстанавливает Health;
DoT уменьшает Health;
buff увеличивает MaxResource;
debuff уменьшает регенерацию;
эффект сжигает Mana;
эффект возвращает ресурс при убийстве;
эффект увеличивает генерацию Rage.

Конкретные правила эффектов определяются Effects System.

Resource System предоставляет безопасные методы изменения ресурсов:

AddHealth
RemoveHealth
Heal
Damage
AddResource
RemoveResource
SetMaxHealth
SetMaxResource
ModifyRegeneration
SetOutOfCombatState

27. Persistence

Состояние ресурсов должно быть сохраняемым.

Для персонажа сохраняются:

CurrentHP;
MaxHP;
CurrentResource;
MaxResource;
ResourceType;
active regeneration state;
resource-related timers;
resource version;
last updated timestamp;
last combat event timestamp (для OutOfCombatDelay).

Persistence не должен зависеть от клиента.

28. Restart Recovery

После server restart ресурсы должны быть восстановлены.

Базовые правила:

CurrentHP не должен быть отрицательным;
CurrentResource не должен быть отрицательным;
регенерация проверяется по Server Time;
OutOfCombatDelay проверяется по Server Time;
просроченные эффекты не применяются;
active timed effects обрабатываются согласно их оставшейся длительности;
если персонаж умер до рестарта, смерть не должна теряться;
если смерть не была зафиксирована до crash, персонаж может быть восстановлен в последнем безопасном состоянии.

29. Events

Resource System может предоставлять события об изменениях ресурсов.

Примеры событий Resource System:

HealthChanged
HealthDepleted
HealthRegenerated
ResourceChanged
ResourceDepleted
ResourceRegenerated
MaxHealthChanged
MaxResourceChanged

События должны быть серверно-авторитетными.

Quest System и другие системы должны получать только релевантные события.

По умолчанию Resource System не должна отправлять каждое малое изменение HP или ресурса во внешние системы.

### События боевого состояния

Следующие события эмитит Combat System, а не Resource System:

InCombatEntered — персонаж вошёл в бой
OutOfCombatEntered — персонаж вышел из боя (после OutOfCombatDelay)

Resource System подписывается на эти события и изменяет поведение регенерации и decay согласно Resource Archetype.

Это разграничение ответственности важно:
- Combat System определяет боевое состояние
- Resource System реагирует на боевое состояние

30. Resource Invariants

INVARIANT-01
Сервер является источником истины для ресурсов.

INVARIANT-02
Клиент не может напрямую изменять CurrentHP, MaxHP, CurrentResource или MaxResource.

INVARIANT-03
CurrentHP не может быть меньше 0.

INVARIANT-04
CurrentResource не может быть меньше 0.

INVARIANT-05
CurrentHP не может превышать MaxHP.

INVARIANT-06
CurrentResource не может превышать MaxResource.

INVARIANT-07
Если CurrentHP достигает 0, персонаж переходит в состояние DEAD.

INVARIANT-08
Способность не может быть использована, если Action Resource недостаточно.

INVARIANT-09
Resource Cost проверяется сервером до разрешения действия.

INVARIANT-10
AFK Farming по умолчанию не расходует ресурсы персонажа.

INVARIANT-11
AFK Farming по умолчанию не приводит к смерти персонажа.

INVARIANT-12
Регенерация ресурсов использует Server Time.

INVARIANT-13
Resource System не хранит валюту, предметы, loot или AFK-награды.

INVARIANT-14
Resource System не определяет экономику.

INVARIANT-15
Смерть по умолчанию не уничтожает экипировку, уровень, опыт или предметы.

INVARIANT-16
После респауна персонаж возвращается с ресурсами согласно правилам Resource Archetype.

INVARIANT-17
Spirit не является активной характеристикой и не используется системой.

INVARIANT-18
OutOfCombatDelay определяет переход между in-combat и out-of-combat состояниями.

INVARIANT-19
Rage Archetype начинает с 0 и спадает вне боя.

INVARIANT-20
Mana Archetype начинает с MaxResource и не спадает вне боя.

INVARIANT-21
Energy Archetype начинает с MaxResource и регенерирует быстро.

INVARIANT-22
По умолчанию прерывание каста не возвращает потраченный ресурс независимо от архетипа. Refund является исключением и указывается явно в правилах конкретной способности.

31. Out of Scope

Этот документ пока не определяет:

точные формулы MaxHP;
точные формулы MaxResource;
точные формулы регенерации;
точные значения Rage generation от боевых действий;
специальные class/content overrides базового Rage decay;
точные значения восстановления после смерти для специальных случаев;
future additional Action Resource archetypes;
конкретные способности;
consumables;
potion cooldowns;
durability;
валюту;
инвентарь;
экономику;
loot;
AFK reward formulas;
UI;
визуализацию полосок HP и ресурса;
PvP-specific resource rules.

---

# Source of Truth Revision v2

- Authoritative Action Resource set: `MANA`, `RAGE`, `ENERGY`, `FOCUS`.
- Mana: Max/Start/Respawn 100, CombatRegen 4/sec, OutOfCombatRegen 12/sec.
- Rage: Max 100, Start/Respawn 0, AA hit +10, direct damage taken +5, OOC decay 5/sec.
- Energy: Max/Start/Respawn 100, regen 10/sec (future Rogue profile).
- Focus: Max/Start/Respawn 100, CombatRegen 8/sec, OutOfCombatRegen 12/sec.
- Spirit не используется ресурсной системой.
- Talent-derived profile override может заменить Focus на Mana без изменения ClassId.


## Talent Loadout Resource Conversion

Если Talent Loadout меняет Action Resource archetype без изменения ClassId (текущий пример: Archer `FOCUS ↔ MANA`), переключение не должно бесплатно восстанавливать ресурс.

```text
oldRatio = OldCurrentResource / OldMaxResource
NewCurrentResource = round(NewMaxResource * oldRatio)
NewCurrentResource = clamp(NewCurrentResource, 0, NewMaxResource)
```

Cooldowns и resource-related InternalCooldowns при переключении не сбрасываются.

Для `RAGE` это правило не используется как способ конвертации другого класса: ResourceArchetype override разрешён только explicit talent/class profile.
