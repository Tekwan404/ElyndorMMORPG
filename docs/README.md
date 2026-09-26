# Documentation

Активная документация Elyndor намеренно сведена к четырём зонам:

```text
docs/source-of-truth/   authoritative architecture/gameplay/UI contracts
docs/development/       local development and Git workflow
docs/deployment/        production/VPS operations
reference/              visual references used by UI docs
```

Начинать чтение нужно с корневого `README.md`: он описывает текущее состояние `main`.

## Что здесь не храним

Не создаём отдельные постоянные файлы для:

- одноразовых implementation plans;
- agent/superpowers plans/specs;
- старых phase snapshots после завершения фазы;
- audit dumps, которые устаревают после следующего merge;
- временных UI prompts;
- документов, которые дублируют существующий gameplay/UI contract.

Такая информация остаётся в Git history, PR или issue. Если решение стало постоянным — оно переносится в соответствующий Source of Truth.

## Быстрые ссылки

- Current implementation: `../README.md`
- Source of Truth: `source-of-truth/README.md`
- Local development: `development/getting-started.md`
- Git workflow: `development/git-workflow.md`
- Production/VPS: `deployment/vps-production.md`
