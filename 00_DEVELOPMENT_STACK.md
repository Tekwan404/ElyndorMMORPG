# Elyndor — Development Stack & Runtime Architecture

**Document:** 00_DEVELOPMENT_STACK.md  
**Status:** Engineering Source of Truth  
**Target:** Telegram Mini App MMORPG  
**Architecture:** Modular Monolith first, horizontal scale later

---

# 1. Итоговый стек

```text
Backend Runtime:       ASP.NET Core / .NET 10 LTS
ORM:                   Entity Framework Core 10
PostgreSQL Provider:   Npgsql.EntityFrameworkCore.PostgreSQL 10.x
Database:              PostgreSQL 18.x
Realtime:              ASP.NET Core SignalR
Scheduled Jobs:        Quartz.NET 3.x
In-process Queue:      System.Threading.Channels
Durable Events:        PostgreSQL Transactional Outbox
Cache:                 Redis + StackExchange.Redis
Local Orchestration:   Aspire AppHost
Observability:         OpenTelemetry + Aspire Dashboard
Frontend:              Vue 3 + TypeScript + Vite
Node.js:               Node.js 24 LTS
Frontend State:        Pinia
Frontend Routing:      Vue Router
Realtime Client:       @microsoft/signalr
Platform:              Telegram Mini App
Local Public Testing:  Tailscale Funnel
Private Testing:       Tailscale Serve
```

Главное архитектурное решение:

> **Не начинаем с микросервисов.** Elyndor строится как модульный монолит с очень чёткими system ownership boundaries из игровых документов.

Это даёт:
- одну кодовую базу;
- простые транзакции;
- простой debug;
- один deployment;
- отсутствие network/queue complexity между Combat, Items, Talents и Character;
- возможность позже вынести отдельный модуль в service, если появится реальная причина.

---

# 2. Что меняется в исходном предложении

Исходная идея была:

```text
Backend:    ASP.NET Core (.NET 10)
ORM:        Entity Framework Core
DB:         PostgreSQL
Realtime:   SignalR
Queue/Jobs: Quartz.NET
Cache:      Redis
Frontend:   Vue 3 + TypeScript
Platform:   Telegram Mini App
```

Она правильная, но есть одна важная поправка:

```text
Quartz.NET != Queue
```

Quartz отвечает за **расписание и durable scheduled jobs**.

Для игровых команд и внутренних событий используем:

```text
System.Threading.Channels
+
BackgroundService
+
PostgreSQL Transactional Outbox для событий, которые нельзя потерять
```

RabbitMQ/Kafka/MassTransit на первом сервере **не нужны**.

Если Elyndor позже разделится на несколько процессов/services, outbox уже даст нормальную точку перехода на message broker.

---

# 3. Версии платформы

## .NET

Использовать:

```text
.NET 10 LTS
SDK line: 10.0.x
```

На дату этой ревизии официальный .NET download/support policy показывает runtime **10.0.10** и SDK **10.0.302**. В repository должен быть `global.json`, а patch обновляется регулярно после CI/test pass — major line остаётся .NET 10 LTS.

## EF Core

```text
EF Core 10.x
```

EF Core 10 требует .NET 10 и является LTS-линейкой.

## PostgreSQL

```text
PostgreSQL 18.x
```

На дату ревизии current minor — **18.4**. Docker image фиксируется на конкретный minor/tag, затем обновляется осознанно после backup/migration/integration tests. PostgreSQL рекомендует использовать актуальный minor своего major.

## Npgsql

```text
Npgsql.EntityFrameworkCore.PostgreSQL 10.x
```

Major provider должен соответствовать EF Core 10.

## Quartz.NET

Использовать **стабильную Quartz.NET 3.x line** и обновлять patch централизованно. Quartz используется только как durable scheduler; game-loop timers на нём не строятся.

---

# 3.1 Developer workstation

Минимум для Windows/macOS/Linux разработчика:

```text
.NET 10 SDK
Node.js 24 LTS + npm
Git
Docker Desktop / Docker Engine (или другой Aspire-compatible container runtime)
Aspire CLI/tooling
Tailscale — только если нужен remote Telegram test
```

Не требовать локально установленный PostgreSQL/Redis: их поднимает Aspire containers. Это уменьшает различия между машинами разработчиков.

Для проверки окружения использовать `aspire doctor` и обычные `dotnet --info` / `node --version`.

