# Elyndor — Equipment Sets Level 5–30

**Документ:** 24_EQUIPMENT_SETS_LEVEL_5_30.md  
**Статус:** Content / Source of Truth  
**Классы:** Warrior / Archer / Mage  
**Диапазон:** Level 5–30  
**Модель:** фиксированные предметы, без случайных аффиксов

> **Единицы secondary stats:** `Accuracy`, `CriticalChance`, `AttackSpeed`, `ArmorPenetration`, `MagicPenetration` в этом документе указаны в процентных пунктах (%), если прямо не сказано иначе.

---

# 1. Главный принцип

На ранних уровнях Elyndor не создаёт новый комплект экипировки для каждого контрольного уровня.

Вместо этого существуют **базовые семейства сетов**.

Например:

```text
Сет Следопыта
Level 5
↓
Сет Следопыта
Level 10
↓
Сет Следопыта
Level 15
↓
...
↓
Сет Следопыта
Level 30
```

Это всё тот же архетип экипировки, но каждая версия имеет:

- другой RequiredLevel;
- более высокий stat budget;
- более высокое качество;
- более высокий Armor / Weapon Damage;
- иногда усиленную величину set bonus.

Название сета и его игровая идея сохраняются.

---

# 2. Контрольные уровни

Для первой прогрессии используются:

```text
Level 5
Level 10
Level 15
Level 18
Level 22
Level 26
Level 30
```

После 30 уровня шкала может продолжаться тем же способом.

---

# 3. Качество по прогрессии

Рекомендуемая ранняя лестница:

| Required Level | Типичное качество |
|---:|---|
| 5 | COMMON |
| 10 | UNCOMMON |
| 15 | UNCOMMON / RARE |
| 18 | RARE |
| 22 | RARE |
| 26 | EPIC |
| 30 | EPIC |

Качество **не привязано навсегда к уровню**.

Босс 15 уровня, например, может дать Rare-версию предмета, хотя обычная версия этого диапазона была бы Uncommon.

---

# 4. Что НЕ даём на экипировке

На обычной экипировке не используются прямые бонусы:

```text
PHYSICAL_PET Damage +X%
SPIRIT_PET Damage +X%
Pet CriticalChance +X%
Pet AttackSpeed +X%
```

Это слишком прямолинейно и превращает питомца в отдельную таблицу процентов на шмоте.

Питомец усиливается через:

- характеристики хозяина;
- Talent Tree;
- Companion System;
- abilities;
- scaling formulas;
- редкие будущие уникальные эффекты.

Обычная экипировка усиливает **персонажа**, а питомец получает выгоду через class scaling.

---

# 5. Состав полного сета

Для ранней игры основной armor set состоит из:

```text
HEAD
CHEST
HANDS
LEGS
FEET
```

То есть:

**5 предметов.**

Weapon, Amulet, Rings, Cloak и Off-Hand не являются обязательной частью armor set.

Так игрок может:

- собирать сет;
- менять оружие отдельно;
- менять украшения под билд;
- не быть заперт в 10 обязательных предметах одного набора.

---

# 6. Set Bonuses

На раннем этапе используются:

```text
2 предмета
4 предмета
```

Пятый предмет нужен для гибкости замены и полного визуального комплекта, но отдельный бонус за 5/5 пока не нужен.

Set bonuses должны быть небольшими.

Они поддерживают архетип, но не делают четыре предмета старого сета обязательными на десять уровней вперёд.

---

# 7. Общий Stat Budget

Числа ниже являются **первым балансным ориентиром**, а не окончательной формулой itemization.

Для Primary Attribute на одном типичном armor item:

| Level | Малый слот | Большой слот |
|---:|---:|---:|
| 5 | 1–2 | 2–3 |
| 10 | 2–3 | 4–5 |
| 15 | 4–5 | 6–8 |
| 18 | 5–6 | 8–10 |
| 22 | 7–8 | 11–13 |
| 26 | 9–10 | 14–16 |
| 30 | 11–13 | 18–20 |

Большие слоты:

```text
CHEST
LEGS
```

Малые:

```text
HEAD
HANDS
FEET
```

Secondary stats распределяются в меньшем бюджете.

---

# 8. WARRIOR — Set Family A

# 🛡️ Комплект Железного Стража

