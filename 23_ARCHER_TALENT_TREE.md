# Elyndor — Archer Talent Tree — Source of Truth

**Класс:** Лучник (`ARCHER`)  
**Level Cap:** 60  
**Talent Points:** 59  
**Ветки:** 🏹 Меткая стрельба / 🐺 Повелитель зверей / ✨ Тайный стрелок  
**Tier unlock:** каждые 5 потраченных очков в ветке открывают следующий Tier  
**Capstone:** 40 spent + 1 отдельное очко

---

# 1. Фундамент класса

Лучник **всегда имеет спутника**.

```text
🏹 Меткая стрельба
Лучник:       ~85–90%
Спутник:      ~10–15%

🐺 Повелитель зверей
Лучник:       ~65–75%
PHYSICAL_PET: ~25–35%

✨ Тайный стрелок
Лучник:       ~75–85%
SPIRIT_PET:   ~15–25%
```

Это балансная цель, а не жёсткая Damage Share формула.

---

# 2. Типы спутников

## PHYSICAL_PET

Физический приручённый зверь.

Архетипы:

- **Хищник** — урон, Bleed, execute;
- **Страж** — защита хозяина, Threat, принятие части урона;
- **Ловчий** — Silence, AttackSpeed reduction, Accuracy reduction, utility.

Таланты с условием `PHYSICAL_PET` не действуют на Spirit Pet.

## SPIRIT_PET

Магический призванный спутник Тайного стрелка.

Использует Magical Damage, SpellPower scaling, magic effects и utility.

Таланты `SPIRIT_PET` не действуют на физического зверя.

---

# 3. Общие правила дерева

```text
Tier 1 →  0 spent
Tier 2 →  5 spent
Tier 3 → 10 spent
Tier 4 → 15 spent
Tier 5 → 20 spent
Tier 6 → 25 spent
Tier 7 → 30 spent
Tier 8 → 35 spent
Tier 9 → 40 spent
```

- Capstone требует 40 вложенных очков + 1 очко.
- Полностью закрыть ветку на Level 60 невозможно.
- Сильные таланты имеют `Prerequisite`.
- Общие utility/resource таланты допускают гибридизацию.
- `PHYSICAL_PET` и `SPIRIT_PET` бонусы строго разделены.

---

# ВЕТКА I — 🏹 МЕТКАЯ СТРЕЛЬБА

**Fantasy:** основной урон наносит сам Лучник.  
**Роль:** Single-target Physical DPS / Precision DPS.  
**Основные статы:** Agility, Accuracy, CriticalChance, ArmorPenetration, AttackSpeed.  
**Ресурс:** Focus.  
**Спутник:** PHYSICAL_PET, вспомогательный.

## TIER 1 — 0 spent

### 🏹 [M-1-1] Твёрдая Рука
`MaxRank 4`

Accuracy: **+1.5% / +3% / +4.5% / +6%**

### 🎯 [M-1-2] Смертельная Точность
`MaxRank 4`

CriticalChance физических выстрелов: **+1.5% / +3% / +4.5% / +6%**

### ◆ [M-1-3] Концентрация Охотника
`MaxRank 4`

После успешной атаки:

- Physical Shot → **+1 / +2 / +3 / +4 Focus**
- Magical Arrow → **+1 / +2 / +3 / +4 Mana**

`InternalCooldown = 1 sec`.

### 🏹 [M-1-4] Натянутая Тетива
`MaxRank 2`

AttackSpeed Auto Attack с Bow: **+3% / +6%**

---

## TIER 2 — 5 spent

### 🎯 [M-2-1] Метка Охотника
`MaxRank 1`

Активная способность. Накладывает `HUNTER_MARK` на 15 сек.

- Physical Shots по цели: **+5% damage**
- Magical Arrows по цели: **+3% damage**

Cooldown: **20 sec**

### 🏹 [M-2-2] Пронизывающая Стрела
`MaxRank 1`

