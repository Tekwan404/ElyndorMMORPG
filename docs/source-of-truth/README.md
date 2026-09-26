# Elyndor — Source of Truth

Этот каталог хранит **действующие контракты игры**, а не журнал разработки и не generated reports.

Текущее реализованное состояние кратко описывается в корневом `README.md`. Source of Truth отвечает на другой вопрос: **как система должна работать и какие правила нельзя тихо менять в коде**.

## Структура

```text
architecture/   stack, roadmap, product/technical boundaries, compatibility
gameplay/       authoritative gameplay systems 01–31
ui/             UI/UX contracts and visual-reference mapping
```

Authoritative authored gameplay data находится непосредственно в `/content`. Generated art audits/import manifests не являются Source of Truth и не должны жить в этом каталоге.

Исторические implementation plans, старые audit snapshots и agent-generated planning files здесь не хранятся. Для истории используется Git.

## Приоритет документов

При конфликте правил:

```text
gameplay system contract
→ dependent UI contract
→ UI master/reference mapping
→ visual reference
```

Картинка не переопределяет механику. Frontend hardcode не переопределяет content/backend contract.

Для вопроса «реализовано ли это уже?» приоритет другой:

```text
current main code + tests + content
→ root README current-state summary
→ roadmap/design documents
```

То есть наличие описанной будущей системы в Source of Truth само по себе не означает, что она уже production-ready.

## Gameplay contracts

`gameplay/01_*` … `gameplay/31_*` описывают Time, Combat, AFK, World, Character, Stats, Resources, Effects, Damage/Healing, Abilities, Progression, Classes, Items, Loot, Monsters, Talents, Contracts/Quests, Bosses, Character Creation, Party, Companion/Pet, class talent trees, equipment sets, Economy, Trade/Auction, Dungeons, Crafting/Professions, Guild and Raid Group design.

Ключевые current boundaries, которые должны учитываться при правках:

- playable creation roster: Warrior / Archer / Mage / Paladin;
- Party max: 5;
- текущий authored group-content runtime: Dungeons 1–5;
- production Raid flow ещё не считается завершённым;
- inventory: base 30 + bonus одного equipped Spatial Artifact;
- gameplay equipment и cosmetic appearance разделены;
- current battle presentation: unified solo/party BattleScreen;
- backend остаётся authoritative для combat, rewards, inventory, economy и identity validation.

## UI contracts

`ui/UI_01_*` … `ui/UI_20_*` задают структуру экранов. `ui/00_MASTER_UI_REFERENCE.md` и соседние `00_*` файлы задают visual language и mapping к `reference/`.

Реальный player UI развивается mobile-first. AI/reference изображения являются визуальным направлением и не могут добавлять механику, валюту, stat или кнопку, которых нет в system contract/runtime.

## Architecture

Основные документы:

- `architecture/00_DEVELOPMENT_STACK.md`
- `architecture/00_DEVELOPMENT_ROADMAP.md`
- `architecture/00_COMPATIBILITY_MATRIX.md`
- `architecture/00_CONTENT_AND_BALANCE_PROFILES.md`
- `architecture/00_PRODUCT_AND_PROTOTYPE_STRATEGY.md`
- `architecture/CONTENT_PLATFORM_ADMIN_FOUNDATION.md`

Roadmap — направление развития, а не чеклист текущей готовности. Current status смотрите в корневом `README.md` и в фактическом `main`.

## Правило изменения

Если меняется gameplay decision:

1. обновить соответствующий system contract;
2. обновить зависимый UI/content contract;
3. изменить implementation/content;
4. добавить/обновить tests и validators;
5. обновить root README, если изменился заметный current-state boundary.

Не создавать новый markdown-план рядом с кодом, если информация уже принадлежит существующему Source of Truth. Одноразовый рабочий план остаётся в PR/issue/commit history.