---

# 4. Solution structure

Рекомендуемая структура репозитория:

```text
/elyndor
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
│   ├── classes/
│   ├── abilities/
│   ├── talents/
│   ├── items/
│   ├── sets/
│   ├── monsters/
│   ├── locations/
│   ├── quests/
│   ├── loot/
│   ├── bosses/
│   ├── economy/
│   ├── merchants/
│   ├── auctions/
│   ├── dungeons/
│   ├── professions/
│   └── recipes/
│
├── tests/
│   ├── Elyndor.UnitTests/
│   ├── Elyndor.IntegrationTests/
│   └── web-e2e/
│
├── docs/
│   └── Source of Truth markdown files
│
├── global.json
├── Directory.Build.props
├── Directory.Packages.props
├── .editorconfig
└── Elyndor.slnx
```

Не делать отдельный `.csproj` на каждую игровую систему.

`01–21` и `26–29` описывают gameplay/domain modules, а `22–25` — class/item content.

Это не отдельные микросервисы и не отдельные assemblies.

---

# 5. Responsibilities проектов

## Elyndor.Core

Чистая игровая логика.

Содержит namespaces/modules:

```text
Time
Combat
Afk
World
Characters
Stats
Resources
Effects
Damage
Abilities
Progression
Classes
Items
Loot
Monsters
Talents
Quests
Bosses
Party
Companions
Economy
Trade
Auction
Dungeons
Crafting
Professions
Guilds
Raids
```

Core не должен зависеть от:
- EF Core;
- PostgreSQL;
- Redis;
- SignalR;
- Telegram;
- ASP.NET HTTP.

Core получает interfaces и domain commands/events.

## Elyndor.Infrastructure

Реализует:
- EF Core repositories;
- PostgreSQL persistence;
- Redis adapters;
- Quartz jobs;
- transactional outbox;
- Telegram authentication validation helpers;
- external HTTP integrations.

## Elyndor.Contracts

Содержит только transport contracts:
- HTTP request/response DTO;
- SignalR command/event DTO;
- error codes;
- contract versions.

Не содержит game rules.

## Elyndor.Server

Composition root:
- ASP.NET Core;
- auth;
- HTTP endpoints;
- SignalR hubs;
- DI registration;
- rate limiting;
- OpenAPI;
- health endpoints;
- hosted workers.

## Elyndor.ServiceDefaults

Общие dev/runtime defaults:
- OpenTelemetry;
- health checks;
- resilience;
- service discovery where useful.

## Elyndor.AppHost

Только local orchestration:
- PostgreSQL;
- Redis;
- Server;
- Vue/Vite;
- environment wiring;
- dashboard.

AppHost не содержит game logic.

---

# 6. Backend libraries

## Required

```text
Microsoft.EntityFrameworkCore
Microsoft.EntityFrameworkCore.Design                # dev tooling
Npgsql.EntityFrameworkCore.PostgreSQL
Microsoft.AspNetCore.Authentication.JwtBearer
Microsoft.AspNetCore.OpenApi
Microsoft.Extensions.Validation
Microsoft.Extensions.Http.Resilience                  # external HTTP/Bot API resilience
Microsoft.Extensions.ServiceDiscovery                  # Aspire/service endpoint wiring
StackExchange.Redis
Quartz
Quartz.Extensions.DependencyInjection
Quartz.Extensions.Hosting
Quartz.Serialization.SystemTextJson
OpenTelemetry.Extensions.Hosting
OpenTelemetry.Exporter.OpenTelemetryProtocol
OpenTelemetry.Instrumentation.AspNetCore
OpenTelemetry.Instrumentation.Http
OpenTelemetry.Instrumentation.Runtime
```

Часть ASP.NET Core functionality приходит из shared framework и не требует отдельного NuGet package.

## Aspire / AppHost

Использовать актуальную **stable Aspire 13.x line**, совместимую с .NET 10, и фиксировать точные package versions централизованно в `Directory.Packages.props`. Обновление Aspire выполняется отдельным dependency PR после запуска integration tests.

AppHost packages:

```text
Aspire.Hosting.AppHost
Aspire.Hosting.PostgreSQL
Aspire.Hosting.Redis
Aspire.Hosting.JavaScript
```

`Aspire.Hosting.JavaScript` даёт `AddViteApp`. Не добавлять второй `WithHttpEndpoint` к Vite resource, если используемый Aspire API уже создаёт endpoint автоматически.

