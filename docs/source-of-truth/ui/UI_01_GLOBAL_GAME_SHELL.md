# Elyndor — UI/UX Specification 01
# Global Game Shell / Navigation

**Document:** docs/source-of-truth/ui/UI_01_GLOBAL_GAME_SHELL.md
**Status:** Approved foundation  
**Platform:** Telegram Mini App  
**Orientation:** Mobile Portrait First  
**Visual direction:** Dark Fantasy MMORPG / Elyndor references

---

# 1. Назначение

Global Game Shell — постоянная оболочка игры, внутри которой открываются основные экраны.

Он определяет:

- верхний HUD;
- нижнюю навигацию;
- кнопку группы;
- трекер квестов;
- кнопку Back;
- переходы между root screens;
- поведение во время Combat;
- возврат после боя;
- loading/reconnect/error states;
- контекст текущей локации.

Shell не определяет внутренний layout конкретных экранов. Для них создаются отдельные UI specifications.

---

# 2. Главная навигационная идея

Отдельная глобальная вкладка `ГОРОД` не используется.

Причина:

> Город — это одна из локаций мира, а не отдельный параллельный режим игры.

Финальная нижняя навигация:

```text
МИР | ГЕРОЙ | ЛОКАЦИЯ | КВЕСТЫ | МЕНЮ
```

`ЛОКАЦИЯ` — центральная context-aware вкладка.

Она всегда показывает место, где физически находится персонаж.

Примеры:

```text
CharacterLocation = CAPITAL_CITY
→ ЛОКАЦИЯ открывает городской экран

CharacterLocation = DARK_FOREST
→ ЛОКАЦИЯ открывает лес / enemies / activities

CharacterLocation = DUNGEON_INSTANCE
→ ЛОКАЦИЯ открывает dungeon-context screen

CharacterLocation = EVENT_AREA
→ ЛОКАЦИЯ открывает event-context screen
```

---

# 3. Разделение МИР / ЛОКАЦИЯ

## МИР

Главный вопрос:

> Куда я могу отправиться?

Содержит:

- регионы;
- локации;
- travel;
- world boss markers;
- world events;
- dungeon entrances;
- требования переходов.

## ЛОКАЦИЯ

Главный вопрос:

> Что я могу делать там, где нахожусь сейчас?

Содержимое зависит от текущего CharacterLocation.

### Если это город

```text
Merchant
Auction
Guild
Blacksmith / Crafting
Alchemy
Cooking
NPC / Quests
other city services
```

### Если это PvE-локация

```text
Location art
Enemies
Elite enemies
Activities
Zone quests
Boss / Event
Dungeon entrance
Travel exits
```

### Если это Dungeon

```text
Dungeon progress
Current encounter
Party
Checkpoint
Boss
Exit
```

Это один из основных navigation invariants проекта:

```text
МИР = destination / travel
ЛОКАЦИЯ = current place / current actions
```

---

# 4. Root Screens

```text
МИР
ГЕРОЙ
ЛОКАЦИЯ
КВЕСТЫ
МЕНЮ
```

## ГЕРОЙ

Внутри:

```text
Character / Equipment
Stats
Inventory
Talents
Companion — только Archer
```

## КВЕСТЫ

Внутри:

```text
Active
Available
Tracked
Rewards
Objectives
```

## МЕНЮ

Второстепенные функции:

```text
Друзья
Группа
Достижения
Почта
Рейтинг
Новости / Обновления
Помощь
Настройки
```

Guild не находится в Menu — это городской social service.

---

# 5. Bottom Navigation

Mobile portrait:

```text
┌────────────────────────────────────────┐
│ Мир │ Герой │ Локация │ Квесты │ Меню │
└────────────────────────────────────────┘
```

`ЛОКАЦИЯ` может быть визуально немного крупнее остальных кнопок.

Иконка центральной вкладки может отражать тип текущего места:

```text
CITY      → castle/tower
FOREST    → trees
DUNGEON   → gate/skull
MOUNTAINS → mountain
EVENT     → event symbol
```

Название при этом остаётся `ЛОКАЦИЯ`, чтобы навигация не меняла смысл.

---

# 6. Combat Mode

Когда сервер подтверждает:

```text
ActivityState = IN_COMBAT
```

нижняя навигация скрывается полностью.

Combat получает весь нижний action space:

```text
abilities
GCD/cooldowns
cast controls
combat actions
```

Игрок не должен случайно открыть Inventory/Auction/Menu во время боя.

