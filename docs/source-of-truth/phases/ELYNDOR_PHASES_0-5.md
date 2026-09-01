# Elyndor MMORPG — Development Phases 0–5

> Status: Prototype Execution Plan
> Scope: From engineering foundation to the first real playable vertical slice
> Product direction: Telegram-first MMORPG
> Prototype classes: Warrior, Archer, Mage
> Validation level range: 1–10
> Role: Roadmap and navigation. Detailed implementation contracts live in the phase-specific documents in this directory.

Detailed execution contracts:

- `docs/source-of-truth/phases/PHASE_01_TELEGRAM_IDENTITY_WORLD.md`
- `docs/source-of-truth/phases/PHASE_02_CHARACTER_STATS_RESOURCES.md`

---

# Общая цель

Первые фазы должны довести Elyndor от пустого инженерного каркаса до версии, которую можно отправить другому человеку ссылкой в Telegram и дать ему пройти первый полноценный игровой цикл:

```text
Telegram
→ вход
→ создание персонажа
→ выбор класса
→ первая локация
→ бой с мобами
→ XP
→ уровень
→ лут
→ экипировка
→ elite
→ локальный босс
→ редкая награда
```

До завершения Phase 5 не расширять проект до большого количества классов, зон, профессий, аукциона, гильдий, рейдов и полноценного endgame.

Главный вопрос прототипа:

> Хочется ли игроку после одного боя провести следующий?

---

# Phase 0 — Engineering Foundation

## Цель

Создать стабильную техническую основу, на которой можно быстро разрабатывать игру без постоянной переделки инфраструктуры.

Phase 0 не должна содержать полноценный gameplay.

## Каноническая структура

```text
ElyndorMMORPG/
├── AGENTS.md
├── .agents/
│   └── skills/
│
├── src/
│   ├── Elyndor.Server/
│   ├── Elyndor.Core/
│   ├── Elyndor.Infrastructure/
│   ├── Elyndor.Contracts/
│   └── Elyndor.ServiceDefaults/
│
├── apphost/
│   └── Elyndor.AppHost/
│
├── web/
│   └── elyndor-web/
│
├── content/
├── tests/
├── references/
├── docs/
├── global.json
├── Directory.Build.props
├── Directory.Packages.props
├── .editorconfig
├── .gitignore
└── Elyndor.slnx
```

## Backend

Подготовить:

- ASP.NET Core;
- EF Core;
- PostgreSQL;
- Redis как optional/supporting infrastructure;
- Quartz;
- SignalR baseline;
- OpenAPI;
- OpenTelemetry;
- Aspire;
- health checks.

## Frontend

Создать:

- Vue 3;
- TypeScript;
- Vite;
- базовый router;
- API client;
- базовую структуру UI;
- mobile-first viewport;
- Telegram Mini App bootstrap layer.

## Data

Создать:

- DbContext;
- migrations;
- migration strategy;
- базовую схему timestamps;
- conventions для IDs;
- UTC policy;
- transaction conventions.

PostgreSQL является permanent source of truth.

Redis не должен содержать единственную копию важного игрового состояния.

## Content pipeline

Создать data-driven content loader.

Минимально поддержать:

```text
content/
├── classes/
├── abilities/
├── monsters/
├── items/
└── locations/
```

Добавить:

- schema validation;
- duplicate ID validation;
- invalid reference validation;
- startup failure при критически некорректном content.

## Testing

Подготовить:

- unit tests;
- integration tests;
- PostgreSQL integration environment;
- frontend tests;
- build verification.

## CI

Минимальный CI должен запускать backend и frontend build/tests согласно фактической конфигурации проекта.

## Definition of Done

Phase 0 завершена, когда:

- solution собирается;
- backend запускается;
- frontend запускается;
- PostgreSQL подключён;
- migration применяется;
- health endpoints работают;
- Aspire поднимает development environment;
- telemetry отображается;
- content loader работает;
- некорректный content обнаруживается;
- backend tests проходят;
- frontend checks проходят;
- secrets отсутствуют в Git;
- структура репозитория соответствует Source of Truth.

---

# Phase 1 — Telegram Identity, Character Creation & First World

