# Elyndor UI Refactor — Master Prompt for Codex

Ты работаешь над проектом **ElyndorMMORPG**.

Главная задача: провести системный рефакторинг UI Telegram Mini App без разрушения существующей логики игры.

## Главные правила

1. Перед любыми изменениями изучи актуальный код проекта и существующую структуру frontend.
2. Не переписывай рабочую бизнес-логику без необходимости.
3. Не меняй API-контракты backend/frontend, если это не требуется для UI-задачи.
4. Не делай все сессии одновременно.
5. Работай строго последовательно: Session 1 → проверка DoD → Session 2 → проверка DoD → Session 3 → финальная проверка.
6. После каждого логического блока запускай build, TypeScript/typecheck, существующие tests и browser/mobile verification, если доступно.
7. Не оставляй дублирующую старую CSS-систему после миграции.
8. Не создавай десятки вариантов одного компонента.
9. Не используй emoji как финальные игровые glyphs.
10. Основной UI должен оставаться mobile-first и удобным для Telegram Mini App.

---

# Визуальное направление Elyndor

Стиль:

- dark fantasy;
- почти чёрный / тёмно-синий фон;
- холодные blue/violet magical accents;
- золото используется редко;
- минимум бессмысленных градиентов;
- минимум постоянного glow;
- высокая читаемость;
- крупные touch targets;
- атмосфера MMO без перегруженного «mobile fantasy casino» UI.

## Главное правило визуального языка

**Glow должен обозначать смысл, а не просто украшать UI.**

Допустимые случаи:

- Legendary item;
- Boss;
- Danger;
- selected/active state;
- важный magical event;
- critical feedback.

Обычные панели, кнопки и карточки не должны постоянно светиться.

---

# SESSION 1 — FOUNDATION + ASSET FACTORY

## Цель

Создать фундамент UI-системы, после которого новые экраны можно собирать из готовых компонентов, а массовые игровые иконки создавать из data-driven конфигов.

## 1. Design tokens

Создай `tokens.css`.

Вынеси туда минимум:

### Colors

- background;
- surfaces;
- borders;
- primary;
- secondary;
- danger;
- warning;
- success;
- text-primary;
- text-secondary;
- text-muted;
- disabled.

### Rarity

- common;
- uncommon;
- rare;
- epic;
- legendary;
- unique.

ВАЖНО: rarity не должна полностью заливать иконку своим цветом.

Использование rarity:

- border;
- небольшой accent;
- слабый semantic glow;
- цвет имени предмета.

Основной background item icon должен оставаться тёмным и нейтральным.

### Layout

- spacing scale;
- border-radius scale;
- component heights;
- touch target sizes.

Минимальный touch target: `44x44px`.

### Typography

- font sizes;
- font weights;
- line heights.

### Effects

- shadows;
- semantic glow values;
- transitions;
- z-index scale.

### Telegram

Добавь fallback-слой для Telegram theme variables.

Telegram theme не должен полностью менять визуальную идентичность Elyndor.

---

# 2. Glyph library

Создай TypeScript-библиотеку, например `src/ui/icons/glyphs.ts`.

Не используй emoji. Glyph должен быть описан чистой SVG-геометрией.

Первая библиотека:

### Weapons
- sword;
- greatsword;
- dagger;
- axe;
- bow;
- staff;
- shield.

### Equipment
- helmet;
- armor;
- boots;
- ring.

### Items
- potion;
- scroll;
- chest;
- ore;
- herb.

### Magic / modifiers
- fire;
- ice;
- lightning;
- poison;
- holy;
- shadow.

### Utility
- skull;
- star;
- lock.

Glyphs должны быть пригодны для повторного использования в одном renderer. Не создавай отдельные SVG-файлы для каждой комбинации предмета.

---

# 3. Data-driven Icon Generator

Не помещай всю логику в Vue-компонент.

Предпочтительная структура:

```text
src/ui/icons/
├── glyphs.ts
├── icon.types.ts
├── icon-renderer.ts
├── IconGenerator.vue
└── presets/
    ├── items.ts
    ├── skills.ts
    └── effects.ts
```

Пример типа:

```ts
type IconConfig = {
  id: string
  glyph: GlyphName
  category: IconCategory
  rarity?: Rarity
  modifier?: ModifierName
  state?: IconState
}
```

Пример предмета:

```ts
{
  id: 'flameblade',
  glyph: 'sword',
  category: 'weapon',
  rarity: 'epic',
  modifier: 'fire'
}
```

## Renderer composition

Иконка должна собираться слоями:

1. neutral dark background;
2. base glyph;
3. optional modifier;
4. rarity border/accent;
5. optional state overlay.

### Modifier examples

`fire`:
- warm inner glow;
- small flame fragments;
- orange/red accent.

`ice`:
- blue cold highlight;
- small crystal fragments.

`poison`:
- green toxic accent.

Не делай эффекты чрезмерно яркими.

## States

Поддержать минимум:

- default;
- selected;
- equipped;
- locked;
- disabled;
- new.

Примеры:

- selected → accent outline;
- equipped → small marker;
- locked → dark overlay + lock;
- disabled → muted/opacity;
- new → subtle indicator.

## Критерий успеха Icon Generator

Добавление нового обычного предмета не должно требовать ручного рисования новой иконки. Должно быть достаточно добавить data-конфиг.

Система должна быть комбинаторной: `glyph + rarity + modifier + state`.

---

# 4. Atomic UI Components

Создай или приведи к единому стандарту:

- `UIButton`;
- `UIPanel`;
- `UICard`;
- `UIHealthBar`;
- `UITabs`;
- `UIModal`;
- `UIToast`;
- `UILoadingState`;
- `UIItemSlot`.

## UIButton

Поддержать:

- default;
- hover;
- active/pressed;
- disabled;
- loading.

Варианты не раздувать. Предпочтительно:

- primary;
- secondary;
- ghost;
- danger.

Не создавать `primaryGold`, `primaryCombat`, `primaryBoss`, `primaryLocation` и подобные контекстные варианты.

---

# 5. Dev UI Playground

Создай dev-only страницу/route, например `/dev/ui`.

Покажи там:

- все Button variants;
- Panel/Card;
- HealthBar;
- Tabs;
- Toast;
- Modal;
- Loading;
- Empty;
- Disabled;
- Cooldown;
- Common/Rare/Epic/Legendary item;
- Fire/Ice/Poison modifiers;
- selected/equipped/locked/new states.

Цель: любое изменение tokens/generator/components должно быть видно на одном экране.

Не включай dev route в production navigation.

---

# 6. Guidelines v0.1

Создай или обнови `UI_DESIGN_GUIDELINES.md`.

Документировать:

- tokens;
- typography;
- spacing;
- rarity;
- glow rule;
- component list;
- icon generator architecture;
- modifiers;
- states;
- mobile rules;
- Telegram rules.

Документ должен быть коротким Source of Truth, а не огромной теоретической статьёй.

---

# SESSION 1 — DEFINITION OF DONE

Session 1 завершена только если:

- tokens.css реально используется;
- новые компоненты используют tokens;
- нет новых hardcoded UI colors без объяснимой причины;
- IconGenerator работает data-driven;
- glyphs не используют emoji;
- rarity не превращает весь item background в яркий цвет;
- dev UI playground работает;
- components отображают основные states;
- UI_DESIGN_GUIDELINES.md обновлён;
- build проходит;
- typecheck проходит;
- tests проходят;
- console errors отсутствуют.

После завершения Session 1:

**ОСТАНОВИСЬ.**

Сделай краткий отчёт:

1. что изменено;
2. какие файлы созданы;
3. какие старые проблемы обнаружены;
4. результаты build/typecheck/tests;
5. что осталось для Session 2.

Не начинай Session 2 автоматически, если явно не попросили продолжать.

---

# SESSION 2 — MIGRATION + SYSTEM STATES

## Цель

Перевести существующие ключевые экраны на новую UI-систему и удалить старый визуальный хаос.

## 1. Global visual cleanup

Проведи аудит существующего CSS.

Удаляй:

- дублирование;
- бессмысленные gradients;
- постоянные glow;
- hardcoded colors;
- одноразовые стили, которые заменены UI components.

Но не удаляй стиль вслепую. Сначала убедись, что соответствующий экран мигрирован.

## 2. Telegram / Mobile foundation

Добавь корректную поддержку:

- safe-area-inset-top;
- safe-area-inset-bottom;
- safe-area-inset-left;
- safe-area-inset-right;
- Telegram viewport;
- Telegram theme fallbacks;
- touch interaction;
- корректное поведение на узких viewport.

Не запрещай accessibility zoom глобально. Не позволяй inputs случайно вызывать неудобный zoom.

## 3. Haptics

Haptics использовать редко.

Допустимо:

- critical combat event;
- legendary drop;
- level up;
- dangerous confirmation;
- важное successful action.

Не использовать вибрацию на каждом tab/button press.

## 4. System states

Для интерактивных элементов и экранов внедрить:

- loading;
- disabled;
- error;
- empty;
- cooldown;
- selected;
- equipped;
- locked;
- new.