## Testing

```text
xunit
Microsoft.NET.Test.Sdk
Microsoft.AspNetCore.Mvc.Testing
Microsoft.Extensions.TimeProvider.Testing
Testcontainers.PostgreSql
Testcontainers.Redis
```

Integration tests используют Testcontainers PostgreSQL/Redis там, где нужен реальный provider/runtime behavior; pure domain tests инфраструктуру не поднимают.

---

# 7. Библиотеки, которые специально НЕ подключаем сейчас

## MediatR

Не нужен.

Вместо:

```text
HTTP/Hub endpoint
→ application service / command handler
→ Core
```

Без magic pipeline и дополнительной абстракции поверх простых команд.

## AutoMapper

Не нужен.

Contracts маппятся явными functions/extensions. Для игры это облегчает поиск ошибок в сложных state DTO.

## FluentValidation

По умолчанию не нужен.

.NET 10 имеет отдельную validation infrastructure; сложная domain validation всё равно принадлежит Core.

## Hangfire

Не нужен, потому что Quartz уже покрывает scheduled jobs.

## RabbitMQ / Kafka / MassTransit

Не нужны до появления нескольких independently deployed consumers/services.

## MongoDB

Не нужен. PostgreSQL покрывает persistent state, JSONB и transactional guarantees.

## Elasticsearch

Не нужен на текущем этапе.

## Tailwind / тяжёлый UI kit

Не брать по умолчанию. Elyndor требует собственного MMORPG visual language, а не UI административной панели.

---

# 7.1 Package version strategy

- `Directory.Packages.props` — один источник NuGet versions.
- `package-lock.json` коммитится; CI использует `npm ci`.
- Не использовать floating `*` versions.
- Major upgrades — отдельный PR + migration/breaking-change review.
- Patch/minor dependency updates — регулярно, но только после automated tests.
- Можно подключить Dependabot/Renovate позже; он не должен автоматически merge major updates.

---

# 8. PostgreSQL — Source of Truth

PostgreSQL является authoritative persistence.

Там хранятся:
- Account;
- Character;
- identity;
- progression;
- inventory/item instances;
- equipment;
- talent loadouts;
- quests;
- party persistent state, если нужно;
- companion ownership/state;
- loot results/pending loot;
- boss/world-event persistent state;
- outbox;
- scheduled durable job state при использовании Quartz persistent store.

Redis **не является Source of Truth**.

---

# 9. ID strategy

Для новых entities рекомендуется использовать:

```text
Guid.CreateVersion7()
```

то есть UUIDv7.

Плюсы для Elyndor:
- глобально уникальный ID;
- приблизительно time-ordered;
- удобнее B-tree insertion, чем полностью random UUIDv4;
- не нужно выдавать последовательные публичные integer IDs.

Telegram user/chat ID хранится как `long`, а не `int`.

---

# 10. EF Core conventions

## Optimistic concurrency

Persistent aggregate/state должен иметь явный version/concurrency token:

```text
StateVersion
```

Команда обновляет данные только если version совпадает.

При conflict:
- reload;
- revalidate command;
- retry только там, где операция безопасно повторяема.

Не использовать blind last-write-wins для:
- Inventory;
- Equipment;
- Talent points;
- Loot claim;
- Currency;
- Party membership.

## Transactions

Одна gameplay operation должна сохраняться атомарно.

Например:

```text
Boss reward claim
→ create ItemInstance
→ update PendingLoot
→ create Quest/Progress event outbox
→ COMMIT
```

---

# 11. Content definitions — не хранить баланс в C# коде

Очень важное решение для удобной разработки.

Классы, abilities, talents, items, monsters и loot tables должны быть **data-driven content**.

Пример:

```text
/content/talents/warrior.json
/content/talents/archer.json
/content/items/level_001_030.json
/content/monsters/pine_forest.json
```

Использовать `System.Text.Json`.

На startup:

```text
Load Content
→ Schema/semantic validation
→ validate IDs/references
→ build immutable runtime definitions
→ calculate ContentVersion hash
→ start server
```

Если content invalid — сервер в production mode не стартует.

Это позволяет менять баланс без переписывания domain code.

---

# 12. Content validation

Startup validator должен проверять минимум:

```text
Duplicate IDs
Missing prerequisite IDs
Missing AbilityId
Missing EffectId
Invalid ResourceType
Invalid StatId
Invalid CompanionTag
Invalid ItemSet references
Talent point/rank consistency
Tier requirement consistency
Loot table cycles
Quest prerequisite cycles
Unknown ClassId
Unsupported control effects
```

Эту же проверку запускать в CI.

---

# 13. Time implementation

Во всём backend запрещено разбрасывать:

```csharp
DateTime.UtcNow
DateTimeOffset.UtcNow
```

Game code получает:

```text
TimeProvider
```

Через DI.

Преимущества:
- соответствует `01_TIME_SYSTEM`;
- детерминированные unit tests;
- можно мгновенно тестировать cooldown/DoT/AFK/respawn без реального ожидания.

---

# 14. Random implementation

Combat/Loot не должны напрямую вызывать случайность из разных мест.

Ввести:

```text
IGameRandom
```

Runtime implementation использует качественный PRNG.

Tests используют deterministic seed.

Каждый важный RNG context может логировать:
- source;
- roll type;
- definition/version;
- result.

Это сильно упростит разбор «почему мне выпало/не выпало» и combat bugs.

---

# 15. Realtime architecture

SignalR используется для:
- Combat state/events;
- Party realtime updates;
- World/Boss notifications;
- Character state deltas;
- server notifications.

Рекомендуется один основной:

```text
GameHub
```

а не отдельный Hub на каждую систему.

SignalR groups:

```text
character:{CharacterId}
party:{PartyId}
combat:{CombatSessionId}
world-event:{WorldEventId}
```

---

# 16. HTTP vs SignalR

## HTTP

Использовать для:
- Telegram auth;
- initial bootstrap;
- character creation;
- inventory/equipment management;
- talent configuration;
- quest UI actions;
- non-realtime read models.

## SignalR

Использовать для:
- use ability;
- target change;
- combat leave attempt;
- pet combat commands;
- combat events;
- realtime Party/World notifications.

---

# 17. Reconnect protocol

Нельзя рассчитывать, что клиент получит каждый SignalR event.

Каждый CombatSession имеет:

```text
StateVersion
EventSequence
```

При reconnect:

```text
Client reconnects
→ authenticate
→ Join character/combat groups
→ Request/Get authoritative CombatSnapshot
→ continue events from new sequence
```

UI строится из snapshot + последующих deltas.

---

# 18. Combat concurrency model

Самая опасная ошибка MMORPG backend — позволить нескольким async handlers одновременно менять один CombatSession.

Для каждого active CombatSession используется **single-writer execution**.

Базовая модель:

```text
Client Commands
AI Decisions
Scheduled Actions
Effect Ticks
      ↓
Channel<CombatCommand>
      ↓
ONE CombatSession processor
      ↓
validated deterministic state changes
```

Используется:

```text
System.Threading.Channels
```

Это позволяет избежать десятков `lock` вокруг HP/Effects/Threat.

---

# 18.1 Combat persistence / crash policy

На первом server architecture **не event-source'им каждый удар** и не пишем каждый HP tick в PostgreSQL.

Runtime CombatSession живёт в памяти single-writer controller. Durable state фиксируется на безопасных transaction boundaries (до/после session и на критических permanent operations).

Если process падает посередине обычного боя:

```text
CombatSession → INTERRUPTED
no XP / loot / kill reward
restore last persisted valid character state
```

Это осознанно проще, чем пытаться восстановить точную очередь атак/DoT/cast после crash. При росте проекта можно добавить versioned combat checkpoints, не меняя gameplay contract.

Boss restart policy остаётся `FAILED + no rewards`.

---

# 19. Combat timers — НЕ Quartz

Auto Attack, Cast, Cooldown, DoT ticks, pet recovery и Effect expiration **не являются Quartz jobs**.

Для runtime Combat используются:
- absolute timestamps;
- `TimeProvider`;
- `PriorityQueue<TElement, TPriority>`;
- async wait до ближайшего события;
- CombatSession command channel.

Не нужен 20/30/60 Hz global game tick, потому что Elyndor не имеет realtime movement/physics.

Это очень хорошо подходит текущему combat design.

---

# 20. Что делает Quartz.NET

Quartz используется для coarse/durable schedules:

```text
World Boss spawn schedules
World Event schedules
Daily/weekly reset
Maintenance jobs
Expired reward cleanup
Economy periodic tasks later
Administrative scheduled actions
```

