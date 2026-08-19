# Elyndor — Classes & Character Creation

**Документ:** 19_CLASS_ROSTER_AND_CHARACTER_CREATION.md  
**Система:** Classes / Character Creation  
**Статус:** Foundation / Source of Truth  
**Версия:** 0.1  
**Level Cap:** 60

---

# 1. Назначение

Этот документ фиксирует:

- доступные классы Elyndor;
- классы первого игрового запуска;
- будущие классы;
- выбор расы;
- выбор пола;
- имя персонажа;
- базовый порядок создания персонажа;
- границы влияния визуальных параметров на gameplay.

Документ не определяет:

- конкретные Talent Tree;
- полный список abilities;
- финальные class balance values;
- внешний вид моделей/портретов;
- косметические предметы;
- сюжет рас;
- PvP balance.

---

# 2. Первый набор классов

Текущий playable roster состоит из трёх классов:

| Класс | Роль | Основной атрибут | Ресурс | Основная идея |
|---|---|---|---|---|
| **Воин** | Tank / Physical DPS / Support | Strength | Rage | ближний бой, тяжёлая броня, Rage, защита, физический урон |
| **Лучник** | Ranged Physical DPS | Agility | Focus | дистанционный физический бой, точность, крит, быстрые атаки |
| **Маг** | Ranged Magical DPS | Intellect | Mana | заклинания, магический урон, эффекты, контроль |

Это стартовый playable roster.

---

# 3. Будущие классы

После первых трёх классов планируется добавить:

| Класс | Предварительная роль | Основной атрибут | Ресурс |
|---|---|---|---|
| **Жрец** | Healer / Support / Magical DPS | Intellect | Mana |
| **Разбойник** | Melee Physical DPS | Agility | Energy |

Эти классы являются следующим class-content expansion после стабилизации текущего roster.

---

# 4. Воин

**ClassId:** `WARRIOR`

## Роль

Воин должен поддерживать несколько направлений развития:

- Tank;
- Physical DPS;
- Party Support / Hybrid.

## Основной атрибут

`Strength`

## Ресурс

`Rage`

## Боевая идея

Воин находится в ближнем бою и использует оружие как основной источник Auto Attack.

Rage:

- начинается с низкого или нулевого значения;
- генерируется в бою;
- расходуется на сильные активные способности;
- может генерироваться от нанесения и получения урона.

## Экипировка

Предварительно:

- Medium Armor;
- Heavy Armor.

Оружие:

- One-Hand Sword;
- Two-Hand Sword;
- Axe;
- Mace.

## Talent Tree

Три направления:

- **Страж** — Tank;
- **Берсерк** — Physical DPS;
- **Командир** — Party Support / Hybrid.

---

# 5. Лучник

**ClassId:** `ARCHER`

## Роль

`Ranged Physical DPS`

Лучник должен быть основным физическим ranged-классом.

## Основной атрибут

`Agility`

## Ресурс

`Focus`

Focus является отдельным Action Resource Archetype.

Перед реализацией Лучника `07_RESOURCE_SYSTEM` должен быть расширен новым архетипом:

`FOCUS`

## Базовая идея Focus

Focus:

- имеет ограниченный MaxResource;
- восстанавливается во время боя;
- используется для выстрелов и специальных атак;
- не требует получения урона для генерации;
- не должен быть полной копией Mana;
- рассчитан на постоянный боевой темп.

Базовый Focus profile определён Resource System:

```text
MaxFocus = 100
StartingFocus = 100
RespawnFocus = 100
CombatRegen = 8/sec
OutOfCombatRegen = 12/sec
```

Ability costs и talent modifiers определяются class content.

## Боевая идея

Лучник:

- атакует с дистанции концептуально;
- в текущей Combat System дистанция не моделируется координатами;
- использует Bow как основной weapon profile;
- делает упор на Accuracy, CriticalChance, AttackSpeed и ArmorPenetration;
- использует Auto Attack как важную часть DPS.

## Экипировка

Предварительно:

- Light Armor;
- Medium Armor.

Оружие:

- Bow.

Дополнительные типы оружия могут быть добавлены позднее.

## Будущие Talent Tree направления

Конкретные ветки будут утверждены отдельным документом.

Рекомендуемая структура:

1. **Стрелок** — чистый ranged DPS.
2. **Охотник** — sustained damage / utility / возможно взаимодействие с питомцами в будущем.
3. **Следопыт** — крит, мобильность концептуально, контроль и tactical gameplay.

Питомцы не входят автоматически в первый implementation только из-за существования ветки Охотника.

---

# 6. Маг

**ClassId:** `MAGE`

## Роль

`Ranged Magical DPS`

## Основной атрибут

`Intellect`

## Ресурс

`Mana`

## Боевая идея

Маг:

- использует Casted и Instant abilities;
- наносит преимущественно Magical Damage;
- применяет Buff/Debuff/DoT/Shield через Effect System;
- имеет высокую силу abilities;
- обладает низкой физической защитой.

## Экипировка