Никакой action не должен выглядеть как «мёртвая кнопка».

Пользователь всегда должен понимать:

- действие началось;
- действие недоступно;
- произошла ошибка;
- нужно ждать cooldown;
- список пуст.

## 5. Combat migration

Пересобрать UI боя на новых primitives.

Приоритет информации:

1. enemy context;
2. enemy HP/status;
3. enemy visual;
4. combat feedback;
5. player HP/resource;
6. large action controls.

Combat log сделать компактным. На маленьком экране игровые действия важнее длинного текста лога.

## 6. Inventory migration

Использовать:

- UIItemSlot;
- UITabs;
- tokens;
- unified item states.

Проверить:

- empty inventory;
- selected item;
- equipped item;
- locked item;
- rarity;
- modifier;
- long names;
- narrow mobile viewport.

## 7. Location migration

Использовать:

- UICard;
- UIButton;
- tokens;
- loading/error/disabled states.

Экран должен показывать игровые решения, а не выглядеть как список одинаковых технических кнопок.

---

# SESSION 2 — DEFINITION OF DONE

- Combat использует новые primitives;
- Inventory использует новые primitives;
- Location использует новые primitives;
- hardcoded UI CSS существенно сокращён;
- нет двух параллельных button/card systems;
- safe areas проверены;
- narrow mobile viewport проверен;
- system states визуально понятны;
- build проходит;
- typecheck проходит;
- tests проходят;
- console errors = 0;
- базовый game flow не сломан.

После завершения: **ОСТАНОВИСЬ и дай отчёт.**

---

# SESSION 3 — POLISH + CONTENT + FLOW

## Цель

Оживить интерфейс после завершения foundation и migration.

## 1. Microanimations

Использовать преимущественно CSS.

Добавить:

- button press feedback;
- panel/card appearance;
- tab transition;
- HealthBar interpolation;
- Mana/resource interpolation;
- damage numbers;
- critical feedback;
- cooldown visual;
- item equipped feedback.

Не добавлять тяжёлую декоративную анимацию без игровой пользы.

## 2. Reduced motion

Учесть `prefers-reduced-motion`.

При reduced motion:

- отключать или сильно сокращать декоративные transitions;
- сохранять важную функциональную обратную связь.

## 3. Batch content configs

Создать data-driven конфиги для существующих:

- items;
- consumables;
- talents;
- effects;
- resources.

Не генерировать вручную сотни отдельных SVG-файлов, если иконки могут рендериться runtime generator.

Если project architecture требует static output — renderer может иметь отдельный build/export script.

## 4. Accessibility Lite

Проверить:

- text contrast;
- 44x44 touch targets;
- focus-visible;
- disabled semantics;
- buttons vs div-click handlers;
- readable font sizes;
- long labels;
- narrow screens.

## 5. Final flow test

Полностью пройти:

`Мир → выбор локации → путешествие → локация → бой → победа → лут → награда / инвентарь`

Проверить:

- loading;
- failure;
- retry;
- disabled action;
- cooldown;
- no loot / empty;
- navigation back;
- page reload/reconnect, если поддерживается текущей фазой проекта.

---

# SESSION 3 — DEFINITION OF DONE

- flow полностью проходим;
- UI визуально консистентен;
- item icons формируются из data-driven системы;
- microfeedback присутствует;
- reduced motion поддержан;
- touch targets проверены;
- нет console errors;
- build проходит;
- typecheck проходит;
- tests проходят;
- mobile browser verification выполнен.

---

# ASSET STRATEGY

## SVG / Procedural

Использовать генератор для:

- обычных предметов;
- расходников;
- ресурсов;
- талантов;
- status effects;
- utility glyphs.

## Raster Art — WebP/AVIF

Использовать отдельный арт для:

- location backgrounds;
- cities;
- major environments;
- characters;
- enemies;
- bosses;
- victory screens;
- important story moments;
- уникальных hero/legendary assets, если процедурной иконки недостаточно.

Не пытаться рисовать сложные игровые background illustrations в SVG.

---

# NON-GOALS

В рамках этих трёх сессий НЕ нужно:

- переделывать backend без необходимости;
- вводить новый framework;
- переписывать весь frontend с нуля;
- создавать полноценный Storybook, если dev playground достаточно;
- делать cinematic animations;
- генерировать сотни AI backgrounds;
- делать onboarding/tutorial;
- добавлять новые игровые системы;
- менять геймдизайн.

Главная задача:

**систематизировать существующий UI и создать foundation для дальнейшего масштабирования Elyndor.**
