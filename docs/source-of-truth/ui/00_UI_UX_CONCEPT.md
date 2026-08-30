# Elyndor — UI / UX Concept

**Document:** docs/source-of-truth/ui/00_UI_UX_CONCEPT.md
**Status:** Product / UX Source of Truth — Concept v1  
**Platform:** Telegram Mini App  
**Primary orientation:** Mobile portrait  
**Reference:** LIGMAR Online используется только как композиционный ориентир. Никакие assets, illustrations, icons, names, fonts или exact layouts не копируются.

---

# 1. Главная цель

Elyndor должен выглядеть как **MMORPG внутри Telegram**, а не как админ-панель, сайт или набор карточек.

Игрок должен открыть Mini App и сразу видеть:

```text
персонажа
текущую ситуацию
HP / Resource
важные действия
игровой мир
```

а не:

```text
dashboard
таблицы
длинные формы
10 одинаковых белых карточек
```

---

# 2. Что берём из композиционной логики LIGMAR

Нам подходит не визуальный стиль как таковой, а несколько UX-решений:

1. **постоянно читаемый верхний боевой/персонажный HUD**;
2. **центральный персонаж как главный визуальный объект**;
3. **экипировка расположена вокруг персонажа**, а не только длинным списком;
4. **в бою игрок и противник визуально противопоставлены**;
5. **skills находятся близко к нижней зоне большого пальца**;
6. secondary actions не конкурируют с основной gameplay area;
7. нижняя навигация остаётся очень предсказуемой.

Elyndor должен сделать эту структуру более чистой, современной и менее перегруженной.

---

# 3. Базовая композиция Elyndor

На большинстве игровых экранов:

```text
┌────────────────────────────┐
│ HEADER / PLAYER HUD        │
├────────────────────────────┤
│                            │
│      GAMEPLAY AREA         │
│                            │
├────────────────────────────┤
│ CONTEXT ACTIONS            │
├────────────────────────────┤
│ BOTTOM NAVIGATION          │
└────────────────────────────┘
```

Это основной invariant UI.

Не нужно на каждом экране изобретать новую структуру.

---

# 4. Экранные зоны

## 4.1. Header

Высота ориентировочно:

```text
72–88 px
```

Содержит:

```text
Avatar
Name
Level
HP
Action Resource
короткий status indicator
```

Справа:

```text
Gold
notifications
menu
```

Не показываем 6 валют одновременно.

Если валют станет много — в header показывается только основная, остальные в Wallet/Economy screen.

---

# 5. Header — layout

```text
┌─────────────────────────────────┐
│ [Avatar]  Danil        Lv. 22   │
│           ██████████  HP        │
│           ███████░░░  Mana      │
│                       🪙 1 420   │
└─────────────────────────────────┘
```

Для Warrior:

```text
HP
Rage
```

Archer:

```text
HP
Focus
```

Arcane Archer:

```text
HP
Mana
```

Mage:

```text
HP
Mana
```

Resource bar меняется по Combat Profile без изменения общего layout.

---

# 6. Bottom Navigation

Не больше пяти главных разделов одновременно.

Предлагаемый вариант:

```text
🌍 Мир
⚔️ Бой / Активность
🧙 Персонаж
🎒 Рюкзак
☰ Ещё
```

`Бой` не обязательно отдельный destination.

Если игрок находится в Combat:

```text
⚔️ Бой
```

ведёт в активную CombatSession.

Если боя нет:

```text
⚔️ Активность
```

может открывать context location actions.

---

# 7. Раздел «Ещё»

Внутри:

```text
Квесты
Группа
Гильдия
Аукцион
Профессии
Почта
Настройки
```

Не нужно загружать Bottom Navigation десятью иконками.

---

# 8. Главный экран / World Screen

Главный экран не должен быть character spreadsheet.

Пример:

```text
┌───────────────────────────────┐
│ PLAYER HUD                    │
├───────────────────────────────┤
│ Сумеречный лес          ⚠ II  │
│                               │
│       [LOCATION ART]          │
│                               │
│  Волк-разоритель     Lv. 12   │
│  Ведьма чащи         Lv. 14   │
│  Старый медведь      Elite    │
│                               │
├───────────────────────────────┤
│ [Исследовать] [Сражаться]     │
│ [Квесты: 2]    [Вернуться]    │
├───────────────────────────────┤
│ World  Activity Character ... │
└───────────────────────────────┘
```

