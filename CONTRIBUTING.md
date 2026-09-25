# Contributing to Elyndor

Elyndor развивается небольшими, проверяемыми вертикальными срезами. Цель процесса — сохранять `main` рабочим, не смешивать несвязанные изменения и не допускать дрейфа между кодом, контентом и Source of Truth.

## Перед изменением

1. Прочитайте корневой `README.md` — это current as-built snapshot.
2. Откройте минимальный набор релевантных документов:
   - `docs/source-of-truth/gameplay/` — игровые правила;
   - `docs/source-of-truth/ui/` — UI/UX-контракт;
   - `docs/source-of-truth/architecture/` — архитектура, roadmap и границы продукта;
   - `AGENTS.md` — инженерные инварианты для coding agents.
3. Проверьте фактический `main`, content и tests. Старый PR/ветка не являются текущим состоянием проекта.

Визуальный reference не переопределяет игровую механику.

## Рабочий процесс

1. Обновите локальный `main`.
2. Создайте короткоживущую ветку от актуального `main`.
3. Делайте один логический change set на ветку.
4. Обновите tests/content/docs вместе с поведением.
5. Прогоните релевантные проверки.
6. Откройте Pull Request.
7. Мержите только после обязательного зелёного CI.
8. После merge удалите рабочую ветку.

Подробная Git-политика: `docs/development/git-workflow.md`.

## Имена веток

```text
feat/<topic>
fix/<topic>
refactor/<topic>
chore/<topic>
docs/<topic>
test/<topic>
```

Ветка решает одну задачу. Не используйте старую отставшую ветку как фундамент нового изменения.

## Коммиты

Предпочтителен Conventional Commit style:

```text
feat:
fix:
refactor:
test:
docs:
chore:
```

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

Docs-only change может не требовать локального полного прогона, но PR всё равно должен дождаться обязательного CI.

## Gameplay и content changes

- сервер остаётся authoritative;
- gameplay constants не прячутся во frontend;
- data-driven content проходит validator;
- изменение механики сопровождается обновлением Source of Truth;
- активные combat sessions используют закреплённый content snapshot там, где это предусмотрено runtime;
- admin/content изменения не обходят утверждённый validation/publish pipeline;
- reconnect, duplicate rewards и persistence failure cases покрываются там, где они релевантны.

## Документация

Не добавляйте в репозиторий постоянный markdown только потому, что агенту/разработчику нужен разовый план.

Постоянная информация должна жить в одном из мест:

- current product state → `README.md`;
- gameplay/architecture/UI rule → `docs/source-of-truth/`;
- local workflow → `docs/development/`;
- production operations → `docs/deployment/`.

Implementation plans, audit dumps и завершённые phase snapshots остаются в PR/issue/Git history.

## Секреты

Никогда не коммитьте:

- Telegram Bot Token;
- JWT signing keys;
- пароли PostgreSQL;
- production connection strings;
- `.env` и локальные override-файлы;
- содержимое `.elyndor/`;
- локальные логи/build artifacts/`*.patch`/`*.diff`.

Если секрет попал в Git, его нужно считать скомпрометированным и ротировать.

## Definition of Done

Change готов, когда:

- scope соответствует одной задаче;
- поведение покрыто релевантными тестами;
- migrations/persistence безопасны, если затронуты;
- reconnect/restart/error states учтены там, где применимо;
- документация не противоречит реализации;
- CI зелёный;
- в diff нет секретов, временных файлов и несвязанных изменений.
