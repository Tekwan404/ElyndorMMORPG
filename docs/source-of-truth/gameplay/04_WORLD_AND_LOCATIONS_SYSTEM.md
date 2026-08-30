Elyndor — World & Locations System Specification

Document: docs/source-of-truth/gameplay/04_WORLD_AND_LOCATIONS_SYSTEM.md
System: World / Locations
Status: Foundation / Source of Truth
Version: 0.3

1. Назначение

World & Locations System определяет правила существования персонажа в игровом мире, типы территорий, уровень угрозы, базовые последствия нахождения online/offline, исследование локаций и взаимодействие мира с Combat и AFK Farming.

World System не определяет конкретные формулы боя, AI отдельных монстров, loot tables или баланс наград.

2. Основной принцип

Elyndor является постоянным миром.

Logout не является автоматическим телепортом персонажа в город или удалением персонажа из мира.

Персонаж остаётся в своём игровом состоянии, пока другая система не изменит его.

Следовательно:

Player Offline ≠ Character Removed From World

3. Location

Location — это игровая область, в которой находится персонаж и для которой действуют определённые правила мира.

Локация может определять:

тип территории;
уровень угрозы;
доступных NPC;
доступных монстров;
ресурсы;
мировые события;
возможность случайных или инициированных encounter;
правила исследования;
правила reinforcements;
правила offline presence;
возможность AFK Farming;
требования входа;
рекомендуемый уровень;
другие свойства мира.

4. Territory Type и Threat Level

Тип территории и уровень угрозы являются разными понятиями.

Territory Type

Определяет правила поведения мира.

Threat Level

Определяет относительную сложность и опасность конкретной зоны.

Пример:

Pine Forest
Territory Type: Adventure
Threat: I
Recommended Level: 5–10

и:

Cursed Thicket
Territory Type: Dangerous
Threat: IV
Recommended Level: 45–50

5. Safe Territory

Безопасная территория предназначена для социального, экономического и сервисного взаимодействия.

Примеры:

столица;
деревня;
торговый пост;
гильдейский город;
дом игрока.

Базовые свойства:

обычные hostile encounters отсутствуют;
персонаж может безопасно оставаться offline;
AFK Farming может быть разрешён, если локация явно позволяет;
доступны NPC и сервисы;
могут быть доступны торговля, банк, аукцион, крафт, восстановление и социальные активности.

Logout в Safe Territory безопасен по умолчанию.

AFK Farming в Safe Territory:

разрешён только если Location.allowAfkBonus = true;
является пассивным бонусным режимом;
не создаёт обычные hostile encounters;
не приводит к смерти;
не расходует ресурсы.

6. Adventure Territory

Adventure Territory — обычная приключенческая зона мира.

Примеры:

лес;
побережье;
старые шахты;
заброшенные поля;
обычные руины.

В такой зоне:

существуют монстры;
игрок может исследовать локацию;
игрок может самостоятельно выбирать цели;
часть монстров может быть passive или neutral;
часть монстров может быть aggressive;
возможны world encounters;
во время боя возможны reinforcements;
offline-персонаж остаётся в зоне;
нападение во время offline возможно согласно поведению NPC и правилам encounter.

Обычная Adventure Territory не обязана постоянно атаковать персонажа.

AFK Farming в Adventure Territory:

разрешён только если Location.allowAfkBonus = true;
является пассивным бонусным режимом;
не создаёт обычные hostile encounters;
не приводит к смерти;
не расходует ресурсы;
не заменяет активное исследование и бой.

7. Dangerous Territory

Dangerous Territory — зона повышенного риска.

Примеры:

проклятый лес;
заражённые болота;
земли орков;
глубокие шахты;
территории сильных хищников.

В такой зоне могут быть:

больше агрессивных монстров;
более сильные противники;
группы мобов;
elite enemies;
редкие ресурсы;
более ценный loot;
повышенный опыт;
более высокая вероятность непредвиденного encounter;
более опасные offline последствия.

Основной принцип:

Risk ↑
Reward ↑

Игрок, покидающий игру в такой зоне, принимает риск того, что персонаж останется уязвимым для мира.

AFK Farming в Dangerous Territory по умолчанию запрещён.

Dangerous Territory сохраняет риск для:

активного присутствия;
обычного offline presence;
world encounters;
боевых событий.

8. Extreme / Endgame Territory

Extreme или Endgame Territory — высокоуровневая территория с максимально опасными world rules.

Возможные элементы:

elite packs;
roaming bosses;
world bosses;
редкие мировые события;
специальные ресурсы;
уникальный loot;
высокая плотность hostile encounters;
особые environmental rules;
PvP, если конкретная зона позже будет его поддерживать.

AFK Farming в Extreme / Endgame Territory по умолчанию запрещён.

Конкретные правила будут определяться содержимым конкретной зоны.

9. Instanced Territory

Отдельный тип пространства:

Dungeon;
Raid;
Arena;
Scenario;
другие инстансы.

Для Instanced Territory могут действовать собственные правила disconnect/offline.

Dungeon System (`docs/source-of-truth/gameplay/28_DUNGEON_SYSTEM.md`) определяет:

оставить персонажа внутри;
удалить его после timeout;
позволить группе продолжить;
завершить участие персонажа;
применить другой сценарий.

AFK Farming в Instanced Territory по умолчанию запрещён.

Инстанс может явно разрешить AFK Farming, но это не является базовым правилом мира.

10. Threat Level

Threat Level отображает относительную сложность пребывания в зоне.

Базовая шкала может использовать уровни:

Threat I
Threat II
Threat III
Threat IV
Threat V

Конкретное количество градаций пока не является финальным.

Threat Level может учитывать:

силу противников;
плотность hostile NPC;
вероятность encounter;
наличие elite enemies;
environmental hazards;
recommended character level;
другие факторы.

Threat Level не заменяет Territory Type.

11. NPC Aggression Model

Локация сама по себе не обязана напрямую атаковать персонажа.

Поведение определяется конкретными NPC/Monster rules.

Базовые категории поведения могут включать:

Passive
Не атакует первым.

Defensive
Атакует только при определённых условиях.

Aggressive
Может самостоятельно инициировать encounter.

Predatory
Активно ищет подходящую цель в рамках своих правил.

Territorial
Атакует при вторжении в определённую область или при взаимодействии с охраняемыми объектами.

Точные AI-правила будут определены отдельно.

12. Character Presence

Персонаж может находиться в локации независимо от того, подключён игрок или нет.

Возможные состояния:

ONLINE + PRESENT
ONLINE + EXPLORING
OFFLINE + PRESENT
AFK_FARMING
IN_COMBAT
DEAD / RESPAWNING
IN_INSTANCE
TRAVELING
OTHER WORLD STATE

Online Status и Character World State не являются одним и тем же значением.

AFK_FARMING является отдельным пассивным режимом и не эквивалентен обычному исследованию или ожиданию в локации.

13. Location State and Travel

Персонаж имеет текущую локацию.

CharacterLocation = current LocationId

Перемещение между локациями изменяет текущую локацию персонажа.

Travel может быть:

мгновенным;
с таймером;
с требованиями;
с возможными рисками, если это будет определено позже.

AFK Farming не перемещает персонажа между локациями.

Если начинается travel, AFK Farming должен быть остановлен.

14. Exploration

Находясь в локации, персонаж может исследовать её.

Exploration является активным или пассивным состоянием присутствия, при котором World System может создавать encounters.

Базовая модель:

Character present in location
  ↓
Exploration / movement inside location
  ↓
Encounter roll by server
  ↓
If success:
  encounter starts
  ↓
Combat

Exploration не требует полноценной физики или ручного перемещения, если это не будет отдельно добавлено позже.

Encounter generation всегда является серверным решением.

Клиент не может напрямую заявить, что encounter должен появиться.

15. Encounter Generation

Локация может определять правила появления противников.

Encounter generation может зависеть от:

Territory Type;
Threat Level;
времени суток;
погоды, если она будет добавлена;
типа NPC;
aggression model;
активности персонажа;
исследования;
мирового события;
других правил локации.

Encounter generation не должен быть бесконечным.

Должны существовать ограничения, например:

minimum time between encounters;
maximum encounters per time window;
cooldown after combat;
encounter caps for offline presence;
location-specific limits.

Конкретные значения определяются отдельно.

16. Combat Reinforcements

Во время активного боя могут появляться дополнительные противники.

Базовая модель:

Combat active
  ↓
Reinforcement roll by server
  ↓
If success and limits allow:
  additional enemy joins combat

Reinforcements являются серверно-контролируемыми.

Reinforcements должны иметь лимиты:

max adds per combat;
max total participants;
reinforcement cooldown;
max reinforcement count per time window;
запрет reinforcements для отдельных encounter.

Reinforcement не должен создавать бесконечный бой.

17. Logout in Safe Territory

Player logs out
  ↓
Character remains in Safe Territory
  ↓
