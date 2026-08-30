# Elyndor — UI/UX Specification 02
# World & Current Location

**Document:** docs/source-of-truth/ui/UI_02_WORLD_AND_LOCATION.md
**Status:** Approved foundation  
**Platform:** Telegram Mini App  
**Orientation:** Mobile Portrait First  
**Depends on:** `docs/source-of-truth/ui/UI_01_GLOBAL_GAME_SHELL.md`, `docs/source-of-truth/gameplay/04_WORLD_AND_LOCATIONS_SYSTEM.md`, `docs/source-of-truth/gameplay/01_TIME_SYSTEM.md`

---

# 1. Назначение

Этот документ определяет два связанных, но разных root-screen:

```text
МИР
ЛОКАЦИЯ
```

Их нельзя смешивать.

```text
МИР
→ куда игрок может отправиться

ЛОКАЦИЯ
→ что игрок может делать там, где находится сейчас
```

---

# 2. Главный UX-принцип

World navigation строится как гибрид:

```text
VISUAL WORLD MAP
+
SELECTED LOCATION CARD
```

На телефоне игрок видит:

1. карту/часть карты;
2. точки известных локаций;
3. туман/неизведанные территории;
4. выбранную точку;
5. снизу — подробную карточку выбранной локации.

---

# 3. Неизведанные территории

Неисследованные области не показывают полную информацию.

Визуально:

```text
████████████
НЕИЗВЕДАННЫЕ ЗЕМЛИ
???
```

Можно показывать:

- силуэт/контур региона;
- туман;
- неизвестный marker;
- направление дороги;
- декоративные landmarks без названия.

Нельзя показывать до открытия:

- точное название;
- mobs;
- boss;
- dungeon;
- quests;
- rewards;
- подробный art preview.

---

# 4. Discovery Model

Базовое открытие мира происходит через исследование связанного маршрута.

Основной вариант:

```text
известная локация
→ соседняя неизвестная точка
→ путешествие
→ прибытие
→ discovery
```

Дополнительные Unlock Conditions:

```text
QUEST
BOSS_DEFEAT
KEY_ITEM
CHARACTER_LEVEL
WORLD_EVENT
SCRIPTED
```

Специальные зоны могут быть скрыты даже при географической близости.

---

# 5. World Map Screen Layout

Mobile-first concept:

```text
┌─────────────────────────────┐
│ GLOBAL HUD                  │
├─────────────────────────────┤
│ МИР                         │
│                             │
│   [visual world map]        │
│                             │
│   ● Capital                 │
│       ╲                     │
│        ● Dark Forest        │
│             ╲               │
│              ? ? ?          │
│                             │
├─────────────────────────────┤
│ SELECTED LOCATION CARD      │
│ Dark Forest                 │
│ Ур. 10–15 · Threat II       │
│ Время пути 02:35            │
│ [ ОТПРАВИТЬСЯ ]             │
├─────────────────────────────┤
│ BOTTOM NAV                  │
└─────────────────────────────┘
```

---

# 6. Map Interaction

Игрок может:

- tap known location;
- tap currently visible unknown region marker;
- pan map;
- limited zoom;
- tap world boss marker;
- tap dungeon/world event marker, если он уже известен.

Не делать desktop-like tiny map controls.

Touch targets остаются mobile-friendly.

---

# 7. Selected Location Card

Для известной точки карточка показывает:

```text
Location Name
Region
Recommended Level
Threat
Travel Time
Current special state
```

Optional:

```text
Dungeon present
World Boss active
World Event active
Quest available
```

Главный CTA:

```text
[ ОТПРАВИТЬСЯ ]
```

Если игрок уже там:

```text
[ ВЫ УЖЕ ЗДЕСЬ ]
```

или:

```text
[ ОТКРЫТЬ ЛОКАЦИЮ ]
```

---

# 8. Travel Time

Путешествие не мгновенное.

Каждая связь мира имеет travel cost/time.

```text
TravelConnection
├── FromLocationId
├── ToLocationId
├── TravelTime
└── TravelPolicy
```

UI показывает итоговое время до старта.

---

# 9. Auto Route

Если destination находится дальше одной связи, система строит маршрут автоматически.

Пример:

```text
Столица
→ Тёмный лес
→ Чёрные болота
→ Древние руины

Общее время: 08:34
```

Игроку не нужно подтверждать каждый промежуточный переход.

---

# 10. Route Preview

Перед стартом:

```text
ДРЕВНИЕ РУИНЫ

Маршрут:
Столица
↓
Тёмный лес
↓
Чёрные болота
↓
Древние руины

Время пути: 08:34

[ ОТПРАВИТЬСЯ ]
```