Предварительно:

- Light Armor.

Оружие:

- Staff;
- Wand.

## Будущие Talent Tree направления

Конкретные ветки определяются отдельным документом.

Базовая структура может быть построена вокруг:

- прямого магического damage;
- DoT / effects;
- control / defensive magic.

---

# 7. Жрец

**ClassId:** `PRIEST`

**Статус:** Future Class

## Предварительная роль

- Healer;
- Support;
- Magical DPS.

## Основной атрибут

`Intellect`

## Ресурс

`Mana`

Жрец не входит в первый playable roster.

Конкретная механика Healing, offensive magic и Talent Tree определяется позже.

---

# 8. Разбойник

**ClassId:** `ROGUE`

**Статус:** Future Class

## Предварительная роль

`Melee Physical DPS`

## Основной атрибут

`Agility`

## Ресурс

`Energy`

Разбойник не входит в первый playable roster.

Его gameplay должен отличаться от Лучника несмотря на общий основной атрибут Agility:

- ближний бой;
- быстрый Energy cycle;
- burst;
- Critical;
- single-target pressure.

---

# 9. Создание персонажа

При первом создании персонажа игрок последовательно выбирает:

```text
Имя
↓
Раса
↓
Пол
↓
Класс
↓
Подтверждение
↓
Создание персонажа
```

Порядок в UI может быть изменён без изменения правил системы.

---

# 10. Имя персонажа

Игрок задаёт имя при создании персонажа.

Character хранит:

`CharacterName`

## Базовые правила

Имя:

- обязательно;
- не может быть пустым;
- проверяется сервером;
- сохраняется вместе с персонажем;
- отображается другим игрокам;
- не зависит от Telegram username.

Рекомендуемые ограничения:

- длина: 3–16 символов;
- пробелы в начале/конце запрещены;
- последовательные пробелы запрещены;
- служебные/зарезервированные имена запрещены.

Финальная политика:

- уникальность имени;
- допустимые алфавиты;
- смена имени

определяется отдельно.

Для первого запуска рекомендуется:

`CharacterName` уникален среди активных персонажей.

---

# 11. Раса

На первом этапе доступны:

- **Человек**
- **Нежить**

Внутренние идентификаторы:

```text
HUMAN
UNDEAD
```

## Влияние расы

Раса является только визуальным и identity-параметром.

Она **не влияет** на:

- Strength;
- Agility;
- Intellect;
- Stamina;
- HP;
- Resource;
- Damage;
- Healing;
- CriticalChance;
- Accuracy;
- Dodge;
- Armor;
- MagicResistance;
- abilities;
- Talent Tree;
- доступные классы;
- доступную экипировку;
- XP;
- loot;
- quests;
- combat formulas.

Человек и Нежить имеют одинаковый gameplay potential.

---

# 12. Race × Class

Все доступные расы могут выбирать любой доступный класс.

Для первого roster:

| Раса | Воин | Лучник | Маг |
|---|---:|---:|---:|
| Человек | ✓ | ✓ | ✓ |
| Нежить | ✓ | ✓ | ✓ |

Будущие Жрец и Разбойник также не должны автоматически иметь race restriction.

Race-class restrictions могут быть введены только отдельным осознанным design decision.

---

# 13. Пол персонажа

При создании выбирается:

- Мужской
- Женский

Внутреннее значение может храниться как:

```text
MALE
FEMALE
```

## Влияние пола

Пол влияет только на визуальное представление персонажа.

Пол **не влияет** на:

- Stats;
- Damage;
- HP;
- Resource;
- Class;
- Talents;
- Equipment power;
- abilities;
- progression;
- loot.

Оба варианта имеют полностью одинаковый gameplay.

---

# 14. Visual Identity

Минимальная Character Identity:

```text
CharacterIdentity
├── CharacterId
├── CharacterName
├── RaceId
├── Gender
└── ClassId
```

Позднее могут быть добавлены:

- лицо;
- причёска;
- цвет волос;
- цвет кожи;
- undead appearance variants;
- portrait;
- cosmetic equipment;
- titles.

Эти параметры не должны менять combat stats без отдельного design decision.

---

# 15. Character Creation Pipeline

Серверный pipeline:

```text
CreateCharacterRequest
↓
Validate CharacterName
↓
Validate RaceId
↓
Validate Gender
↓
Validate ClassId
↓
Load ClassDefinition
↓
Create Character Identity
↓
Apply Class BaseStatProfile
↓
Initialize Action Resource
↓
Grant Starting Abilities
↓
Grant Starting Equipment
↓
Set Starting Location
↓
Persist Character
↓
CharacterCreated
```

Все операции должны выполняться сервером.

---

# 16. Starting Class State

После создания персонажа система должна иметь:

```text
Level = 1
XP = 0

Race = selected
Gender = selected
Class = selected

Base Stats = ClassDefinition
Resource = Class Resource Archetype
KnownAbilities = StartingAbilityIds
Equipment = StartingEquipmentProfile
Location = StartingLocation
```

---