---

# 7. Верхний HUD

Показывается почти на всех non-combat screens.

Базовая структура:

```text
[Avatar] Name · Level
HP       █████████
Resource ████████

                         Gold 12 450
                         Crystal 35
                         Party 3/5
```

Реальный layout должен быть компактным.

---

# 8. Character Identity

Слева в HUD:

```text
Avatar
Character Name
Level
optional Class Icon
```

Tap на avatar/name:

```text
→ Hero root
```

---

# 9. HP / Resource

Всегда показываем:

```text
HP
Class Action Resource
```

Resource:

```text
Warrior → Rage
Archer → Focus
Arcane Archer → Mana
Mage → Mana
```

Frontend получает server-confirmed ResourceState.

---

# 10. Currency HUD

Постоянно показываем компактно:

```text
🪙 12.4K
💎 35
```

Tap:

```text
→ Wallet / Economy summary
```

Gold/Crystal не должны визуально спорить с HP/resource.

---

# 11. Party Quick Access

Без группы:

```text
👥+
```

В группе:

```text
👥 3/5
```

Tap:

```text
→ Party screen / overlay
```

Возможные badges:

```text
invite
member disconnected
member dead
```

Группа не занимает отдельный слот нижней навигации.

---

# 12. Quest Tracker

На gameplay screens показывается компактный tracker максимум на 2–3 выбранных квеста.

Пример:

```text
Охота на волков
3 / 8

Старый рудник
Доберитесь до входа
```

Tap:

```text
→ Quest details
```

Показывать на:

```text
World
Location
City
```

Скрывать на:

```text
Hero detail screens
Inventory
Talents
Auction
Merchant
Settings
```

Во время Combat обычный tracker скрывается, но objective update может коротко появиться toast'ом.

---

# 13. Back Button

Используется собственная фэнтезийная кнопка:

```text
←
```

На root screens её нет.

На nested screens она возвращает по UI stack.

Пример:

```text
Location
→ Auction
→ Item Details

← Auction
← Location
```

Telegram/browser Back должен вызывать тот же in-game Back на nested screens и не закрывать Mini App внезапно на root screen.

---

# 14. Victory Flow

Обычный бой:

```text
Location
→ Enemy
→ Combat
→ Victory
→ Reward Result
→ Continue
→ Location
```

После поражения:

```text
Combat
→ Death / Defeat
→ Respawn flow
→ valid location according to Character System
```

---

# 15. Reward Result

После обычной победы показывается компактный экран/карточка результата.

```text
ПОБЕДА

+850 XP
+34 Gold

Loot:
[icon] Rare Sword
[icon] Wolf Pelt x2

[ ПРОДОЛЖИТЬ ]
```

Цель:

- дать ощущение награды;
- показать progression;
- выделить редкий loot;
- не затягивать flow каждого обычного боя.

Rare/Epic/Legendary/Unique получают усиленный visual emphasis:

- rarity frame;
- glow;
- animation;
- item inspect.

Legendary/Unique appearance опирается на `AppearanceProfileId`.

---

# 16. Boss / Dungeon Result

Для World Boss / Dungeon Completion / Major Event используется расширенный result screen.

Может показывать:

```text
Boss defeated
Party summary
Contribution summary
XP
Gold
Loot
Quest updates
Dungeon completion
New unlock
```

Отдельно проектируется вместе с Raid/Boss UI.

---

# 17. Location Context Header

Под Global HUD на вкладке `ЛОКАЦИЯ`:

Обычная зона:

```text
Dark Forest
Threat II
Recommended Level 10–15
```

Город:

```text
Capital City
SAFE
```

Dungeon:

```text
Ancient Mine
Dungeon
Encounter 3/4
```

---

# 18. City Availability Rule

Merchant/Auction/Crafting/Guild не являются global shortcuts.

Они существуют только когда персонаж физически находится в City Location.

Если игрок в Forest:

- не показывать затемнённый Merchant;
- не показывать Auction как недоступную кнопку;
- показывать только реальный контент текущей локации.

Чтобы воспользоваться городскими сервисами:

```text
World
→ choose City
→ Travel
→ Location
→ City services
```

Это делает мир цельным и не превращает город в телепортируемое меню.

---

# 19. Loading

Первый запуск:

```text
Elyndor atmospheric loading
→ Telegram auth
→ Character bootstrap
→ World/Location snapshot
→ Game Shell
```

