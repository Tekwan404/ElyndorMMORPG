Elyndor — Combat System Specification

Document: docs/source-of-truth/gameplay/02_COMBAT_SYSTEM.md
System: Combat
Status: Foundation / Source of Truth
Version: 0.5

1. Назначение

Combat System определяет правила боевого взаимодействия между персонажами, NPC и другими боевыми сущностями.

Бой в Elyndor является real-time системой.

Игрок не управляет перемещением персонажа непосредственно во время боя.

Основная модель:

Player
  ↓
Target Selection
  ↓
Combat
  ├── Auto Attack
  ├── Manual Abilities
  ├── Reinforcements
  └── Threat / Aggro

Combat System не определяет конкретные формулы урона, AI монстров, loot tables, экономику или баланс наград.

2. Основная концепция

Бой не является пошаговым.

Во время боя время продолжает идти непрерывно.

Параллельно могут происходить:

Auto Attack;
Cast;
Cooldown;
Buff;
Debuff;
DoT;
HoT;
дополнительные атаки;
присоединение новых противников;
смерть участника;
завершение боя;
изменение threat;
выбор цели мобами.

3. Отсутствие ручного перемещения

Игрок не управляет перемещением персонажа внутри конкретного боя.

Бой не использует:

перемещение;
позицию;
дистанцию;
facing;
knockback;
dash;
dodge roll;
pathfinding.

Игрок управляет:

выбором цели;
способностями;
предметами;
боевыми решениями;
попыткой покинуть бой, если это разрешено механикой.

4. Target

Бой может начинаться в результате:

действия игрока;
нападения NPC/монстра;
присоединения другого участника;
другого игрового события.

Участник может иметь выбранную цель.

Выбор цели не означает, что остальные участники перестают существовать.

Игрок может вручную выбирать цель.

Враждебные NPC и мобы выбирают цель через Threat System.

5. Auto Attack

Auto Attack является постоянным боевым циклом.

После начала боя и при наличии допустимой цели персонаж автоматически выполняет базовые атаки.

Пример:

Attack
  ↓
Wait
  ↓
Attack
  ↓
Wait
  ↓
Attack

6. Weapon Base Attack Speed

Базовую скорость автоатаки определяет используемое оружие.

У персонажа нет единого фиксированного базового интервала независимо от оружия.

Пример концепции:

Dagger          → fast base interval
One-hand sword  → medium base interval
Bow             → medium/slow base interval
Two-hand weapon → slow base interval

Конкретные числовые значения будут определены позднее.

Финальный интервал атаки формируется из:

Weapon Base Attack Interval
+/- Attack Speed Modifiers

Модификаторами могут быть:

характеристики;
buffs;
debuffs;
talents;
equipment effects;
временные боевые эффекты.

7. Attack Interval

Auto Attack использует временной интервал.

Если атака произошла в:

12:00:00.350

и финальный интервал равен 2 секундам, следующая базовая атака планируется на:

12:00:02.350

Auto Attack не обязан быть синхронизирован с глобальными секундами.

8. Attack State

Базовая атака рассматривается как отдельное действие.

Упрощённо:

Ready
  ↓
Attack Started
  ↓
Attack Resolved
  ↓
Next Attack Scheduled

Конкретные стадии wind-up/recovery могут быть добавлены позднее.

9. Abilities

Abilities являются отдельными боевыми действиями.

Они могут иметь разные типы поведения.

Базово выделяются:

Casted Ability;
Instant Ability;
Next Attack Modifier;
Taunt Ability;
другие типы, которые будут добавляться при проектировании Ability System.

10. Casted Ability

Способность может иметь Cast Time.

Пример:

Fireball
Cast Time = 2 sec

Во время настоящего каста персонаж считается занятым этим действием.

Базовое правило:

Casted Ability блокирует выполнение обычной Auto Attack на время каста.

Если Auto Attack становится готовой во время каста, она ожидает завершения каста.

Пример:

0.0 — Fireball cast starts
0.5 — Auto Attack becomes ready
2.0 — Fireball resolves
2.0+ — Auto Attack cycle may continue

Точная политика переноса уже готовой автоатаки будет уточняться отдельно.

11. Instant Ability

Instant Ability не требует длительного каста.

Она может быть применена между автоатаками и по умолчанию не останавливает боевой цикл Auto Attack.

