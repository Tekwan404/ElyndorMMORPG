# Elyndor — MASTER UI Visual Reference

**Status:** PRIMARY VISUAL CANON  
**Purpose:** единая визуальная точка отсчёта для UI/UX, frontend и генерации новых экранов.

---

# 1. Иерархия истины

При конфликте источников используется такой порядок:

```text
SYSTEM SOURCE OF TRUTH (01–31)
        ↓
UI SPECIFICATION (UI_01–UI_20)
        ↓
MASTER UI REFERENCE
        ↓
PNG VISUAL REFERENCES
```

Следовательно:

- системный документ определяет механику;
- UI document определяет структуру экрана, действия и states;
- этот документ определяет общий visual language;
- PNG показывает настроение, композицию, плотность, иконки и декоративный язык;
- случайные числа, названия, уровни и подписи на AI-reference PNG **не являются gameplay data**.

---

# 2. Основной стиль Elyndor

Целевой образ:

```text
modern dark-fantasy MMORPG
mobile-first
Telegram Mini App
deep navy / black
blue-violet magical light
restrained gold accents
bright saturated MMORPG icons
strong character / enemy / location art
semi-transparent dark panels
compact readable game HUD
```

Игра не должна выглядеть как:

```text
SaaS dashboard
generic mobile app
light flat UI
bronze antique spreadsheet
casino UI
overloaded desktop MMO shrunk to phone
```

---

# 3. Цветовой характер

Основа:

- почти чёрный;
- глубокий сине-чёрный;
- midnight blue;
- muted violet.

Акценты:

- gold — границы, rarity, важные CTA;
- blue/violet — magic;
- red — danger/Rage/enemy;
- green — positive/ready/safe statuses;
- яркие локальные цвета — внутри ability/item icons.

Gold не должен заливать весь интерфейс.

---

# 4. Панели

Панели:

- тёмные;
- слегка прозрачные;
- тонкая фактура;
- небольшие fantasy bevel/frame детали;
- достаточно воздуха;
- не закрывают полностью арт.

UI chrome служит игре, а не конкурирует с ней.

---

# 5. Иконки

Иконки — одна из главных сильных сторон выбранного стиля.

Нужно сохранять:

- detailed fantasy rendering;
- high contrast;
- readable silhouette;
- насыщенные цвета;
- rarity frame;
- magic glow only where meaningful.

Ability/item icon должен выглядеть как игровой предмет/умение, а не line-icon мобильного приложения.

---

# 6. Character / Enemy / Location Art

Главный принцип:

```text
ART > CHROME
```

Персонаж, противник, босс, спутник или локация должны занимать больше визуального внимания, чем декоративные рамки.

---

# 7. Анимации

Допустимы:

- subtle idle;
- light cloth movement;
- particles;
- fog/embers;
- small parallax;
- spell/legendary glow;
- reward reveal.

Обязательно:

```text
Settings → disable decorative animation/effects
Reduced Motion support
```

---

# 8. Typography

Нужны максимум:

```text
1 display/game fantasy font
1 UI-readable font
```

Заголовки могут быть атмосферными.

Числа, cooldown, stats, prices и small labels должны читаться мгновенно.

---

# 9. Primary references

```text
references/PRIMARY_VISUAL_CANON/01_overall_ui_direction.png
references/PRIMARY_VISUAL_CANON/02_hero_and_raid.png
references/PRIMARY_VISUAL_CANON/03_city_trade_guild.png
references/PRIMARY_VISUAL_CANON/04_raid_boss_roar.png
references/PRIMARY_VISUAL_CANON/05_raid_boss_shadow_rift.png
references/PRIMARY_VISUAL_CANON/06_inventory.png
references/PRIMARY_VISUAL_CANON/07_city_hub.png
references/PRIMARY_VISUAL_CANON/08_merchant.png
references/PRIMARY_VISUAL_CANON/09_normal_combat.png
references/PRIMARY_VISUAL_CANON/10_mage_talents.png
references/PRIMARY_VISUAL_CANON/11_guild.png
```

Сводная доска:

```text
references/00_MASTER_VISUAL_REFERENCE_BOARD.jpg
```

---

# 10. Screen → reference mapping

## Global shell / shared UI
Primary:
- 01 Overall UI
- 02 Hero / Raid
- 07 City Hub

## World / Location
Primary:
- 07 City Hub
- 03 City / Trade / Guild

World-map layout определяется `UI_02`, потому что отдельный canonical map reference ещё не создавался.

## Hero
Primary:
- 02 Hero / Raid
- 06 Inventory

## Inventory
Primary:
- 06 Inventory

## Character Stats
Structure определяется `UI_05`.

`STRUCTURE_ONLY/character_stats_structure_ONLY_not_style.png` разрешено использовать **только как layout reference**.

Его бронзово-чёрный visual language отвергнут и НЕ является стилем Elyndor.

## Talents
Primary:
- 10 Mage Talents

На production mobile одна branch должна быть крупной и читабельной.

## Companion
Общий стиль:
- 01 Overall UI
- Hero visual language

## Normal Combat
Primary:
- 09 Normal Combat

## World Boss / Raid
Primary:
- 04 Raid Roar
- 05 Raid Shadow Rift
- 02 Hero / Raid

## City
Primary:
- 07 City Hub
- 03 City / Trade / Guild

## Merchant
Primary:
- 08 Merchant

## Auction
Primary:
- 03 City / Trade / Guild
- 06 Inventory

## Guild
Primary:
- 11 Guild
- 03 City / Trade / Guild

## Dungeon
Style:
- 09 Normal Combat
- 04/05 Raid
- location visual language

## Crafting
Style:
- 08 Merchant
- 06 Inventory
- 07 City Hub

---

# 11. Forbidden reference mistakes

При создании нового UI нельзя автоматически переносить из reference PNG:

- Level 100;
- случайные валюты;
- неутверждённые Stats;
- случайный bottom nav;
- premium refresh buttons;
- несуществующие mechanics;
- персонажа Mage на экран другого класса;
- Companion tab для Warrior/Mage;
- Slow/Root/Fear;
- Block/Parry/Spirit/CastSpeed;
- любые AI-опечатки.

---

# 12. Canonical navigation

```text
МИР | ГЕРОЙ | ЛОКАЦИЯ | КВЕСТЫ | МЕНЮ
```

Во время combat bottom navigation скрывается.

City — Location, не глобальная вкладка.

---

# 13. Canonical visual equipment rule

Hero visual:

```text
Legendary / Unique first
→ Epic
→ Rare
→ lower rarities
```

Appearance персонажа должен быть консистентен во всех screens.

---

# 14. Новые визуальные референсы

Каждый новый reference должен:

1. читать соответствующий `UI_XX` документ;
2. читать связанные system documents;
3. использовать этот Master UI Reference;
4. использовать PRIMARY_VISUAL_CANON;
5. не изобретать gameplay;
6. после утверждения добавляться в `references/PRIMARY_VISUAL_CANON` либо отдельную approved screen-reference папку.

---

# 15. Итог

Если разработчику или генератору нужно понять:

> «как должен выглядеть Elyndor?»

начинать с:

```text
00_MASTER_UI_REFERENCE.md
references/00_MASTER_VISUAL_REFERENCE_BOARD.jpg
```

Если нужно понять:

> «как работает экран?»

читать соответствующий:

```text
UI_XX_*.md
```

Если нужно понять:

> «как работает механика?»

читать:

```text
01–31 Source of Truth
```