Стоимость: **25 Focus**

Наносит **140% AttackPower Physical Damage** и получает **+10% ArmorPenetration** только для этого выстрела.

Cooldown: **6 sec**

### 🦅 [M-2-3] Верный Спутник
`MaxRank 4`

PHYSICAL_PET:

- Damage +**1.5% / 3% / 4.5% / 6%**
- MaxHP +**2% / 4% / 6% / 9%**

### 🎯 [M-2-4] Холодный Расчёт
`MaxRank 2`

По цели выше 80% HP CriticalChance выстрелов: **+3% / +6%**

---

## TIER 3 — 10 spent

### 🏹 [M-3-1] Прицельный Выстрел
`MaxRank 1`

Cast Time: **1.5 sec**  
Стоимость: **30 Focus**

Урон: **190% AttackPower Physical Damage**

Cooldown: **8 sec**

### 🎯 [M-3-2] Глубокая Метка
`MaxRank 2`

**Требует:** `M-2-1 Метка Охотника`

По цели с `HUNTER_MARK` CriticalDamage: **+5% / +10%**

### 🏹 [M-3-3] Пробивающий Наконечник
`MaxRank 4`

ArmorPenetration: **+2% / +4% / +6% / +9%**

### ◆ [M-3-4] Экономный Выстрел
`MaxRank 2`

После Critical Shot следующая Focus-способность стоит на **10% / 20% меньше**.

Duration: 5 sec. Не стакается.

---

## TIER 4 — 15 spent

### 🏹 [M-4-1] Двойной Спуск
`MaxRank 2`

После Auto Attack: **8% / 14% шанс** выпустить вторую стрелу за **40%** обычного Auto Attack damage.

Вторая стрела не критует и не запускает proc chains.

### 🎯 [M-4-2] Безошибочный Прицел
`MaxRank 2`

**Требует:** `M-3-1 Прицельный Выстрел`

Прицельный Выстрел:

- Accuracy +**5% / +10%**
- CriticalChance +**4% / +8%**

### 🏹 [M-4-3] Раскрытая Защита
`MaxRank 2`

**Требует:** `M-2-2 Пронизывающая Стрела`

После попадания Пронизывающей Стрелой следующие 2 Physical Shots получают **+5% / +10% ArmorPenetration**.

Duration: 8 sec.

### 🦅 [M-4-4] Синхронная Атака
`MaxRank 2`

После твоего Critical Shot PHYSICAL_PET получает AttackSpeed **+5% / +10%** на 4 sec.

`InternalCooldown = 5 sec`.

---

## TIER 5 — 20 spent

### 🏹 [M-5-1] Снайперская Концентрация
`MaxRank 1`

Off-GCD. На 10 sec:

- Accuracy +10%
- CriticalChance +8%
- ArmorPenetration +10%

Cooldown: **90 sec**

### 🎯 [M-5-2] Отмеченная Жертва
`MaxRank 2`

**Требует:** `M-2-1 Метка Охотника`

По отмеченной цели:

- Auto Attack Damage +**4% / 8%**
- Прицельный Выстрел Damage +**5% / 10%**

### 🏹 [M-5-3] Быстрая Перезарядка
`MaxRank 4`

Cooldown Пронизывающей Стрелы и Прицельного Выстрела: **−0.5 / −0.75 / −1.0 / −1.5 sec**

### ◆ [M-5-4] Боевой Ритм
`MaxRank 2`

Каждый третий успешный Shot подряд восстанавливает:

- **+5 / +8 Focus**
- или **+5 / +8 Mana**, если активна магическая ветка.

Miss сбрасывает счётчик.

---

## TIER 6 — 25 spent

### 🏹 [M-6-1] Смертельная Серия
`MaxRank 3`

Critical Shot даёт stack на 6 sec.

Каждый stack: CriticalDamage **+1.5% / +2.25% / +3%**  
MaxStacks: 3.

### 🎯 [M-6-2] Охотничий Инстинкт
`MaxRank 2`