# 17. Race и Character System

Character System является владельцем сохранённого:

- RaceId;
- Gender;
- CharacterName;
- ClassId.

Race не должна создавать отдельную боевую подсистему.

---

# 18. Class и Character System

Class System предоставляет Character System:

- ClassDefinition;
- BaseStatProfile;
- LevelGrowthProfile;
- ResourceArchetype;
- equipment permissions;
- StartingAbilityIds;
- TalentTreeId.

Character хранит только `ClassId` как постоянную identity-ссылку.

---

# 19. Смена расы

Для первого запуска:

`Race Change = disabled`

Раса выбирается при создании персонажа.

Позднее race change может стать косметической функцией.

Так как Race не влияет на gameplay, смена расы не должна требовать пересчёта Stats.

---

# 20. Смена пола

Для первого запуска:

`Gender Change = disabled`

Позднее может быть добавлена как cosmetic character customization.

Она не требует изменения combat state.

---

# 21. Смена имени

Для первого запуска рекомендуется:

`Rename = disabled`

Позднее может быть добавлена отдельная Rename operation.

CharacterId никогда не меняется при Rename.

---

# 22. Смена класса

Для первого запуска:

`Class Change = disabled`

Класс является основным gameplay choice персонажа.

Смена класса требует отдельной системы миграции:

- Stats;
- Resource;
- Abilities;
- Talents;
- Equipment validation.

Поэтому обычная смена ClassId не допускается.

---

# 23. Первый экран создания персонажа

Рекомендуемый UX:

```text
┌────────────────────────────────┐
│        СОЗДАНИЕ ГЕРОЯ          │
│                                │
│ Имя: [________________]         │
│                                │
│ Раса                           │
│ [ Человек ] [ Нежить ]         │
│                                │
│ Пол                            │
│ [ Мужчина ] [ Женщина ]        │
│                                │
│ Класс                          │
│ [ Воин ] [ Лучник ] [ Маг ]    │
│                                │
│        [ СОЗДАТЬ ГЕРОЯ ]       │
└────────────────────────────────┘
```

При выборе класса центральная часть экрана может показывать:

- изображение класса;
- роль;
- основной атрибут;
- ресурс;
- краткое описание gameplay.

---

# 24. Карточки классов

## ⚔ Воин

```text
ВОИН

Роль:
Tank / Physical DPS / Support

Основной атрибут:
Strength

Ресурс:
Rage

Тяжёлый боец ближнего боя.
Получает ярость в бою и превращает её
в мощные атаки и защитные способности.
```

## 🏹 Лучник

```text
ЛУЧНИК

Роль:
Ranged Physical DPS

Основной атрибут:
Agility

Ресурс:
Focus

Дистанционный физический боец.
Использует точность, критические удары
и быстрый темп стрельбы.
```

## ✦ Маг

```text
МАГ

Роль:
Ranged Magical DPS

Основной атрибут:
Intellect

Ресурс:
Mana

Использует заклинания и магические эффекты.
Сильный урон компенсируется
низкой физической защитой.
```

---

# 25. Class Roster Roadmap

```text
FIRST PLAYABLE ROSTER
├── WARRIOR
├── ARCHER
└── MAGE

FUTURE
├── PRIEST
└── ROGUE
```

Добавление нового класса не должно требовать изменения Character Creation System.

Новый класс добавляется через новый `ClassDefinition` и связанные content profiles.

---

# 26. Invariants

**INVARIANT-01**  
Каждый персонаж имеет ровно одно имя.

**INVARIANT-02**  
Каждый персонаж имеет ровно одну Race.

**INVARIANT-03**  
Каждый персонаж имеет ровно один Gender.

**INVARIANT-04**  
Каждый персонаж имеет ровно один ClassId.

**INVARIANT-05**  
Race не влияет на gameplay.

**INVARIANT-06**  
Gender не влияет на gameplay.

**INVARIANT-07**  
Все доступные расы могут выбирать все доступные классы.

**INVARIANT-08**  
Class влияет на gameplay через ClassDefinition.

**INVARIANT-09**  
Имя, Race, Gender и Class валидируются сервером.

**INVARIANT-10**  
Клиент не может самостоятельно изменить Character Identity после создания.

**INVARIANT-11**  
Первый playable roster: Warrior / Archer / Mage.

**INVARIANT-12**  
Priest / Rogue являются future classes.

**INVARIANT-13**  
Archer использует `FOCUS`; Resource System является владельцем утверждённого Focus profile.

---

# 27. Out of Scope

Этот документ пока не определяет:

- внешний вид Human;
- внешний вид Undead;
- конкретные gender models;
- hairstyles;
- faces;
- skin tones;
- race lore;
- racial abilities;
- racial stats;
- race restrictions;
- paid rename;
- paid race change;
- paid gender change;
- class change;
- Priest Talent Tree;
- Rogue Talent Tree;
- Mage Talent Tree;
- конкретный стартовый gear;
- конкретные class base stats;
- финальные Ability unlock levels;
- UI animations.