---

# 11. Travel Continues Offline

После старта travel использует absolute server time.

```text
StartedAt
ArrivesAt
```

Закрытие Telegram Mini App не останавливает путь.

После возвращения:

```text
if ServerTime >= ArrivesAt
→ travel complete
→ CharacterLocation = destination
```

---

# 12. Travel State UI

Во время пути:

```text
ПУТЕШЕСТВИЕ

Столица
↓
Тёмный лес

01:48

Следующая точка:
Тёмный лес

[ ОТМЕНИТЬ ПУТЬ ]
```

---

# 13. Travel While Using Other Screens

Во время travel игрок может:

```text
Hero
Equipment
Inventory
Stats
Talents
Quests
Menu
Party
Friends
Achievements
Settings
```

Нельзя:

```text
Combat
Dungeon entry
Merchant
Auction
Crafting Station
start another Travel
local interaction
```

HUD показывает compact Travel indicator:

```text
🚶 01:48
```

Tap:

```text
→ Travel Status
```

---

# 14. Cancel Travel

Travel можно отменить.

Базовая policy:

```text
Cancel
→ return to original valid origin Location
```

Игрок не получает промежуточную destination бесплатно.

Если позже мир станет сложнее, можно добавить nearest-checkpoint policy, но текущая модель — возврат в origin.

---

# 15. Dangerous Destination

Игроку разрешено идти в location выше своего уровня.

Не использовать hard level wall, если LocationDefinition явно не требует lock.

Пример warning:

```text
⚠ СМЕРТЕЛЬНАЯ ОПАСНОСТЬ

Рекомендуемый уровень: 30–35
Ваш уровень: 12

Монстры здесь значительно сильнее.

[ НАЗАД ]
[ ВСЁ РАВНО ИДТИ ]
```

Это MMO-world freedom, а не UI error.

---

# 16. Locked Destination

Если location имеет настоящий requirement:

```text
Quest
Key Item
Boss Defeat
Level Gate
Scripted Unlock
```

CTA заменяется.

Пример:

```text
🔒 ЛОКАЦИЯ ЗАКРЫТА

Требуется:
Завершить «Падение Стража»
```

Нельзя показывать кнопку "всё равно идти".

---

# 17. World Boss Markers

World Boss отображается на карте, если регион/босс уже открыт игроком.

Состояния:

```text
INACTIVE
UPCOMING
AVAILABLE
ACTIVE_COMBAT
```

Визуально:

```text
серый      → Неактивен
золотой    → Появится через 42:15
красный    → ДОСТУПЕН
фиолетовый → ИДЁТ БОЙ
```

---

# 18. World Boss Notifications

При значимом boss state:

```text
Boss became AVAILABLE
```

игрок получает системное уведомление:

```text
Варгремор появился
Пепельная долина
```

Дополнительно:

```text
МИР •
```

badge на bottom navigation.

Уведомления world boss могут отключаться в Settings.

---

# 19. World Event Markers

World Event использует отдельный marker.

Состояния могут показывать:

```text
Scheduled
Active
Ending Soon
Completed/Cooldown
```

Не использовать тот же icon, что World Boss.

---

# 20. Dungeon Marker

Известный Dungeon отображается на World Map.

Tap:

```text
Dungeon preview card
```

Пример:

```text
ДРЕВНИЕ ШАХТЫ

Dungeon
Ур. 20–25
3–5 игроков

Boss: ???
Reward Tier: Rare–Epic

[ ПОДРОБНЕЕ ]
```

Если entrance находится не в текущей location:

```text
[ ПОСТРОИТЬ МАРШРУТ ]
```

---

# 21. Current Location Root

`ЛОКАЦИЯ` всегда строится из текущего CharacterLocation.

Основная структура PvE-location:

```text
GLOBAL HUD

LOCATION TITLE
Threat / Level

LARGE LOCATION ART

TRACKED QUESTS

SPECIAL ACTIVITIES

NORMAL ENEMIES

ZONE QUESTS

EXITS / ROUTES
```

---

# 22. Location Art

Главный art занимает заметную часть первого экрана.

Это не background wallpaper, а основной визуальный anchor.

Показывает:

- biome;
- architecture;
- mood;
- weather;
- danger fantasy;
- optional monster silhouettes.

---

# 23. Light Animated Effects

Location art может иметь лёгкие эффекты:

```text
fog
embers
snow
rain particles
floating dust
subtle light flicker
very light parallax
```

Не превращать screen в тяжёлый animated scene.

---

# 24. Performance Setting

Settings:

```text
Атмосферные эффекты
[ ON / OFF ]
```