По цели ниже 30% HP:

- Accuracy +**3% / 6%**
- CriticalChance +**3% / 6%**

### 🏹 [M-6-3] Тяжёлая Стрела
`MaxRank 1`

Next Attack Modifier. Стоимость: 20 Focus.

Следующая Auto Attack наносит **175% обычного урона**.

Cooldown: **12 sec**

### 🦅 [M-6-4] Прикрывающий Спутник
`MaxRank 2`

После полученного хозяином Critical Hit PHYSICAL_PET отвечает атакой за **40% / 65%** своего обычного Auto Attack damage.

`InternalCooldown = 5 sec`.

---

## TIER 7 — 30 spent

### 🎯 [M-7-1] Идеальный Выстрел
`MaxRank 1`

**Требует:** `M-3-1 Прицельный Выстрел`, `M-4-2 Безошибочный Прицел`

Прицельный Выстрел по цели с `HUNTER_MARK`:

- Cast Time −0.25 sec
- Damage +15%

### 🏹 [M-7-2] Дробящий Наконечник
`MaxRank 3`

Critical Physical Shot накладывает на 6 sec персональный `BROKEN_ARMOR`.

Твои атаки получают ArmorPenetration +**3% / 6% / 10%**.

### ◆ [M-7-3] Без Потерь
`MaxRank 2`

Miss физическим Shot возвращает **40% / 70%** потраченного Focus.

### 🏹 [M-7-4] Быстрый Добор
`MaxRank 2`

После убийства следующий Casted Shot в течение 8 sec:

- Cast Time −**20% / 40%**
- Focus Cost −**10% / 20%**

---

## TIER 8 — 35 spent

### 🏹 [M-8-1] Выстрел В Сердце
`MaxRank 1`

**Требует:** `M-7-1 Идеальный Выстрел`

Прицельный Выстрел по цели ниже 25% HP: **+25% Damage**

### 🎯 [M-8-2] Совершенная Метка
`MaxRank 2`

**Требует:** `M-5-2 Отмеченная Жертва`

`HUNTER_MARK`:

- Duration +**5 / 10 sec**
- Cooldown −**3 / 6 sec**

### 🏹 [M-8-3] Идеальный Момент
`MaxRank 1`

Активация Снайперской Концентрации сбрасывает Cooldown Пронизывающей Стрелы и Прицельного Выстрела.

---

## TIER 9 — CAPSTONE

### ★ [M-9-1] МАСТЕР СТРЕЛЫ
`MaxRank 1`

**Требование:** 40 вложенных очков в этой ветке. Дополнительных node prerequisites нет.

Пассивно:

- AttackPower +8%
- Accuracy +5%

По цели под `HUNTER_MARK`:

- CriticalChance +5%
- Critical Shot снижает remaining CD Снайперской Концентрации на 1 sec (`ICD = 2 sec`)

Каждый 5-й успешный Shot по отмеченной цели выпускает дополнительную стрелу за **60% обычного Auto Attack damage**.

---

# ВЕТКА II — 🐺 ПОВЕЛИТЕЛЬ ЗВЕРЕЙ

**Fantasy:** хозяин и зверь — одна боевая связка.  
**Роль:** Sustained DPS / Pet-centric DPS / Utility.  
**Ресурс:** Focus.  
**Спутник:** только `PHYSICAL_PET`.

## TIER 1 — 0 spent

### 🐺 [B-1-1] Крепкая Связь
`MaxRank 4`

PHYSICAL_PET MaxHP: **+4% / +8% / +12% / +15%**

### 🐾 [B-1-2] Острые Инстинкты
`MaxRank 4`

PHYSICAL_PET Damage: **+2% / +4% / +6% / +9%**

### ◆ [B-1-3] Общий Ритм
`MaxRank 4`

Pet attack имеет **10% / 15% / 20% / 25% шанс** восстановить хозяину +3 Focus.