Пример:

0.0 — Auto Attack
0.7 — Instant Ability used
1.4 — next Auto Attack

Если конкретная instant-способность должна вмешиваться в Auto Attack, это указывается в самой способности.

12. Next Attack Modifier

Некоторые способности не наносят урон непосредственно.

Они изменяют следующую подходящую базовую атаку.

Упрощённая модель:

Player uses ability
  ↓
NEXT_ATTACK effect applied
  ↓
Auto Attack occurs
  ↓
Effect modifies attack
  ↓
Effect consumed

Пример разбойника:

Poisoned Blade
Instant
Next attack:
- deals bonus damage
- applies Poison

Пример лучника:

Aimed Shot Modifier
Instant or very short activation
Next ranged attack:
- bonus physical damage
- additional armor interaction

Такие способности позволяют игроку использовать обычную автоатаку как основу активного боевого решения.

13. Auto Attack и abilities

Базовое правило:

Auto Attack работает независимо от instant-способностей, если способность явно не взаимодействует с Auto Attack.

При этом настоящий Cast временно блокирует выполнение Auto Attack.

Next Attack Modifier, наоборот, специально существует для взаимодействия со следующей Auto Attack.

14. Combat Snapshot

Параметры casted ability по умолчанию фиксируются в момент успешного начала каста.

Пример:

12:00:00 — Spell Power = 500, Fireball starts
12:00:01 — +200 Spell Power buff applied
12:00:02 — Fireball resolves

Fireball использует snapshot от 500 Spell Power.

Следующая способность использует уже актуальное состояние.

Instant Ability делает snapshot в момент использования.

Next Attack Modifier не обязан snapshot'ить будущую атаку: сама атака разрешается в момент её фактического выполнения.

15. Attack Speed Changes

Изменение Attack Speed не пересчитывает уже разрешённую атаку задним числом.

Оно влияет на будущие циклы Auto Attack.

Точная политика изменения уже запланированной, но ещё не разрешённой атаки будет определена при проектировании Combat Stats.

16. Cooldown

После использования способность может переходить в Cooldown.

Cooldown является временным состоянием способности.

Ability Used
  ↓
Cooldown Starts
  ↓
Ability unavailable
  ↓
Cooldown Ends
  ↓
Ability available

17. Resource Cost

Способность может потреблять ресурсы.

Например:

Mana;
Energy;
Rage;
Focus;
другие классовые ресурсы.

Стоимость проверяется сервером согласно правилам способности.

18. Buffs и Debuffs

Buff изменяет состояние сущности на определённый период.

Debuff работает аналогично, но оказывает негативное влияние.

Примеры:

Attack Speed Increase;
Stun;
Silence;
Poison;
Damage Reduction;
Attack Speed Reduction.

Конкретный список и правила эффектов будут определены в Effects System.

19. DoT / HoT

Damage over Time и Healing over Time являются временными эффектами.

Пример:

Poison
Duration = 10 sec
Tick = every 2 sec

Каждый Tick является отдельным разрешением эффекта.

Snapshot/dynamic rules для DoT/HoT определяются Effects System.

20. Multiple Combatants

Бой не ограничивается моделью Player vs Enemy.

В одном бою могут участвовать несколько сущностей.

Новый противник может присоединиться к уже происходящему бою.

Игрок может менять выбранную цель, при этом остальные участники продолжают существовать в Combat State.

Каждый враждебный моб выбирает цель независимо через Threat System.

21. Combat Start

Бой может начаться как:

Player Initiated
Игрок инициирует боевое действие.

Enemy Initiated
Монстр или другой противник нападает первым.

Combat Join
Дополнительная сущность присоединяется к существующему бою.

22. Reinforcements

Во время активного боя к нему могут присоединяться дополнительные противники.

Reinforcement — это серверно-контролируемое появление дополнительного участника боя.

Reinforcements могут происходить:

по правилам локации;
по правилам encounter;
по правилам конкретной боевой сцены;
по случайному шансу;
по скриптовому событию.

Базовые принципы:

Reinforcement не определяется клиентом.
Reinforcement не должен создавать бесконечный поток врагов.
Reinforcement должен иметь лимиты.

Возможные лимиты:

max adds per combat;
max total participants;
reinforcement cooldown;
max reinforcement count per time window;
запрет reinforcements для отдельных encounter.

