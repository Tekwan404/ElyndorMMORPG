# Elyndor — UI/UX Specification 05 — Character Stats

**Document:** `UI_05_CHARACTER_STATS.md`  
**Status:** Approved foundation  
**Platform:** Telegram Mini App  
**Orientation:** Mobile Portrait First  
**Depends on:**
- `UI_03_HERO.md`
- `06_ATTRIBUTES_AND_STATS_SYSTEM.md`
- `13_ITEM_EQUIPMENT_SYSTEM.md`
- `16_TALENT_SYSTEM.md`

---

# 1. Назначение

Экран `ХАРАКТЕРИСТИКИ` объясняет игроку силу персонажа и происхождение итоговых значений, не превращая интерфейс в бухгалтерскую таблицу.

Он использует только утверждённые Stats из `06_ATTRIBUTES_AND_STATS_SYSTEM.md` и не вводит скрытых характеристик.

---

# 2. Основная структура

```text
[GLOBAL HUD]
[Hero Tabs]

ХАРАКТЕРИСТИКИ

Level 27 | Gear Score 1248
Max HP 3840 | Resource 100

ОСНОВНЫЕ
Strength
Agility
Intellect
Stamina

АТАКА
Attack Power
Spell Power
Critical Chance
Critical Damage
Accuracy
Armor Penetration
Magic Penetration
Attack Speed

ЗАЩИТА
Armor
Magic Resistance
Dodge
```

Группы идут именно в этом порядке.

---

# 3. Строка характеристики

Каждая строка содержит:

```text
[icon] Название                Значение >
```

Tap открывает detail sheet.

Процентные Stats показываются процентом. Flat Stats — числом. Attack Speed показывает понятное итоговое значение/множитель согласно read-model, а не внутренний raw coefficient.

---

# 4. Detail Sheet

Пример:

```text
КРИТИЧЕСКИЙ УДАР
18.4%

База          5%
Экипировка   +8.4%
Таланты      +5%

Шанс нанести критический удар.

[ ПОДРОБНЫЕ ИСТОЧНИКИ ]
```

Первая разбивка всегда агрегированная:

```text
Base
Equipment
Talents
Effects
```

Подробные источники раскрываются отдельно.

---

# 5. Подробные источники

Expanded breakdown может показать:

```text
Посох Жреца            +25 Intellect
Мантия Мудреца         +18
Кольцо Разума           +8
Талант: Ясность         +6
Blessing Effect         +6
```

UI не пересчитывает значения самостоятельно: сервер/read-model отдаёт breakdown.

---

# 6. Armor / Magic Resistance

Для Armor и Magic Resistance показывать полезный **пример против равного по уровню противника**, но обязательно маркировать его как оценку.

```text
Armor 420
≈ 80.8% reduction vs equal-level physical damage
```

Ниже:

```text
Фактический результат зависит от уровня,
penetration и конкретного эффекта.
```

Так игрок понимает смысл числа, но не принимает estimate за абсолютную гарантию.

---

# 7. Class Relevance

UI может визуально слегка выделять Primary Attribute текущего класса:

```text
Warrior → Strength
Archer → Agility
Mage → Intellect
Arcane Archer → Intellect
```

Но остальные Stats не скрываются.

Не добавлять automatic labels `лучший стат` / `плохой стат` для build.

---

# 8. Buff / Debuff State

Temporary modifiers могут отображаться отдельным marker:

```text
Intellect 96  (+6 effect)
```

Tap показывает active source и remaining duration, если Effect System это предоставляет.

---

# 9. Gear Score

Gear Score остаётся display-only.

Он присутствует в summary, но не сортирует все Stats и не заменяет подробный breakdown.

---

# 10. Visual Style

Использовать наш Elyndor dark-fantasy visual language:

- deep blue/black panel backgrounds;
- bright stat icons;
- restrained gold frames;
- violet/arcane highlights;
- минимум бронзовой «старинной таблицы»;
- панели чуть прозрачнее, чем в неудачном промежуточном mockup.

Основной reference по структуре — последний mockup Characteristics, но **не по цветовой стилистике**.

---

# 11. Approved Decisions

1. Все утверждённые Stats видимы.
2. Stats grouped: Primary / Attack / Defense.
3. Tap stat → explanation.
4. Breakdown: Base / Equipment / Talents / Effects.
5. Detailed sources доступны вторым уровнем.
6. Armor/MR показывают equal-level estimate с предупреждением.
7. Gear Score display-only.
8. Не показывать Spirit / Block / Parry / CastSpeed.