**Назначение:** Tank / Guardian Warrior.

Основные характеристики:

```text
Strength
Stamina
Armor
MagicResistance
Accuracy
```

Не используем Block и Parry.

## Приоритет слотов

### HEAD
- Stamina
- MagicResistance

### CHEST
- высокий Armor
- Stamina
- Strength

### HANDS
- Strength
- Accuracy

### LEGS
- высокий Armor
- Stamina
- MagicResistance

### FEET
- Stamina
- Armor

---

## Set Bonus

### 2 предмета

```text
Stamina +3%
```

### 4 предмета

```text
Threat от Auto Attack и физических abilities +5%
```

Это не делает Guardian обязательным для DPS Warrior.

---

# 9. Железный Страж — версии

## Level 5 — COMMON

Пример полного набора:

```text
Шлем Железного Стража
+2 Stamina

Кираса Железного Стража
+3 Stamina
+2 Strength

Перчатки Железного Стража
+2 Strength

Поножи Железного Стража
+3 Stamina

Сапоги Железного Стража
+2 Stamina
```

Минимальный introduction-set.

---

## Level 10 — UNCOMMON

```text
HEAD
+3 Stamina
+2 MagicResistance

CHEST
+5 Stamina
+4 Strength

HANDS
+3 Strength
+2 Accuracy

LEGS
+5 Stamina
+3 MagicResistance

FEET
+3 Stamina
+Armor budget
```

---

## Level 15 — RARE

```text
HEAD
+5 Stamina
+3 Strength
+3 MagicResistance

CHEST
+8 Stamina
+6 Strength
+high Armor

HANDS
+5 Strength
+3 Accuracy

LEGS
+8 Stamina
+4 MagicResistance
+high Armor

FEET
+5 Stamina
+3 Strength
```

---

## Level 18 — RARE

```text
HEAD
+6 Stamina
+4 Strength
+4 MagicResistance

CHEST
+10 Stamina
+8 Strength

HANDS
+6 Strength
+4 Accuracy

LEGS
+10 Stamina
+5 MagicResistance

FEET
+6 Stamina
+4 Strength
```

---

## Level 22 — RARE

```text
HEAD
+8 Stamina
+5 Strength
+5 MagicResistance

CHEST
+13 Stamina
+11 Strength

HANDS
+8 Strength
+5 Accuracy

LEGS
+13 Stamina
+7 MagicResistance

FEET
+8 Stamina
+5 Strength
```

---

## Level 26 — EPIC

```text
HEAD
+10 Stamina
+7 Strength
+6 MagicResistance

CHEST
+16 Stamina
+14 Strength

HANDS
+10 Strength
+6 Accuracy

LEGS
+16 Stamina
+8 MagicResistance

FEET
+10 Stamina
+7 Strength
```

---

## Level 30 — EPIC

```text
HEAD
+13 Stamina
+9 Strength
+8 MagicResistance

CHEST
+20 Stamina
+18 Strength

HANDS
+13 Strength
+8 Accuracy

LEGS
+20 Stamina
+10 MagicResistance

FEET
+13 Stamina
+9 Strength
```

---

# 10. WARRIOR — Set Family B

# ⚔️ Комплект Кровавого Завоевателя

**Назначение:** Berserker / Physical DPS Warrior.

Основные характеристики:

```text
Strength
CriticalChance
Accuracy
AttackSpeed
ArmorPenetration
```

Stamina присутствует, но слабее, чем у Железного Стража.

---

## Set Bonus

### 2 предмета

```text
CriticalChance +2%
```

### 4 предмета

```text
После расходования 30+ Rage одной способностью:
AttackPower +4% на 4 sec
InternalCooldown = 8 sec
```

---

## Scaling Profile

Вместо повторения каждого предмета:

| Level | Strength | Secondary budget |
|---:|---:|---:|
| 5 | низкий | Accuracy |
| 10 | низкий-средний | Accuracy / Crit |
| 15 | средний | Crit / AttackSpeed |
| 18 | средний+ | Crit / Accuracy |
| 22 | высокий | Crit / ArmorPen |
| 26 | высокий+ | AttackSpeed / ArmorPen |
| 30 | максимальный ранний | Crit / ArmorPen / Accuracy |

### Level 30 пример