Если job обязан пережить server restart — использовать persistent store PostgreSQL.

Для нового проекта использовать System.Text.Json serialization и stable Quartz 3.x.

---

# 21. Transactional Outbox

Некоторые события нельзя потерять между COMMIT и обработчиком.

Пример:

```text
Character receives boss loot
+ XP
+ quest progress event
```

Database transaction:

```text
UPDATE gameplay state
INSERT OutboxMessage
COMMIT
```

После commit background worker забирает outbox и dispatches domain/integration notification.

На одном сервере broker для этого не нужен.

---

# 22. Redis role

Redis подключаем, но используем осознанно.

Подходит для:
- distributed cache;
- short-lived presence/cache;
- temporary rate-limit/counters later;
- leaderboard/read optimization later;
- SignalR backplane после горизонтального масштабирования.

Не хранить только в Redis:
- Character;
- Inventory;
- loot ownership;
- talent points;
- Currency;
- permanent companion state.

`ConnectionMultiplexer` должен жить singleton на процесс; не создавать Redis connection на каждый request.

---

# 23. SignalR scale-out

На первом сервере:

```text
1 ASP.NET process
→ обычный SignalR
```

Redis backplane **не включать без необходимости**.

Когда появятся несколько ASP.NET instances:

```text
Microsoft.AspNetCore.SignalR.StackExchangeRedis
```

и Redis backplane.

Это должен быть scale-out шаг, а не обязательная сложность первого запуска.

---

# 24. Telegram Mini App authentication

Frontend никогда не является источником Telegram identity.

Flow:

```text
Telegram opens Mini App
↓
Frontend reads Telegram.WebApp.initData
↓
POST /api/auth/telegram
↓
Backend validates Telegram signature/hash + auth_date
↓
Account resolved/created
↓
Backend returns short-lived Elyndor access token
↓
Frontend uses token for HTTP + SignalR
```

Никогда не доверять:

```text
Telegram.WebApp.initDataUnsafe
```

без server-side validation.

Bot Token существует только на backend.

---

# 24.1 Development Auth

Для обычной разработки в браузере Telegram `initData` отсутствует. Чтобы не подделывать Telegram SDK руками, разрешён отдельный **Development-only auth provider**.

Правила:
- включается только `Development` environment + explicit config flag;
- создаёт заранее заданного test Account/Character;
- endpoint не регистрируется в Production;
- при использовании Tailscale Funnel Development Auth **обязательно выключен**;
- интеграционные тесты Telegram auth отдельно проверяют реальную HMAC/`auth_date` validation.

Это ускоряет обычную UI/gameplay разработку и не становится production backdoor.

---

# 25. Auth token storage

Для Mini App рекомендуется:

- short-lived Elyndor JWT;
- хранить token только в runtime memory/Pinia;
- не сохранять bearer token в `localStorage`;
- после reload повторно пройти Telegram initData exchange;
- SignalR получает token через accessTokenFactory.

Это упрощает session handling и снижает ценность украденного persistent browser storage.

---

# 26. ASP.NET Core middleware

Включить:

```text
ProblemDetails
Authentication
Authorization
RateLimiter
OpenAPI (development/admin)
HealthChecks
Request logging / OpenTelemetry
```

Ошибки API должны иметь stable machine-readable code:

```json
{
  "code": "ABILITY_COOLDOWN",
  "message": "...",
  "stateVersion": 421
}
```

Frontend не должен парсить русский текст ошибки для логики.

---

# 27. Rate limiting

Особенно ограничить:
- Telegram auth endpoint;
- character creation/name checks;
- party invite spam;
- talent respec;
- inventory mutations;
- admin endpoints.

Combat realtime spam дополнительно ограничивается server command validation и per-character command rate.

---

# 28. Vue stack

```text
Vue 3
TypeScript
Vite
Vue Router
Pinia
@microsoft/signalr
@vueuse/core
Vitest
Playwright
ESLint
Prettier
sass
openapi-typescript
openapi-fetch
```

`create-vue` уже умеет создать TypeScript/Vite project с Router, Pinia, Vitest, Playwright, ESLint и Prettier options.

---

# 29. Frontend architecture

