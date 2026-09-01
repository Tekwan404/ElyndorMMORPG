# Elyndor — Unified Development Roadmap

**Document:** docs/source-of-truth/architecture/00_DEVELOPMENT_ROADMAP.md
**Status:** Engineering Source of Truth  
**Replaces:** all previous roadmap drafts  
**Stack:** ASP.NET Core .NET 10 · EF Core 10 · PostgreSQL 18 · SignalR · Quartz.NET when scheduled jobs begin · Redis only for a measured need · Vue 3 · TypeScript · Telegram Mini App
**Architecture:** Modular Monolith  
**Development model:** Build → Playtest → Refine → Expand  
**Final target:** полноценная Closed Beta с полной архитектурой игры, но функционал включается итерациями.

---

# 1. Основной принцип

Не существует отдельной «упрощённой beta-архитектуры».

Есть одна архитектура Elyndor.

Мы вводим её частями:

```text
реализовать вертикальный кусок
→ automated tests
→ локальный playtest
→ Telegram playtest
→ исправить UX/balance/bugs
→ зафиксировать документацию
→ следующий кусок
```

Каждая фаза должна заканчивать **рабочим игровым состоянием**, а не только набором backend-классов.

---

# 2. Главные правила разработки

1. Source of Truth меняется раньше gameplay-кода.
2. Backend server-authoritative.
3. Frontend развивается параллельно, а не в самом конце.
4. Content и Balance data-driven.
5. Не создавать второй engine внутри AI/Pet/Boss/Talent.
6. Не внедрять инфраструктуру «на всякий случай».
7. Каждый reward/mutation, который может повториться, проектируется idempotent.
8. Reconnect/restart behavior определяется сразу.
9. После каждой крупной фазы — реальный playtest.
10. Один PR = одна понятная задача.

---

# 3. Архитектура solution

```text
Elyndor.sln
├── src/
│   ├── Elyndor.AppHost/
│   ├── Elyndor.ServiceDefaults/
│   ├── Elyndor.Core/
│   │   ├── Modules/
│   │   │   ├── Time/
│   │   │   ├── Character/
│   │   │   ├── World/
│   │   │   ├── Stats/
│   │   │   ├── Resource/
│   │   │   ├── Damage/
│   │   │   ├── Effect/
│   │   │   ├── Ability/
│   │   │   ├── Combat/
│   │   │   ├── Progression/
│   │   │   ├── Class/
│   │   │   ├── Item/
│   │   │   ├── Loot/
│   │   │   ├── Monster/
│   │   │   ├── Talent/
│   │   │   ├── Party/
│   │   │   ├── Companion/
│   │   │   ├── Quest/
│   │   │   ├── Boss/
│   │   │   └── AFK/
│   │   └── Common/
│   ├── Elyndor.Infrastructure/
│   ├── Elyndor.Contracts/
│   └── Elyndor.Api/
├── frontend/
└── tests/
    ├── Elyndor.Core.Tests/
    ├── Elyndor.Infrastructure.Tests/
    ├── Elyndor.Api.Tests/
    └── Elyndor.E2E/
```

`Core` не зависит от EF Core / Redis / SignalR / Quartz.

`Infrastructure` реализует contracts Core.

---

# 4. Local Development

Для обычной разработки:

```text
dotnet run --project src/Elyndor.AppHost
```

Aspire оркестрирует:

```text
PostgreSQL
Elyndor.Api
Vue/Vite
OpenTelemetry
Health
```

Docker Compose не является обязательным вторым local orchestrator.

Redis is added to AppHost only when a current feature has a measured cache, presence, rate-limit, leaderboard, or scale-out requirement. Its absence does not block the foundation.

Для VPS/release environment контейнеризация настраивается отдельно.

---

# 5. Frontend baseline

Создать Vue через официальный scaffold с:

```text
Vue 3
TypeScript
Vite
Vue Router
Pinia
Vitest
Playwright
ESLint
Prettier
@microsoft/signalr
openapi-typescript
openapi-fetch
```

OpenAPI является источником TypeScript API contracts.

Не переписывать DTO руками.

---

# 6. Общая последовательность

```text
PHASE 0   Engineering Foundation
PHASE 1   Telegram Identity + Character + Time + World
PHASE 2   Stats + Resources + Content Profiles
PHASE 3A  Effect + Damage + Ability Kernel
PHASE 3B  Warrior Ability Kit
PHASE 3C  Talent Engine + Warrior Talent Content + 2 Loadouts
PHASE 4   CombatSession + Monster System + Monster AI + Whispering Forest
PHASE 5   Progression + Items + Equipment + Loot + First Local Boss
PHASE 7   Party
PHASE 8   Companion + Archer
PHASE 9   Mage
PHASE 10  Quests + World Content
PHASE 11  Bosses + World Events
PHASE 12  Dungeons
PHASE 13  Currency + Economy
PHASE 14  Trade + Auction
PHASE 15  AFK Farming
PHASE 16  Full Itemization
PHASE 17  Crafting + Professions
PHASE 18  Level 30–60 + Endgame
PHASE 19  UI/UX Polish + Content Completion
PHASE 20  Hardening + Closed Beta
```