Основная artwork зоны занимает пространство.

Threat/territory показываются коротким значком, а не отдельной огромной панелью.

---

# 9. Character Screen — главный концепт

Это один из ключевых экранов.

## Desktop-like inventory UI не использовать

Не делаем:

```text
список:
Шлем
Нагрудник
Перчатки
Поножи
...
```

как основной вид.

Вместо этого персонаж — центр композиции.

---

# 10. Character Screen — layout

```text
┌───────────────────────────────┐
│ PLAYER HUD                    │
├───────────────────────────────┤
│        Уровень 22             │
│      18 420 / 24 000 XP       │
│                               │
│ [HEAD]           [CLOAK]      │
│                               │
│        ╭────────╮             │
│ [MAIN] │        │ [OFF]       │
│        │ HERO   │             │
│ [AMUL] │  ART   │ [CHEST]     │
│        │        │             │
│ [RING] │        │ [HANDS]     │
│        ╰────────╯             │
│ [RING]           [LEGS]       │
│                  [FEET]       │
├───────────────────────────────┤
│ СИЛА 124  БРОНЯ 310   ⓘ      │
│ [Снаряжение] [Статы] [Таланты]│
├───────────────────────────────┤
│ BOTTOM NAV                    │
└───────────────────────────────┘
```

На узком экране slots располагаются не идеально симметрично, а так, чтобы не становиться меньше минимального tap target.

---

# 11. Equipment Icon

Размер:

```text
52–60 px mobile
```

Tap target:

```text
минимум 48 × 48
```

Содержимое:

```text
item icon
rarity frame
item level / tier optional
маленький lock/broken marker, если понадобится
```

Не писать название предмета непосредственно возле каждого slot.

Tap:

```text
slot
→ bottom sheet item tooltip
```

---

# 12. Item Tooltip

Не отдельная страница.

Bottom Sheet:

```text
┌───────────────────────────────┐
│ Пепельный длинный лук    EPIC │
│ Level 22                      │
│                               │
│ 84–112 Damage                 │
│ 2.60 sec                      │
│                               │
│ +13 Agility                   │
│ +5% Accuracy                  │
│ +4% CriticalChance            │
│                               │
│ ▲ +7 Damage                   │
│ ▼ −2 Accuracy                 │
│                               │
│ [Надеть]          [Закрыть]   │
└───────────────────────────────┘
```

Comparison должен быть моментальным.

---

# 13. Character Tabs

Не делать отдельную Bottom Navigation для каждого подраздела персонажа.

Внутренний segmented navigation:

```text
Снаряжение
Характеристики
Таланты
```

В будущем:

```text
Облик
```

---

# 14. Stats Screen

Не показывать сразу таблицу из 40 чисел.

Первый слой:

```text
PRIMARY
Strength / Agility / Intellect
Stamina

OFFENSE
AttackPower / SpellPower
CriticalChance
Accuracy
Penetration

DEFENSE
Armor
MagicResistance
Dodge
```

Tap по группе:

```text
→ expanded values
```

Каждый stat имеет `ⓘ`.

---

# 15. Combat Screen — основная идея

Игрок и противник должны читаться **за 0.5 секунды**.

Верх:

```text
PLAYER                       ENEMY
```

Центр:

```text
Character Art  VS  Enemy Art
```

Низ:

```text
skills
cast state
context actions
```

---

# 16. Combat Screen — wireframe

```text
┌────────────────────────────────┐
│ [ME] Danil              Wolf   │
│ █████████ HP      HP ███████░  │
│ █████░░ Focus                  │
│ 🔥❄️ buffs          debuffs ☠️ │
├────────────────────────────────┤
│                                │
│   [PLAYER]          [ENEMY]    │
│                                │
│          -427                  │
│                    CRIT 812!   │
│                                │
├────────────────────────────────┤
│         Fireball               │
│     ▓▓▓▓▓▓▓░░  1.2s           │
├────────────────────────────────┤
│ [1] [2] [3] [4] [5] [6]       │
│ [7] [8] [Potion] [Utility]     │
├────────────────────────────────┤
│ [Сменить цель] [Покинуть бой]  │
├────────────────────────────────┤
│ BOTTOM NAV                     │
└────────────────────────────────┘
```