## Цель

Игрок должен впервые открыть Elyndor через Telegram, создать персонажа и попасть в игровой мир.

После закрытия Mini App состояние должно сохраняться.

## Authentication flow

```text
Telegram Mini App
→ raw initData
→ Elyndor.Server
→ validation
→ Account lookup/create
→ application session
```

Backend должен проверять Telegram initData server-side.

Нельзя доверять Telegram user ID из frontend или initDataUnsafe как доказательству аутентификации.

## Account

Минимальная модель:

```text
Account
- Id
- TelegramUserId
- CreatedAt
- LastSeenAt
```

## Character creation

Игрок выбирает:

### Race
- Human
- Undead

### Gender
- Male
- Female

### Class
- Warrior
- Archer
- Mage

### Name

Сервер валидирует имя и запрещает некорректное/повторное создание через duplicate request.

## First world slice

Создать минимум:

```text
Starter Town
→ Whispering Forest
→ Deep Forest
```

Игрок должен видеть текущую локацию, доступные переходы и не иметь возможности телепортироваться произвольно через изменённый frontend request.

## Persistence

После повторного входа сервер восстанавливает account, character, class, race и текущую location.

## Definition of Done

- Telegram auth работает;
- invalid initData отклоняется;
- account создаётся idempotently;
- персонаж создаётся;
- доступны Warrior / Archer / Mage;
- первая location существует;
- travel server-authoritative;
- character state переживает повторный вход;
- basic mobile UI работает;
- frontend flow проверен в браузере.

---

# Phase 2 — Character Stats & Class Resources

## Цель

Сделать персонажа полноценной RPG-сущностью.

## Primary stats

- Strength;
- Agility;
- Intellect;
- Stamina.

## Derived stats

Примерный набор:

- Max HP;
- Attack Power;
- Spell Power;
- Armor;
- Crit Chance;
- Dodge.

## Class resources

### Warrior
`Rage`

### Archer
`Focus`

### Mage
`Mana`

## Class profiles

Каждый class profile задаёт базовые stats, resource type, resource rules, allowed weapon categories и prototype combat identity.

## Level range

Prototype validation:

```text
Level 1–10
```

## Definition of Done

- три класса имеют разные resource models;
- stats рассчитываются сервером;
- клиент не может прислать себе новые stats;
- stat pipeline покрыт tests;
- class definitions находятся в data-driven content;
- reconnect восстанавливает корректные resources/state.

---

# Phase 3A — Combat Kernel

## Цель

Создать переиспользуемый combat rules engine до появления полноценных encounters.

## Core ability model

Ability должна поддерживать:

- ID;
- class;
- resource cost;
- cooldown;
- cast time;
- target rules;
- damage/heal;
- effects;
- interruptibility;
- server-side validation.

## Class content boundary

Phase 3A реализует общий kernel и тестовые определения для deterministic harness. Production class kits не входят в Phase 3A.

## Combat design axes

```text
TIMING
RESOURCE
PRIORITY
REACTION
RISK
```

## Damage pipeline

Сервер определяет hit, crit, damage, mitigation, death и resource effects.

## Effects

Поддержать основу:

- buff;
- debuff;
- DoT;
- temporary stat modifier;
- duration;
- stacking rules.

## Time and RNG

Использовать тестируемые TimeProvider и RNG для deterministic tests и headless simulation.

## Definition of Done

- ability engine работает;
- damage считается сервером;
- cooldown/cast/resource validation работает;
- effects работают;
- invalid requests отклоняются;
- deterministic tests возможны;
- headless harness выполняет deterministic sequence без браузера и Monster System.

---

# Phase 3B — Warrior Ability Kit

## Цель

Первым production class slice подключить Warrior к общему Combat Kernel без Monster System.

## Scope

- Rage integration;
- bounded Warrior active kit из действующих Source of Truth;
- damage/healing/effects только через Phase 3A pipelines;
- deterministic dummy tests;
- server-side validation, cooldown, GCD, cast/interrupt и structured events.

## Out of scope

- talents;
- CombatSession;
- Monster System и AI;
- encounters, XP, loot и equipment rewards.

## Definition of Done