```text
HEAD
+11 Strength
+4% CriticalChance

CHEST
+18 Strength
+8 Stamina

HANDS
+13 Strength
+5% AttackSpeed

LEGS
+18 Strength
+4% ArmorPenetration

FEET
+11 Strength
+5% Accuracy
```

---

# 11. ARCHER — Set Family A

# 🏹 Комплект Следопыта

**Назначение:** Меткая стрельба / физический Лучник.

Основные характеристики:

```text
Agility
Accuracy
CriticalChance
AttackSpeed
ArmorPenetration
```

Никаких прямых pet damage modifiers.

---

## Set Bonus

### 2 предмета

```text
Accuracy +2%
```

### 4 предмета

```text
После Critical Shot:
следующая Auto Attack получает +6% Damage.
Duration = 5 sec.
```

Не запускает отдельную дополнительную атаку.

---

## Level 5 — COMMON

```text
HEAD
+2 Agility

CHEST
+3 Agility
+2 Stamina

HANDS
+2 Agility

LEGS
+3 Agility

FEET
+2 Agility
```

---

## Level 10 — UNCOMMON

```text
HEAD
+3 Agility
+2 Accuracy

CHEST
+5 Agility
+3 Stamina

HANDS
+3 Agility
+2 CriticalChance

LEGS
+5 Agility
+2 Accuracy

FEET
+3 Agility
+2 AttackSpeed
```

---

## Level 15 — RARE

```text
HEAD
+5 Agility
+3 Accuracy

CHEST
+8 Agility
+5 Stamina

HANDS
+5 Agility
+3 CriticalChance

LEGS
+8 Agility
+3 ArmorPenetration

FEET
+5 Agility
+3 AttackSpeed
```

---

## Level 18 — RARE

```text
HEAD
+6 Agility
+4 Accuracy

CHEST
+10 Agility
+6 Stamina

HANDS
+6 Agility
+4 CriticalChance

LEGS
+10 Agility
+4 ArmorPenetration

FEET
+6 Agility
+4 AttackSpeed
```

---

## Level 22 — RARE

```text
HEAD
+8 Agility
+5 Accuracy

CHEST
+13 Agility
+8 Stamina

HANDS
+8 Agility
+5 CriticalChance

LEGS
+13 Agility
+5 ArmorPenetration

FEET
+8 Agility
+5 AttackSpeed
```

---

## Level 26 — EPIC

```text
HEAD
+10 Agility
+6 Accuracy

CHEST
+16 Agility
+10 Stamina

HANDS
+10 Agility
+6 CriticalChance

LEGS
+16 Agility
+6 ArmorPenetration

FEET
+10 Agility
+6 AttackSpeed
```

---

## Level 30 — EPIC

```text
HEAD
+13 Agility
+8 Accuracy

CHEST
+20 Agility
+13 Stamina

HANDS
+13 Agility
+8 CriticalChance

LEGS
+20 Agility
+8 ArmorPenetration

FEET
+13 Agility
+8 AttackSpeed
```

---

# 12. ARCHER — Set Family B

# ✨ Комплект Эфирного Охотника

**Назначение:** Тайный стрелок.

Основные характеристики:

```text
Intellect
SpellPower
MagicPenetration
Accuracy
Mana
```

Важно:

этот сет **не усиливает SPIRIT_PET напрямую**.

Spirit получает выгоду через scaling класса от Intellect / SpellPower владельца.

---

## Set Bonus

### 2 предмета

```text
MaxMana +4%
```

### 4 предмета

```text
После Critical Magical Arrow:
MagicPenetration +4% на 5 sec.
InternalCooldown = 8 sec.
```

---

## Scaling Profile

### Level 5
На этом уровне сет может ещё не выпадать, если `Тайны Магии` недоступны так рано.

Если ветка доступна:
- небольшой Intellect;
- Mana;
- Accuracy.

### Level 10
- Intellect;
- SpellPower;
- Accuracy.

### Level 15
- Intellect;
- SpellPower;
- MagicPenetration.

### Level 18
- Intellect;
- SpellPower;
- Mana;
- MagicPenetration.

### Level 22
- высокий Intellect;
- SpellPower;
- Accuracy;
- MagicPenetration.

### Level 26
- Intellect;
- SpellPower;
- MagicPenetration;
- Mana.

### Level 30 пример