`InternalCooldown = 2 sec`

### 🏹 [B-1-4] Уверенный Охотник
`MaxRank 2`

Agility: **+3% / +6%**

---

## TIER 2 — 5 spent

### 🐺 [B-2-1] Команда: Фас
`MaxRank 1`

Активная способность, 20 Focus, CD 10 sec.

Эффект зависит от pet archetype:

- Хищник → усиленный Physical Hit
- Страж → Threat + защита
- Ловчий → utility debuff

### 🐾 [B-2-2] Звериная Выносливость
`MaxRank 4`

PHYSICAL_PET:

- Armor +**2% / 4% / 6% / 9%**
- MagicResistance +**2% / 4% / 6% / 9%**

### 🐺 [B-2-3] Совместная Охота
`MaxRank 2`

Если хозяин и pet бьют одну цель: оба получают Damage +**2% / 4%**.

### ◆ [B-2-4] Быстрый Приказ
`MaxRank 2`

Cooldown `Команда: Фас`: **−1 / −2 sec**

---

## TIER 3 — 10 spent

### 🐺 [B-3-1] Хищник: Кровавый Укус
`MaxRank 2`

Только `PREDATOR`.

Critical Pet Attack накладывает Bleed на 6 sec:

- Rank 1: **24% PetAttackPower total**
- Rank 2: **42% total**

### 🛡️ [B-3-2] Страж: Перехват
`MaxRank 2`

Только `GUARDIAN`.

При direct damage по хозяину: **10% / 18% шанс** перенаправить 30% этого damage на pet.

Перенаправленный damage является настоящим damage event и **может перевести pet в DEFEATED**.

`InternalCooldown = 3 sec`

### ◆ [B-3-3] Ловчий: Срыв Ритма
`MaxRank 2`

Только `TRAPPER`.

Special Pet Attack на 6 sec:

- AttackSpeed цели −**5% / 10%**
- Accuracy цели −**3% / 6%**

### 🐾 [B-3-4] Дрессировка
`MaxRank 4`

Pet Ability Damage: **+3% / +6% / +9% / +12%**

---

## TIER 4 — 15 spent

### 🐺 [B-4-1] Звериная Ярость
`MaxRank 2`

После Critical Shot pet получает AttackSpeed +**6% / 12%** на 5 sec.

### 🏹 [B-4-2] Охотник Стаи
`MaxRank 2`

После Critical Attack питомца следующий Shot хозяина получает Damage +**5% / 10%**.

### 🐾 [B-4-3] Кровь И Клык
`MaxRank 2`

По цели с pet Bleed хозяин наносит Physical Damage +**3% / 6%**.

### ◆ [B-4-4] Командный Голос
`MaxRank 4`

Focus Cost pet-command abilities: **−4% / −8% / −12% / −15%**

---

## TIER 5 — 20 spent

### 🐺 [B-5-1] Звериный Натиск
`MaxRank 1`

Off-GCD, 10 sec.

PHYSICAL_PET:

- Damage +20%
- AttackSpeed +20%

Хозяин:

- Focus regeneration +15%

Cooldown: **100 sec**

### 🐾 [B-5-2] Закалённый Спутник
`MaxRank 4`

PHYSICAL_PET получает на **4% / 8% / 12% / 15% меньше AoE/Encounter-wide Damage**.

### 🏹 [B-5-3] Единство Цели
`MaxRank 2`

Если хозяин и pet попадают по одной цели в течение 2 sec, цель получает от них Damage +**2% / 4%** на 4 sec.

### ◆ [B-5-4] Вернуть К Хозяину
`MaxRank 1`

Снимает с pet Silence и один removable negative effect.

Cooldown: 45 sec.

---

## TIER 6 — 25 spent

### 🐺 [B-6-1] Хищник: Добивание
`MaxRank 2`

`PREDATOR` наносит цели ниже 20% HP Damage +**8% / 15%**.