---

# 17. Skill Layout

На телефоне важнее reachability, чем максимальное количество icon.

Основной active row:

```text
6 abilities
```

Дополнительный row:

```text
2–4 situational abilities/items
```

Не показывать 20 кнопок одновременно.

---

# 18. Skill Icon States

Одна и та же icon должна ясно различать:

```text
READY
COOLDOWN
NO_RESOURCE
INVALID_TARGET
SILENCED
STUNNED
CASTING
QUEUED
PROC_READY
```

## READY

полный icon.

## COOLDOWN

radial dark overlay + remaining seconds.

## NO_RESOURCE

desaturated + resource symbol.

## SILENCED

small lock/silence marker.

## QUEUED

тонкая светящаяся рамка.

## PROC_READY

анимированная рамка конкретного цвета school/build.

Не менять положение кнопки.

---

# 19. Mage Fire proc UI

Для `Предел Жара`:

```text
Fireball crit streak
```

не требует большой отдельной панели.

Вариант:

```text
🔥 ● ○ ○
🔥 ● ● ○
🔥 ● ● ●
```

markers находятся над `Fireball`.

После третьего crit:

```text
Огненная Комета
```

загорается отдельной active icon.

Показывается:

```text
8.0
```

через expiration radial ring.

---

# 20. Arcane Charge UI

Не отдельная Mana bar.

```text
Mana ███████░░

      ◆ ◆ ◆ ◇
```

Charges располагаются прямо над relevant Arcane abilities.

Причина:

`ARCANE_CHARGE` — effect stack, не Action Resource.

---

# 21. Frost UI

На target status:

```text
❄ x3
◇ BRITTLE
```

Не использовать огромный текст:

```text
TARGET HAS FROSTBITE STACKS: 3
```

Все важные status effect читаются icon + stack number.

---

# 22. Cast Bar

Cast bar появляется только во время Casted Ability.

Положение:

```text
непосредственно над skills
```

Содержит:

```text
ability icon
ability name
progress
remaining time
```

Пример:

```text
🔥 Fireball
███████░░░ 0.6s
```

Interrupt:

```text
bar резко гаснет
короткий feedback
```

---

# 23. Damage Numbers

Не превращать экран в MMO combat text wall.

Приоритет:

```text
player outgoing damage
critical
incoming damage
important heal/shield
```

Обычные маленькие periodic ticks группируются.

Например:

```text
Burn 22
Burn 22
Burn 22
```

можно визуально показывать компактнее, чем три гигантских числа.

---

# 24. Effects

Player buffs:

```text
под player resource bars
```

Target debuffs:

```text
под enemy HP bar
```

Количество одновременно видимых:

```text
до 6
```

Остальные:

```text
+3
```

Tap:

```text
полный список в bottom sheet
```

---

# 25. Multi-target Combat

Combat не использует position.

Поэтому UI не рисует battlefield с координатами.

Если врагов несколько:

```text
Current Target
```

показывается крупно.

Другие:

```text
[Wolf 82%] [Cultist 41%] [Mage 100%]
```

маленькая горизонтальная target strip.

Tap меняет target.

---

# 26. Party в Combat

Party не должна забирать половину экрана.

Collapsed:

```text
[3]
```

Expanded strip:

```text
Tank    ███████
Mage    █████░░
Archer  ███████
```

Для healer class позже strip расширяется, но сейчас Warrior/Archer/Mage не требуют raid-frame UI.

---

# 27. Archer Companion

На combat screen рядом с player HUD:

```text
🐺 72%
```

Tap:

```text
Companion bottom sheet
```

Не создавать вторую огромную full-size HP bar рядом с Player.

Если `DEFEATED`:

```text
🐺 DEFEATED
```

и recovery timer.

---

# 28. World → Combat transition

Никаких white loading page.

```text
Location Screen
→ target selected
→ central area transitions to Combat
→ Header сохраняется
→ Bottom Navigation сохраняется
```

Это создаёт ощущение одной игры, а не перехода между веб-страницами.

---

# 29. Inventory Screen