---

# PHASE 0 — Engineering Foundation

**Execution status:** Complete. Command evidence and the final checklist are maintained in `docs/source-of-truth/architecture/PHASE_00_ENGINEERING_FOUNDATION_IMPLEMENTATION.md`.

**Ориентир:** ~1 неделя, но Done важнее срока.

## Repository

- [ ] `README.md`
- [ ] `global.json`
- [ ] `.editorconfig`
- [ ] `.gitignore`
- [ ] `Directory.Build.props`
- [ ] `Directory.Packages.props`
- [ ] solution/projects
- [ ] development secrets policy

## Git

Минимальный вариант:

```text
main
feature/*
```

Если нужен `develop`, используем его только если реально помогает workflow.

Не создавать branching ritual ради самого branching ritual.

## Backend packages

- [ ] ASP.NET Core .NET 10
- [ ] EF Core 10
- [ ] Npgsql
- [ ] SignalR
- [ ] Quartz.NET when the first durable scheduled job is implemented
- [ ] Redis client only when a measured Redis use case exists
- [ ] OpenTelemetry
- [ ] Aspire integrations
- [ ] OpenAPI

## Aspire

- [ ] PostgreSQL resource
- [ ] Redis resource only when a measured Redis use case exists; deferred by default
- [ ] API project
- [ ] frontend project
- [ ] dashboard/logging/tracing
- [ ] health checks

## CI

GitHub Actions:

- [ ] restore
- [ ] build
- [ ] `dotnet test`
- [ ] frontend install/build/test
- [ ] lint
- [ ] `dotnet ef migrations has-pending-model-changes` when the first real model/migration exists; do not create an empty migration only for this check
- [ ] content validation command

## Content loader skeleton

Создать folders:

```text
content/
├── classes/
├── abilities/
├── talents/
├── items/
├── sets/
├── monsters/
├── locations/
├── quests/
├── loot/
└── bosses/
```

- [ ] ContentVersion
- [ ] BalanceVersion
- [ ] schema validation
- [ ] duplicate ID validation
- [ ] missing reference validation

## DONE

Новый checkout запускается одной понятной командой и показывает:

```text
API healthy
PostgreSQL healthy
Frontend running
Aspire dashboard running
```

If a later phase introduces Redis for a measured need, that resource must also be healthy; Redis is not a Phase 0 prerequisite.

---

# PHASE 1 — Telegram Identity + Character + Time + World

Связанные docs:

```text
01 Time
04 World
05 Character
12 Class
19 Character Creation
00 UI/UX
```

## Telegram Bot / Mini App

- [ ] создать bot
- [ ] Mini App entry
- [ ] Telegram `initData` server validation
- [ ] signature/hash validation
- [ ] `auth_date` freshness policy
- [ ] TelegramUserId → AccountId
- [ ] application session/auth cookie или JWT policy

Bot webhook нужен только для функций, которые действительно требуют Bot API events.

Mini App login сам по себе не должен зависеть от webhook.

## Database

Первые entities:

```text
Account
Character
CharacterState
CharacterLocation
LocationDefinition reference
```

## Character Creation

- [ ] name
- [ ] Human / Undead
- [ ] Male / Female
- [ ] Warrior / Archer / Mage
- [ ] server validation
- [ ] atomic creation
- [ ] duplicate name policy

Endpoints:

```text
GET  /api/me
GET  /api/character
POST /api/character
```

## Time

- [ ] `IServerTime`
- [ ] UTC only
- [ ] absolute `EndsAt`
- [ ] no client time authority

Quartz не используется для каждого короткого combat timer.

Он нужен для durable scheduled jobs/world events.

## World

Seed:

```text
Стартовый город — SAFE
Лес — ADVENTURE
Глубокий лес — DANGEROUS
```

- [ ] travel
- [ ] recommended level
- [ ] threat
- [ ] persistent location
- [ ] reconnect/restart

Endpoints:

```text
GET  /api/world/locations
POST /api/world/travel
```

## Frontend параллельно

- [ ] boot/auth screen
- [ ] character creation
- [ ] Player HUD
- [ ] basic World screen
- [ ] bottom navigation
- [ ] reconnect overlay

## SignalR

`GameHub`:

- [ ] connection
- [ ] reconnect
- [ ] character state snapshot
- [ ] connection state

## DONE

Игрок открывает Mini App, создаёт персонажа, переходит между локациями и после повторного входа видит корректное состояние.

---

# PHASE 2 — Stats + Resources + Content Profiles

Docs:

```text
06 Stats
07 Resources
11 Progression contracts
12 Class
00 Content & Balance
```

## Stats

Реализовать только утверждённые:

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

Не вводить:

```text
Spirit
Block
Parry
CastSpeed
MovementSpeed
```

## Stat pipeline

```text
Base
→ Class
→ Equipment
→ Talent
→ Effect
→ Final
```

Runtime FinalStats держать в memory/runtime state.

Не использовать Redis как обязательный cache каждого расчёта.

## Resources

Authoritative profiles:

```text
Mana
Rage
Energy
Focus
Health
```