### 🛡️ [B-6-2] Страж: Живая Преграда
`MaxRank 2`

Пока `GUARDIAN` выше 50% HP, хозяин получает Damage Taken −**2% / 4%**.

### ◆ [B-6-3] Ловчий: Немой Приказ
`MaxRank 1`

`TRAPPER` через `Команда: Фас` накладывает Silence 2 sec.

Cooldown Silence-компонента: **20 sec per target**.

### 🐾 [B-6-4] Безусловное Послушание
`MaxRank 2`

После окончания Stun/Silence pet получает на 5 sec:

- AttackSpeed +**5% / 10%**
- Damage +**3% / 6%**

---

## TIER 7 — 30 spent

### 🐺 [B-7-1] Совершенный Хищник
`MaxRank 1`

**Требует:** `B-3-1 Кровавый Укус`

Pet Bleed: Duration +2 sec, Damage +15%.

### 🛡️ [B-7-2] Несокрушимый Страж
`MaxRank 1`

**Требует:** `B-3-2 Перехват`

Раз в 60 sec удар по хозяину больше 20% MaxHP уменьшается на 20%. Pet получает direct damage = 10% своего MaxHP.

### ◆ [B-7-3] Совершенный Ловчий
`MaxRank 1`

**Требует:** `B-3-3 Срыв Ритма`

Debuff дополнительно снижает Damage Dealt цели на 5%.

### 🐾 [B-7-4] Одна Кровь
`MaxRank 2`

Когда хозяин получает Effective Healing, pet получает **5% / 10%** от этого лечения.

---

## TIER 8 — 35 spent

### 🐺 [B-8-1] Альфа-Инстинкт
`MaxRank 3`

PHYSICAL_PET:

- CriticalChance +**3% / 5.5% / 8%**
- CriticalDamage +**4% / 7% / 10%**

### 🐾 [B-8-2] Безупречная Координация
`MaxRank 1`

После `Команда: Фас` следующий Shot хозяина в течение 5 sec:

- Damage +15%
- Resource Cost −20%

### 🐺 [B-8-3] Неудержимая Стая
`MaxRank 1`

Активация `Звериного Натиска`:

- сбрасывает CD `Команда: Фас`
- даёт pet иммунитет к Silence на 3 sec

---

## TIER 9 — CAPSTONE

### ★ [B-9-1] ПОВЕЛИТЕЛЬ ЗВЕРЕЙ
`MaxRank 1`

**Требование:** 40 вложенных очков в этой ветке. Дополнительных node prerequisites нет.

PHYSICAL_PET:

- Damage +10%
- MaxHP +10%

Critical Shot хозяина: 20% шанс заставить pet выполнить дополнительную Auto Attack (`ICD = 3 sec`).

Critical Hit pet: следующий Shot хозяина +10% Damage на 5 sec.

---

# ВЕТКА III — ✨ ТАЙНЫЙ СТРЕЛОК

**Fantasy:** магический Лучник с духом-питомцем.  
**Роль:** Magical Ranged DPS / Effect DPS / Hybrid Utility.  
**Основные статы:** Intellect, SpellPower, CriticalChance, MagicPenetration.  
**Ресурс:** Mana.  
**Спутник:** `SPIRIT_PET`.

## TIER 1 — 0 spent

### ✨ [A-1-1] Тайны Магии
`MaxRank 1`

Ключевой талант.

```text
Focus → Mana
Primary offensive scaling: Agility → Intellect
PHYSICAL_PET → SPIRIT_PET
```

Bow и Auto Attack сохраняются. Existing Agility не конвертируется в Intellect: физическая Agility-экипировка остаётся физической, а Magical Arrow/Spirit scaling использует Intellect/SpellPower. Это делает смену gear между физическим и магическим билдом осмысленной.

Сразу после изучения талант также открывает базовую способность `ARCANE_ARROW` / **Чародейская Стрела**, чтобы ветка была функциональна уже с первого очка:

```text
Type: Instant Magical Arrow
Mana Cost: 10
Damage: 110% SpellPower Magical Damage
Cooldown: 0
UsesGlobalCooldown: true
CanCrit: true
UsesHitCheck: true
```

Это базовый filler Тайного стрелка. `Фантомная Стрела` из Tier 2 остаётся более сильной cooldown-способностью и не заменяется.

### ✨ [A-1-2] Чародейская Меткость
`MaxRank 4`

Accuracy Magical Arrows: **+1.5% / +3% / +4.5% / +6%**

### ✨ [A-1-3] Эфирная Сила
`MaxRank 4`

SpellPower: **+2% / +4% / +6% / +9%**

### ◆ [A-1-4] Концентрация Разума
`MaxRank 2`

Mana regeneration during combat: **+4% / +8%**

---

## TIER 2 — 5 spent

### ✨ [A-2-1] Фантомная Стрела
`MaxRank 1`

Magical Arrow. Урон: **145% SpellPower Magical Damage**.

Cooldown: **5 sec**

### 👻 [A-2-2] Эфирная Связь
`MaxRank 4`

SPIRIT_PET:

- Damage +**2% / 4% / 6% / 9%**
- MaxHP +**2% / 4% / 6% / 9%**

### ✨ [A-2-3] Магический Наконечник
`MaxRank 4`

MagicPenetration: **+2% / +4% / +6% / +9%**

### 🎯 [A-2-4] Тайная Метка
`MaxRank 2`

По цели с `HUNTER_MARK` Magical Arrow Damage +**2% / 4%**.

---

## TIER 3 — 10 spent

### ✨ [A-3-1] Призрачный Залп
`MaxRank 1`

AoE Magical Damage: **70% SpellPower** всем врагам encounter.

Cooldown: 10 sec.

### 👻 [A-3-2] Духовный Импульс
`MaxRank 2`

SPIRIT_PET damage: **15% / 25% шанс** восстановить хозяину +3 Mana.

`ICD = 2 sec`

### ✨ [A-3-3] Чародейский Критик
`MaxRank 4`

CriticalChance Magical Arrows: **+1.5% / 3% / 4.5% / 6%**

### ◆ [A-3-4] Переплетение Энергии
`MaxRank 2`

После Mana-spending Magical Arrow SPIRIT_PET получает Damage +**4% / 8%** на 4 sec.

---

## TIER 4 — 15 spent

### ✨ [A-4-1] Зачарованный Выстрел
`MaxRank 1`

Next Attack Modifier.

Следующая Bow Auto Attack:

- Physical component = 70% обычного
- дополнительный Magical component = 70% SpellPower scaling

Cooldown: 10 sec.

### ✨ [A-4-2] Эфирный Ожог
`MaxRank 2`

**Требует:** `A-2-1 Фантомная Стрела`

Critical Phantom Arrow накладывает Magical DoT на 6 sec:

- Rank 1: **30% SpellPower total**
- Rank 2: **48% total**

### 👻 [A-4-3] Ответ Духа
`MaxRank 2`

Critical Magical Arrow заставляет SPIRIT_PET нанести дополнительную magic attack за **40% / 65%** обычного ability damage.

`ICD = 4 sec`

### ◆ [A-4-4] Эфирная Защита
`MaxRank 2`

Пока SPIRIT_PET жив, хозяин получает MagicResistance +**3% / 6%**.

---

## TIER 5 — 20 spent

### ✨ [A-5-1] Чародейский Поток
`MaxRank 1`

Off-GCD, 10 sec:

- SpellPower +15%
- MagicPenetration +10%
- Mana Cost Magical Arrows −15%

Cooldown: **100 sec**

### 👻 [A-5-2] Единство Духа
`MaxRank 2`

Если хозяин и Spirit атакуют одну цель, оба наносят Magical Damage +**2% / 4%**.

### ✨ [A-5-3] Стабильные Чары
`MaxRank 4`

