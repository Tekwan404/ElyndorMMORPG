# Elyndor — UI/UX Specification 03
# Hero / Character Screen

**Document:** UI_03_HERO.md  
**Status:** Approved foundation  
**Platform:** Telegram Mini App  
**Orientation:** Mobile Portrait First

---

# 1. Назначение

`ГЕРОЙ` — центральный character-management экран Elyndor.

Он объединяет:

```text
ПЕРСОНАЖ
ИНВЕНТАРЬ
ХАРАКТЕРИСТИКИ
ТАЛАНТЫ
СПУТНИК — только Archer
```

Основной визуальный объект экрана — сам персонаж, а не таблицы и панели.

---

# 2. Главная композиция

Mobile-first схема:

```text
[GLOBAL HUD]

[ПЕРСОНАЖ | ИНВЕНТАРЬ | ХАРАКТЕРИСТИКИ | ТАЛАНТЫ]

      HEAD        AMULET

      CLOAK       CHEST

      HANDS       RING 1

        CHARACTER
          MODEL

      LEGS        RING 2

      FEET

 MAIN HAND       OFF HAND

Name · Level · Class
Gear Score

[context actions]
```

Для Archer в tabs дополнительно появляется `СПУТНИК`.

Companion tab у Warrior/Mage не disabled — его вообще нет.

---

# 3. Character Model

Персонаж:

- крупно по центру;
- занимает большую часть usable area;
- визуально важнее UI chrome;
- показывает race/gender/class;
- показывает текущую визуальную экипировку;
- используется как единый appearance source для остальных экранов.

Один и тот же внешний вид должен использоваться в:

```text
Hero
Party
Profile
Victory
Raid/World Boss participant preview
future social screens
```

---

# 4. Character Animation

По умолчанию допустимы лёгкие эффекты:

```text
idle breathing
subtle body movement
cloth / cloak movement
weapon glow
light particles
Legendary/Unique VFX
```

Настройки:

```text
Анимация персонажа     ON / OFF
Атмосферные эффекты    ON / OFF
```

При `OFF` модель становится статичной.

Reduced Motion имеет приоритет над декоративными анимациями.

---

# 5. Equipment Slots

Используются:

```text
HEAD
CHEST
HANDS
LEGS
FEET
CLOAK
MAIN_HAND
OFF_HAND
AMULET
RING_1
RING_2
```

Слоты располагаются вокруг модели.

Кольца и амулет остаются в основной композиции, но физически на body model не обязаны отображаться.

---

# 6. Visual Equipment

Character renderer:

```text
Base Character
+ HEAD
+ CHEST
+ HANDS
+ LEGS
+ FEET
+ CLOAK
+ MAIN_HAND
+ OFF_HAND
```

Gameplay и appearance разделены:

```text
EquippedItem
→ stats/gameplay

DisplayedAppearance
→ visual
```

---

# 7. Порядок внедрения визуального шмота

Не требуется сразу рисовать каждый Common item.

Этапы:

```text
Phase 1:
LEGENDARY / UNIQUE

Phase 2:
EPIC

Phase 3:
RARE

Phase 4:
UNCOMMON / COMMON
```

Приоритет слотов:

```text
1. MAIN_HAND
2. HEAD
3. CHEST
4. CLOAK
5. OFF_HAND
6. HANDS
7. LEGS
8. FEET
```

Это даёт максимум визуального эффекта при разумной стоимости производства контента.

---

# 8. Legendary / Unique

Legendary/Unique могут иметь:

```text
unique silhouette
unique textures
special glow
light particles
special idle effect
unique icon/frame
```

Эффекты не должны мешать читаемости интерфейса.

---

# 9. Hide Helmet / Cloak

## Helmet

```text
Hide Helmet = нет
```

Если шлем имеет appearance, он отображается.

## Cloak

```text
Показывать плащ
ON / OFF
```

При OFF:

- предмет остаётся экипирован;
- stats работают;
- set bonus работает;
- скрывается только appearance layer.

---

# 10. Gear Score

`Gear Score` разрешён.

Но:

```text
Gear Score != gameplay stat
```

Он является derived display value.

Используется для:

- общего ощущения progression;
- быстрого сравнения экипировки;
- профиля;
- party/dungeon recommendation.

Не используется напрямую в Damage/Healing/Defense формулах.

---

# 11. Hero Summary

Под персонажем:

```text
DANIL
Level 27 · Mage

Gear Score 1 248
```

Дополнительно позже:

```text
Title
Guild
```

только после появления соответствующих gameplay systems.

HP и Resource уже есть в Global HUD, поэтому второй огромный блок HP внутри Hero не нужен.

---

# 12. Equipment Slot States

Equipped:

```text
[item icon]
rarity frame
optional set marker
```

Empty:

```text
slot silhouette
```

Possible hint:

```text
▲
```

если в Inventory есть потенциально более сильный совместимый предмет.

Это hint, а не authoritative утверждение "лучший предмет".

---

# 13. Tap Equipped Item

Tap:

```text
→ Item Bottom Sheet
```

Bottom Sheet:

```text
[ICON] ITEM NAME
RARITY

Required Level
Slot
Item Type

Stats
Affixes
Set
Special Effect
Legendary Effect
Binding
Appearance

[ СНЯТЬ ]
```

Если действие запрещено — показывается причина.

---

# 14. Tap Empty Slot

Например:

```text
tap HEAD
```

Результат:

```text
→ Hero / Inventory
→ filter = HEAD compatible
```

Показываются только предметы, которые потенциально относятся к этому slot.

Authoritative equip validation остаётся на сервере.

---

# 15. Item Comparison

При выборе нового предмета обязательно показывать сравнение.

Пример:

```text
ТЕКУЩИЙ             НОВЫЙ

18 Intellect      → 30 Intellect
6 Critical        → 10 Critical
22 Stamina        → 14 Stamina
```

Delta:

```text
+12 Intellect
+4 Critical
-8 Stamina
```

Положительные/отрицательные значения визуально различаются.

Но UI не должен автоматически считать любой +stat хорошим для конкретного build.

---

# 16. Special Effects In Comparison

Нельзя свести сравнение к Gear Score.

Обязательно показывать:

```text
special effects
legendary effects
set bonuses
weapon profile changes
binding
appearance
```

---

# 17. Equip Flow

```text
Inventory item
→ [ЭКИПИРОВАТЬ]
→ server validation
→ EquipResult
→ equipment update
→ stats update
→ appearance update
```

Во время запроса:

- button disabled;
- small loader;
- без full-screen loading.

---

# 18. Equip Errors

Примеры:

```text
Недостаточный уровень
Неверный класс
Нельзя во время боя
Предмет заблокирован другой операцией
Предмет уже недоступен
```

Inline/toast.

---

# 19. Two-Handed Weapon

Если MAIN_HAND = two-handed:

```text
OFF_HAND = occupied
```

OFF_HAND не выглядит обычным пустым слотом.

Tap OFF_HAND:

```text
→ details MAIN_HAND weapon
```

---

# 20. Set Items

Set item показывает marker.

Item Details:

```text
IRON GUARDIAN

2 / 5 equipped

2p ✓
4p locked
```

---

# 21. Rarity Presentation

Rarity:

```text
COMMON
UNCOMMON
RARE
EPIC
LEGENDARY
UNIQUE
```

Должна различаться через:

```text
frame
name treatment
icon detail
subtle glow
label
```

Не только цветом.

---

# 22. New Item

Недавно полученный item может иметь:

```text
NEW
```

до первого inspect/acknowledgement.

---

# 23. Appearance Fallback

Если для предмета ещё нет индивидуального visual:

```text
generic compatible appearance
```

или class/base appearance для слота.