- [ ] clamp
- [ ] spend
- [ ] restore
- [ ] regen
- [ ] out-of-combat rules
- [ ] respawn rules
- [ ] versioned profile

## Classes

Content:

```text
WARRIOR
ARCHER
MAGE
```

- [ ] BaseStatProfile
- [ ] LevelGrowthProfile
- [ ] ResourceProfile
- [ ] AllowedWeapons
- [ ] AllowedArmor

## Frontend

- [ ] real HP/resource bar
- [ ] character stat screen
- [ ] stat tooltips

## DONE

Class/resource/stat состояние рассчитывается data-driven и одинаково восстанавливается после reload.

---

# PHASE 3A — Effect + Damage + Ability Kernel

Это главный технический фундамент боя.

## Effect

- [ ] EffectDefinition
- [ ] ActiveEffect
- [ ] SourceId
- [ ] TargetId
- [ ] duration/ticks
- [ ] snapshot/dynamic
- [ ] stacks
- [ ] refresh/replace/add/ignore
- [ ] DoT
- [ ] HoT
- [ ] Shield
- [ ] Stat Modifier
- [ ] Stun
- [ ] Silence
- [ ] Lethal Damage Prevention
- [ ] dispel categories
- [ ] boss/elite DR

Не поддерживать:

```text
Slow
Root
Fear
Charm
```

## Tick scheduling

Короткие effect ticks:

```text
runtime scheduler / BackgroundService
```

не Quartz job на каждый DoT.

После restart состояние восстанавливается из timestamps/snapshot policy.

## Damage

Pipeline:

```text
Validate
→ Hit/Miss/Dodge
→ Crit
→ Base
→ Penetration
→ Armor/MR
→ Damage modifiers
→ Minimum
→ Shields
→ Lethal Prevention
→ HP
→ Result
```

True Damage:

```text
bypass Armor/MR
does NOT bypass shield automatically
```

Shield bypass только explicit flag.

## Healing

- [ ] crit
- [ ] modifiers
- [ ] effective healing
- [ ] overhealing
- [ ] threat relevant healing

## Ability

Types:

```text
Instant
Casted
Next Attack Modifier
Taunt
```

- [ ] cooldown
- [ ] GCD
- [ ] queue
- [ ] cast
- [ ] interrupt
- [ ] school lockout
- [ ] resource validation
- [ ] target validation
- [ ] snapshot
- [ ] proc safety

## TargetTypes

```text
SELF
SINGLE_ENEMY
SINGLE_ALLY
ALL_ENEMIES_IN_COMBAT
N_ENEMIES_IN_COMBAT
SELF_AND_PARTY_MEMBERS_IN_COMBAT
ACTIVE_COMPANION
OWNER
```

## Frontend

Создать уже сейчас:

- [ ] Ability icon component
- [ ] cooldown overlay
- [ ] disabled/no-resource state
- [ ] cast bar
- [ ] effect icon row

## DONE

Ability можно выполнить полностью server-side без полноценного CombatSession UI.

---

# PHASE 3B — Warrior Ability Kit

Phase 3B starts only after the Phase 3A Definition of Done passes.

- reuse the Phase 3A kernel instead of creating Warrior-specific damage/effect engines;
- integrate authoritative Rage generation and spending;
- implement the bounded Warrior active kit from current Source of Truth;
- verify every distinct ability mechanic through the deterministic headless harness;
- do not add Monster System, production encounters, XP, loot, or talents.

## DONE

The Warrior kit executes server-side through the shared kernel and deterministic tests cover its resource, timing, damage, defense, control, and failure behavior.

---

# PHASE 3C — Talent Engine + Warrior Talent Content + 2 Loadouts

Phase 3C starts only after the Phase 3B Definition of Done passes. The detailed requirements formerly listed under Phase 6 remain authoritative, but their execution position is moved here.

- data-driven talent definitions, ranks, prerequisites, points, and modifier families;
- PostgreSQL persistence for exactly two saved loadouts and one active loadout;
- full Warrior talent content where Source of Truth is complete;
- party/boss/elite-dependent nodes may be represented and validated, but their owning runtime integrations remain deferred;
- no self-only fallback unless the owning Source of Truth explicitly allows it;
- Talent UI consumes server/content data and does not duplicate the tree in Vue.

## DONE

Warrior talents and two loadouts persist atomically, rebuild the Talent stage of authoritative stats, and modify supported kernel behavior without scattered per-talent hardcoding.

Verified 2026-09-01: the 96-node Warrior package validates, supported Phase 3 hooks execute through typed resolvers, deferred hooks declare their later runtime owner, mutation retry state is persisted in PostgreSQL, and the server-driven mobile Talent UI uses optimized Berserker art.

---

# PHASE 4 — CombatSession + Monster System + Monster AI + Whispering Forest

Docs:

```text
02 Combat
15 Monster AI
22 Warrior Tree
00 UI/UX
```

## CombatSession

- [ ] create
- [ ] participants
- [ ] target
- [ ] combat state
- [ ] end
- [ ] escape
- [ ] interrupted on unrecoverable server restart

## Auto Attack

- [ ] WeaponProfile
- [ ] weapon damage
- [ ] BaseAttackInterval
- [ ] AttackSpeed
- [ ] next attack modifiers
- [ ] cast interaction