No hostile world encounter

Персонаж остаётся в безопасном состоянии согласно правилам зоны.

Если игрок запустил AFK Farming в разрешённой Safe Territory:

AFK Farming continues
  ↓
No ordinary hostile encounter
  ↓
Passive bonus result accrues

18. Logout in Adventure / Dangerous Territory

Player logs out
  ↓
Character remains in location
  ↓
World continues
  ↓
Possible encounter

Logout не предоставляет иммунитет от правил мира.

Если в зоне существует NPC, способный инициировать encounter, offline-персонаж потенциально может стать целью.

Исключение:

Если персонаж находится в AFK Farming в разрешённой Adventure Territory:

ordinary offline encounters по умолчанию не начинаются;
AFK Farming обрабатывается как пассивный бонусный режим.

В Dangerous Territory AFK Farming по умолчанию недоступен, поэтому logout в Dangerous Territory остаётся риском.

19. Offline Encounter

Обычное offline-пребывание не является бесконечным фармом.

Модель:

Character waits in location
  ↓
World encounter may occur
  ↓
Combat starts
  ↓
Combat resolves
  ↓
If character survives:
Character returns to waiting state

После победы не запускается автоматически бесконечная цепочка боёв только потому, что игрок offline.

Следующий encounter должен возникнуть по правилам мира.

Offline encounters должны иметь лимиты и не должны превращаться в автоматический фарм.

20. AFK Farming и World Presence

AFK Farming не равен обычному logout.

Ordinary Presence

Player leaves character in location
  ↓
Character waits or remains present
  ↓
World may create encounter

AFK Farming

Player explicitly selects AFK Farming
  ↓
Character enters passive bonus mode
  ↓
AFK System calculates limited bonus results
  ↓
No ordinary encounters by default

AFK Farming разрешён только в:

Safe Territory;
Adventure Territory.

И только если:

Location.allowAfkBonus = true

AFK Farming по умолчанию:

не создаёт ordinary encounters;
не создаёт Combat Session;
не спавнит реальных врагов;
не расходует ресурсы;
не ломает экипировку;
не приводит к смерти;
не перемещает персонажа;
не заменяет активный игровой процесс.

21. Player Responsibility

Опасность logout является частью игрового решения.

Игрок должен понимать разницу между безопасной и опасной территорией.

Интерфейс должен явно предупреждать, если персонаж остаётся уязвимым после выхода.

Пример сообщения:

Опасная территория.
После выхода персонаж останется в этой локации и может подвергнуться нападению.

UI-формулировка может быть изменена позднее, но сам принцип предупреждения является желательным.

Для AFK Farming интерфейс должен показывать:

локацию;
разрешён ли AFK;
ожидаемый бонус;
ограничения;
состояние инвентаря;
продолжительность, если она ограничена.

22. Location Reward Principle

Опасные зоны могут давать более ценный контент, но World System не определяет конкретные числа наград.

Базовый принцип:

Higher Risk
should enable
Higher Potential Reward

Это может выражаться через:

опыт;
качество loot;
редкие ресурсы;
доступ к уникальным событиям;
доступ к сильным противникам;
другие системы.

Конкретный баланс определяется Economy/Loot/Progression systems.

AFK Farming не должен нарушать этот принцип.

AFK Farming в опасных зонах по умолчанию отсутствует, чтобы risk/reward оставался осмысленным.

23. Travel and Return

После смерти или выхода из удалённой области возвращение в нужную локацию не обязано быть мгновенным.

Перемещение между зонами будет определяться отдельной Travel/Movement System.

World System хранит состояние того, где персонаж находится.

AFK Farming не является способом путешествия.

24. Quest System Integration

World System должна предоставлять Quest System подтверждённые факты о состоянии мира и действиях персонажа, которые могут использоваться как условия или цели заданий.

Примеры:

LocationEntered
LocationLeft
NpcInteracted
WorldObjectInteracted
WorldEventOccurred
ExplorationProgressOccurred
EncounterStarted
ReinforcementJoined

Это позволяет создавать задания, связанные с миром, например:

посетить конкретную локацию;
добраться до опасной территории;
поговорить с NPC;
взаимодействовать с объектом мира;
находиться в определённой зоне во время мирового события;
выполнить действие в локации с нужным Territory Type или другим контекстом.

World System не должна хранить квестовый прогресс и не должна самостоятельно решать, выполнена ли цель задания.

Пример:

Character enters Old Mine
    ↓
World System emits LocationEntered
    ↓
