# Contributing to Elyndor

Elyndor развивается небольшими, проверяемыми вертикальными срезами. Цель процесса — сохранять `main` рабочим, не смешивать несвязанные изменения и не допускать дрейфа между кодом, контентом и Source of Truth.

## Source of Truth

Перед изменением поведения прочитайте минимальный набор релевантных документов:

1. `docs/source-of-truth/gameplay/` — игровые правила.
2. `docs/source-of-truth/ui/` — UI/UX-контракт.
3. `docs/source-of-truth/architecture/` и `docs/source-of-truth/phases/` — архитектура, порядок и статус реализации.
4. `AGENTS.md` — актуальные инженерные инварианты и правила для coding agents.

Визуальный reference не переопределяет игровую механику.

## Рабочий процесс

1. Обновите локальный `main`.
2. Создайте короткоживущую ветку от актуального `main`.
3. Делайте один логический change set на ветку.
4. Обновите тесты и документацию вместе с изменением.
5. Прогоните релевантные проверки.
6. Откройте Pull Request и заполните шаблон.
7. Мержите только после зелёного CI.
8. После merge удалите рабочую ветку.

Подробная Git-политика: `docs/development/git-workflow.md`.

## Имена веток

Используйте один из префиксов:

- `feat/` — новая функциональность;
- `fix/` — исправление дефекта;
- `refactor/` — изменение структуры без смены поведения;
- `chore/` — инфраструктура и housekeeping;
- `docs/` — только документация;
- `test/` — тестовая инфраструктура или coverage.

Имя должно описывать одну задачу, например `feat/admin-monster-editor`, а не `feature/big-update`.

## Коммиты

Предпочтителен Conventional Commit стиль:

- `feat:`
- `fix:`
- `refactor:`
- `test:`
- `docs:`
- `chore:`

Не переписывайте историю `main` и не используйте force-push для общей ветки.

## Проверки

Полный локальный набор:

```powershell
dotnet build Elyndor.slnx --configuration Release
dotnet test Elyndor.slnx --configuration Release
dotnet run --project tools/Elyndor.ContentValidator -- content/package.json
npm run lint --prefix web/elyndor-web
npm run format:check --prefix web/elyndor-web
npm run test:unit --prefix web/elyndor-web
npm run build --prefix web/elyndor-web
npm run test:e2e --prefix web/elyndor-web
```

Не каждый docs-only change требует локального полного прогона, но Pull Request должен дождаться обязательного CI.

## Gameplay и content changes

- сервер остаётся authoritative;
- gameplay constants не прячутся во frontend;
- data-driven content должен проходить validator;
- изменение механики сопровождается обновлением Source of Truth;
- активные combat sessions продолжают использовать закреплённую версию content;
- admin/content изменения не обходят pipeline `draft -> validate -> revision -> review diff -> publish`.

## Секреты

Никогда не коммитьте:

- Telegram Bot Token;
- JWT signing keys;
- пароли PostgreSQL;
- production connection strings;
- `.env` и локальные override-файлы;
- содержимое `.elyndor/`;
- локальные логи, build artifacts, `*.patch` и `*.diff`.

Если секрет случайно попал в Git, его нужно считать скомпрометированным и ротировать, а не только удалить из следующего коммита.

## Definition of Done

Change считается готовым, когда:

- scope соответствует одной задаче;
- поведение покрыто релевантными тестами;
- migrations и persistence безопасны, если они затронуты;
- reconnect/restart и error states учтены там, где это применимо;
- документация не противоречит реализации;
- CI зелёный;
- в diff нет секретов, временных файлов и несвязанных изменений.