Reinforcement по умолчанию присоединяется к текущему бою, а не создаёт новый Combat Session.

Каждый reinforcement моб имеет собственную threat table.

При присоединении reinforcement моба:

его threat table изначально пуста, если не определено иное;
он может получить начальный Threat через Presence Threat, если это разрешено его правилами;
он выбирает цель по общим Threat rules.

Конкретные reinforcement chances, лимиты и условия будут определены отдельно.

23. Offline Combat

Offline не делает персонажа невидимым для мира.

Если персонаж оставлен в опасной локации, на него может произойти нападение в соответствии с правилами World System и поведением NPC.

Отсутствие игрока само по себе не останавливает уже начавшийся бой.

После завершения одного обычного offline-боя персонаж не превращается автоматически в бесконечного фарм-бота.

Если он выжил, он возвращается в состояние присутствия/ожидания в локации.

Offline-персонажи могут:

генерировать Threat;
быть целями мобов;
участвовать в threat table;
получать урон;
умирать.

Offline status не даёт иммунитет к Threat.

### Offline Combat Controller

Если игрок теряет соединение или выходит во время реального CombatSession, сервер не превращает персонажа в полноценного бота с идеальной ротацией.

Базовый offline controller:

```text
Auto Attack = enabled
Passive Effects = enabled
Companion AI = enabled, если companion ACTIVE
Manual Class Abilities = disabled by default
Consumables = disabled
Talent active abilities = disabled by default
```

Отдельная ability может иметь `AllowOfflineAutoUse = true`, но это является явным content rule, а не default.

Это предотвращает exploit `logout → идеальная автоматическая ротация`, но позволяет персонажу защищаться базовыми атаками, если бой уже начался или на него напали offline.

После окончания такого боя новый бесконечный combat chain автоматически не запускается.


24. Death

Если HP персонажа достигает нуля, персонаж считается погибшим.

На базовом этапе:

экипировка не уничтожается;
уровень не теряется;
опыт не теряется;
предметы не теряются.

После смерти персонаж возвращается в город с неполными ресурсами.

Возвращение обратно в опасную локацию требует времени и определяется World/Travel System.

Конкретные правила смерти и респауна определяются Character System и Resource System.

25. Combat End

Бой заканчивается, когда:

одна из сторон погибла;
бой прекращён разрешённым действием;
произошёл другой предусмотренный системой результат.

Автоматическое начало следующего боя после победы не является базовым правилом обычного присутствия в локации.

26. Ordinary Presence vs AFK Farming

Обычное нахождение персонажа в локации не является AFK Farming.

Ordinary Presence

Character remains in location
  ↓
Possible world encounter
  ↓
Combat
  ↓
Character survives
  ↓
Character waits again

AFK Farming

Player explicitly starts AFK Farming
  ↓
Character remains in allowed Safe/Adventure location
  ↓
Passive bonus result over time
  ↓
No full combat simulation by default

AFK Farming не создаёт CombatSession по умолчанию.

AFK Farming не генерирует Threat по умолчанию.

AFK Farming не заставляет мобов выбирать AFK-персонажа целью.

Если внешняя система явно создаёт бой для персонажа, применяются обычные Combat rules.

27. Server Authority

Сервер является источником истины для:

HP;
damage;
resource;
cooldown;
cast;
buffs;
debuffs;
target;
combat state;
death;
combat result;
reinforcement spawn;
threat table;
aggro target selection.

Клиент сообщает намерение. Сервер определяет результат.

28. Combat Results for External Systems

Combat System должна предоставлять другим системам подтверждённые сервером факты о результате боя.

Например:

EnemyKilled
EnemyType
EnemyId
Location
Participants
CombatResult
ReinforcementJoined
ThreatChanged
AggroTargetChanged

Quest System может использовать такие факты для проверки прогресса задания.

Combat System при этом не должна сама определять, относится ли убийство или другой боевой результат к конкретному квесту, и не должна напрямую увеличивать квестовый прогресс.

Пример:

Combat System
    ↓
EnemyKilled
    ↓
Quest System
    ↓
Check active objectives
    ↓
Update quest progress if conditions match

29. Threat / Aggro System

Threat System определяет, кого атакует враждебный NPC или моб в бою.

Threat не является характеристикой персонажа из Attributes and Stats System.