- Warrior abilities представлены versioned content;
- Rage не может быть потрачена дважды;
- distinct Warrior mechanics проходят deterministic tests;
- frontend не определяет damage, crit, resource result или effect result.

---

# Phase 3C — Talent Engine & Warrior Talent Content

## Цель

Реализовать data-driven Talent Engine, два persisted loadout и полное Warrior talent content там, где Source of Truth определён однозначно.

## Scope

- ranks, prerequisites, points, branches and typed modifiers;
- exactly two saved loadouts and one active loadout;
- atomic persistence and authoritative stat recalculation;
- Warrior talent content and Talent UI driven by server/content data;
- nodes that require Party/Boss/Elite integrations remain representable and validatable, but their owning runtime integration is deferred.

## Out of scope

- Party System;
- Boss/Elite runtime behavior;
- Monster System;
- XP mutations, loot and economy.

## Definition of Done

- talent purchase and loadout mutations are idempotent and transaction-safe;
- full defined Warrior tree loads and validates;
- supported modifiers integrate through typed hooks rather than scattered talent-id switches;
- mobile Talent UI renders real server/content state.

## Verified implementation status — 2026-09-01

- Phase 3C is implemented as the bounded Talent Engine slice.
- All 96 Warrior nodes have a typed supported modifier or an explicit deferred runtime owner.
- Exactly two PostgreSQL-backed loadouts are retained; mutations use optimistic concurrency and retry identifiers.
- Supported stat, resource, damage, effect-duration, ability-cost, cooldown, penetration, unlock, and AoE hooks use shared pipelines.
- Berserker artwork is content-driven and shared by active talent nodes and their unlocked ability presentation.
- Party, CombatSession event, Monster, Boss/Elite, equipment, XP, and loot integrations remain deferred to their owning phases.

---

# Phase 4 — CombatSession, Monsters, Monster AI & Whispering Forest

## Цель

Получить первые настоящие бои.

## Combat session

Combat server-authoritative.

Рекомендуемая модель:

```text
CombatSession = single writer
```

## Enemy content

Для первого slice:

```text
6–8 normal monsters
2 elite monsters
```

Пример Whispering Forest:

Normal:
- Wolf;
- Wild Boar;
- Forest Spider;
- Bandit;
- Lost Undead;
- Forest Predator.

Elite:
- Alpha Wolf;
- Bandit Champion.

## Monster AI

Минимально:

- target selection;
- basic attack;
- cooldown ability;
- dangerous ability;
- telegraph/cast state;
- death.

## Telegraphs

Опасные действия должны быть читаемы до resolution, если игрок может на них отреагировать.

Игрок решает:

- interrupt;
- defensive;
- burst;
- принять удар;
- сохранить ресурс.

## Reconnect

При reconnect клиент получает authoritative combat snapshot.

## Telemetry

Минимум:

- combat_started;
- combat_finished;
- combat_lost;
- ability_used;
- player_died;
- combat_duration;
- class;
- enemy type.

## Headless simulation

Пример:

```text
Warrior level 5
vs
Alpha Wolf
10 000 simulations
```

Собирать TTK, win rate, death rate, damage distribution, ability usage и resource starvation.

## Definition of Done

- Warrior реально сражается;
- Archer реально сражается;
- Mage реально сражается;
- минимум 6–8 normal mobs;
- минимум 2 elites;
- combat server-authoritative;
- reconnect работает;
- duplicate completion не создаёт duplicate rewards;
- telemetry собирается;
- headless simulation запускается;
- можно провести 10–20 минут боёв через Telegram Mini App.

---

# Phase 5 — XP, Loot, Inventory, Equipment & First Local Boss

## Цель

Создать первый настоящий RPG gameplay loop.

## Core loop

```text
kill monster
→ XP
→ loot
→ inventory
→ compare
→ equip
→ become stronger
→ stronger enemy
→ elite
→ local boss
→ rare reward
```

## XP & leveling

Поддержать Level 1–10.

XP grants должны быть server-authoritative, idempotent и transactional.

## Items

### ItemDefinition
- ID;
- name;
- rarity;
- type;
- required level;
- class restriction;
- stat modifiers;
- sell value;
- loot metadata.