Не показывать пустые HP/currency widgets до получения bootstrap state.

При обычных UI переходах использовать skeleton/small spinner, а не fullscreen loader.

---

# 20. Reconnect

При SignalR disconnect:

```text
Соединение потеряно
Переподключение...
```

Gameplay actions временно disabled.

После reconnect:

```text
request authoritative snapshot
→ reconcile state
→ restore valid screen
```

Если сервер сообщает `IN_COMBAT`, Combat UI получает приоритет независимо от того, какой screen был открыт до disconnect.

---

# 21. Error States

## Action Error

```text
Недостаточно золота
Предмет больше недоступен
Нельзя выполнить во время боя
```

Показывать toast/inline.

## State Error

```text
Auction listing уже куплен
Party распущена
Dungeon expired
```

Показать сообщение и вернуть пользователя на safe destination/root.

---

# 22. Notifications / Badges

Примеры:

```text
QUESTS → quest updated/new quest
MENU → mail/news/system
HERO → talent point / equipment notification
WORLD → world event / boss
LOCATION → local event/action
```

Не использовать badge для каждого мелкого события.

---

# 23. Global Toasts

Подходят для:

```text
+34 Gold
Quest updated
Item equipped
Recipe learned
Party invite received
```

Rare rewards не ограничиваются toast'ом.

---

# 24. Touch / Mobile Rules

Практический minimum touch target:

```text
≈ 44–48 CSS px
```

Иконка может выглядеть меньше, но hit-area должна быть удобной.

Учитывать Telegram/mobile safe areas:

- top;
- bottom navigation;
- home indicator;
- combat ability row;
- modal actions.

---

# 25. Visual Language

Использовать наши generated Elyndor references:

```text
reference/UI_01-02_GLOBAL_SHELL_WORLD.png
reference/UI_03-04_HERO_INVENTORY.png
reference/UI_05-06_STATS_TALENTS.png
reference/UI_07-08_COMPANION_COMBAT.png
reference/UI_09-10_RAID_PARTY.png
reference/UI_11-12_QUESTS_CITY.png
reference/UI_13-14_MERCHANT_AUCTION.png
reference/UI_15-16_DUNGEON_CRAFTING.png
reference/UI_17-18_MENU_WALLET.png
reference/UI_19-20_SETTINGS_GUILD.png
```

Главные visual rules:

- dark fantasy;
- яркие детализированные MMORPG icons;
- gold/magical borders;
- deep blue/black backgrounds;
- rarity glow;
- крупный character/enemy art;
- mobile-first;
- никаких generic SaaS/dashboard layouts.

---

# 26. Shell State Model

```text
GameShellState
├── ActiveRoot
├── NavigationStack
├── CharacterSummary
├── ResourceSummary
├── WalletSummary
├── PartySummary
├── TrackedQuestSummary
├── LocationSummary
├── ConnectionState
├── ActivityState
└── NotificationBadges
```

Frontend state — projection server-authoritative state.

---

# 27. Root Navigation Rule

При переключении root вкладки nested stack предыдущей root-ветки очищается.

Пример:

```text
Hero → Talents → Fire Branch
Tap World
→ World root
```

Возврат на Hero по умолчанию:

```text
→ Hero root
```

Remember-last-subtab можно определить отдельно в screen spec.

---

# 28. Approved Decisions

Зафиксировано:

1. Bottom navigation скрывается в Combat.
2. Navigation = `МИР | ГЕРОЙ | ЛОКАЦИЯ | КВЕСТЫ | МЕНЮ`.
3. City является Location, а не global tab.
4. HUD почти везде показывает HP/resource/Gold/Crystal.
5. Party quick button = `👥+` / `👥 N/5`.
6. Quest tracker = 2–3 tracked quests.
7. Menu содержит secondary systems.
8. Hero содержит Stats / Inventory / Talents / Equipment.
9. Companion tab существует только у Archer.
10. Back — отдельная игровая кнопка.
11. После боя есть Victory/Reward screen.
12. Current Location показывает арт, врагов, активности и zone quests.

---

# 29. Next Specification

Следующий документ:

```text
docs/source-of-truth/ui/UI_02_WORLD_AND_LOCATION.md
```

Перед ним нужно определить:

- World representation: map/list/hybrid;
- travel UX;
- location card hierarchy;
- enemy cards;
- elite/boss presentation;
- dungeon entrance;
- zone quests;
- event markers;
- danger/threat presentation.