```text
┌───────────────────────────────┐
│ PLAYER HUD                    │
├───────────────────────────────┤
│ Рюкзак                 31/40  │
│ [All][Gear][Consum][Material] │
│                               │
│ □ □ □ □ □                     │
│ □ □ □ □ □                     │
│ □ □ □ □ □                     │
│ □ □ □ □ □                     │
│                               │
├───────────────────────────────┤
│ Sort                     🔍   │
├───────────────────────────────┤
│ BOTTOM NAV                    │
└───────────────────────────────┘
```

Используем grid, потому что это game inventory.

---

# 30. Talent Screen

Дерево вертикальное.

Не уменьшать все 96 talents на одном экране.

Top:

```text
[Build 1] [Build 2]
Points: 23
```

Branch selector:

```text
🔥 Пламя
🔮 Тайная
❄️ Лёд
```

или три колонки на широком desktop.

На телефоне:

```text
одна выбранная ветка крупно
```

с возможностью быстро переключаться между ветками.

---

# 31. Talent Node

```text
[ICON]
2/3
```

State:

```text
LOCKED
AVAILABLE
LEARNED
MAXED
PREREQUISITE MISSING
```

Dependency lines минимальные и читаемые.

Tap:

```text
Talent Detail Sheet
```

Long press не обязателен — в Telegram WebView это менее предсказуемый основной UX.

---

# 32. Talent Detail Sheet

```text
Искра Критика
Rank 2 / 3

CriticalChance FIRE abilities
+4%

Следующий rank:
+6%

Требуется:
Tier 1

[Изучить]
```

Если активный talent:

```text
[−] 2/3 [+]
```

только если текущая respec policy позволяет.

---

# 33. Quest UI

На World Screen:

```text
Квесты 2
```

Tap → sheet / screen.

Quest tracker в основной зоне показывает максимум:

```text
2 tracked objectives
```

Не засоряем world screen десятью objectives.

---

# 34. Boss UI

Boss:

```text
boss portrait/name
large HP bar
phase marker
important boss effects
```

World boss:

```text
не показывать 30 player HP bars
```

Показывается Party + boss.

---

# 35. Modal Policy

Использовать:

```text
Bottom Sheet
```

для:

- item;
- talent;
- effect;
- companion;
- target;
- quick confirmation.

Full-screen modal только для сложных flow:

- Character Creation;
- Auction listing;
- major settings.

---

# 36. Icon philosophy

Иконки должны быть игровыми, а не Material Icons с бизнес-панели.

Одна ability = один хорошо узнаваемый silhouette.

Цвет внутри artwork помогает школе:

```text
Fire → жар / искра / красно-оранжевая энергия
Arcane → фиолетово-синяя геометрия
Frost → ледяной бело-синий силуэт
```

Но color не является единственным различием.

Каждая icon должна читаться даже при desaturation.

---

# 37. Rarity Frames

Frame вокруг item icon:

```text
COMMON
UNCOMMON
RARE
EPIC
LEGENDARY
UNIQUE
```

Основное artwork остаётся главным.

Не делать всю карточку предмета залитой rarity color.

---

# 38. Visual Style

Elyndor:

```text
dark fantasy
глубокий тёмный фон
контрастные игровые illustrations
тонкие металлические/рунические frames
яркие ability icons
очень мало чисто белых surfaces
```

Не превращать UI в чрезмерно декоративную рамку.

Content должен занимать больше места, чем chrome.

---

# 39. Typography

Нужен максимум:

```text
1 display/game font для крупных titles
1 UI font для всего текста
```

Для чисел:

- очень высокая читаемость;
- tabular numerals там, где прыгают timers/resources.

Нельзя использовать декоративный fantasy font для:

```text
item stats
cooldowns
quest text
chat
```

---

# 40. Information hierarchy

Порядок внимания в Combat:

```text
1. Enemy HP / danger
2. Player HP / resource
3. Ready abilities
4. Cast / Proc
5. Effects
6. Secondary combat log
```

Порядок внимания в Character:

```text
1. Character
2. Equipped gear
3. Level/progress
4. Summary stats
5. Details
```

---

# 41. Combat Log

По умолчанию collapsed.

```text
Последнее:
Critical 812
Wolf used Bite
```

Swipe/tap:

```text
full combat log
```

Это особенно полезно на раннем тесте, но не должно постоянно занимать треть экрана.

---