```text
HEAD
+13 Intellect
+8 Accuracy

CHEST
+20 Intellect
+18 SpellPower

HANDS
+13 Intellect
+8% CriticalChance для Magical Arrow abilities

LEGS
+20 Intellect
+8 MagicPenetration

FEET
+13 Intellect
+MaxMana bonus
```

---

# 13. Что носит Повелитель зверей

Для Beast Mastery **не нужен отдельный обязательный pet-stat set**.

Это принципиальное решение.

Повелитель зверей выбирает между:

```text
Комплект Следопыта
```

и универсальными предметами с:

```text
Agility
AttackSpeed
Accuracy
CriticalChance
Focus / Focus regeneration
Stamina
```

Питомец масштабируется от хозяина через Companion System.

Это позволяет Beast Mastery использовать обычную экипировку Лучника и не создаёт отдельную экономику предметов вида:

```text
Pet Damage
Pet Crit
Pet Haste
```

---

# 14. MAGE — Set Family A

# ✦ Комплект Пламенного Адепта

**Назначение:** основной Magical DPS.

Основные характеристики:

```text
Intellect
SpellPower
CriticalChance
MagicPenetration
Accuracy
```

---

## Set Bonus

### 2 предмета

```text
SpellPower +3%
```

### 4 предмета

```text
Critical Magical Damage увеличивается ещё на 5%.
```

---

## Level 5 — COMMON

```text
HEAD
+2 Intellect

CHEST
+3 Intellect
+2 Stamina

HANDS
+2 Intellect

LEGS
+3 Intellect

FEET
+2 Intellect
```

---

## Level 10 — UNCOMMON

```text
HEAD
+3 Intellect
+2 Accuracy

CHEST
+5 Intellect
+4 SpellPower

HANDS
+3 Intellect
+2 CriticalChance

LEGS
+5 Intellect
+3 SpellPower

FEET
+3 Intellect
+Mana
```

---

## Level 15 — RARE

```text
HEAD
+5 Intellect
+3 Accuracy

CHEST
+8 Intellect
+7 SpellPower

HANDS
+5 Intellect
+3 CriticalChance

LEGS
+8 Intellect
+5 SpellPower

FEET
+5 Intellect
+3 MagicPenetration
```

---

## Level 18 — RARE

```text
HEAD
+6 Intellect
+4 Accuracy

CHEST
+10 Intellect
+9 SpellPower

HANDS
+6 Intellect
+4 CriticalChance

LEGS
+10 Intellect
+7 SpellPower

FEET
+6 Intellect
+4 MagicPenetration
```

---

## Level 22 — RARE

```text
HEAD
+8 Intellect
+5 Accuracy

CHEST
+13 Intellect
+12 SpellPower

HANDS
+8 Intellect
+5 CriticalChance

LEGS
+13 Intellect
+9 SpellPower

FEET
+8 Intellect
+5 MagicPenetration
```

---

## Level 26 — EPIC

```text
HEAD
+10 Intellect
+6 Accuracy

CHEST
+16 Intellect
+15 SpellPower

HANDS
+10 Intellect
+6 CriticalChance

LEGS
+16 Intellect
+12 SpellPower

FEET
+10 Intellect
+6 MagicPenetration
```

---

## Level 30 — EPIC

```text
HEAD
+13 Intellect
+8 Accuracy

CHEST
+20 Intellect
+19 SpellPower

HANDS
+13 Intellect
+8 CriticalChance

LEGS
+20 Intellect
+15 SpellPower

FEET
+13 Intellect
+8 MagicPenetration
```

---

# 15. MAGE — Set Family B

# 🔮 Комплект Хранителя Маны

**Назначение:** sustained caster / defensive magic / resource build.

Основные характеристики:

```text
Intellect
MaxMana
Mana regeneration
Stamina
MagicResistance
Accuracy
```

---

## Set Bonus

### 2 предмета

```text
MaxMana +5%
```

### 4 предмета

```text
Когда Mana падает ниже 30%:
Mana Cost abilities −6% на 8 sec.
InternalCooldown = 30 sec.
```

---

## Scaling Profile