Threat является боевым состоянием, которое хранится сервером для каждого враждебного NPC/моба.

29.1. Базовое правило

Для каждого враждебного NPC/моба ведётся отдельная threat table.

Каждый участник боя, который может быть целью этого моба, имеет ThreatValue.

Моб атакует валидную цель с наибольшим ThreatValue.

Базовая модель:

Hostile Mob
  ↓
Threat Table
  ├── Target A — Threat 1200
  ├── Target B — Threat 800
  └── Target C — Threat 350
  ↓
Mob attacks Target A

29.2. Threat Table

Для каждого враждебного NPC/моба сервер хранит:

ThreatTable[mobId]
  ├── TargetId
  ├── ThreatValue
  ├── LastThreatChangeAt
  ├── ForcedTargetUntil, optional
  └── ThreatModifiers, optional

ThreatValue не может быть меньше нуля.

Threat Table является серверным состоянием.

Клиент не может напрямую изменять ThreatValue.

29.3. Участники и цели

Участником боя может быть:

персонаж игрока;
союзный NPC;
враждебный NPC;
враждебный моб;
другая боевая сущность, если она добавлена.

Для каждого враждебного моба threat table содержит цели из противоположной стороны.

Каждый враждебный моб имеет собственную threat table.

Threat не является общим для всех мобов в бою.

Пример:

Mob A имеет собственную threat table.
Mob B имеет собственную threat table.
Mob C имеет собственную threat table.

Каждый моб может атаковать разные цели.

29.4. Источники Threat

Threat может расти от следующих источников:

Damage;
Healing;
Taunt abilities;
Presence / proximity-like behavior, если это разрешено правилами моба.

29.5. Threat от урона

Нанесение урона увеличивает Threat для атакующего.

Базовое правило:

Threat += Effective Damage × Damage Threat Multiplier

Default:

Damage Threat Multiplier = 1.0

Пример:

Воин нанёс 150 урона.
Threat воина к этому мобу увеличивается на 150.

DoT effects:

каждый DoT tick генерирует Threat в момент нанесения эффективного урона;
Threat генерируется по тем же правилам, если эффект не указывает другое.

Если урон не был нанесён, например:

полный иммунитет;
полное поглощение;
miss, если miss реализован;
уклонение, если dodge реализован;

то Threat по умолчанию не генерируется.

Конкретные правила могут быть переопределены способностью, эффектом или мобом.

29.6. Threat от лечения

Лечение союзников увеличивает Threat для лекаря.

Базовое правило:

Threat += Effective Healing × Healing Threat Multiplier

Default:

Healing Threat Multiplier = 1.0

Overhealing не генерирует Threat.

Пример:

Жрец вылечил союзника на 200 HP.
Threat жреца увеличивается на 200.

Распределение healing threat в core:

Healing threat применяется ко всем враждебным мобам, которые находятся в том же CombatSession и участвуют в бою.

Party System является владельцем membership. CombatSession хранит party/team context участников и использует его для targeting, healing threat и reward eligibility.

Если лечение происходит вне боя, оно по умолчанию не генерирует Threat.

HoT effects:

каждый HoT tick генерирует Threat в момент эффективного лечения;
overhealing tick не генерирует Threat.

29.7. Threat от Taunt

Taunt-способности являются явным источником Threat.

Taunt может:

добавлять фиксированное количество Threat;
устанавливать Threat на уровень текущего лидера плюс бонус;
принудительно назначать цель на время действия эффекта.

Базовая core-модель:

Taunt adds flat Threat.
Taunt may apply ForcedTarget effect.

Пример:

Taunt
+500 Threat
ForcedTarget duration = 3 seconds

Если ForcedTarget активен:

моб обязан атаковать цель Taunt, если цель валидна;
даже если другая цель имеет больший Threat.

Если ForcedTarget цель умирает или становится невалидной:

ForcedTarget прекращается;
моб выбирает цель по обычным Threat rules.

Если моб имеет иммунитет к Taunt:

ForcedTarget не применяется;
добавление Threat может быть уменьшено или отключено согласно правилам моба.

29.8. Presence Threat

Некоторые мобы могут генерировать Threat просто от присутствия цели рядом.

В текущей модели нет физического перемещения и дистанции.

Поэтому "нахождение рядом" означает не координаты, а серверный факт:

цель находится в той же локации/encounter;
цель участвует в бою;
цель находится в состоянии, которое моб распознаёт как угрозу;
цель не скрыта и не находится в недоступном состоянии.

Presence Threat может быть:

однократным при входе в бой;
периодическим;
зависящим от типа моба;
зависящим от Territory Type;
зависящим от мирового события.

Пример:

Territorial Wolf
Presence Threat = +10 per second to all valid targets engaged with it

Presence Threat не должен требовать:

координат;
pathfinding;
movement speed;
distance calculation.

Если конкретному мобу Presence Threat не нужен, он просто не использует этот источник.

29.9. Target Selection

Моб выбирает цель по следующему порядку:

1. Если активен ForcedTarget и цель валидна:
   моб атакует ForcedTarget.

2. Иначе моб выбирает валидную цель с наибольшим ThreatValue.

3. Если несколько целей имеют одинаковый ThreatValue:
   текущая цель сохраняет приоритет;
   если текущей цели нет, выбирается цель, получившая Threat позже.

Валидная цель должна:

быть живой;
быть доступной для атаки;
не находиться в состоянии immunity к атакам моба;
находиться в том же боевом контексте;
соответствовать правилам моба.

Если текущая цель становится невалидной:

она удаляется из threat table или помечается невалидной;
моб выбирает следующую цель с наибольшим Threat.

29.10. Re-evaluation

Базовый обязательный триггер Базовое правило:

Re-evaluation происходит в момент планирования следующей Auto Attack моба.

Когда моб готовится выполнить следующую Auto Attack:

1. Сервер проверяет threat table.
2. Если ForcedTarget активен и валиден, моб атакует ForcedTarget.
3. Иначе моб выбирает валидную цель с наибольшим ThreatValue.
4. Если текущая цель невалидна, выбирается следующая цель с наибольшим Threat.

Это означает, что моб не обязан менять цель мгновенно при каждом изменении Threat.

Смена цели происходит в момент, когда моб планирует следующую атаку.

Если моб ещё не начал атаковать, первичный выбор цели происходит в момент, когда моб должен запланировать первую атаку.

Дополнительные триггеры могут быть добавлены позднее, но не являются обязательными Базовое правило:

применение Taunt;
истечение ForcedTarget;
scripted events;
специальные boss mechanics.

29.11. Threat Decay

По умолчанию Threat не уменьшается со временем.

Threat может быть уменьшен только если это явно определено:

способностью;
эффектом;
правилом моба;
скриптом;
мировым событием.

Примеры будущих механик:

Feign Death;
Vanish;
Threat Reduction;
Threat Reset.

В текущей версии такие механики не фиксируются.

29.12. Threat Reset

Threat table очищается когда:

моб умирает;
бой завершается;
все цели становятся невалидными;
происходит явный scripted reset;
моб возвращается в idle state, если это определено AI rules.

Threat не сохраняется между разными боями по умолчанию.

29.13. Multiple Combatants

Если в бою несколько мобов:

каждый моб выбирает цель независимо;
Threat table каждого моба независима;
один персонаж может быть целью одного моба;
другой персонаж может быть целью другого моба.

Пример:

Mob A attacks Warrior — highest threat.
Mob B attacks Priest — highest threat.
Mob C attacks Warrior — highest threat.

29.14. Player Target Selection

Threat System определяет цели для враждебных NPC/мобов.

Игрок по-прежнему может вручную выбирать цель, если это разрешено боевым интерфейсом.

Threat не ограничивает выбор цели игроком.

29.15. Offline Characters

Offline-персонажи могут генерировать Threat и быть целями мобов.

Если персонаж находится в offline combat:

его действия, разрешённые offline combat controller, генерируют Threat;
мобы могут выбирать его целью;
Threat table продолжает работать серверно.

Offline status не даёт иммунитет к Threat.

### Offline Combat Controller

Если игрок теряет соединение или выходит во время реального CombatSession, сервер не превращает персонажа в полноценного бота с идеальной ротацией.

Базовый offline controller:

```text
Auto Attack = enabled
Passive Effects = enabled
Companion AI = enabled, если companion ACTIVE
Manual Class Abilities = disabled by default
Consumables = disabled
Talent active abilities = disabled by default
```

Отдельная ability может иметь `AllowOfflineAutoUse = true`, но это является явным content rule, а не default.

