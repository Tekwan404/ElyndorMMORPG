Elyndor — Character System Specification

Document: docs/source-of-truth/gameplay/05_CHARACTER_SYSTEM.md
System: Character / Presence
Status: Foundation / Source of Truth
Version: 0.1

1. Назначение

Character System определяет персонажа как постоянную игровую сущность.

Система описывает:

существование персонажа;
принадлежность персонажа игроку;
состояние персонажа в мире;
базовые переходы между состояниями;
связь персонажа с World, Combat, AFK Farming, Time и Quest системами;
базовые правила смерти, респауна, logout и offline-состояния.

Character System не определяет:

конкретные формулы урона;
формулы опыта;
лут-таблицы;
экономику;
AI монстров;
классовую систему в деталях;
полный список характеристик;
баланс способностей;
UI.

2. Основной принцип

Персонаж в Elyndor является постоянной сущностью мира.

Игрок может выйти из игры, но персонаж не удаляется из мира автоматически.

Player Logout ≠ Character Removal

Персонаж продолжает существовать в своём текущем мировом состоянии, пока другая система явно не изменит его.

Сервер является единственным авторитетным источником состояния персонажа.

Клиент может отправлять намерения:

войти в игру;
выйти из игры;
начать бой;
использовать способность;
начать AFK Farming;
начать путешествие;
взаимодействовать с объектом;
остановить активность.

Но итоговое состояние персонажа определяет сервер.

3. Character Entity

Character — это игровая сущность, представляющая конкретного персонажа игрока.

Каждый персонаж имеет уникальный идентификатор и набор сохраняемых данных.

Базовая структура:

Character
  ├── Identity
  ├── State
  ├── Location
  ├── Attributes
  ├── Progression
  ├── Equipment
  ├── Inventory
  └── Relationships

Конкретная техническая структура хранения будет определена позднее.

4. Identity

Каждый персонаж должен иметь устойчивую идентичность.

Базовые поля:

CharacterId
AccountId
CharacterName
RaceId
Gender
ClassId
CreatedAt
LastActiveAt
CharacterVersion

CharacterId является основным идентификатором персонажа для всех игровых систем.

CharacterName может изменяться по правилам будущей системы имён, но CharacterId остаётся неизменным.

5. Character Ownership

Персонаж принадлежит аккаунту игрока.

Ownership определяет право игрока управлять персонажем.

При этом:

один аккаунт может иметь одного или нескольких персонажей, если это будет разрешено будущей системой;
персонаж не является собственностью другого игрока;
персонаж не может быть удалён другим игроком;
передача персонажа другому аккаунту не является базовой механикой.

Конкретные правила мультиперсонажей пока не фиксируются.

6. Character State Model

Состояние персонажа не должно быть одним простым enum.

Рекомендуется разделять состояние персонажа на несколько независимых осей:

Connection State
World Presence State
Activity State
Life State

Это позволяет корректно обрабатывать ситуации вида:

игрок offline, но персонаж находится в мире;
игрок offline, но персонаж находится в бою;
персонаж мёртв, независимо от online/offline;
персонаж находится в AFK Farming;
персонаж путешествует;
персонаж находится в инстансе.

7. Connection State

Connection State описывает только подключение игрока.

Возможные состояния:

CONNECTED
Игрок подключён к серверу и может управлять персонажем.

DISCONNECTED
Игрок не подключён. Персонаж продолжает существовать в мире.

RECONNECTING
Игрок временно потерял соединение, но сессия может быть восстановлена.

Connection State не определяет, жив ли персонаж, где он находится и чем занят.

8. World Presence State

World Presence State описывает, где персонаж находится с точки зрения мира.

Возможные состояния:

PRESENT_IN_WORLD
Персонаж находится в обычной мировой локации.

IN_INSTANCE
Персонаж находится в инстансе.

TRAVELING
Персонаж перемещается между локациями.

TECHNICAL_TRANSITION
Техническое состояние миграции, загрузки или восстановления.