| Level | Главный акцент |
|---:|---|
| 5 | Intellect / Mana |
| 10 | Intellect / Mana / Stamina |
| 15 | Mana / Accuracy |
| 18 | Mana regen / MagicResistance |
| 22 | Intellect / MaxMana / Stamina |
| 26 | Mana regen / Accuracy / MR |
| 30 | высокий Intellect + resource sustain |

### Level 30 пример

```text
HEAD
+13 Intellect
+MaxMana
+MagicResistance

CHEST
+20 Intellect
+13 Stamina
+MaxMana

HANDS
+13 Intellect
+8 Accuracy

LEGS
+20 Intellect
+Mana regeneration
+MagicResistance

FEET
+13 Intellect
+Mana regeneration
```

---

# 16. Итоговые семейства

На Level 1–30 достаточно шести основных armor-set families:

```text
WARRIOR
├── 🛡️ Железный Страж
└── ⚔️ Кровавый Завоеватель

ARCHER
├── 🏹 Следопыт
└── ✨ Эфирный Охотник

MAGE
├── ✦ Пламенный Адепт
└── 🔮 Хранитель Маны
```

Это не означает, что игрок носит только эти предметы.

Между сетовыми предметами должны существовать:

- dungeon drops;
- boss items;
- quest rewards;
- standalone weapons;
- standalone jewelry;
- отдельные Rare/Epic pieces.

---

# 17. Почему не нужен отдельный Beast Master set

Повелитель зверей уже получает огромную часть своей идентичности из:

- Talent Tree;
- pet archetype;
- Companion scaling;
- pet abilities.

Если добавить обычный сет:

```text
Pet Damage +10%
Pet Crit +8%
Pet AttackSpeed +12%
```

то экипировка начнёт определять питомца сильнее самого Talent Tree.

Это неправильный приоритет.

Правильнее:

```text
Agility ↑
AttackSpeed ↑
Focus sustain ↑
Accuracy ↑
CriticalChance ↑
```

↓

хозяин становится сильнее

↓

Companion System рассчитывает часть роста питомца от хозяина.

---

# 18. Оружие остаётся отдельной прогрессией

Armor Set не определяет Weapon.

На тех же контрольных уровнях:

```text
5 / 10 / 15 / 18 / 22 / 26 / 30
```

должны существовать отдельные weapon families:

```text
Warrior:
1H / 2H

Archer:
Bow

Mage:
Staff / Wand
```

Weapon progression особенно важна, потому что Weapon задаёт:

- Weapon Damage;
- BaseAttackInterval;
- часть scaling abilities.

---

# 19. Следующий шаг баланса

Перед созданием полной базы предметов нужно отдельно утвердить:

```text
ITEM_STAT_BUDGET
```

Он должен определять:

- сколько Primary Stat стоит один budget point;
- сколько стоит 1% Crit;
- сколько стоит 1% Accuracy;
- сколько стоит 1% AttackSpeed;
- сколько стоит 1% ArmorPenetration;
- сколько стоит 1% MagicPenetration;
- сколько стоит SpellPower;
- сколько стоит Stamina;
- сколько стоит Armor;
- сколько стоит MaxMana/Focus;
- сколько стоит regeneration.

До этого текущие значения используются как **content prototype**.

---

# 20. Главный принцип раннего gear progression

Игрок должен узнавать знакомую линейку:

```text
Я нашёл Следопыта 10 уровня.
↓
Через несколько уровней ищу Следопыта 15.
↓
Но Rare chest с босса 13 уровня может быть лучше
моей обычной части сета.
↓
Я сравниваю реальные характеристики,
а не просто надеваю предмет с большим Level.
```

Gear progression должна создавать выбор, а не автоматическую замену каждого предмета при повышении уровня.


---

# Authoritative Itemization Clarifications

В этом content-документе значения:
- `Accuracy`;
- `CriticalChance`;
- `AttackSpeed`;
- `ArmorPenetration`;
- `MagicPenetration`

в строках вида `+N` трактуются как **percentage points**, если явно не указана другая единица.

Primary Attributes (`Strength`, `Agility`, `Intellect`, `Stamina`) и `SpellPower` являются flat values.

`MaxMana` / regeneration являются Resource modifiers.

Никаких `Block`, `Parry`, `CastSpeed`, `Spirit` и прямых обычных `Pet Damage/Crit/AttackSpeed` affix здесь не используется.

Set bonuses реализуются через `SetDefinition` из Item System.