Отсутствие art asset не мешает Equip.

---

# 24. Transmog Ready

В будущем Hero может получить:

```text
ВНЕШНИЙ ВИД / TRANSMOG
```

Но текущая архитектура уже должна поддерживать:

```text
EquippedItemId
DisplayedAppearanceProfileId
```

без подмены gameplay предмета.

---

# 25. Hero Tabs

## Warrior

```text
Персонаж
Инвентарь
Характеристики
Таланты
```

## Mage

```text
Персонаж
Инвентарь
Характеристики
Таланты
```

## Archer

```text
Персонаж
Инвентарь
Характеристики
Таланты
Спутник
```

---

# 26. Tab Presentation

На маленьком телефоне допустим:

```text
horizontal scroll
```

или icon + short label.

Не уменьшать весь текст до нечитаемого состояния ради одновременного показа всех вкладок.

---

# 27. Remember Last Hero Tab

В течение session рекомендуется помнить последнюю Hero tab.

Пример:

```text
Hero → Talents
→ World
→ Hero
→ Talents
```

Но nested dialogs/sheets закрываются при уходе.

---

# 28. Archer Companion Presence

На Character tab Archer можно показать небольшой:

```text
[portrait] Active Companion
Predator
```

Tap:

```text
→ Companion tab
```

Но companion не должен конкурировать с главным Character Model.

---

# 29. Visual Class Identity

Один общий UI framework, но subtle class accents:

```text
Warrior → steel / rage
Archer  → nature / teal / gold
Mage    → arcane / blue / violet
```

Не строить три разных приложения для трёх классов.

---

# 30. Mobile Constraints

Reference width:

```text
360–430 CSS px
```

Основной Character tab желательно помещать примерно в один screen.

Небольшой scroll допустим, но модель и ключевые equipment slots должны быть видны сразу.

---

# 31. Back Behavior

```text
Hero
→ Item Details
← Hero

Hero
→ Inventory filtered by HEAD
← Hero / Character
```

Используется игровая кнопка Back из `UI_01`.

---

# 32. Loading

Показывать:

```text
character silhouette
equipment skeleton
summary skeleton
```

Не блокировать вкладки только потому, что тяжёлый appearance asset ещё догружается.

---

# 33. Reconnect

После reconnect:

```text
Character snapshot
Equipment snapshot
Appearance snapshot
```

пересобираются из authoritative state.

Stale appearance payload не должен возвращать старую экипировку.

---

# 34. Visual Reference

Основной reference:

```text
references/02_character_and_raid_boss.png
```

Дополнительно:

```text
references/06_inventory_mage.png
references/01_overall_ui_direction.png
```

Берём:

- крупного героя в центре;
- equipment slots вокруг;
- яркие MMO icons;
- dark fantasy;
- gold / magical frames.

AI-generated тексты и случайные цифры не являются частью ТЗ.

---

# 35. Approved Decisions

Зафиксировано:

1. Персонаж крупно по центру.
2. Equipment slots вокруг модели.
3. Idle/VFX включены по умолчанию и отключаются в Settings.
4. Уникальный visual сначала гарантируется Legendary/Unique.
5. Остальные rarity добавляются поэтапно.
6. Hide Helmet не нужен.
7. Hide Cloak нужен.
8. Gear Score используется как derived UI value.
9. Tabs: Character / Inventory / Characteristics / Talents.
10. Archer дополнительно получает Companion.
11. Accessories остаются вокруг character model.
12. Tap equipped item открывает item details.
13. Tap empty slot открывает filtered inventory.
14. Item comparison обязателен.
15. Appearance героя одинаковый во всех UI contexts.

---

# 36. Следующие Character UI Documents

```text
UI_04_INVENTORY_AND_ITEMS.md
UI_05_CHARACTER_STATS.md
UI_06_TALENTS.md
UI_07_COMPANION.md
```

`UI_07_COMPANION` существует только для Archer.