```text
/web/elyndor-web/src
├── app/
├── api/
├── realtime/
├── telegram/
├── stores/
├── game/
│   ├── combat/
│   ├── world/
│   ├── character/
│   ├── inventory/
│   ├── talents/
│   ├── quests/
│   ├── party/
│   └── companion/
├── components/
├── assets/
└── styles/
```

Не превращать Pinia в копию серверной базы.

Store хранит:
- текущий UI/read state;
- current authoritative snapshots/deltas;
- pending client UI state.

Server остаётся Source of Truth.

---

# 30. MMORPG UI libraries

Не подключать Element Plus / Vuetify / Ant Design как основу игрового интерфейса.

Они хороши для dashboard/business UI, но Elyndor нужен собственный visual identity.

Использовать:
- Vue SFC;
- CSS variables;
- SCSS;
- собственные Button/Card/Panel/Tooltip/ProgressBar компоненты.

Так UI не будет выглядеть как CRM.

---

# 31. HTTP contract generation

Чтобы C# DTO и TypeScript не расходились:

1. ASP.NET генерирует OpenAPI.
2. Frontend build/dev script генерирует TypeScript API types/client.

Рекомендуемый dev/runtime pair:

```text
openapi-typescript   # OpenAPI → TypeScript types
openapi-fetch        # маленький typed HTTP client
```

SignalR events не полагать на OpenAPI — для них держать явные versioned contracts в `Elyndor.Contracts` и зеркальные/generated TS types. Любое изменение realtime contract требует contract test.

---

# 32. Local development через Aspire

Aspire AppHost поднимает:

```text
PostgreSQL
Redis
Elyndor.Server
Vue/Vite
Aspire Dashboard
```

Идеальный local flow:

```text
aspire run
```

Дальше разработчик видит:
- сервисы;
- endpoints;
- logs;
- traces;
- health;
- container state

в одном dashboard.

PostgreSQL и Redis не нужно устанавливать руками локально — достаточно container runtime.

---

# 33. Local dev ingress

В обычной разработке Aspire может управлять endpoint'ами Server/Vite. Не нужно вручную открывать PostgreSQL/Redis наружу.

Local browser flow:

```text
Aspire AppHost
├── PostgreSQL
├── Redis
├── Elyndor.Server
└── Vue/Vite (HMR)
```

Frontend обращается к API через dev proxy или Aspire reference. SignalR endpoint:

```text
/hubs/game
```

---

# 34. Public Telegram test from local machine

Для первых Telegram tests самый надёжный режим — **single origin без Vite HMR наружу**:

```text
npm run build
↓
Vue dist копируется/публикуется как static files Elyndor.Server
↓
ASP.NET Core одновременно обслуживает:
/              SPA
/api/*          HTTP API
/hubs/game      SignalR
↓
Tailscale Funnel → только ASP.NET port
```

Рекомендуемый test port:

```text
Elyndor.Server public-test: 5080
```

Команда:

```text
tailscale funnel 5080
```

Публичный URL Tailscale (`https://<node>.<tailnet>.ts.net`) указывается как Telegram Mini App URL.

Плюсы:
- один HTTPS origin;
- нет CORS;
- SignalR WebSocket идёт тем же origin;
- не надо проксировать HMR наружу;
- PostgreSQL и Redis остаются только локальными/internal.

Для быстрой локальной UI-разработки всё равно используется Vite HMR через Aspire.

---

# 35. Tailscale modes

## Testers без Tailscale

```text
tailscale funnel 5080
```

Funnel используется только для dev/test ingress, не как production hosting.

## Testers внутри tailnet

```text
tailscale serve 5080
```

Serve приватнее и доступен только tailnet.

Никогда не публиковать через Serve/Funnel:
- PostgreSQL;
- Redis;
- Aspire Dashboard;
- admin/debug endpoints без отдельной защиты.

---

# 36. Secrets

Никогда не коммитить:
- Telegram Bot Token;
- JWT signing secret/private key;
- database production credentials;
- Redis production credentials.

Local:

```text
dotnet user-secrets
```

или Aspire secret/environment configuration.

Frontend `VITE_*` считается публичным — туда нельзя класть secrets.

---

# 37. Observability

Сразу ввести OpenTelemetry.

Минимальные traces/metrics:

```text
HTTP request duration
SignalR connections
SignalR command latency
Active CombatSessions
Combat command queue length
Scheduled action lateness
Ability validation failures
DB query duration
DB transaction retries
Outbox backlog
Redis availability
Quartz misfires
Loot resolution latency
```

