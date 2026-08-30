# Elyndor

**Telegram Mini App MMORPG — MASTER Source of Truth v7.1**

Этот архив — текущая единая база проекта Elyndor: игровые системы, архитектура, балансные правила, UI/UX-спецификации и утверждённые визуальные референсы.

> Если открываешь проект впервые — начни с этого файла.

## Быстрый старт

Читайте в таком порядке:

```text
README.md
→ docs/source-of-truth/00_MASTER_PROJECT_INDEX.md
→ docs/source-of-truth/architecture/00_DEVELOPMENT_ROADMAP.md
→ нужный system document 01–31
→ нужный UI document UI_01–UI_20
→ docs/source-of-truth/ui/00_MASTER_UI_REFERENCE.md
→ reference/UI_*.png
```

## Что является источником истины

При конфликте документов действует строгий приоритет:

```text
01–31 SYSTEM SOURCE OF TRUTH
        ↓
UI_01–UI_20 UI/UX SPECIFICATIONS
        ↓
docs/source-of-truth/ui/00_MASTER_UI_REFERENCE.md
        ↓
PNG/JPG REFERENCES
```

То есть картинка никогда не может переопределить игровую механику. Случайные уровни, цифры, названия, валюты, кнопки и подписи на AI-референсе — только визуальный наполнитель, если они не подтверждены Markdown-документами.

## Структура проекта

### Инженерные и управляющие документы

```text
docs/source-of-truth/00_MASTER_PROJECT_INDEX.md
docs/source-of-truth/architecture/00_DEVELOPMENT_ROADMAP.md
docs/source-of-truth/architecture/00_DEVELOPMENT_STACK.md
docs/source-of-truth/architecture/00_COMPATIBILITY_MATRIX.md
docs/source-of-truth/architecture/00_CONTENT_AND_BALANCE_PROFILES.md
docs/source-of-truth/ui/00_MASTER_UI_REFERENCE.md
docs/source-of-truth/ui/00_UI_REFERENCE_INDEX.md
docs/source-of-truth/ui/00_UI_UX_CONCEPT.md
docs/source-of-truth/ui/00_UI_PACK_SUMMARY.md
docs/archive/00_FULL_AUDIT_V7_1.md
docs/archive/00_MANIFEST.md
```

### Игровые системы 01–31

```text
01  Time
02  Combat
03  AFK Farming
04  World & Locations
05  Character
06  Attributes & Stats
07  Resources
08  Effects
09  Damage & Healing
10  Abilities
11  Progression
12  Classes
13  Items & Equipment
14  Loot
15  Monster & AI
16  Talents
17  Quests
18  Bosses & World Events
19  Class Roster & Character Creation
20  Party
21  Companion & Pet
22  Warrior Talent Tree
23  Archer Talent Tree
24  Equipment Sets 5–30
25  Mage Talent Tree
26  Currency & Economy
27  Trade & Auction
28  Dungeon
29  Crafting & Professions
30  Guild
31  Raid Group
```

### UI/UX 01–20

```text
UI_01  Global Game Shell
UI_02  World & Location
UI_03  Hero
UI_04  Inventory & Items
UI_05  Character Stats
UI_06  Talents
UI_07  Companion
UI_08  Normal Combat
UI_09  World Boss / Raid Combat
UI_10  Party
UI_11  Quests
UI_12  City Location
UI_13  Merchant
UI_14  Auction
UI_15  Dungeon
UI_16  Crafting & Professions
UI_17  Menu
UI_18  Wallet & Economy
UI_19  Settings & System States
UI_20  Guild
```

## Текущий фундамент игры

```text
Level Cap             60
Playable Classes      Warrior / Archer / Mage
Future Classes        Priest / Rogue
Party                  max 5
Raid                   max 20, subgroups of 5
Guild                  default 50 members
Talent Loadouts        exactly 2
Talent Points @ 60     59
Inventory              default 40 slots
```

Ресурсы:

```text
Warrior       Rage
Archer        Focus
Arcane Archer Mana
Mage          Mana
```

Активные характеристики:

```text
Strength
Agility
Intellect
Stamina

AttackPower
SpellPower
CriticalChance
CriticalDamage
Accuracy
ArmorPenetration
MagicPenetration
AttackSpeed

Armor
MagicResistance
Dodge
```

Не использовать как активные Stats без отдельного изменения Source of Truth:

```text
Spirit
Block
Parry
CastSpeed
MovementSpeed
```

Текущий control set:

```text
STUN
SILENCE
```

## Экономика

```text
GOLD
→ основная игровая валюта
→ tradeable
→ Auction currency

CRYSTAL
→ rare/premium currency
→ может добываться игровым путём
→ non-tradeable
→ не используется на Auction
```