# 42. Debug Overlay

В тестовой среде:

```text
Ping
ServerTime offset
CombatSessionId
ContentVersion
BalanceVersion
SignalR state
```

Debug overlay включается отдельно и никогда не смешивается с player UI.

---

# 43. Telegram Integration

Использовать Telegram host capabilities там, где они улучшают UX:

- theme / safe area;
- Back Button;
- haptic feedback;
- viewport changes;
- fullscreen/expanded mode, если поддерживается целевым клиентом.

Game UI всё равно остаётся собственным визуальным языком Elyndor.

---

# 44. Haptics

Короткий feedback:

```text
ability accepted
critical proc ready
item equipped
talent learned
boss phase
```

Не вибрировать на каждый обычный damage tick.

---

# 45. Reconnect UX

При потере SignalR:

```text
не выбрасывать игрока на login
```

Overlay:

```text
Соединение потеряно
Восстанавливаем...
```

После reconnect:

```text
server snapshot
→ UI reconciles state
```

Кнопки временно disabled до подтверждённого state.

---

# 46. Loading

Не показывать spinner на весь экран при каждом API request.

Использовать:

```text
skeleton
local button pending state
small status
```

Full-screen loading только:

```text
initial game boot
major session recovery
```

---

# 47. Error states

Ошибка должна объяснять gameplay-причину:

```text
Недостаточно Mana
Способность ещё восстанавливается
Цель уже мертва
Питомец побеждён
Нельзя менять билд в бою
```

Не:

```text
400 Bad Request
InvalidOperationException
```

---

# 48. Character Screen — первый implementation slice

На первом UI playtest достаточно:

```text
character artwork placeholder
real HP/Resource
real level/XP
real equipment slots
real item tooltip
real stats
```

Не ждать финального art production.

---

# 49. Combat Screen — первый implementation slice

Обязательно:

```text
player/enemy HP
resource
ability icons
cooldown
cast bar
buff/debuff icons
target change
leave combat
combat events
```

Можно временно использовать placeholder character/monster artwork.

Главное — проверить UX ритм.

---

# 50. UI component architecture

Рекомендуемые reusable Vue components:

```text
PlayerHud
ResourceBar
StatusEffectRow
GameIcon
CooldownOverlay
CastBar
CombatTargetCard
SkillBar
TargetStrip
EquipmentSlot
ItemTooltipSheet
TalentNode
TalentDetailsSheet
CompanionBadge
PartyStrip
BottomNavigation
ContextActionBar
GameBottomSheet
ConnectionOverlay
```

---

# 51. State ownership на frontend

Pinia хранит **client representation**, не game truth.

Пример:

```text
characterStore
combatStore
inventoryStore
talentStore
worldStore
partyStore
```

SignalR/API event:

```text
server state/event
→ store update
→ UI
```

UI не рассчитывает damage/cooldown результат самостоятельно.

Для cooldown он может анимировать оставшееся время по серверному `EndsAt`, но source of truth остаётся server.

---

# 52. Screen flow

```text
Telegram
  ↓
Boot
  ↓
Character exists?
  ├─ no → Character Creation
  └─ yes
       ↓
World
 ├─ Location
 │    └─ Combat
 ├─ Character
 │    ├─ Equipment
 │    ├─ Stats
 │    └─ Talents
 ├─ Inventory
 └─ More
      ├─ Quests
      ├─ Party
      ├─ Auction
      ├─ Professions
      └─ Settings
```

---

# 53. Что точно не делать

```text
длинный главный экран на 8 экранов scroll
по одной огромной карточке на каждую характеристику
desktop MMORPG hotbar на 30 slots
UI из стандартных bootstrap cards
постоянно открытый combat log
отдельная full-screen страница для каждого item tooltip
мелкие 32px equipment slots
10 bottom-nav icons
progress bars без числовых значений
```

---

# 54. Первый UX Playtest

Игроку не объясняем UI голосом.

Даём задачи:

1. найди текущую локацию;
2. начни бой;
3. используй ability;
4. пойми, почему ability сейчас нельзя нажать;
5. смени цель;
6. посмотри предмет;
7. надень предмет;
8. найди Talent Tree;
9. переключи Loadout;
10. вернись в мир.