Это предотвращает exploit `logout → идеальная автоматическая ротация`, но позволяет персонажу защищаться базовыми атаками, если бой уже начался или на него напали offline.

После окончания такого боя новый бесконечный combat chain автоматически не запускается.


29.16. AFK Farming

AFK Farming по умолчанию не создаёт CombatSession.

Следовательно:

AFK Farming не генерирует Threat;
AFK Farming не создаёт threat tables;
AFK Farming не заставляет мобов выбирать AFK-персонажа целью.

Если будущий риск-AFK режим будет добавлен, его взаимодействие с Threat должно быть описано отдельно.

29.17. Threat Modifiers

Способности, эффекты, экипировка и таланты могут изменять генерацию Threat.

Примеры:

+20% Threat generation;
-30% Threat generation;
Taunt effectiveness +50%;
Healing threat reduced by 15%;
Next ability generates no threat.

Такие модификаторы применяются сервером в момент генерации Threat.

По умолчанию все источники используют multiplier = 1.0.

### Role-based Threat Multipliers

Для того чтобы Threat System работала корректно, разные роли должны иметь разные базовые множители генерации Threat.

Текущие default balance values:

Tank role:
  Damage Threat Multiplier = 1.5

DPS role:
  Damage Threat Multiplier = 1.0

Healer role:
  Healing Threat Multiplier = 0.5

Эти значения являются текущими authoritative defaults и меняются только через versioned Balance Profile.

Role может определяться через:

Class RoleProfile;
ветку талантов, например Страж;
явный Effect/Talent modifier;
экипировку только если конкретный item content явно меняет Threat.

Для core используется упрощённая role model.

Если роль не определена, используются дефолтные множители:

Damage Threat Multiplier = 1.0
Healing Threat Multiplier = 1.0

### Пример

Tank наносит 100 урона:
Threat = 100 × 1.5 = 150

DPS наносит 100 урона:
Threat = 100 × 1.0 = 100

Healer лечит 100 HP:
Threat = 100 × 0.5 = 50

Таким образом танк генерирует больше Threat от того же урона, а лекарь генерирует меньше Threat от того же лечения.

29.18. Threat Events

Combat System может эмитить события:

ThreatChanged
ThreatCleared
AggroTargetChanged
ForcedTargetApplied
ForcedTargetExpired

Эти события могут использоваться:

Combat System;
AI System;
UI, если threat display будет добавлен;
Analytics;
debug tools.

Quest System по умолчанию не должен получать каждый ThreatChanged event.

29.19. Threat Invariants

INVARIANT-THREAT-01
Каждый враждебный NPC/моб имеет собственную threat table.

INVARIANT-THREAT-02
Каждый потенциальный участник боя может иметь ThreatValue для конкретного враждебного моба.

INVARIANT-THREAT-03
Моб атакует валидную цель с наибольшим Threat, если нет активного ForcedTarget.

INVARIANT-THREAT-04
ForcedTarget имеет приоритет над обычным Threat, пока активен и валиден.

INVARIANT-THREAT-05
Threat является серверным состоянием.

INVARIANT-THREAT-06
Клиент не может напрямую изменять Threat.

INVARIANT-THREAT-07
Threat генерируется только серверно-подтверждёнными действиями.

INVARIANT-THREAT-08
Нанесение эффективного урона увеличивает Threat атакующего.

INVARIANT-THREAT-09
Эффективное лечение увеличивает Threat лекаря.

INVARIANT-THREAT-10
Overhealing не генерирует Threat.

INVARIANT-THREAT-11
Taunt может добавлять Threat и/или применять ForcedTarget.

INVARIANT-THREAT-12
Presence Threat не использует координаты, дистанцию или movement.

INVARIANT-THREAT-13
Presence Threat применяется только если моб явно поддерживает такое поведение.

INVARIANT-THREAT-14
Каждый моб выбирает цель независимо.

INVARIANT-THREAT-15
Threat не сохраняется между боями по умолчанию.

INVARIANT-THREAT-16
AFK Farming по умолчанию не генерирует Threat.

INVARIANT-THREAT-17
Offline-персонажи могут генерировать Threat и быть целями мобов.

INVARIANT-THREAT-18
Threat System не требует Movement System.

INVARIANT-THREAT-19
Re-evaluation цели моба происходит в момент планирования следующей Auto Attack моба.