## Threat

- [ ] per monster
- [ ] damage threat
- [ ] healing threat
- [ ] Taunt
- [ ] ForcedTarget

## Monster

Seed:

```text
6–8 Normal
2 Elite
```

## AI

- [ ] event-driven decisions
- [ ] ability conditions
- [ ] fallback autoattack
- [ ] target from threat
- [ ] decision coalescing

Никакого `Aggression radius`.

Нет spatial distance.

Encounter запускается через:

```text
Location rules
AggressionProfile
World encounter trigger
```

## Warrior integration

Reuse the completed Phase 3B Warrior kit. Phase 4 does not redesign or duplicate the kit.

Level 1–10 playtest kit:

```text
Auto Attack
Basic Strike
Rage
offensive ability
defensive ability
Provoke
```

Полное дерево ещё не обязательно включать.

## Combat UI

Первый настоящий screen:

```text
Player/Enemy HUD
character/enemy art
skill row
cast bar
effects
target strip
Leave Combat
collapsed combat log
```

## Tailscale Playtest

Собрать frontend production build.

Один origin:

```text
/
api/
hubs/game
```

ASP.NET отдаёт Mini App и API.

Публикуем HTTPS для тестеров через Tailscale Funnel.

## PLAYTEST MILESTONE 1

Игрок из Telegram:

```text
создал Warrior
→ пошёл в лес
→ начал бой
→ использовал abilities
→ убил моба
→ повторил несколько боёв
```

## DONE

10–20 минут Warrior combat loop реально играется через Telegram.

---

# PHASE 5 — Progression + Items + Equipment + Loot + First Local Boss

Docs:

```text
11 Progression
13 Items
14 Loot
24 Equipment Sets
```

## Progression

- [ ] Level
- [ ] CurrentXP
- [ ] LifetimeXP
- [ ] ExperienceGrant
- [ ] idempotency
- [ ] multi-level-up
- [ ] Level Cap 60

XP curve является `ExperienceCurveProfile`.

Runtime не hardcode'ит одну формулу.

Content сначала:

```text
Level 1–15
```

## Items

- [ ] ItemDefinition
- [ ] ItemInstance
- [ ] Inventory
- [ ] Item State
- [ ] RequiredLevel
- [ ] class requirements

Inventory initial capacity можно начать с 40, но capacity остаётся content/config value.

## Equipment slots

```text
MAIN_HAND
OFF_HAND
HEAD
CHEST
HANDS
LEGS
FEET
CLOAK
AMULET
RING_1
RING_2
```

- [ ] atomic equip
- [ ] atomic unequip
- [ ] two-hand rules
- [ ] stat invalidation
- [ ] HP clamp

## Loot

- [ ] Guaranteed
- [ ] Chance
- [ ] Weighted Group
- [ ] Personal Loot
- [ ] LootResult
- [ ] pending loot
- [ ] idempotency

## Content

Первые:

```text
10–20 предметов
несколько weapons
первые set pieces
normal/elite loot tables
```

## UI

- [ ] inventory grid
- [ ] equipment around character
- [ ] item tooltip bottom sheet
- [ ] item compare
- [ ] loot notification
- [ ] XP progress

## PLAYTEST MILESTONE 2

```text
Combat
→ XP
→ Level
→ Loot
→ Inventory
→ Equip
→ Stats
→ следующий Combat ощущается иначе
```

---

# LEGACY PHASE 6 REQUIREMENTS — MOVED TO PHASE 3C

This section is retained as the detailed checklist for Phase 3C. It is not a later execution phase and must not be implemented a second time.

Docs:

```text
16 Talent System
22 Warrior Tree
```

Current Warrior Tree:

```text
96 nodes
3 branches
59 points
```

## Talent Engine

- [ ] TalentDefinition
- [ ] ranks
- [ ] prerequisites
- [ ] required spent
- [ ] branch/tier
- [ ] stat modifiers
- [ ] resource modifiers
- [ ] ability modifiers
- [ ] effect modifiers
- [ ] event triggers
- [ ] equipment conditional
- [ ] proc safety

## Loadouts

Обязательно:

```text
LOADOUT_1
LOADOUT_2
```

- [ ] one active
- [ ] persist both
- [ ] switch outside combat
- [ ] atomic apply/remove derived effects
- [ ] cooldowns не сбрасываются
- [ ] HP/resource не восстанавливаются
- [ ] gear не меняется автоматически

## Warrior content

Полностью подключить:

```text
Страж
Берсерк
Командир
```

Commander Party effects пока могут self-only до Phase 7, но сам Party-target contract уже существует.

## UI

- [ ] Talent branch screen
- [ ] node states
- [ ] prerequisite lines
- [ ] 2 loadout tabs
- [ ] point counter
- [ ] talent details sheet

## DONE

Warrior реально меняет игровой стиль через talents.

---

# PHASE 7 — Party

Docs:

```text
20 Party
```

## Party entity

```text
MaxSize = 5
```

- [ ] Create
- [ ] Invite
- [ ] Accept
- [ ] Leave
- [ ] Kick
- [ ] Leader
- [ ] transfer leadership
- [ ] offline member
- [ ] disband