Это поможет балансировать не только код, но и реальную нагрузку игры.

---

# 38. Logging context

Каждый game log по возможности содержит IDs:

```text
AccountId
CharacterId
PartyId
CombatSessionId
CompanionId
AbilityId
QuestId
RewardSourceId
CorrelationId
ClientCommandId
```

Не логировать Bot Token или полный auth token.

---

# 39. Tests — обязательно

## Unit tests

Тестируют Core без PostgreSQL/Redis:
- damage formulas;
- mitigation;
- resource changes;
- talent modifiers;
- prerequisites;
- effect stacking;
- lethal prevention;
- threat;
- XP;
- loot roll determinism.

## Integration tests

Тестируют:
- EF mappings;
- concurrency;
- migrations;
- idempotency;
- outbox;
- auth endpoint;
- SignalR reconnect/snapshot;
- inventory/loot transaction.

## Frontend

Vitest:
- stores;
- formatters;
- UI game rules presentation.

Playwright:
- create character;
- enter location;
- start combat;
- use ability;
- equip item;
- switch talent loadout;
- party flow.

---

# 40. Game simulation tests

Помимо обычных unit tests нужен **headless simulation runner**.

Он запускает тысячи боёв без frontend:

```text
Warrior vs Monster
Archer builds vs Monster
Boss party simulations
60 sec DPS
180 sec DPS
AoE encounters
Tank DTPS/EHP
Pet contribution
Mana/Focus sustainability
```

Результат сохраняется в CSV/JSON для сравнения balance changes.

Это будет намного полезнее ручного тестирования каждой цифры талантов.

---

# 41. Talent/content CI validator

Каждый Pull Request должен автоматически проверять:

```text
all prerequisite IDs exist
no duplicate TalentId
branch possible points in accepted range
capstone threshold valid
unsupported Stats absent
unsupported Controls absent
PHYSICAL_PET/SPIRIT_PET filters valid
no proc recursion without explicit permission
all AbilityIds resolve
all SetIds resolve
```

Текущая design target:

```text
Warrior branches: 70 possible rank-points
Archer branches:  69 possible rank-points
Available at 60:  59
```

---

# 41.1 DbContext strategy

Для gameplay persistence на старте использовать **один `GameDbContext`**, чтобы Inventory/Talents/Character/Loot могли участвовать в одной PostgreSQL transaction. Не делать DbContext на каждый markdown-module.

Правила:
- HTTP/Application operation → scoped `GameDbContext`;
- Background/Quartz worker → `IDbContextFactory<GameDbContext>` / новый короткоживущий context на job;
- CombatSession **никогда не держит DbContext открытым весь бой**;
- никаких parallel operations на одном DbContext instance;
- Quartz store использует свои tables/schema и не является частью GameDbContext domain model.

Если позже размер model станет проблемой, разделение contexts делается по измеренной необходимости, а не заранее.

---

# 42. Database migrations

EF Core migrations хранятся в Git.

Development:

```text
dotnet ef migrations add <Name>
dotnet ef database update
```

Не создавать schema вручную параллельно migrations.

Production migration позже выполняется отдельным controlled deployment step, а не случайно при первом HTTP request.

---

# 43. PostgreSQL schemas

Для удобства можно разделить логически:

```text
public / game      — gameplay persistent tables
jobs               — Quartz persistent store
ops                — outbox/audit/admin, если понадобится
```

Не делать отдельную database на каждую domain system.

---

# 44. JSONB

PostgreSQL JSONB использовать для:
- metadata;
- content snapshots;
- versioned flexible context;
- audit payloads.

Не превращать основные Character/Item/Quest tables в один огромный JSON document.

Core state, по которому часто фильтруем/обновляем, остаётся relational.

---

# 45. Redis invalidation

Cache всегда должен иметь понятный owner/key/version.

Лучше удалить stale cache, чем пытаться вручную изменить копию permanent character state в двух местах.

Для content definitions на первом сервере чаще достаточно immutable in-memory cache — Redis там вообще не обязателен.

---

# 46. Deploy trajectory

## Stage 1 — local

```text
Aspire
PostgreSQL container
Redis container
ASP.NET
Vite
```

## Stage 2 — remote tests

Тот же local server + Tailscale Funnel/Serve.

## Stage 3 — first permanent server