Mana Cost Magical Arrow abilities: **−2% / −4% / −6% / −9%**

### 🎯 [A-5-4] Магическая Уязвимость
`MaxRank 2`

Critical Magical Arrow на 6 sec даёт только твоим Magical Attacks MagicPenetration +**5% / 10%**.

---

## TIER 6 — 25 spent

### ✨ [A-6-1] Эфирный Яд
`MaxRank 2`

Magical Arrow: **10% / 18% шанс** наложить Magical DoT на 6 sec:

- Rank 1: **24% SpellPower total**
- Rank 2: **36% total**

`ICD = 4 sec`

### 👻 [A-6-2] Дух-Хранитель
`MaxRank 2`

После Critical Hit по хозяину Spirit создаёт Shield:

**3% / 5% MaxHP хозяина**

Duration 5 sec, `ICD = 12 sec`.

### ✨ [A-6-3] Чистая Мана
`MaxRank 2`

При Mana ниже 30% Mana Cost Magical Arrows: **−8% / −15%**

### ◆ [A-6-4] Разрыв Заклинания
`MaxRank 1`

Magical Arrow:

- 60% SpellPower Magical Damage
- Silence 2 sec

Cooldown: **30 sec**

---

## TIER 7 — 30 spent

### ✨ [A-7-1] Совершенная Фантомная Стрела
`MaxRank 2`

**Требует:** `A-2-1 Фантомная Стрела`, `A-4-2 Эфирный Ожог`

Phantom Arrow:

- Damage +**8% / 15%**
- Cooldown −**0.5 / 1 sec**

### 👻 [A-7-2] Эхо Заклинания
`MaxRank 1`

После Magical Arrow: 12% шанс, что Spirit повторит магический компонент за **50% исходного Magical Damage**.

`ICD = 5 sec`, `CanTriggerFromProc = false`.

### ✨ [A-7-3] Насыщение Эфиром
`MaxRank 4`

При Mana выше 70% SpellPower: **+2% / +4% / +6% / +9%**

### ◆ [A-7-4] Последняя Искра
`MaxRank 2`

Когда Mana падает ниже 15%, раз в 60 sec восстанавливает **8% / 15% MaxMana**.

---

## TIER 8 — 35 spent

### ✨ [A-8-1] Призрачный Ливень
`MaxRank 1`

**Требует:** `A-3-1 Призрачный Залп`

Залп:

- Damage +15%
- накладывает `ARCANE_EXPOSURE` на 5 sec
- следующие Magical Arrows по этим целям +5% Damage

### 👻 [A-8-2] Истинная Форма Духа
`MaxRank 3`

SPIRIT_PET:

- SpellPower scaling +**5% / 10% / 15%**
- CriticalChance +**2% / 4% / 6%**

### ✨ [A-8-3] Совершенный Поток
`MaxRank 1`

Активация Чародейского Потока:

- восстанавливает **15% MaxMana**
- сбрасывает CD Фантомной Стрелы
- Spirit Damage +15% на 10 sec

---

## TIER 9 — CAPSTONE

### ★ [A-9-1] ТАЙНЫЙ СТРЕЛОК
`MaxRank 1`

**Требование:** 40 вложенных очков в этой ветке. Дополнительных node prerequisites нет.

Пассивно:

- SpellPower +8%
- MagicPenetration +5%

Каждый 4-й успешный Magical Arrow создаёт `PHANTOM_ECHO`:

- дополнительный Magical Damage = **55% SpellPower**
- Spirit мгновенно атакует за **40% обычного damage**

Во время Чародейского Потока:

- Magical Arrow CriticalChance +5%
- Spirit AttackSpeed +15%

---

# БАЛАНС И ГИБРИДИЗАЦИЯ

## 🏹 Меткая стрельба
Лучший single-target Physical DPS. Pet вспомогательный. Билд не должен разваливаться, если pet временно недоступен.

## 🐺 Повелитель зверей
Pet даёт примерно **25–35% общей эффективности**. Без pet билд заметно слабее — это цена специализации.