## Combat integration

```text
Party != CombatSession
```

- [ ] Party Ally
- [ ] Party effect targeting
- [ ] group XP participation
- [ ] reward eligibility context
- [ ] Commander effects

## UI

- [ ] party sheet
- [ ] invite
- [ ] party strip in combat

## DONE

Два и более реальных Telegram account играют вместе и получают корректные Commander effects/rewards.

---

# PHASE 8 — Companion + Archer

Docs:

```text
21 Companion
23 Archer Tree
```

## Companion engine

- [ ] CompanionDefinition
- [ ] CompanionInstance
- [ ] owner
- [ ] collection
- [ ] active companion
- [ ] stats scaling
- [ ] HP
- [ ] combat participation
- [ ] threat
- [ ] effects
- [ ] abilities
- [ ] AI
- [ ] DEFEATED
- [ ] recovery
- [ ] persistence

Physical pets:

```text
PREDATOR
GUARDIAN
TRAPPER
```

Spirit:

```text
SPIRIT_PET
```

## Archer

Base:

```text
Bow
Focus
physical companion
```

Branches:

```text
Меткая стрельба
Повелитель зверей
Тайный стрелок
```

Arcane Archer:

```text
Focus → Mana
Agility scaling → Intellect
Physical Pet → Spirit Pet
```

## UI

- [ ] companion badge
- [ ] pet selection
- [ ] pet defeated/recovery
- [ ] Focus/Mana resource profile swap
- [ ] Archer proc state

## DONE

Три Archer builds принципиально отличаются в реальном combat loop.

---

# PHASE 9 — Mage

Docs:

```text
25 Mage Talent Tree
00 UI/UX
```

## Base Mage

```text
Fireball
Arcane Spark
Ice Shard
Mana
Staff/Wand
```

## Branches

```text
🔥 Пламя
🔮 Тайная магия
❄️ Лёд
```

## Fire must include

`Предел Жара`:

```text
3 consecutive critical Fireballs
→ HEAT_LIMIT
→ Огненная Комета
→ 0 Mana
→ 0.5 sec Cast
```

## Arcane

```text
ARCANE_CHARGE
Arcane Burst
Mana sequencing
```

## Frost

```text
FROSTBITE
Ice Lance
moderate shield
Stun/Silence
AttackSpeed/Accuracy pressure
```

No:

```text
Slow
Root
Fear
```

## UI

Mage-specific combat state:

```text
Fire crit markers
Arcane Charges
Frostbite stacks
```

## PLAYTEST MILESTONE 3 — Class Complete

```text
Warrior
Archer
Mage

Talents
2 Loadouts
Party
Companion
Gear
Combat
```

На этом milestone уже имеет смысл серьёзно сравнивать классовый баланс.

---

# PHASE 10 — Quests + World Content

Docs:

```text
17 Quest
04 World
```

## Quest engine

- [ ] QuestDefinition
- [ ] CharacterQuest
- [ ] state machine
- [ ] objective progress
- [ ] idempotent event processing
- [ ] reward claim

Objective types:

```text
KILL
COLLECT
VISIT
INTERACT
USE_ABILITY
REACH_LEVEL
DEFEAT_BOSS
COMPLETE_EVENT
```

## Party policy

Shareable:

```text
Kill
Boss
World Event
```

Personal by default:

```text
Collect
Use
Dialogue/Interact
```

## Content

- [ ] 10–15 starter quests
- [ ] 1–2 chains
- [ ] Level 1–10 complete route
- [ ] extend to 15–20

## UI

- [ ] available/active
- [ ] tracked objective max 2 on world
- [ ] claim
- [ ] quest chain

## DONE

Новый игрок может прогрессировать по понятной цепочке без developer commands.

---

# PHASE 11 — Bosses + World Events

Docs:

```text
18 Boss & World Event
14 Loot
20 Party
```

## Boss

- [ ] definition
- [ ] instance
- [ ] atomic activation
- [ ] phases
- [ ] wipe
- [ ] cooldown
- [ ] participation timeline
- [ ] CompletionId

## ParticipationPolicy

Считать:

```text
time
qualifying actions
damage
effective healing
support
tanking/threat
```

Presence only не даёт reward.

## Restart

```text
ACTIVE
→ server process loss
→ FAILED
→ no reward
→ recovery
```

## First content

```text
1 regional boss
1 scheduled world event
```

Потом второй boss.

## Admin

- [ ] spawn/reset boss
- [ ] event start
- [ ] XP grant
- [ ] item grant
- [ ] character reset for tests
- [ ] Telegram whitelist/admin role

## DONE

Группа 1–5 может пройти boss, а late join/healer/support reward работают корректно.

---

# PHASE 12 — Dungeon

До coding создать/утвердить:

```text
docs/source-of-truth/gameplay/28_DUNGEON_SYSTEM.md
```

## First Dungeon

```text
Party enters
→ isolated instance
→ encounter 1
→ encounter 2
→ elite
→ boss
→ completion
→ reward
→ exit
```

Dungeon использует существующие:

```text
World
Party
Combat
Monster
Boss
Loot
Quest
```

Не создаёт отдельные версии этих систем.

## PLAYTEST MILESTONE 4 — MMORPG Group Loop

```text
Quest
→ Party
→ Dungeon
→ Boss
→ Loot
→ Talent/Gear upgrade
→ следующий content
```

---

# PHASE 13 — Currency + Economy

Design Source of Truth готов:

```text
docs/source-of-truth/gameplay/26_CURRENCY_AND_ECONOMY_SYSTEM.md
```

## Wallet

- [ ] CurrencyDefinition
- [ ] Wallet
- [ ] CurrencyGrant
- [ ] CurrencySpend
- [ ] idempotency
- [ ] audit

## Basic currency

```text
Gold
```

## Sources

```text
quests
mobs
bosses
selling
events
```

## Sinks

Только реальные:

```text
NPC items
services
respec, если выбран такой economy rule
crafting costs
auction fees later
```

## UI

- [ ] wallet
- [ ] NPC buy/sell

## DONE

Gold уже имеет смысл получать и тратить.

---

# PHASE 14 — Trade + Auction

Design Source of Truth готов:

```text
docs/source-of-truth/gameplay/27_TRADE_AND_AUCTION_SYSTEM.md
```

## Direct Trade

- [ ] participant validation
- [ ] item ownership
- [ ] currency
- [ ] both confirm
- [ ] atomic commit
- [ ] cancel/reconnect

## Auction

- [ ] listing
- [ ] duration
- [ ] price
- [ ] buy
- [ ] expiration
- [ ] seller payout
- [ ] fee
- [ ] search/filter

## Security

Race condition/reconnect не должны:

```text
duplicate item
duplicate gold
double-buy listing
```

---

# PHASE 15 — AFK Farming

AFK специально идёт после нормальной progression/economy.

Тогда можно сравнить:

```text
active XP/hour
active loot/hour
AFK XP/hour
AFK loot/hour
Gold/hour
```

## AFK

- [ ] session
- [ ] allowed location
- [ ] explicit start
- [ ] EndsAt
- [ ] reward snapshot/profile
- [ ] offline calculation
- [ ] claim
- [ ] cap policy

AFK:

```text
не real combat simulation
не boss farm
не legendary/unique primary source
не automatic quest completion
```

## DONE

AFK полезен, но активная игра остаётся главным способом развития.

---

# PHASE 16 — Full Itemization

Engine fields уже существуют раньше.

Здесь включаем полноценный content generation.

## Order

```text
Fixed Rare
→ Affixed Rare
→ Epic
→ Legendary
→ Unique
```

## Needed profiles

```text
ITEM_STAT_BUDGET
AFFIX_BUDGET
RARITY_BUDGET
REWARD_TIER
```

## SetDefinition

- [ ] 2-piece
- [ ] 4-piece
- [ ] data-driven effects

## Rules

Обычный gear не содержит:

```text
Pet Damage +X%
Pet Crit +X%
Pet AttackSpeed +X%
```

Pet scales through owner stats/system.

## PLAYTEST MILESTONE 5 — Economy + Item Chase

```text
loot
trade
auction
affixes
legendary
unique
build optimization
```

---

# PHASE 17 — Crafting + Professions

Design Source of Truth готов:

```text
docs/source-of-truth/gameplay/29_CRAFTING_AND_PROFESSION_SYSTEM.md
```

Initial professions:

```text
Blacksmith
Alchemy
Cooking
```

## Crafting

- [ ] ProfessionState
- [ ] profession level
- [ ] recipes
- [ ] ingredients
- [ ] crafting result
- [ ] idempotency
- [ ] market integration

Crafting не должен существовать отдельно от Economy.

---

# PHASE 18 — Level 30–60 + Endgame

На этом этапе engine уже не должен требовать фундаментальной переделки.

Основная работа:

```text
content
balance
quests
locations
monsters
dungeons
bosses
items
sets
legendary
unique
economy
```

Content brackets:

```text
30–35
35–40
40–45
45–50
50–55
55–60
```

## Level 60 loop

Нужны:

```text
endgame dungeons
world bosses
rare chase
legendary/unique chase
auction economy
professions
repeatable activities
build optimization
```

---

# PHASE 19 — UI/UX Polish + Content Completion

UI создаётся с Phase 1.

Здесь не «начинаем frontend», а доводим его.

Используем:

```text
docs/source-of-truth/ui/00_UI_UX_CONCEPT.md
```

## Character

- [ ] final gear composition
- [ ] stat hierarchy
- [ ] item compare
- [ ] character artwork integration

## Combat

- [ ] final skill ergonomics
- [ ] proc readability
- [ ] target strip
- [ ] party strip
- [ ] companion badge
- [ ] boss states
- [ ] damage number noise reduction

## Telegram

- [ ] safe areas
- [ ] BackButton
- [ ] haptics
- [ ] viewport/resizing
- [ ] loading/reconnect
- [ ] mobile device matrix

## UX playtests

Дать задачи без подсказок:

```text
start combat
use skill
equip item
find talent
switch loadout
create party
select pet
accept quest
enter dungeon
buy/sell
```