### ItemInstance
- unique ID;
- definition ID;
- owner;
- created timestamp;
- mutable properties при необходимости.

## Inventory

Поддержать add/remove item, capacity, duplicate request protection и inventory full behavior.

## Equipment

Поддержать базовые slots согласно GDD.

Equip должен:

```text
validate ownership
→ validate class
→ validate level
→ update equipment
→ recalculate stats
→ commit transaction
```

## Item content

Для первого slice:

```text
15–25 meaningful items
```

## Loot tables

Разделить normal monster loot, elite loot и local boss loot.

## First Local Boss

Полноценная MMO Boss System остаётся более поздней фазой.

Phase 5 получает одного локального boss encounter, использующего текущий combat engine.

Пример:

```text
Fenrir, Corrupted Alpha
Location: Deep Forest
```

Пример mechanics:

1. Basic Attack;
2. Savage Bite — сильный casted hit;
3. Howl — временный buff/debuff;
4. Enrage на низком HP.

Boss должен проверять interrupt, defensive timing, resource conservation и burst window.

На этой фазе НЕ добавлять:

- global scheduled spawning;
- world boss participation system;
- MMO contribution scoring;
- raid groups;
- complex lockouts.

## Boss rewards

Например:

- guaranteed Uncommon+;
- шанс Rare;
- по одному build-interesting item для каждого prototype class.

Награда должна быть idempotent.

## First Real Playable Version

После Phase 5 должно быть возможно:

```text
Telegram
→ auth
→ create character
→ choose Warrior / Archer / Mage
→ Starter Town
→ Whispering Forest
→ fight normal mobs
→ earn XP
→ level up
→ get items
→ equip upgrades
→ fight elites
→ enter Deep Forest
→ defeat local boss
→ receive meaningful loot
```

## Definition of Done

- Level 1–10 реально проходим;
- все три класса играбельны;
- normal mobs существуют;
- elites существуют;
- XP работает;
- levels работают;
- inventory работает;
- equipment работает;
- 15–25 предметов существуют;
- loot tables работают;
- stats меняются от gear;
- local boss существует;
- boss имеет 2–3+ meaningful mechanics;
- boss награда защищена от duplicate grant;
- reconnect не ломает progression;
- frontend flows проверены;
- build/tests проходят;
- playtest build можно отправить другому человеку.

---

# Что НЕ делать до завершения Phase 5

- четвёртый класс;
- Level 60 content;
- полноценные guilds;
- auction house;
- professions;
- сложный crafting;
- raids;
- world boss infrastructure;
- PvP;
- огромные talent trees;
- десятки зон;
- сотни предметов;
- microservices;
- Kubernetes;
- Kafka.

---

# Development Gate после Phase 5

Перед переходом дальше проверить:

## Combat
- Хотя бы один класс действительно интересно играть?
- Три класса ощущаются по-разному?
- Игрок принимает решения, а не просто ждёт cooldown?

## Progression
- Новый уровень ощущается как прогресс?
- Новый предмет хочется сравнить/экипировать?
- Есть причина убить ещё одного моба?

## Telegram
- Игра быстро открывается?
- Можно сделать meaningful action за короткую сессию?
- Возвращение после закрытия Mini App удобно?

## Content
- Первая зона не выглядит пустой?
- Elite отличается от normal enemy?
- Local boss ощущается как кульминация зоны?

## Technical
- нет duplicate rewards;
- persistence надёжна;
- reconnect работает;
- telemetry доступна;
- build/tests стабильны.

Если core loop не работает — не маскировать проблему новым контентом.

---

# Краткая карта

```text
PHASE 0
Engineering Foundation
        ↓
PHASE 1
Telegram + Character Creation + First World
        ↓
PHASE 2
Stats + Warrior/Archer/Mage Resources
        ↓
PHASE 3
Abilities + Damage + Effects
        ↓
PHASE 4
Combat + Monsters + Elites + 3-Class Prototype
        ↓
PHASE 5
XP + Loot + Inventory + Equipment + Local Boss
        ↓
FIRST REAL PLAYABLE ELYNDOR BUILD
```