INVARIANT-THREAT-20
Role-based Threat Multipliers применяются к генерации Threat.

INVARIANT-THREAT-21
Tank role генерирует больше Threat от урона чем DPS role по умолчанию.

INVARIANT-THREAT-22
Healer role генерирует меньше Threat от лечения чем другие роли по умолчанию.

INVARIANT-THREAT-23
Reinforcement моб имеет собственную threat table.

INVARIANT-THREAT-24
Reinforcement моб присоединяется к текущему бою, а не создаёт новый Combat Session.

30. Combat Invariants

INVARIANT-01
Combat является real-time.

INVARIANT-02
Базовую скорость Auto Attack задаёт оружие.

INVARIANT-03
Игрок не управляет перемещением персонажа внутри боя.

INVARIANT-04
Instant Ability по умолчанию не останавливает Auto Attack cycle.

INVARIANT-05
Casted Ability по умолчанию блокирует выполнение Auto Attack на время каста.

INVARIANT-06
Next Attack Modifier изменяет следующую подходящую Auto Attack и затем расходуется согласно своим правилам.

INVARIANT-07
Casted Ability по умолчанию snapshot'ит основные параметры в момент успешного начала каста.

INVARIANT-08
Несколько противников могут одновременно участвовать в одном бою.

INVARIANT-09
Обычное offline-пребывание не превращает персонажа в автоматический фарм.

INVARIANT-10
Сервер является авторитетным источником боевого состояния.

INVARIANT-11
Бой не использует перемещение, позицию, дистанцию, facing или knockback.

INVARIANT-12
Reinforcements являются серверно-контролируемыми и должны иметь лимиты.

INVARIANT-13
Reinforcement не должен создавать бесконечный поток врагов.

INVARIANT-14
Threat System определяет цель для враждебных NPC/мобов.

INVARIANT-15
Игрок может вручную выбирать цель независимо от Threat.

INVARIANT-16
AFK Farming по умолчанию не создаёт CombatSession.

31. Out of Scope

Этот документ пока не определяет:

формулы урона;
точные характеристики персонажа;
классы;
конкретные способности;
Global Cooldown;
очередь способностей;
точную политику переноса Auto Attack после Cast;
экипировку;
броню;
сопротивления;
критический удар;
уклонение;
парирование;
AI;
PvP balance;
Movement System;
перемещение персонажа внутри боя;
позиционирование;
дистанцию;
facing;
pathfinding;
knockback;
dash;
dodge roll;
position-based abilities;
конкретные reinforcement chances;
конкретные reinforcement limits;
конкретные reinforcement cooldowns;
конкретные role-based Threat Multiplier значения;
конкретный механизм определения роли;
pet threat rules, если pets будут добавлены;
stealth threat rules, если stealth будет добавлен;
threat decay formulas, если не определены отдельно;
threat UI, если threat display будет добавлен;
агро-радиус в физическом смысле.

---

# Source of Truth Revision v2

- PartyId и OwnerCharacterId/CompanionId могут входить в CombatParticipantContext; Party и Companion не создают отдельный Combat Engine.
- PartyEffect targeting использует `SELF_AND_PARTY_MEMBERS_IN_COMBAT`; случайный участник того же encounter не считается Party Ally.
- Companion является полноценным Combat participant и использует те же Damage/Effect/Ability/Threat правила.
- Перед переходом CurrentHP в 0 Combat вызывает универсальный Lethal Damage Prevention check из Damage/Effect pipeline.
- Proc-created attacks по умолчанию имеют `CanTriggerFromProc = false`, если content явно не разрешает иное.


## Combat Process Restart Policy

Обычный активный `CombatSession` не реконструируется посередине после process/server restart.

```text
ACTIVE normal CombatSession
→ process/server restart
→ INTERRUPTED
→ no XP / no Loot / no Kill reward
→ participants restore last persisted valid Character/Resource state
→ CombatSession closes
```

Правило специально fail-safe: restart не должен превращать незавершённый бой в победу или смерть задним числом.

Boss/World Event использует более конкретную policy из `18_BOSS_AND_WORLD_EVENT_SYSTEM`, которая также завершает активный encounter через FAILED без награды.

Logout/disconnect без server restart по-прежнему **не** прерывает бой: работает Offline Combat Controller.
