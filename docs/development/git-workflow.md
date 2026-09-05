# Git workflow

Этот документ фиксирует рабочую Git-политику ElyndorMMORPG.

## Основная ветка

`main` — единственная постоянная интеграционная ветка и должна всегда оставаться в состоянии, пригодном для запуска и дальнейшей разработки.

Нормальный поток:

```text
main
-> short-lived branch
-> Pull Request
-> CI
-> merge
-> delete branch
```

Feature development напрямую в `main` не ведётся.

## Branch naming

```text
feat/<topic>
fix/<topic>
refactor/<topic>
chore/<topic>
docs/<topic>
test/<topic>
```

Отдельная ветка должна решать одну задачу. Временные `*-temp-check*`, staging-ветки и экспериментальные ветки удаляются после завершения или отказа от эксперимента.

## Pull Requests

PR должен:

- быть основан на актуальном `main`;
- содержать один логический change set;
- описывать Summary, Safety и Verification;
- явно отмечать изменения gameplay/content contracts;
- не смешивать housekeeping с игровой механикой;
- проходить CI до merge.

Для больших изменений сначала делите работу на самостоятельные вертикальные срезы.

## Merge strategy

Предпочтительный способ — **Squash merge** для feature/fix/chore PR: история `main` остаётся читаемой, а промежуточные корректирующие коммиты ветки не засоряют trunk.

Merge commit используйте только когда действительно нужно сохранить структуру нескольких связанных коммитов. Rebase merge не является стандартным вариантом для проекта.

## История

Запрещено:

- force-push в `main`;
- rewrite уже опубликованной истории `main`;
- reset `main` на старый commit ради отмены функции.

Отмена уже влитого изменения делается отдельным revert/fix PR.

## Рекомендуемые настройки GitHub

Для `main`:

- включить branch protection или repository ruleset;
- Require a pull request before merging;
- Require status checks to pass before merging;
- обязательный status check: текущий `ci`;
- запретить force pushes;
- запретить deletion;
- approvals: 0, пока проект ведёт один разработчик; увеличить до 1 при появлении постоянного reviewer;
- включить Automatically delete head branches.

GitHub branch protection блокирует force-push/deletion защищённой ветки и может требовать PR/status checks; автоматическое удаление head branches поддерживается отдельно в настройках репозитория.

## Cleanup

После merge или закрытия задачи:

1. убедиться, что PR закрыт;
2. удалить head branch;
3. не держать отдельную ветку «на всякий случай» — история уже хранится Git;
4. для возвращения к идее создать свежую ветку от текущего `main`.

Stale ветка не должна становиться базой для новой работы спустя много изменений в `main`.

## CI

Текущий workflow: `.github/workflows/ci.yml`.

Он проверяет:

- PowerShell launcher syntax;
- .NET restore/build/test;
- static game content validation;
- frontend lint/unit tests/build;
- Playwright shell E2E.

Ослабление CI должно быть отдельным осознанным изменением с объяснением причины.