```text
Linux VPS
ASP.NET container/process
PostgreSQL
Redis
reverse proxy / TLS
Vue static build
```

## Stage 4 — scale

Только когда измерения покажут необходимость:
- несколько Server instances;
- SignalR Redis backplane;
- dedicated workers;
- broker;
- read replicas;
- horizontal combat partitioning.

---

# 47. Что не масштабировать заранее

Не проектировать сейчас:
- Kubernetes;
- service mesh;
- event sourcing всей игры;
- десятки микросервисов;
- Kafka;
- distributed actor framework;
- sharding PostgreSQL;
- Redis Cluster.

Сначала настоящий игровой loop, нагрузочные данные и реальные bottlenecks.

---

# 48. Recommended first implementation order

```text
1. Solution + Aspire + PostgreSQL + Redis + Vue
2. Telegram auth/bootstrap
3. Character creation
4. Content loader + validator
5. Stats / Resource / Time
6. Combat runtime single-writer loop
7. Abilities / Effects / Damage
8. Warrior basic kit + basic monsters
9. Loot / Items / Progression
10. Party
11. Archer + Companion
12. Mage basic kit
13. Talents + 2 loadouts (Warrior / Archer; Mage after tree approval)
14. Quests
15. Boss / world event
16. AFK
17. Equipment/content expansion
```

После каждого вертикального блока:

```text
implement
→ automated tests
→ manual playtest
→ balance/performance data
→ refine
→ next block
```

---

# 49. Final engineering rules

1. Server authoritative всегда.
2. PostgreSQL — permanent truth.
3. Redis — cache/scale helper, не truth.
4. Quartz — scheduler, не combat timer и не message queue.
5. CombatSession меняется одним writer одновременно.
6. Time только через TimeProvider.
7. RNG только через IGameRandom.
8. Gameplay content data-driven.
9. HTTP/SignalR contracts versioned.
10. Reconnect всегда восстанавливается из authoritative snapshot.
11. Durable side effects используют transaction/idempotency/outbox.
12. Не добавляем библиотеку, пока она реально не убирает сложность.

---

# 50. Official technical references used for this stack review

- .NET support policy: https://dotnet.microsoft.com/en-us/platform/support/policy
- EF Core 10: https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew
- PostgreSQL versioning/current minor: https://www.postgresql.org/support/versioning/
- PostgreSQL 18 docs: https://www.postgresql.org/docs/18/
- Npgsql EF Core 10: https://www.npgsql.org/efcore/release-notes/10.0.html
- ASP.NET Core SignalR Redis scale-out: https://learn.microsoft.com/aspnet/core/signalr/redis-backplane
- Redis .NET / StackExchange.Redis: https://redis.io/docs/latest/develop/clients/dotnet/
- Quartz.NET 3.x: https://www.quartz-scheduler.net/documentation/quartz-3.x/
- Aspire AppHost: https://aspire.dev/get-started/app-host/
- Aspire prerequisites: https://aspire.dev/get-started/prerequisites/
- Node.js LTS: https://nodejs.org/en/download
- Vue Quick Start: https://vuejs.org/guide/quick-start.html
- Vue TypeScript: https://vuejs.org/guide/typescript/overview
- Telegram Mini Apps: https://core.telegram.org/bots/webapps
- Tailscale Funnel: https://tailscale.com/kb/1223/funnel
- Tailscale Serve: https://tailscale.com/docs/features/tailscale-serve

---

# Economy / Instance engineering notes

## PostgreSQL remains authoritative

Wallet, direct trade commit, auction settlement and crafting material consumption should use PostgreSQL transactions/concurrency.

Redis must **not** become the authoritative wallet or item owner store.

## Quartz usage

Quartz is appropriate for durable wake-ups such as:
- auction expiration;
- timed craft completion;
- dungeon cleanup;
- scheduled economy jobs.

Authoritative time check still uses Time System / `TimeProvider`.

## Search

Auction search starts with PostgreSQL indexed read model.

Do not add Elasticsearch/OpenSearch until real query volume proves PostgreSQL insufficient.

## Transaction boundaries

Because Elyndor is a modular monolith in one database, prefer a single local transaction for:
- merchant item + Gold;
- direct trade ownership + Gold;
- auction purchase + settlement state;
- craft ingredient consumption + Gold fee + operation creation.

Outbox publishes resulting domain events after durable commit.