По умолчанию персонаж находится в мире.

TECHNICAL_TRANSITION не является нормальным игровым состоянием и должен быть кратковременным.

9. Activity State

Activity State описывает, чем персонаж занят в данный момент.

Возможные состояния:

IDLE
Персонаж присутствует в локации, но не выполняет специальную активность.

EXPLORING
Персонаж активно исследует локацию.

IN_COMBAT
Персонаж участвует в бою.

AFK_FARMING
Персонаж находится в пассивном бонусном режиме AFK Farming.

INTERACTING
Персонаж взаимодействует с NPC, объектом мира или другой активностью.

Activity State применяется только для живого персонажа.

Если персонаж мёртв, Activity State должен быть очищен или переведён в неактивное состояние.

10. Life State

Life State описывает базовое состояние жизни персонажа.

Возможные состояния:

ALIVE
Персонаж жив и может действовать.

DEAD
Персонаж мёртв.

RESPAWNING
Персонаж находится в процессе возрождения.

Life State имеет наивысший приоритет.

Если персонаж мёртв:

он не может быть в AFK Farming;
он не может быть в активном бою;
он не может исследовать локацию;
он не может путешествовать;
он не может выполнять квестовые действия;
он не может быть целью обычных hostile encounters до респауна.

11. Primary Character State

Для внешних систем может вычисляться производное состояние персонажа.

Пример:

PrimaryCharacterState

Возможные значения:

DEAD
RESPAWNING
IN_INSTANCE
IN_COMBAT
AFK_FARMING
TRAVELING
EXPLORING
INTERACTING
ONLINE_PRESENT
OFFLINE_PRESENT

Приоритет определения:

DEAD / RESPAWNING
→ IN_INSTANCE
→ IN_COMBAT
→ AFK_FARMING
→ TRAVELING
→ EXPLORING / INTERACTING
→ ONLINE_PRESENT / OFFLINE_PRESENT

Primary Character State может быть производным значением и не обязан быть единственным хранимым полем.

12. Character Lifecycle

Базовый жизненный цикл персонажа:

Character Created
  ↓
Character Enters World
  ↓
Normal World State
  ↓
Possible Activities
  ↓
Possible Death
  ↓
Respawn
  ↓
Normal World State

Удаление персонажа не является базовым игровым процессом.

Правила удаления, очистки или архивации персонажей определяются отдельно.

13. Login

Когда игрок входит в игру:

Connection State становится CONNECTED.

Если персонаж жив и находится в мире:

игрок получает управление персонажем;
персонаж продолжает существовать в текущем состоянии;
персонаж не создаётся заново;
персонаж не телепортируется автоматически в безопасную зону, если это не предусмотрено отдельной механикой.

Если персонаж мёртв:

игрок может увидеть состояние смерти;
персонаж продолжает процесс респауна по серверным правилам;
конкретный UX смерти определяется отдельно.

Если персонаж находится в AFK Farming:

вход игрока может остановить AFK Farming или перевести его в активный режим, если это определено AFK System.

По умолчанию:

Player login does not automatically delete AFK state unless AFK System decides so.

Но для упрощения первой реализации можно считать:

Player login stops AFK Farming
  ↓
Character returns to normal presence

Конкретное поведение должно быть согласовано с AFK System.

14. Logout

Когда игрок выходит из игры:

Connection State становится DISCONNECTED.

Персонаж не удаляется.

Последствия logout зависят от текущего состояния персонажа и правил World System.

Базовые правила:

Logout в Safe Territory:

Character remains safe
No ordinary hostile encounter

Logout в Adventure Territory:

Character remains in location
Ordinary offline presence rules apply
Possible encounter according to World System

Logout в Dangerous Territory:

Character remains in location
Risk remains
Possible encounter according to World System

Logout во время боя:

Combat does not cancel
Character remains in combat
Control transfers to offline combat resolution rules

Logout во время AFK Farming:

AFK Farming may continue
Character remains in AFK mode
Ordinary encounters are suppressed by AFK rules

Logout во время Travel:

Travel may continue according to Time System
Character remains in TRAVELING state
Arrival occurs when travel timer completes

Logout во время EXPLORING:

EXPLORING как активная онлайн-активность прекращается
Character becomes ordinary offline presence
World encounter rules may apply

15. Location

Персонаж всегда имеет текущее местоположение.

Базовые поля:

CurrentLocationId
PreviousLocationId
LocationEnteredAt
SubLocationId, optional

CurrentLocationId определяет, в какой мировой локации находится персонаж.

Конкретные правила локации определяются World System.

Character System хранит ссылку на локацию, но не определяет:

spawn rates;
threat level;
territory type;
NPC aggression;
encounter frequency;
loot;
resource nodes.

16. Travel

Персонаж может перемещаться между локациями.

Travel является отдельным состоянием.

Travel Start
  ↓
Character leaves source location
  ↓
TRAVELING state
  ↓
Travel time passes
  ↓
Character arrives at destination location
  ↓
PRESENT_IN_WORLD state

Во время Travel:

персонаж не находится в обычной локации в полном смысле;
персонаж не должен запускать AFK Farming;
персонаж не должен начинать обычное исследование;
персонаж может быть ограничен в действиях, если это будет определено Travel System.

AFK Farming не перемещает персонажа.

Если начинается Travel, AFK Farming должен быть остановлен.

17. Exploration

Персонаж может исследовать текущую локацию.

Exploration является активной формой присутствия.

Базовая модель:

Character present in location
  ↓
Player explores
  ↓
Server evaluates encounters
  ↓
Possible combat

Exploration может быть прекращена:

игроком;
началом боя;
началом Travel;
началом AFK Farming;
logout;
смертью;
другим системным событием.

Exploration не должен быть бесконечным источником боёв.

Encounter generation во время Exploration определяется World System.

18. Combat Relationship

Character System не разрешает бой, но предоставляет состояние персонажа для Combat System.

Персонаж может участвовать в бою, если:

персонаж жив;
персонаж находится в подходящем состоянии;
Combat System или World System создали боевое событие.

При входе в бой:

Activity State становится IN_COMBAT.

При выходе из боя:

если персонаж жив:
  Activity State возвращается в IDLE или другое разрешённое состояние;
если персонаж мёртв:
  Life State становится DEAD.

Если игрок disconnect во время боя:

Character remains IN_COMBAT.

Бой не должен автоматически отменяться из-за logout.

Если игрок offline во время боя:

персонаж продолжает бой под управлением серверных offline combat rules;
конкретные правила offline-боя определяются Combat System.

19. AFK Farming Relationship

AFK Farming является отдельным пассивным режимом.

Character System позволяет персонажу находиться в состоянии:

AFK_FARMING

Но конкретные правила AFK Farming определяются AFK Farming System.

Базовые ограничения:

AFK Farming может быть начат только если:

персонаж жив;
персонаж находится в Safe Territory или Adventure Territory;
конкретная локация разрешает AFK Farming;
персонаж не находится в бою;
персонаж не находится в состоянии смерти;
персонаж не находится в Travel;
персонаж не находится в Instanced Territory, если инстанс явно не разрешает AFK.

AFK Farming по умолчанию:

не расходует ресурсы персонажа;
не ломает экипировку;
не приводит к смерти;
не запускает обычные world encounters;
не перемещает персонажа;
не заменяет активную игру.

Если начинается реальный бой, AFK Farming должен быть остановлен.

Если начинается Travel, AFK Farming должен быть остановлен.

Если персонаж умирает по любой разрешённой причине, AFK Farming должен быть остановлен.

20. Death

Если HP персонажа достигает нуля, персонаж переходит в состояние DEAD.

Death является авторитетным серверным событием.

Death может произойти в результате:

реального боя;
offline combat;
скриптового события;
другой разрешённой системы.