---

# PHASE 20 — Hardening + Full Closed Beta

## Infrastructure

Для постоянной закрытой beta перейти с local Funnel на нормальный deployment.

Пример:

```text
VPS
Docker
ASP.NET API + static Mini App
PostgreSQL
Redis
HTTPS reverse proxy
```

Tailscale остаётся для admin/private infrastructure при необходимости.

## Database

- [ ] daily backups
- [ ] restore test
- [ ] migration policy
- [ ] indexes
- [ ] query profiling

## Backend

- [ ] concurrency
- [ ] idempotency
- [ ] transaction boundaries
- [ ] SignalR reconnect
- [ ] Redis outage behavior
- [ ] Quartz duplicate prevention
- [ ] restart
- [ ] outbox recovery
- [ ] auth abuse
- [ ] rate limits
- [ ] content patch validation

## Load testing

- [ ] 10 concurrent
- [ ] 50 concurrent
- [ ] 100+ as target grows
- [ ] SignalR connections
- [ ] multiple CombatSessions
- [ ] world boss
- [ ] auction contention
- [ ] reward resolution

## Observability

- [ ] logs
- [ ] traces
- [ ] metrics
- [ ] alerts
- [ ] admin audit
- [ ] ContentVersion
- [ ] BalanceVersion

## Player analytics

Useful:

```text
class distribution
level time
death rate
ability usage
talent distribution
quest completion
boss wipe
dungeon completion
loot acquisition
auction volume
AFK usage
retention
```

---

# 7. Playtest milestones

## MILESTONE A — First Combat

После Phase 4:

```text
Telegram
Warrior
World
Combat
```

Вопрос:

> Сам бой вообще приятный?

---

## MILESTONE B — Progression Loop

После Phase 5:

```text
Combat
XP
Loot
Equipment
```

Вопрос:

> Хочется ли ещё один бой ради развития?

---

## MILESTONE C — Build Loop

После Phase 9:

```text
Warrior
Archer
Mage
Talents
2 Loadouts
Party
Companion
```

Вопрос:

> Классы и билды реально ощущаются по-разному?

---

## MILESTONE D — MMORPG Loop

После Phase 12:

```text
Quest
Party
Dungeon
Boss
Loot
```

Вопрос:

> Есть ли ощущение совместной MMORPG, а не одиночного кликера?

---

## MILESTONE E — Long-term Loop

После Phase 16:

```text
Economy
Trade
Auction
AFK
Affixes
Legendary
Unique
```

Вопрос:

> Есть ли причина возвращаться и фармить?

---

## MILESTONE F — Full Closed Beta

После Phase 20.

---

# 8. Frontend development rule

Frontend задача присутствует в каждой gameplay phase.

Не допускается:

```text
backend feature ready
но её нельзя нормально использовать из Mini App
```

Definition of Done gameplay-feature включает:

```text
backend
frontend
persistence
reconnect
error state
tests
documentation
```

---

# 9. Definition of Done

## Server

- [ ] server authoritative
- [ ] validation
- [ ] permissions/eligibility
- [ ] persistence
- [ ] concurrency policy
- [ ] idempotency when required
- [ ] restart/reconnect behavior
- [ ] structured error

## Frontend

- [ ] normal
- [ ] loading
- [ ] disabled
- [ ] error
- [ ] reconnect
- [ ] Telegram mobile layout

## Content

- [ ] IDs validated
- [ ] references valid
- [ ] ContentVersion
- [ ] BalanceVersion
- [ ] no gameplay constants hidden in UI

## Tests

Минимально:

```text
unit test critical rule
integration test DB/mutation
E2E critical player flow
```

---

# 10. Testing pyramid

Не тестировать каждую строку ради coverage.

Приоритет:

1. экономические/предметные mutations;
2. combat formulas;
3. talent prerequisite;
4. resource/cooldown;
5. reward idempotency;
6. reconnect/restart;
7. Party/Companion ownership;
8. Telegram auth.

E2E нужны для:

```text
Character Creation
Combat
Equip
Talent
Party
Quest
Dungeon
Auction purchase
```

---

# 11. Admin/debug tooling

Начать рано, а не после release.

Нужны команды/endpoint с admin permission:

```text
grant XP
set Level in test environment
grant Item
teleport Location
spawn Monster
spawn Boss
reset Boss
start Event
reset Character
inspect CombatSession
inspect active Effects
inspect ContentVersion
```

Все admin actions audit log.

---

# 12. Git tasks

Feature names:

```text
feature/telegram-auth
feature/character-creation
feature/stats-pipeline
feature/damage-resolver
feature/ability-casting
feature/combat-session
feature/warrior-rage
feature/item-equip
feature/talent-loadouts
feature/party
feature/archer-companion
feature/mage-fire
```

Не использовать giant task:

```text
implement-combat
```

---

# 13. Первые задачи буквально по порядку