## ✨ Тайный стрелок
Лучший Magical Ranged путь Лучника. Сохраняет Bow/Auto Attack/Marks/Arrow fantasy и не превращается в Mage.

## Разрешённая синергия
- `Концентрация Охотника` работает с Focus и Mana.
- `HUNTER_MARK` усиливает Physical Shots сильнее, Magical Arrows слабее.
- общие Accuracy/resource/defensive utility таланты могут быть полезны гибридам.

## Жёсткое разделение
- Beast Mastery: `PHYSICAL_PET only`
- Arcane Spirit talents: `SPIRIT_PET only`

---

# СТРУКТУРА ОЧКОВ — REVISED

После балансного прохода каждая ветка содержит:

```text
Меткая стрельба:   69 возможных rank-points
Повелитель зверей: 69 возможных rank-points
Тайный стрелок:    69 возможных rank-points

Доступно на Level 60: 59 Talent Points
```

Игрок вынужден отказаться минимум от 10 rank-points даже при глубоком single-branch build. Это создаёт настоящий выбор внутри специализации без увеличения максимальной силы отдельных талантов: дополнительные ranks в основном добавляют промежуточные ступени к прежнему maximum value.

## Capstone rule

Capstone требует **40 spent points в своей ветке + 1 point на сам Capstone**. Он не требует конкретного Tier 8 node, поэтому одна специализация поддерживает несколько глубоких вариантов.

---

# КЛЮЧЕВЫЕ ЦЕПОЧКИ

## Меткая стрельба
```text
Метка Охотника
  ├─ Глубокая Метка
  └─ Отмеченная Жертва
       ↓
     Совершенная Метка

Прицельный Выстрел
  ↓
Безошибочный Прицел
  ↓
Идеальный Выстрел
  ↓
Выстрел В Сердце
```

## Повелитель зверей
```text
Хищник: Кровавый Укус → Совершенный Хищник
Страж: Перехват → Несокрушимый Страж
Ловчий: Срыв Ритма → Немой Приказ → Совершенный Ловчий

Команда: Фас
  ↓
Безупречная Координация
```

## Тайный стрелок
```text
Тайны Магии
  ↓
Focus → Mana
Agility → Intellect
PHYSICAL_PET → SPIRIT_PET

Фантомная Стрела
  ↓
Эфирный Ожог
  ↓
Совершенная Фантомная Стрела
```

---

# МИНИМАЛЬНЫЕ ИКОНКИ

```text
🏹 Physical Shot / Bow
🎯 Accuracy / Mark / Precision
🐺 Physical Pet
🐾 Pet Passive / Training
🛡️ Guardian Pet / Defense
👻 Spirit Pet
✨ Magical Arrow / SpellPower
◆ Resource / Utility / Control
★ Capstone
```

---

# FINAL DESIGN INTENT

```text
🏹 МЕТКАЯ СТРЕЛЬБА
Я — главный источник урона. Pet помогает.

🐺 ПОВЕЛИТЕЛЬ ЗВЕРЕЙ
Мы с питомцем — единая связка.

✨ ТАЙНЫЙ СТРЕЛОК
Я превращаю стрелковую механику в магическую,
а физического зверя заменяю духом.
```

Спутник у Лучника есть всегда, но каждая ветка по-разному распределяет силу между хозяином и спутником.


---

# SYSTEM DEPENDENCIES — AUTHORITATIVE

Archer tree requires:
- `07_RESOURCE_SYSTEM`: FOCUS 100 / 8 sec combat / 12 sec out of combat;
- `21_COMPANION_AND_PET_SYSTEM`;
- `PHYSICAL_PET` / `SPIRIT_PET` tags;
- `ACTIVE_COMPANION` Ability TargetType;
- talent-derived combat profile override for `FOCUS → MANA` and `Agility → Intellect`.

Pet direct gear percentages are not required by this tree.