Quest System checks active objectives
    ↓
Relevant objective progresses

Таким образом World System предоставляет контекст мира, а Quest System содержит правила задания.

25. World Invariants

INVARIANT-01
Logout не удаляет персонажа из мира автоматически.

INVARIANT-02
Online Status и Character Presence являются разными понятиями.

INVARIANT-03
Safe Territory по умолчанию защищает offline-персонажа от обычных hostile encounters.

INVARIANT-04
Adventure/Dangerous Territory может создавать encounters для offline-персонажа согласно правилам мира и NPC.

INVARIANT-05
Обычное offline-пребывание не является AFK Farming.

INVARIANT-06
После обычного offline-боя персонаж не начинает автоматически бесконечную цепочку фарма.

INVARIANT-07
Territory Type и Threat Level являются разными характеристиками локации.

INVARIANT-08
Опасные территории могут предоставлять более ценный контент, но конкретный баланс наград определяется другими системами.

INVARIANT-09
Instanced Territory может иметь собственные disconnect/offline rules.

INVARIANT-10
World System определяет контекст и состояние мира, но не заменяет Combat, AI, Loot или AFK Farming.

INVARIANT-11
AFK Farming разрешён только в Safe Territory и Adventure Territory по умолчанию.

INVARIANT-12
AFK Farming требует, чтобы конкретная локация явно разрешала AFK.

INVARIANT-13
AFK Farming запрещён в Dangerous Territory по умолчанию.

INVARIANT-14
AFK Farming запрещён в Extreme / Endgame Territory по умолчанию.

INVARIANT-15
AFK Farming не запускает обычные world encounters по умолчанию.

INVARIANT-16
AFK Farming не перемещает персонажа между локациями.

INVARIANT-17
Исследование локации может приводить к появлению encounters согласно серверным правилам.

INVARIANT-18
Reinforcements во время боя могут появляться только по серверным правилам и в рамках лимитов.

INVARIANT-19
Encounter generation не должен создавать бесконечную автоматическую цепочку боёв.

26. Out of Scope

Этот документ пока не определяет:

конкретную карту мира;
список всех локаций;
точные уровни угрозы;
точные recommended levels;
spawn formulas;
encounter frequency;
reinforcement chance;
reinforcement limits;
конкретные Monster AI rules;
PvP rules;
travel time formulas;
loot tables;
economy;
world event schedules;
weather system;
environmental damage formulas;
AFK reward formulas;
конкретный UI.

27. Базовая структура мира

WORLD
 │
 ├── Safe Territory
 │    ├── ordinary safe presence
 │    └── optional AFK Farming
 │
 ├── Adventure Territory
 │    ├── ordinary presence
 │    ├── exploration encounters
 │    ├── reinforcements
 │    └── optional AFK Farming
 │
 ├── Dangerous Territory
 │    ├── ordinary presence
 │    ├── higher risk
 │    ├── exploration encounters
 │    ├── reinforcements
 │    └── AFK Farming disabled by default
 │
 ├── Extreme / Endgame Territory
 │    ├── high risk world rules
 │    └── AFK Farming disabled by default
 │
 └── Instanced Territory
      ├── instance-specific rules
      └── AFK Farming disabled by default

CHARACTER PRESENCE
 │
 ├── Online
 ├── Offline
 ├── Exploring
 ├── AFK Farming
 ├── In Combat
 └── Traveling

WORLD EVENTS / ENCOUNTERS
 │
 ↓
COMBAT

Отдельно:

Player explicitly starts AFK Farming
  ↓
AFK FARMING BONUS MODE

Таким образом постоянное присутствие персонажа в мире, исследование локаций, обычный Combat и AFK Farming остаются связанными, но независимыми системами.

---

# Source of Truth Revision v2

- Party и Companion имеют собственные системы; World хранит только location/presence context, необходимый для них.
- Logout не телепортирует персонажа: правило persistent presence сохраняется.
- Spatial Aura не существует, пока нет системы расстояния; Party buffs не требуют расстояния и работают через Party targeting.

# Source of Truth Revision v5 — City / Dungeon Services

World location may expose interaction/service references:

```text
MERCHANT
AUCTION_HOUSE
CRAFTING_STATION
DUNGEON_ENTRANCE
GUILD_SERVICE (future owner)
```

World confirms location/interaction availability but does not own:
- merchant prices;
- auction listings;
- crafting recipes;
- dungeon instance state;
- guild membership.

Dungeon uses `LocationId + DungeonInstanceId` as isolated world scope.