Optional позже:

```text
LOW
MEDIUM
HIGH
```

При OFF:

- static art;
- no parallax;
- no particles;
- gameplay UI unchanged.

---

# 25. Reduced Motion

Отдельно уважать system/user reduced-motion preference.

Даже при Atmospheric Effects ON:

- не делать резких camera movement;
- не использовать aggressive looping motion;
- не анимировать весь UI постоянно.

---

# 26. Location Header

Пример обычной зоны:

```text
ТЁМНЫЙ ЛЕС
Threat II
Рекомендуемый уровень 10–15
```

City:

```text
ЭЛИНДОР
SAFE
Столица королевства
```

Dungeon context:

```text
ДРЕВНИЕ ШАХТЫ
Dungeon
Encounter 3 / 4
```

---

# 27. Quest Tracker Placement

После location art:

```text
TRACKED QUESTS
```

Maximum:

```text
2–3
```

Compact.

Не занимать половину screen.

---

# 28. Special Activities First

Особые активности располагаются выше обычных mobs.

Причина:

- создают identity location;
- быстро показывают важный content;
- стимулируют возвращаться;
- имеют больший visual weight.

Order:

```text
Elite
Regional Boss
Dungeon
World Event
Special Interaction
```

Только доступные в текущей location элементы.

---

# 29. Special Activity Cards

Пример Elite:

```text
┌───────────────────────────┐
│ ☠ СТАРЫЙ МЕДВЕДЬ         │
│ Elite · Ур. 15            │
│                           │
│ [art/icon]                │
│                           │
│ Редкий противник          │
│ [ ОТКРЫТЬ ]               │
└───────────────────────────┘
```

Boss и Dungeon получают более крупные cards.

---

# 30. Regional Boss Card

```text
👑 ХРАНИТЕЛЬ ЧАЩИ

Regional Boss
Ур. 18

State:
Доступен

[ ПОДРОБНЕЕ ]
[ В БОЙ ]
```

Если boss on cooldown:

```text
Возвращение: 18:42
```

---

# 31. Dungeon Card Inside Location

```text
🏰 ДРЕВНИЕ ШАХТЫ

Ур. 20–25
3–5 игроков
4 encounters

Reward:
Rare–Epic

[ ОТКРЫТЬ ДАНЖ ]
```

Если Party не подходит:

UI показывает requirement, но не скрывает dungeon.

---

# 32. Normal Enemy List

Обычные mobs не используют огромные cards.

Mobile-friendly row:

```text
ВРАГИ

[icon] Лесной волк      Ур.12   [⚔]
[icon] Разбойник        Ур.13   [⚔]
[icon] Старый медведь   Ур.14   [⚔]
```

---

# 33. Enemy Row Interaction

Tap row:

```text
→ Enemy Details
```

Tap quick combat button:

```text
[⚔]
→ Start Combat request
```

Если server подтверждает:

```text
→ Combat UI
```

---

# 34. Enemy Details

Пример:

```text
ЛЕСНОЙ ВОЛК

[enemy art]

Уровень 12
NORMAL
Тип: Зверь

HP: 1 240

Краткое описание.

[ В БОЙ ]
```

---

# 35. Loot Information

Exact drop table **не показывается** на Enemy Details.

Причина:

- не превращать location screen в spreadsheet;
- сохранить discovery;
- не перегружать mobile UI.

Позже информация может появиться в:

```text
BESTIARY / GLOSSARY
```

через `MENU`.

Bestiary может открывать known drops после discovery/kill conditions.

---

# 36. Bestiary Future Entry

Menu future:

```text
МЕНЮ
→ Бестиарий / Глоссарий
```

Potential data:

```text
Enemy
Region
Kills
Known abilities
Known drops
Boss lore
Materials
```

Это отдельный будущий UI spec.

---

# 37. Zone Quests

Location показывает локально релевантные quests.

Раздел:

```text
КВЕСТЫ ЛОКАЦИИ
```

Rows:

```text
! Новое задание
○ Активное
✓ Готово к сдаче
```

Tap:

```text
→ Quest Details
```

---

# 38. Exits / Routes

Внизу:

```text
ПЕРЕХОДЫ

Лесная дорога
→ Столица
01:20

Старый тракт
→ Чёрные болота
02:45
```

Tap:

```text
→ Route preview
```

---

# 39. City as Location

City использует тот же root `ЛОКАЦИЯ`.

Вместо mobs:

```text
CITY ART

TRACKED QUESTS

ГОРОДСКИЕ СЕРВИСЫ
Merchant
Auction
Guild
Forge
Alchemy
Cooking

NPC / Quests
Exits
```

