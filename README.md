# Elyndor

Elyndor — mobile-first dark-fantasy MMORPG. Сейчас игра запускается как Telegram Mini App, но игровая архитектура и доменная модель не должны зависеть от Telegram сильнее, чем требуется для identity/entry flow.

> **Текущий снимок:** `main` @ `78ef47a` (25 сентября 2026).  
> Этот README описывает **то, что реально есть в `main` сейчас**. Дизайн будущих систем живёт в `docs/source-of-truth/`.

## Что уже работает

### Игровой цикл

- Telegram identity, аккаунт, персонаж и восстановление игровой сессии.
- Мир, travel, server-driven экран текущей локации и exploration encounters.
- Обычный бой, боссы и подземелья через server-authoritative `CombatSession`.
- Прогрессия, XP, характеристики, ресурсы классов, предметы, экипировка и loot.
- Контракты/quest flow, AFK/auto-hunt foundation и награды.
- Party до **5 игроков**, приглашения и Telegram-уведомления о приглашении.
- Reconnect в активный бой и защита reward/mutation путей от повторного применения там, где это уже покрыто системой.

### Классы

В character creation доступны четыре класса:

| Класс | Ресурс | Текущее состояние |
| --- | --- | --- |
| Warrior | Rage | playable; активный runtime-content включает Guardian slice, а полный дизайн дерева содержит Guardian / Berserker / Commander |
| Archer | Focus (+ Mana для Arcane mechanics) | playable; companion/pet и три talent-направления присутствуют в content |
| Mage | Mana | playable; Fire / Arcane / Frost talent content присутствует |
| Paladin | Mana | playable; ability/talent content и character-creation flow присутствуют |

Важно: наличие talent/content definition не означает, что каждая механика каждого узла уже прошла полный gameplay regression. Полный four-class ability/talent pass остаётся отдельной задачей.

### Бой и UI

В `main` уже используется единый battle screen для solo и party:

- mobile-first battlefield без отдельного party-экрана;
- союзники представлены персонажами на арене;
- выбор ally/enemy target;
- aggro state отделён от выбранной цели;
- skills и consumables разделены;
- floating damage/healing feedback;
- cooldown/readiness states;
- сворачиваемый combat log drawer;
- адаптация под узкие mobile viewport'ы и party до 5 игроков;
- Playwright preview tests на геометрию/overflow/formation.

### Подземелья и боссы

Authored high-level group content сейчас оформлен как **dungeons на 1–5 игроков**, а не как production raid flow.

В content/runtime присутствуют, среди прочего:

- Ancient Mine;
- Eclipsed Citadel;
- Shattered Order Citadel;
- Black Bastion.

Black Bastion имеет data-driven набор из шести boss encounters с отдельными механиками/AI.

### Инвентарь и экипировка

Текущая модель инвентаря — **единый inventory + Spatial Artifact**:

- базовая вместимость: **30 слотов**;
- один экипированный Spatial Artifact добавляет capacity;
- artifact не занимает обычный inventory slot, пока экипирован;
- equip/unequip/swap/salvage используют effective capacity;
- mobile inventory использует адаптивную сетку и item bottom sheet.

Старое правило `40 slots` больше не является текущим состоянием игры.

### Косметика и магазин

- Premium Store имеет mobile-first storefront.
- Игровая premium currency отображается как `CRYSTAL` domain currency / player-facing ether-shard presentation.
- Реальные backend-backed offers покупаются authoritative purchase flow.
- Некоторые витринные cosmetics/services остаются presentation foundation и не становятся «фейковой покупкой», пока нет backend domain.
- Реализован wardrobe/skin flow: совместимые облики, ownership, equip и обновление appearance в бою/игре.

### Presence и Telegram integration

- authenticated browser heartbeat и online-count monitoring;
- Telegram party invite notification с Web App deep-link;
- Telegram admin/monitoring infrastructure;
- Telegram остаётся текущим entry channel, но gameplay state хранится сервером и в PostgreSQL.

## Что ещё не считается завершённым

Следующие области не надо выдавать за готовый production feature:

- полноценный Raid gameplay: raid domain/эксперименты существуют, но текущий production content идёт через dungeon pipeline; незавершённый raid PR не является частью `main`;
- полный runtime-аудит всех talents/abilities четырёх классов;
- единый окончательно закрытый item-icon pipeline для всех предметов;
- полный player-facing text/content audit всех предметов и локаций;
- PvP;
- завершённые Trade/Auction/Guild/Crafting/Professions как production-ready игровые циклы;
- endgame 30–60 как полностью отполированный content layer.

## Технический стек

- **Backend:** .NET 10, ASP.NET Core, EF Core, PostgreSQL 18, SignalR.
- **Frontend:** Vue 3, TypeScript, Vite, Pinia.
- **Local orchestration:** .NET Aspire.
- **Testing:** xUnit/.NET tests, Vitest, Playwright, content validation.
- **Observability:** OpenTelemetry / Aspire dashboard.
- **Architecture:** modular monolith, server-authoritative gameplay, data-driven content.

## Структура репозитория

```text
apphost/                 Aspire orchestration
src/                     backend/domain/server
web/elyndor-web/         player frontend
admin/                    admin tooling/frontend where applicable
content/                  versioned gameplay content
reference/                approved visual/UI references
docs/source-of-truth/     gameplay, architecture and UI contracts
docs/development/         only active developer workflow docs
docs/deployment/          production/VPS operations
tests/                    backend/integration tests
tools/                    validators and developer tooling
```

Документация намеренно разделена так:

1. `README.md` — **as-built snapshot**, что реально есть сейчас.
2. `docs/source-of-truth/` — authoritative gameplay/architecture/UI contracts.
3. `docs/development/` — только актуальные инструкции разработки.
4. `docs/deployment/` — актуальная эксплуатация.
5. Одноразовые implementation plans, старые audit snapshots и AI planning notes в активной документации не хранятся: история уже есть в Git.

## Быстрый старт

Требования: .NET 10 SDK, Node.js 24 LTS (или поддерживаемый Node 22.18+), Docker Desktop/совместимый container runtime.

```powershell
npm ci --prefix web/elyndor-web
dotnet run --project apphost/Elyndor.AppHost
```

Полная локальная инструкция: `docs/development/getting-started.md`.

Основные проверки:

```powershell
dotnet build Elyndor.slnx --configuration Release
dotnet test Elyndor.slnx --configuration Release
dotnet run --project tools/Elyndor.ContentValidator -- content/package.json
npm run lint --prefix web/elyndor-web
npm run test:unit --prefix web/elyndor-web
npm run build --prefix web/elyndor-web
npm run test:e2e --prefix web/elyndor-web
```

## Правила изменения игры

- `main` не используется для feature-work напрямую.
- Gameplay/backend authoritative; клиент не является источником результата боя, награды или identity.
- Контент versioned/data-driven и проходит validation.
- Новая механика сначала меняет соответствующий Source of Truth, затем implementation и tests.
- Один PR — одна логическая задача.
- После крупного вертикального среза: automated checks → local playtest → Telegram/mobile playtest → polish.

См. `AGENTS.md`, `CONTRIBUTING.md`, `docs/development/git-workflow.md` и `docs/source-of-truth/README.md`.