Если человек постоянно спрашивает «куда нажать?» — меняем интерфейс, а не пишем ещё один tutorial popup.

---

# 55. Итоговый UI intent

Elyndor должен ощущаться так:

```text
LIGMAR-like readability
+
более чистая visual hierarchy
+
меньше UI chrome
+
больше мира/персонажа
+
настоящая MMORPG information density
+
Telegram-mobile ergonomics
```

Главное правило:

> Игрок всегда должен видеть игру, а не интерфейс вокруг игры.

---

# Equipment Appearance — UI rule

Экран персонажа должен показывать реальный внешний вид экипированных видимых предметов.

Приоритет визуально отображаемых слотов:

```text
MAIN_HAND
HEAD
CHEST
CLOAK
HANDS
LEGS
FEET
OFF_HAND
```

Legendary/Unique equipment может иметь собственный узнаваемый visual.

При смене предмета модель персонажа обновляется сразу после подтверждённого сервером EquipResult.

В перспективе UI допускает отдельный cosmetic/transmog selector, но gameplay equipment и displayed appearance всегда визуально различимы в данных.

## Class-specific tabs

Вкладка `Спутник` существует **только для Archer**.

```text
Warrior:
Character / Equipment / Stats / Talents / Skills

Mage:
Character / Equipment / Stats / Talents / Skills

Archer:
Character / Equipment / Stats / Talents / Skills / Companion
```

Не показывать disabled/empty вкладку Companion другим классам.

---

# Screen-by-screen UI/UX workflow

После system audit UI больше не проектируется пачкой.

Для каждого экрана:

```text
1. Assistant задаёт gameplay/UX вопросы.
2. Владелец игры отвечает.
3. Ответы фиксируются как screen decisions.
4. Создаётся mobile-first информационная архитектура.
5. Определяются states/errors/loading/reconnect.
6. Определяется navigation.
7. Создаётся детальное UI/UX ТЗ.
8. Наши generated Elyndor images используются как visual/style reference.
9. Только после утверждения экран идёт в implementation roadmap.
```

## System-backed screens now available

После документов 26–29 можно детально проектировать:

```text
Merchant
Auction
Dungeon
Profession/Crafting
Wallet/Economy
```

## Guild screen warning

Визуальный референс Guild уже существует, но Guild System rules ещё не утверждены.

Перед Guild UI specification нужно определить:
- создание гильдии;
- стоимость;
- размер;
- ranks;
- permissions;
- invitations;
- chat;
- guild bank;
- progression;
- raids/events;
- guild bonuses.

UI не должен выдумывать эти правила вместо gameplay design.

---

## Approved World / Location Specification

Authoritative:

```text
docs/source-of-truth/ui/UI_02_WORLD_AND_LOCATION.md
```

Key navigation split:

```text
МИР      → destination / route / discovery / travel
ЛОКАЦИЯ  → current-place gameplay and services
```

World Map = hybrid visual map + selected-location card.
Travel is timed and continues offline.
City services exist only inside City Location.

---

## Approved Hero Specification

Authoritative:

```text
docs/source-of-truth/ui/UI_03_HERO.md
```

Hero is character-centered.

```text
Character
Inventory
Characteristics
Talents
Companion — Archer only
```

Visual equipment rollout:

```text
Legendary/Unique
→ Epic
→ Rare
→ Uncommon/Common
```

Gear Score is display-only.

---

## Approved Inventory / Items Specification

Authoritative:

```text
docs/source-of-truth/ui/UI_04_INVENTORY_AND_ITEMS.md
```

Canonical mobile inventory:

```text
4-column grid
capacity 40
filters + sort
tap → bottom sheet
multi-select
user lock
NEW rarity treatment
```

Selling remains Merchant-context only.


---

## Complete UI Specification Set

```text
UI_01 Global Shell
UI_02 World / Location
UI_03 Hero
UI_04 Inventory / Items
UI_05 Character Stats
UI_06 Talents
UI_07 Companion
UI_08 Normal Combat
UI_09 World Boss / Raid Combat
UI_10 Party
UI_11 Quests
UI_12 City Location
UI_13 Merchant
UI_14 Auction
UI_15 Dungeon
UI_16 Crafting / Professions
UI_17 Menu
UI_18 Wallet / Economy
UI_19 Settings / System States
UI_20 Guild
```