Telegram Stars — внешний payment rail, а не внутренняя игровая валюта.

Auction сейчас:

```text
fixed-price BUYOUT ONLY
```

## Мир и навигация

Главная нижняя навигация:

```text
МИР | ГЕРОЙ | ЛОКАЦИЯ | КВЕСТЫ | МЕНЮ
```

Во время Combat она скрывается.

Ключевое правило:

```text
МИР      → куда можно отправиться
ЛОКАЦИЯ  → что можно делать там, где персонаж находится сейчас
```

Город — это Location, а не отдельная глобальная вкладка.

Travel занимает реальное время и продолжается офлайн.

## Герой и экипировка

Character-centered UI:

```text
Персонаж
Инвентарь
Характеристики
Таланты
Спутник — только Archer
```

Визуальная экипировка внедряется поэтапно:

```text
Legendary / Unique
→ Epic
→ Rare
→ Uncommon / Common
```

Gameplay item и displayed appearance разделены архитектурно. Это оставляет фундамент под будущий Transmog.

## Таланты

```text
Warrior = 96 nodes
Archer  = 96 nodes
Mage    = 96 nodes
```

У каждого класса 3 ветки. Гибридные билды разрешены. У персонажа ровно 2 сохранённых loadout.

## Профессии

```text
Blacksmithing
Alchemy
Cooking
```

Все три текущие профессии можно развивать одним персонажем.

## Визуальный стиль

Основной документ:

```text
docs/source-of-truth/ui/00_MASTER_UI_REFERENCE.md
```

Текущие утверждённые composite boards:

```text
reference/UI_01-02_GLOBAL_SHELL_WORLD.png
...
reference/UI_19-20_SETTINGS_GUILD.png
```

Каталог `references/` из исторического v7.1 manifest был удалён и не является доступным источником в текущем checkout. Актуальное соответствие экранов и composite boards находится в `docs/source-of-truth/ui/00_UI_REFERENCE_INDEX.md`.

Ключевой visual language:

```text
modern dark fantasy MMORPG
deep navy / black
blue-violet magic light
restrained gold accents
bright detailed MMO icons
large character / enemy / location art
semi-transparent dark panels
mobile-first readability
```

## Стек

Базовое направление:

```text
.NET 10
ASP.NET Core
EF Core
PostgreSQL
SignalR
Quartz.NET
Redis only for a measured need
Vue 3 + TypeScript
Telegram Mini App
OpenTelemetry / Aspire
```

Архитектура — modular monolith. Не превращать игровые модули в микросервисы без реальной необходимости.

## Как вносить изменения

Если меняется игровая механика:

```text
1. изменить system document 01–31
2. проверить dependent systems
3. изменить dependent UI document
4. обновить compatibility / roadmap при необходимости
5. обновить visual reference только после механики
6. прогнать полный audit
```

Нельзя молча менять механику только в UI-картинке или в одном prompt.

## Что делать дальше

Текущий пакет уже содержит полный архитектурный и UI/UX foundation. Следующий рабочий цикл:

```text
UI spec
→ final visual reference
→ implementation tasks
→ implementation
→ playtest
→ refine
```

Для начала реализации используйте:

```text
docs/source-of-truth/00_MASTER_PROJECT_INDEX.md
docs/source-of-truth/architecture/00_DEVELOPMENT_ROADMAP.md
docs/source-of-truth/ui/UI_01_GLOBAL_GAME_SHELL.md
docs/source-of-truth/ui/UI_02_WORLD_AND_LOCATION.md
docs/source-of-truth/ui/UI_03_HERO.md
docs/source-of-truth/ui/UI_08_NORMAL_COMBAT.md
```

## Проверка пакета

Последний полный аудит:

```text
docs/archive/00_FULL_AUDIT_V7_1.md
```

Integrity hashes:

```text
docs/archive/00_MANIFEST.md
```

## Development

Первый компилируемый foundation находится в `src/`, `apphost/`, `web/`, `tests/` и `content/`.

Engineering foundation завершён и зафиксирован в `docs/source-of-truth/architecture/PHASE_00_ENGINEERING_FOUNDATION_IMPLEMENTATION.md`. Текущая реализация — `docs/source-of-truth/architecture/PHASE_01_TELEGRAM_IDENTITY_WORLD_IMPLEMENTATION.md`; для работы через Codex сначала читать `AGENTS.md`.

Локальный стек запускается через Aspire:

```powershell
npm ci --prefix web/elyndor-web
dotnet run --project apphost/Elyndor.AppHost
```

Static game content проверяется той же командой, которую запускает CI:

```powershell
dotnet run --project tools/Elyndor.ContentValidator -- content/package.json
```

Подробности окружения, secrets policy и команды проверки: `docs/development/getting-started.md`.