Не существует global shortcut:

```text
ГОРОД
```

---

# 40. City Service Visibility

Если игрок не в City:

он вообще не видит Merchant/Auction/Crafting в текущей Location.

Чтобы открыть:

```text
World
→ travel to City
→ Location
→ service
```

---

# 41. Safe City Presentation

City Location:

```text
SAFE
```

может визуально отличаться:

- warmer lighting;
- fewer danger markers;
- no enemy rows;
- more service cards;
- NPC/quest emphasis.

---

# 42. Location Activity Priority

Ordering rule:

```text
1. critical current event
2. tracked quest context
3. boss/dungeon/elite
4. normal enemies
5. optional quests
6. exits
```

Если World Event активен:

он может temporarily подняться выше tracked quest block.

---

# 43. Dynamic Location State

Location screen обновляется по server events.

Examples:

```text
Boss became available
World Event started
Quest objective completed
Dungeon unlocked
Elite defeated/cooldown
Travel exit unlocked
```

Не требовать manual refresh.

---

# 44. Empty Location State

Если location не содержит combat:

```text
No enemies
```

не показывать пустой раздел `ВРАГИ`.

Location может состоять из:

```text
art
NPC
quests
services
story interaction
exits
```

---

# 45. Combat Start

Combat может начаться:

```text
quick enemy button
enemy details
boss CTA
scripted encounter
world event
```

Pipeline UI:

```text
tap
→ button disabled
→ server validation
→ CombatStarted
→ Shell hides bottom nav
→ Combat UI
```

---

# 46. Combat Start Error

Possible:

```text
Enemy no longer available
Character already in combat
Travel state active
Activity locked
Boss unavailable
```

Показывается short inline/toast error.

---

# 47. Return After Normal Combat

После Victory Result:

```text
Continue
→ current Location root
```

Scroll position:

recommended reset/restore around enemy/activity block, а не всегда top-of-page.

Это уменьшает раздражение при farm loop.

---

# 48. Farm Loop UX

Для повторного фарма:

```text
Location
→ quick combat
→ Combat
→ Victory
→ Continue
→ Location near enemy list
```

Не заставлять каждый раз:

```text
map
→ location
→ enemy details
→ fight
```

---

# 49. Travel Arrival

Когда travel завершается:

если app открыт:

```text
Arrival animation/toast
→ CharacterLocation changed
→ Location root available
```

Пример:

```text
Вы прибыли:
ТЁМНЫЙ ЛЕС

[ ОТКРЫТЬ ЛОКАЦИЮ ]
```

Если app был закрыт:

при bootstrap:

```text
Travel completed
→ current location = destination
```

---

# 50. World Map Fog Reveal

При first discovery:

```text
fog fade
location icon appears
name reveal
short discovery banner
```

Пример:

```text
НОВАЯ ЛОКАЦИЯ ОТКРЫТА
Чёрные болота
```

Animation disabled/reduced when Atmospheric Effects OFF / Reduced Motion ON.

---

# 51. Location Discovery Reward

UI может показать:

```text
New Location
XP, if gameplay system grants it
new quests
new routes
new dungeon
```

UI **не создаёт** reward.

Показывает только confirmed server result.

---

# 52. World Boss Active Combat State

Если World Boss уже сражается:

card/map marker может показывать:

```text
ИДЁТ БОЙ
Participants: 17
Boss HP: 63%
```

Только если Boss System предоставляет такие public summary data.

Не строить client-side estimate.

---

# 53. World Boss Entry

Tap active boss:

```text
Boss Preview
```

Показывает:

```text
Boss art
Level
HP/state
Location
Availability
recommended party
current participants summary
```

Если игрок в другой location:

```text
[ ПОСТРОИТЬ МАРШРУТ ]
```

Если уже там:

```text
[ ПРИСОЕДИНИТЬСЯ ]
```

если Boss rules разрешают.

---

# 54. Quest Marker On Map

Known location может иметь badge:

```text
!
```

если там:

- available quest;
- tracked objective;
- turn-in.

Не показывать десятки quest icons одновременно.

Map uses aggregate marker.

---

# 55. Map Visual Density

Mobile map одновременно показывает ограниченное количество labels.

Priority:

```text
current location
selected location
city
active world boss
active world event
tracked quest destination
nearby discovered nodes
```

Другие labels появляются при pan/zoom/select.

---

# 56. Current Location Marker

На World Map текущая location всегда явно отмечена:

```text
YOU ARE HERE
```

или визуальной player marker.

Не заставлять пользователя угадывать своё положение.

---

# 57. Selected vs Current Location

Разные visual states:

```text
Current = character marker / strong glow
Selected = highlighted border/pulse
Destination = route line / arrow
```

---

# 58. Route Line

При preview:

```text
known route path
```

подсвечивается на карте.

Unknown intermediate path не раскрывается, если destination locked/unknown.

---

# 59. Offline Travel Notification — Future

Позже можно добавить Telegram notification:

```text
Вы прибыли в Тёмный лес
```

если продуктовые/notification permissions это позволяют.

Это future convenience, не dependency текущего travel system.

---

# 60. Settings Integration

Settings includes:

```text
Атмосферные эффекты       ON/OFF
Уведомления World Boss    ON/OFF
Уведомления World Event   ON/OFF
```

Future:

```text
Map animation quality
Travel notifications
```

---

# 61. Required Assets Per Location

Каждая значимая location должна иметь content references:

```text
LocationBackgroundArt
LocationThumbnail
MapIcon
MapMarkerStyle
ThreatVisual
OptionalAtmosphereProfile
```

City, dungeon entrance и major event location могут иметь special variants.

---

# 62. Required Assets Per Enemy

```text
EnemyIcon
EnemyPortrait / CardArt
Rarity/Type frame if needed
```

Обычным mobs не требуется full-body unique UI art на каждой строке.

---

# 63. Required Assets Per Dungeon

```text
DungeonIcon
DungeonCardArt
EntranceVisual
Boss teaser art, optional
```

---

# 64. Required Assets Per Boss

```text
BossPortrait
BossFullArt
MapMarker
BossFrame
State visual
```

---

# 65. World Screen States

World UI must support:

```text
NORMAL
TRAVELLING
ROUTE_PREVIEW
UNKNOWN_SELECTED
LOCKED_SELECTED
WORLD_BOSS_AVAILABLE
WORLD_EVENT_ACTIVE
CONNECTION_LOST
LOADING
```

---

# 66. Location Screen States

Location UI must support:

```text
NORMAL
SAFE_CITY
DUNGEON_CONTEXT
EVENT_CONTEXT
BOSS_AVAILABLE
NO_COMBAT_CONTENT
TRAVEL_LOCKED
LOADING
RECONNECTING
```

---

# 67. Loading

Map initial load:

- skeleton/background;
- known markers after snapshot;
- no fake marker data.

Location:

- keep previous art where safe;
- skeleton dynamic blocks;
- do not blank entire screen during small refresh.

---

# 68. Reconnect

If reconnect changes current location:

```text
discard stale Location state
→ authoritative snapshot
→ open correct Location context
```

If player entered Combat while offline:

```text
Combat UI overrides World/Location
```

---

# 69. Notifications / Badges

World tab:

```text
active boss
major event
newly discovered route
```

Location tab:

```text
new local activity
quest ready
dungeon unlocked
```

Avoid badge spam.

---

# 70. Approved Decisions

Зафиксировано владельцем проекта:

1. World uses hybrid map + selected-location card.
2. Unknown territories remain hidden until exploration.
3. Travel takes real time.
4. Travel continues offline.
5. Normal enemies use compact rows + optional detail card.
6. Exact mob loot is not shown on Location screen.
7. Future Bestiary/Glossary may reveal known drops.
8. Elite/Boss/Dungeon are visually separated above normal mobs.
9. Dungeon receives a large premium card.
10. World Boss uses visible world-map states.
11. World Boss availability produces notification/badge.
12. Location art uses light atmospheric effects.
13. Atmospheric effects can be disabled.
14. Auto-route is used for multi-hop travel.
15. Travel can be cancelled back to origin.
16. Player may enter dangerous higher-level zones after warning.
17. Special activities appear above ordinary enemies.
18. City is a Location and only exposes city services while physically there.

---

# 71. Visual Reference Direction

Use included references for style:

```text
reference/UI_01-02_GLOBAL_SHELL_WORLD.png
reference/UI_11-12_QUESTS_CITY.png
reference/UI_13-14_MERCHANT_AUCTION.png
reference/UI_09-10_RAID_PARTY.png
```

For World Map itself a dedicated new visual reference should be generated only after this UX structure is accepted.

Visual direction remains:

```text
dark fantasy
high-detail icons
gold / arcane frames
deep atmospheric art
strong readability
mobile portrait
```

---

# 72. Next Specification

Next:

```text
docs/source-of-truth/ui/UI_03_HERO.md
```

Hero interview must determine:

- main character pose/model;
- equipment slot placement;
- tabs;
- item comparison;
- visual equipment layering;
- legendary/unique appearance;
- stats density;
- inventory presentation;
- quick actions;
- class-specific Companion tab;
- how much character animation is used.