AFK Farming по умолчанию не приводит к смерти.

При смерти:

Life State становится DEAD;
Activity State очищается;
персонаж перестаёт быть участником активных игровых действий;
персонаж не может быть целью обычных hostile encounters;
запускается процесс респауна.

Базовые последствия смерти:

экипировка не уничтожается;
уровень не теряется;
опыт не теряется;
предметы не теряются;
персонаж возвращается в город или точку респауна с неполными ресурсами.

Конкретные штрафы, длительность респауна и UX смерти определяются отдельно.

21. Respawn

После смерти персонаж проходит процесс респауна.

DEAD
  ↓
Respawn timer
  ↓
RESPAWNING
  ↓
ALIVE at respawn point

Respawn point определяется отдельно.

Базовый вариант:

город;
безопасная зона;
bind location, если система будет добавлена.

После респауна:

Life State становится ALIVE;
World Presence State становится PRESENT_IN_WORLD;
Activity State становится IDLE;
ресурсы персонажа восстанавливаются частично или по правилам системы.

Respawn должен корректно работать даже если игрок offline.

Если игрок находится offline во время смерти:

сервер продолжает процесс респауна;
после завершения персонаж может быть уже жив;
игрок при входе получает отчёт о смерти, если это предусмотрено UI.

22. Character Attributes

Character System определяет существование атрибутов персонажа, но не фиксирует конкретные формулы.

Базовые категории:

Health
Primary Resource
Level
Experience
Base Stats
Equipment State
Inventory State
Progression State

Конкретный список атрибутов будет определён в Character Stats System.

23. Health

Health представляет текущую и максимальную жизнеспособность персонажа.

Базовые поля:

CurrentHP
MaxHP

Если CurrentHP достигает 0, персонаж умирает.

Формулы урона, регенерации, защиты и устойчивости определяются отдельно.

24. Primary Resource

Персонаж имеет один активный Action Resource для использования способностей плюс Health. Talent Loadout может заменить archetype Action Resource без изменения ClassId.

Примеры:

Mana;
Energy;
Rage;
Focus;
другой классовый ресурс.

Конкретный ресурс зависит от класса, билда или архетипа.

Resource cost проверяется Combat System или Ability System.

AFK Farming по умолчанию не расходует ресурсы.

25. Level и Experience

Персонаж имеет уровень и опыт.

Level и Experience являются сохраняемыми полями.

Базовое правило:

Смерть не уменьшает уровень.
Смерть не отнимает опыт.
Смерть не уничтожает предметы.

Формулы получения опыта и повышения уровня определяются Progression System.

26. Equipment

Персонаж может иметь экипировку.

Character System признаёт существование экипировки, но не определяет:

item stats;
durability formulas;
repair rules;
item rarity;
item sockets;
item degradation;
equipment bonuses.

Базовое правило на текущем этапе:

Смерть не уничтожает экипировку.
AFK Farming не ломает экипировку.

Если durability будет добавлена позже, она должна быть отдельной системой.

27. Inventory

Персонаж имеет инвентарь.

Character System признаёт существование инвентаря, но не определяет:

capacity formulas;
item stacking;
weight limits;
currency storage;
bank rules;
trade rules.

AFK Farming может быть остановлено или ограничено, если инвентарь полон.

Конкретное поведение определяется AFK Farming и Economy системами.

28. Persistence

Состояние персонажа должно быть сохраняемым.

Сервер должен уметь восстанавливать:

текущую локацию;
состояние жизни;
уровень и опыт;
ресурсы;
экипировку;
инвентарь;
активные таймеры;
состояние AFK;
состояние Travel;
состояние Instance, если применимо;
последние известные безопасные состояния.

Persistence не должен зависеть от клиента.

29. Restart Recovery

После server restart состояние персонажа должно быть восстановлено.

Базовые принципы:

время не останавливается;
таймеры продолжают учитываться;
cooldowns и respawn timers проверяются по Server Time;
AFK Farming может быть продолжен или корректно завершён;
Travel может быть продолжен или корректно завершён;
death state не должен теряться, если он был зафиксирован;
если смерть не была зафиксирована до crash, персонаж может быть восстановлен в последнем безопасном состоянии.

Если при restart Character был помечен как участник обычного ACTIVE CombatSession, применяется Combat Process Restart Policy: незавершённый бой закрывается как INTERRUPTED без reward, а Character восстанавливается в последнее persisted valid state. Boss/World Event использует более конкретную policy Boss System.

30. Character Events

Character System может предоставлять другим системам факты о состоянии персонажа.

Примеры событий:

CharacterCreated
CharacterLoggedIn
CharacterLoggedOut
CharacterLocationChanged
CharacterTravelStarted
CharacterTravelCompleted
CharacterExplorationStarted
CharacterExplorationStopped
CharacterCombatEntered
CharacterCombatLeft
CharacterAfkStarted
CharacterAfkStopped
CharacterDied
CharacterRespawned
CharacterStateChanged

Эти события могут использоваться Quest System, World System, Analytics и другими системами.

Character System не должна самостоятельно изменять прогресс квестов.

31. Quest Integration

Character System может предоставлять Quest System факты о персонаже.

Например:

CharacterEnteredLocation
CharacterReachedLevel
CharacterDied
CharacterRespawned
CharacterStartedAfk
CharacterStoppedAfk

Quest System самостоятельно решает, являются ли эти события целью задания.

Пример:

Character enters Old Mine
  ↓
Character System emits CharacterLocationChanged
  ↓
Quest System checks active objectives
  ↓
Relevant objective progresses

Character System не хранит квестовый прогресс.

32. Character Invariants

INVARIANT-01
Персонаж является постоянной серверной сущностью.

INVARIANT-02
Logout не удаляет персонажа из мира автоматически.

INVARIANT-03
Connection State и World Presence State являются разными понятиями.

INVARIANT-04
Life State имеет приоритет над Activity State.

INVARIANT-05
Мёртвый персонаж не может выполнять обычные игровые активности.

INVARIANT-06
Персонаж не может начать AFK Farming в состоянии смерти или боя.

INVARIANT-07
AFK Farming разрешён только в Safe Territory и Adventure Territory, если локация явно разрешает AFK.

INVARIANT-08
AFK Farming по умолчанию не расходует ресурсы персонажа.

INVARIANT-09
AFK Farming по умолчанию не приводит к смерти персонажа.

INVARIANT-10
AFK Farming не перемещает персонажа между локациями.

INVARIANT-11
Travel должен останавливать AFK Farming.

INVARIANT-12
Logout во время боя не отменяет бой автоматически.

INVARIANT-13
Смерть является серверно-авторитетным событием.

INVARIANT-14
Смерть по умолчанию не уничтожает экипировку, уровень, опыт или предметы.

INVARIANT-15
После смерти персонаж возвращается в точку респауна с неполными ресурсами.

INVARIANT-16
Respawn должен корректно обрабатываться даже если игрок offline.

INVARIANT-17
Состояние персонажа должно быть восстанавливаемо после server restart.

INVARIANT-18
Клиент не является источником истины для состояния персонажа.

33. Out of Scope

Этот документ пока не определяет:

классы;
talent trees;
конкретные характеристики;
формулы урона;
формулы регенерации;
формулы опыта;
лут-таблицы;
экономику;
PvP;
guilds;
housing;
mounts;
pets;
summons;
UI;
визуализацию;
конкретную базу данных;
конкретную серверную архитектуру;
правила удаления персонажа;
правила переноса персонажа между серверами.

---

# Source of Truth Revision v2

- Character Identity включает `CharacterName`, `RaceId`, `Gender`, `ClassId`.
- Race и Gender не изменяют Stats, abilities, equipment power или progression.
- Companion является отдельной owned entity и не встраивается в Character identity.
- Party membership хранится Party System, а не Character System.