```text
001 Solution + central packages
002 Aspire AppHost
003 PostgreSQL
004 Redis need assessment — deferred by default
005 OpenTelemetry + health
006 Vue scaffold
007 CI
008 Content loader
009 Telegram initData validator
010 Account persistence
011 Character creation
012 World location persistence
013 Basic HUD
014 Stats pipeline
015 Resource state
016 Effect definitions/runtime
017 Damage resolver
018 Ability validation
019 Cast/GCD/cooldown
020 Auto Attack
021 CombatSession
022 Threat
023 Monster AI
024 Warrior Rage
025 Warrior starter abilities
026 Combat UI
027 Tailscale Telegram test
028 XP progression
029 Inventory
030 Equipment
031 Loot
032 Item UI
033 Warrior Talents
034 Dual Loadouts
035 Party
036 Companion engine
037 Archer
038 Mage
039 Quest
040 Boss
041 Dungeon
```

После этого economy layers.

---

# 14. Что специально НЕ добавлять пока нет проблемы

```text
Kafka
RabbitMQ
MassTransit
microservices
Kubernetes
generic repository поверх EF Core
AutoMapper по умолчанию
MediatR в каждом handler
event sourcing всей игры
GraphQL
distributed locks везде
Redis cache для каждого FinalStats
```

Это можно добавить позже, если появится измеримая причина.

---

# 15. Оценки сроков

Сроки в исходном roadmap полезны только как rough planning.

Не считать:

```text
14–16 недель = обещание полной игры
```

Работа считается по milestone.

Можно вести:

```text
planned
in progress
playtest
needs refinement
done
```

для каждой Phase.

---

# 16. Что считать первым закрытым тестом

**First Closed Playtest** можно запустить после Phase 5.

Есть:

```text
Telegram
Character
World
Warrior
Combat
XP
Loot
Inventory
Equipment
```

Это уже полезный тест gameplay.

---

# 17. Когда три класса считаются готовыми

После Phase 9:

```text
Warrior
Archer
Mage
```

должны иметь:

- базовый class kit;
- работающий ресурс;
- рабочий gear;
- полное talent tree;
- два loadout;
- combat UI;
- playtest.

---

# 18. Full Closed Beta Definition

Полная закрытая beta должна иметь не просто «по одной кнопке от каждой системы».

Минимально:

```text
✅ Telegram auth
✅ Character creation
✅ Warrior / Archer / Mage
✅ Level 1–60 architecture
✅ meaningful playable content range
✅ Combat
✅ Talents + 2 loadouts
✅ Party
✅ Companion
✅ Quests
✅ Bosses
✅ World Events
✅ Dungeon
✅ XP
✅ Items
✅ Sets
✅ Affixes
✅ Legendary / Unique
✅ Currency
✅ Economy
✅ Trade
✅ Auction
✅ AFK
✅ Professions / Crafting
✅ restart safety
✅ monitoring/backups
```

Функции появляются и тестируются до этой точки последовательно.

---

# 19. Design documents status

Текущий Source of Truth содержит:

```text
25_MAGE_TALENT_TREE — готов
26_CURRENCY_AND_ECONOMY_SYSTEM — готов
27_TRADE_AND_AUCTION_SYSTEM — готов
28_DUNGEON_SYSTEM — готов
29_CRAFTING_AND_PROFESSION_SYSTEM — готов
30_GUILD_SYSTEM — готов
31_RAID_GROUP_SYSTEM — готов
```

System-design coverage 01–31 завершён для текущего foundation.


# 20. Итог

Этот roadmap объединяет:

- конкретные чеклисты предыдущего roadmap;
- vertical-slice подход второго roadmap;
- исправленный dependency order;
- frontend параллельно gameplay;
- ранние Telegram playtests;
- полноценные Archer/Mage;
- Party до group content;
- Economy до балансировки AFK;
- Dungeon/Trade/Crafting как обязательные части полной игры.

Главный критерий:

> На каждом этапе Elyndor должен становиться более интересной игрой, а не только более большой кодовой базой.

---

# 21. UI/UX specification status

Полный mobile-first UI/UX pack создан:

```text
UI_01_GLOBAL_GAME_SHELL
UI_02_WORLD_AND_LOCATION
UI_03_HERO
UI_04_INVENTORY_AND_ITEMS
UI_05_CHARACTER_STATS
UI_06_TALENTS
UI_07_COMPANION
UI_08_NORMAL_COMBAT
UI_09_WORLD_BOSS_COMBAT
UI_10_PARTY
UI_11_QUESTS
UI_12_CITY_LOCATION
UI_13_MERCHANT
UI_14_AUCTION
UI_15_DUNGEON
UI_16_CRAFTING_AND_PROFESSIONS
UI_17_MENU
UI_18_WALLET_AND_ECONOMY
UI_19_SETTINGS_AND_SYSTEM_STATES
UI_20_GUILD
```

Следующий UI-цикл:

```text
UI specification
→ final approved visual reference
→ implementation tasks
→ implementation
→ playtest
→ refinement
```

Visual implementation follows `docs/source-of-truth/ui/00_MASTER_UI_REFERENCE.md`, `docs/source-of-truth/ui/00_UI_REFERENCE_INDEX.md`, and the current boards in `reference/`.


---

# UI/UX Design Pack Status

`UI_01–UI_20` are complete for the current foundation. Implementation must validate each screen against its UI specification, linked system documents and the MASTER visual canon.
