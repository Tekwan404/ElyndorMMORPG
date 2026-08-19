# Elyndor — Mage Talent Tree — Source of Truth

**Document:** 25_MAGE_TALENT_TREE.md  
**Class:** Mage (`MAGE`)  
**Level Cap:** 60  
**Talent Points:** 59  
**Primary Attribute:** Intellect  
**Action Resource:** Mana  
**Armor:** Light  
**Weapons:** Staff / Wand  
**Branches:** 🔥 Пламя / 🔮 Тайная магия / ❄️ Лёд  
**Tier unlock:** каждые 5 потраченных очков в ветке  
**Capstone:** 40 spent + 1 point

---

# 1. Идентичность Мага

Mage — ranged magical damage dealer, для которого главными решениями являются:

- какой spell cast использовать сейчас;
- сколько Mana потратить;
- продолжить burst или сохранить ресурс;
- поддерживать DoT / Charge / debuff;
- оставить контроль/барьер на опасный момент;
- какую школу заклинаний использовать для конкретного build.

Mage не использует отдельный CastSpeed stat.

Если талант ускоряет cast, он модифицирует **CastTime конкретной ability** или группы abilities.

---

# 2. Школы Мага

Школа заклинания задаётся `AbilityTag`, а не новым DamageType.

```text
FIRE
ARCANE
FROST
```

Все три школы по умолчанию наносят:

```text
DamageType = MAGICAL
```

Следовательно:

- урон уменьшается MagicResistance;
- работает MagicPenetration;
- способность может критовать;
- способность может промахнуться;
- school tag используется талантами, эффектами и lockout rules.

---

# 3. Базовый Mage Kit

Точное распределение unlock levels хранится Class/Content Profile.

## Fireball

```text
AbilityId: MAGE_FIREBALL
Type: CASTED
Tag: FIRE
CastTime: 1.8 sec
ManaCost: 20
Damage: 125% SpellPower
DamageType: MAGICAL
```

Главная тяжёлая базовая атака школы Огня.

## Arcane Spark

```text
AbilityId: MAGE_ARCANE_SPARK
Type: INSTANT
Tag: ARCANE
ManaCost: 15
Cooldown: 3 sec
Damage: 75% SpellPower
DamageType: MAGICAL
```

Быстрая способность для заполнения окон между cast.

## Ice Shard

```text
AbilityId: MAGE_ICE_SHARD
Type: CASTED
Tag: FROST
CastTime: 1.5 sec
ManaCost: 18
Damage: 105% SpellPower
DamageType: MAGICAL
```

Базовый контролируемый Frost cast.

---

# 4. Философия веток

```text
🔥 ПЛАМЯ
максимальный личный DPS
Crit → Burn → burst windows
главный heavy cast = Fireball

🔮 ТАЙНАЯ МАГИЯ
Mana management + spell sequencing
ARCANE_CHARGE → Arcane Burst
гибридные переходы между школами

❄️ ЛЁД
DPS + utility + умеренная выживаемость
FROSTBITE → debuff / Ice Lance / controlled Stun
не использует Slow или Root
```

---

# 5. Общие правила дерева

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

Capstone требует 40 очков в своей ветке и ещё 1 очко на сам talent.

На Level 60:

```text
Available Talent Points = 59
Minimum primary branch for capstone = 41
Remaining hybrid points = 18
```

Полностью закрыть любую ветку невозможно:

```text
Пламя:        64 rank-points
Тайная магия: 61 rank-points
Лёд:          63 rank-points
```

---

# 6. Proc Safety

Для всех дополнительных damage proc:

```text
CanTriggerFromProc = false
```

если талант явно не говорит обратное.

Один proc-created damage event:

- не создаёт тот же proc рекурсивно;
- не считается отдельным Fireball/Ice Shard/Arcane Spark cast;
- не увеличивает streak/charge, если это не написано прямо.

---

# ВЕТКА — 🔥 ПЛАМЯ

**Роль:** максимальный личный Magical DPS / crit burst / Burn.


## TIER 1 — 0 spent


### [F-1-1] Точное Пламя

`MaxRank 4`


Accuracy способностей с тегом `FIRE`: **+2% / +4% / +6% / +8%**.


### [F-1-2] Искра Критика

`MaxRank 4`


CriticalChance `FIRE`-способностей: **+2% / +4% / +6% / +8%**.


### [F-1-3] Жар Внутри

`MaxRank 4`


SpellPower для `FIRE`-способностей: **+3% / +6% / +9% / +12%**.


### [F-1-4] Экономное Горение

`MaxRank 2`


После Critical Hit любой `FIRE`-способностью восстановить **2 / 4 Mana**. `InternalCooldown = 1 sec`.


## TIER 2 — 5 spent


### [F-2-1] Разогретый Fireball

`MaxRank 3`


`MAGE_FIREBALL`: Damage **+4% / +8% / +12%**.


### [F-2-2] Искра Воспламенения

`MaxRank 2`


Critical `Fireball` накладывает `BURN` на 4 sec: **4% / 7% SpellPower per sec**. Burn snapshot'ит SpellPower при применении.


### [F-2-3] Первый Ожог

`MaxRank 2`


По цели выше 80% HP `FIRE` Damage **+4% / +8%**.


### [F-2-4] Быстрый Розжиг

`MaxRank 2`


После `Arcane Spark` или `Ice Shard` следующий `Fireball` в течение 5 sec получает CastTime **−0.10 / −0.20 sec**. Не стакается.


## TIER 3 — 10 spent


### [F-3-1] Вспышка

`MaxRank 1`


Открывает Instant Ability `FLAME_FLASH`: **95% SpellPower Magical Damage**, 18 Mana, CD 8 sec. `FIRE`, может критовать.


### [F-3-2] Пожирающее Пламя

`MaxRank 3`


**Требует:** `F-2-2`


Твои `FIRE`-способности по цели с твоим `BURN`: Damage **+2% / +4% / +6%**.


### [F-3-3] Горячая Кровь

`MaxRank 2`


Critical `Fireball` даёт `HOT_BLOOD` на 5 sec: следующий `FIRE` cast стоит на **10% / 20%** меньше Mana.


### [F-3-4] Пламенный Пробой

`MaxRank 4`


MagicPenetration `FIRE`-способностей: **+3% / +6% / +9% / +12%**.


## TIER 4 — 15 spent


### [F-4-1] Огненная Волна

`MaxRank 1`


Открывает `FIRE_WAVE`: всем врагам CombatSession **75% SpellPower Magical Damage**, 30 Mana, CD 10 sec. `FIRE`.


### [F-4-2] Раздувание

`MaxRank 2`


**Требует:** `F-2-2`


`BURN` Damage **+10% / +20%**; critical `Fireball` обновляет duration собственного Burn до 4 sec, но не добавляет второй Burn.


### [F-4-3] Неугасимый Ритм

`MaxRank 2`


Два успешных `FIRE` cast подряд дают `FIRE_RHYTHM`: CriticalChance следующего `FIRE` cast **+4% / +8%**. Miss/Interrupt сбрасывает серию.


### [F-4-4] Опаляющий Финал

`MaxRank 2`


По цели ниже 25% HP `FIRE` Damage **+5% / +10%**.


## TIER 5 — 20 spent


### [F-5-1] Возгорание

`MaxRank 1`


Активная Off-GCD ability. На 10 sec: `FIRE` SpellPower scaling **+15%**, CriticalChance **+8%**. CD **100 sec**.


### [F-5-2] Огненный След

`MaxRank 2`


**Требует:** `F-3-1`


`Flame Flash` после попадания усиливает следующий `Fireball` в течение 5 sec: Damage **+8% / +15%**.


### [F-5-3] Жажда Жара

`MaxRank 4`


**Требует:** `F-3-1`


Каждый Critical `FIRE` hit снижает remaining cooldown `Flame Flash` на **0.5 / 1 / 1.5 / 2 sec**. `ICD = 1 sec`.


### [F-5-4] Разрушительный Огонь

`MaxRank 2`


CriticalDamage `FIRE`-способностей: **+8% / +15%**.


## TIER 6 — 25 spent


### [F-6-1] Предел Жара

`MaxRank 1`


**Требует:** `F-2-1`, `F-5-4`


После **3 подряд Critical `Fireball`** получить `HEAT_LIMIT` на 8 sec и открыть бесплатный быстрый cast **`Огненная Комета`**: 0 Mana, CastTime 0.5 sec, **240% SpellPower Magical Damage**, `FIRE`. Не считается `Fireball`, не продолжает streak, `CanTriggerFromProc = false`. Любой некритический `Fireball` сбрасывает streak.


### [F-6-2] Перегрев

`MaxRank 2`


**Требует:** `F-6-1`


Пока активен `HEAT_LIMIT`, CriticalChance `Огненной Кометы` **+5% / +10%** и она накладывает Burn на 4 sec с **4% / 7% SpellPower/sec**.


### [F-6-3] Пламенная Индукция

`MaxRank 2`


**Требует:** `F-5-1`


Во время `Возгорания` CastTime `Fireball` **−0.15 / −0.30 sec**.


### [F-6-4] Пылающий Ответ

`MaxRank 2`


Когда ты получаешь Magical Critical Hit, следующий `FIRE` cast в течение 6 sec наносит **+5% / +10% Damage**. `ICD = 8 sec`.


## TIER 7 — 30 spent


### [F-7-1] Кометный Удар

`MaxRank 1`


**Требует:** `F-6-1`


`Огненная Комета` при Critical Hit дополнительно наносит **35% SpellPower** через 1 sec. Вторичный урон не критует и не запускает proc chains.


### [F-7-2] Пепельная Метка

`MaxRank 2`


После `Fireball` Critical Hit цель получает personal debuff на 6 sec: твой MagicPenetration против неё **+5% / +10%**.


### [F-7-3] Неистовство Пламени

`MaxRank 3`


**Требует:** `F-5-1`


Во время `Возгорания` каждый успешный `FIRE` cast даёт stack `INFERNO` (max 3), каждый stack: `FIRE` Damage **+2% / +3% / +4%**. Все stacks исчезают с окончанием `Возгорания`.


### [F-7-4] Пожар Без Остатка

`MaxRank 2`


**Требует:** `F-3-1`


Убийство врага твоим `FIRE` damage восстанавливает **5% / 8% MaxMana** и сбрасывает cooldown `Flame Flash`. `ICD = 8 sec`.


## TIER 8 — 35 spent


### [F-8-1] Идеальное Возгорание

`MaxRank 1`


**Требует:** `F-5-1`, `F-4-1`


Активация `Возгорания` сбрасывает cooldown `Flame Flash` и `Fire Wave`, а первый `Fireball` во время окна имеет **+15% CriticalChance**.


### [F-8-2] Сердце Пожара

`MaxRank 2`


**Требует:** `F-4-2`


Пока на цели твой Burn, CriticalDamage `FIRE` по ней **+5% / +10%** и Burn duration после нового `Fireball` не может стать ниже 4 sec.


### [F-8-3] Звезда Пепла

`MaxRank 1`


**Требует:** `F-7-1`


`Огненная Комета` по цели ниже 30% HP наносит **+20% Damage**.


## TIER 9 — 40 spent


### [F-9-1] ВОПЛОЩЕНИЕ ПЛАМЕНИ

`MaxRank 1`


**Требует:** `F-8-1`, `F-8-3`


Capstone. Требует 40 spent. Пассивно `FIRE` Damage **+8%**. `Возгорание` длится +3 sec. После расходования `HEAT_LIMIT` следующий `Fireball` в течение 5 sec имеет CastTime 1.0 sec и ManaCost −50%. Critical `Огненной Кометы` снижает remaining CD `Возгорания` на 3 sec (`ICD = 6 sec`).


---

# ВЕТКА — 🔮 ТАЙНАЯ МАГИЯ

**Роль:** Mana mastery / spell sequencing / sustained burst / MagicPenetration.


## TIER 1 — 0 spent


### [A-1-1] Тайная Точность

`MaxRank 4`


Accuracy всех Magical abilities: **+2% / +4% / +6% / +8%**.


### [A-1-2] Глубокий Резерв

`MaxRank 4`


MaxMana **+3% / +6% / +9% / +12%**.


### [A-1-3] Чистая Сила

`MaxRank 4`


SpellPower **+3% / +6% / +9% / +12%**.


### [A-1-4] Экономия Формулы

`MaxRank 2`


ManaCost всех Mage abilities **−3% / −6%**.


## TIER 2 — 5 spent


### [A-2-1] Тайный Заряд

`MaxRank 1`


`Arcane Spark` после успешного попадания создаёт `ARCANE_CHARGE` на 12 sec. MaxStacks = 4. Новый stack обновляет duration.


### [A-2-2] Накопленная Мощь

`MaxRank 4`


**Требует:** `A-2-1`


Каждый `ARCANE_CHARGE` увеличивает Damage `Arcane Spark` на **2% / 4% / 6% / 8%**.


### [A-2-3] Проводник Маны

`MaxRank 2`


При Mana выше 70% MagicPenetration **+4% / +8%**.


### [A-2-4] Стабильное Плетение

`MaxRank 2`


После завершённого Casted Mage spell восстановить **1 / 2 Mana**. `ICD = 1 sec`.


## TIER 3 — 10 spent


### [A-3-1] Тайный Взрыв

`MaxRank 1`


**Требует:** `A-2-1`


Открывает `ARCANE_BURST`: CastTime 1.2 sec, 25 Mana, CD 6 sec. База **110% SpellPower Magical Damage** + **35% SpellPower за каждый ARCANE_CHARGE**; при cast consumes все charges.


### [A-3-2] Усиленный Заряд

`MaxRank 2`


**Требует:** `A-3-1`


MaxStacks `ARCANE_CHARGE` остаётся 4, но каждый stack дополнительно даёт `Arcane Burst` **+8% / +15% CriticalChance** суммарно при 4 stacks.


### [A-3-3] Тайное Проникновение

`MaxRank 4`


MagicPenetration: **+3% / +6% / +9% / +12%**.


### [A-3-4] Остаточный Импульс

`MaxRank 2`


**Требует:** `A-3-1`


После расходования 3+ `ARCANE_CHARGE` следующий `Arcane Spark` в течение 5 sec стоит **0 / 0 Mana** и наносит **+10% / +20% Damage**. Rank 1: cost −50%, Rank 2: cost = 0.


## TIER 4 — 15 spent


### [A-4-1] Эхо Заклинания

`MaxRank 2`


После успешной Casted Mage ability **8% / 14% шанс** повторить 35% её Magical Damage через 0.5 sec. Echo не критует, `CanTriggerFromProc = false`, не создаёт Charges.


### [A-4-2] Манаворот

`MaxRank 3`


Если Mana ниже 30%, Mana regeneration in combat **+20% / +40% / +60%**.


### [A-4-3] Переполнение

`MaxRank 2`


Если Mana выше 80%, SpellPower **+4% / +8%**.


### [A-4-4] Точный Расчёт

`MaxRank 2`


Missed Mage ability возвращает **40% / 70%** её ManaCost.


## TIER 5 — 20 spent


### [A-5-1] Перегрузка Маны

`MaxRank 1`


**Требует:** `A-2-1`


Активная Off-GCD ability, 10 sec. SpellPower **+15%**, MagicPenetration **+10%**; Mage ability ManaCost **+10%**. `Arcane Spark` создаёт 2 Charges вместо 1. CD **100 sec**.


### [A-5-2] Контролируемый Взрыв

`MaxRank 2`


**Требует:** `A-3-1`


`Arcane Burst` при 4 Charges: Damage **+10% / +20%**.


### [A-5-3] Возврат Энергии

`MaxRank 4`


**Требует:** `A-3-1`


`Arcane Burst` после расходования Charges возвращает **2 / 3 / 4 / 5 Mana за каждый consumed Charge**.


### [A-5-4] Раскол Формулы

`MaxRank 2`


Critical Mage ability даёт на 5 sec personal MagicPenetration **+3% / +6%** против текущей цели. Не стакается.


## TIER 6 — 25 spent


### [A-6-1] Арканный Каскад

`MaxRank 1`


**Требует:** `A-2-1`


При 4 `ARCANE_CHARGE` `Arcane Spark` меняется на `ARCANE_CASCADE`: Instant, 20 Mana, CD 8 sec, **150% SpellPower Magical Damage**, consumes 2 Charges.


### [A-6-2] Сбережённое Время

`MaxRank 2`


**Требует:** `A-3-1`


После `Arcane Burst` CastTime следующего Casted Mage ability в течение 5 sec **−0.15 / −0.30 sec**.


### [A-6-3] Мана Не Пропадает

`MaxRank 2`


Overheal Mana отсутствует: если resource restore должен превысить MaxMana, **25% / 50%** избыточного восстановления превращается в shield до **3% MaxHP**, duration 5 sec. `ICD = 8 sec`.


### [A-6-4] Тайное Подавление

`MaxRank 1`


Открывает `ARCANE_SEAL`: 60% SpellPower Magical Damage + **Silence 2 sec**, 25 Mana, CD 30 sec.


## TIER 7 — 30 spent


### [A-7-1] Совершенный Взрыв

`MaxRank 2`


**Требует:** `A-3-1`


`Arcane Burst` CastTime **−0.15 / −0.30 sec** и Damage **+5% / +10%**.


### [A-7-2] Эхо Перегрузки

`MaxRank 1`


**Требует:** `A-4-1`, `A-5-1`


Во время `Перегрузки Маны` шанс `Эхо Заклинания` удваивается, но не может сработать чаще 1 раза в 2 sec.


### [A-7-3] Резонанс Школ

`MaxRank 3`


После `FIRE` или `FROST` ability следующий `ARCANE` hit в течение 5 sec наносит **+5% / +10% / +15% Damage**. После `ARCANE` hit следующий FIRE/FROST hit получает **+3% / +6% / +9% Damage**.


### [A-7-4] Последний Резерв

`MaxRank 2`


Когда Mana впервые падает ниже 15%: восстановить **8% / 15% MaxMana**. `ICD = 60 sec`.


## TIER 8 — 35 spent


### [A-8-1] Абсолютная Формула

`MaxRank 1`


**Требует:** `A-7-1`


`Arcane Burst` при 4 Charges не может Miss и получает **+15% CriticalChance**.


### [A-8-2] Бесконечная Цепь

`MaxRank 2`


**Требует:** `A-3-1`


Critical `Arcane Burst` после расходования 4 Charges создаёт **1 / 2 ARCANE_CHARGE** после завершения cast.


### [A-8-3] Совершенная Перегрузка

`MaxRank 1`


**Требует:** `A-5-1`, `A-3-1`


Активация `Перегрузки Маны` восстанавливает **15% MaxMana** и сбрасывает cooldown `Arcane Burst`.


## TIER 9 — 40 spent


### [A-9-1] АРХИМАГ

`MaxRank 1`


**Требует:** `A-8-1`, `A-8-3`


Capstone. Требует 40 spent. Max `ARCANE_CHARGE` = 5. `Arcane Burst` получает бонус пятого stack по той же формуле. При расходовании 5 Charges: 35% его итогового damage повторяется через 1 sec как Arcane Echo, не критует и не запускает proc chains. SpellPower **+8%**.


---

# ВЕТКА — ❄️ ЛЁД

**Роль:** контролируемый Magical DPS / debuffs / умеренная выживаемость.


## TIER 1 — 0 spent


### [I-1-1] Холодная Точность

`MaxRank 4`


Accuracy `FROST` abilities: **+2% / +4% / +6% / +8%**.


### [I-1-2] Острый Лёд

`MaxRank 4`


CriticalChance `FROST` abilities: **+2% / +4% / +6% / +8%**.


### [I-1-3] Закалённый Разум

`MaxRank 4`


MagicResistance **+3% / +6% / +9% / +12%**.


### [I-1-4] Экономия Холода

`MaxRank 2`


ManaCost `FROST` abilities **−5% / −10%**.


## TIER 2 — 5 spent


### [I-2-1] Обморожение

`MaxRank 4`


Успешный `Ice Shard` накладывает `FROSTBITE` на 6 sec, max 3 stacks. Каждый stack снижает AttackSpeed цели на **1% / 2% / 3% / 4%**. Это stat debuff, не Slow.


### [I-2-2] Ледяная Трещина

`MaxRank 2`


**Требует:** `I-2-1`


По цели с 3 `FROSTBITE` твой `FROST` Damage **+4% / +8%**.


### [I-2-3] Хрустальный Отклик

`MaxRank 2`


Когда ты получаешь Critical Damage, создаётся небольшой universal shield **2% / 4% MaxHP** на 5 sec. `InternalCooldown = 12 sec`. Это страховка от следующего удара, а не полноценная защитная ротация.


### [I-2-4] Чистый Снег

`MaxRank 2`


После успешного `Ice Shard` Accuracy следующей Mage ability в течение 4 sec **+3% / +6%**.


## TIER 3 — 10 spent


### [I-3-1] Ледяное Копьё

`MaxRank 1`


**Требует:** `I-2-1`


Открывает Instant `ICE_LANCE`: **95% SpellPower Magical Damage**, 18 Mana, CD 6 sec. По цели с 3 `FROSTBITE`: +25% Damage и consumes 1 stack.


### [I-3-2] Хрупкость

`MaxRank 2`


Critical `FROST` hit накладывает `BRITTLE` на 6 sec: твой CriticalDamage по цели **+5% / +10%**. Не стакается.


### [I-3-3] Белый Шум

`MaxRank 2`


**Требует:** `I-2-1`


Твои `FROSTBITE` stacks также уменьшают Accuracy цели на **1% / 2% за stack**.


### [I-3-4] Ледяной Пробой

`MaxRank 4`


MagicPenetration `FROST`: **+3% / +6% / +9% / +12%**.


## TIER 4 — 15 spent


### [I-4-1] Ледяной Раскол

`MaxRank 1`


**Требует:** `I-2-1`


Открывает `ICE_FRACTURE`: всем врагам CombatSession **70% SpellPower Magical Damage**, 28 Mana, CD 12 sec. Цели с 3 `FROSTBITE` получают **Stun 1 sec** и теряют 3 stacks. Stun проходит через обычный DR.


### [I-4-2] Прочная Корка

`MaxRank 2`


**Требует:** `I-2-3`


Твои shields получают AbsorbAmount **+5% / +10%**. Не создаёт новый shield сам по себе.


### [I-4-3] Морозный Ответ

`MaxRank 2`


После Dodge или полностью поглощённого shield'ом удара следующий `FROST` spell в течение 5 sec наносит **+5% / +10% Damage**. `ICD = 5 sec`.


### [I-4-4] Ровный Холод

`MaxRank 2`


`Ice Shard` CastTime **−0.10 / −0.20 sec**.


## TIER 5 — 20 spent


### [I-5-1] Сердце Зимы

`MaxRank 1`


Активная Off-GCD ability, 10 sec: `FROST` Damage +12%, `Ice Shard` CastTime −0.25 sec, FROST ManaCost −15%. CD **100 sec**.


### [I-5-2] Расколотый Панцирь

`MaxRank 2`


**Требует:** `I-3-1`, `I-3-2`


`Ice Lance` по цели с `BRITTLE` наносит **+8% / +15% Damage**.


### [I-5-3] Ледяная Экономия

`MaxRank 4`


Critical `FROST` ability восстанавливает **1 / 2 / 3 / 4 Mana**. `ICD = 1 sec`.


### [I-5-4] Холодный Прицел

`MaxRank 2`


По цели ниже 30% HP Accuracy и CriticalChance `FROST` abilities **+3% / +6%**.


## TIER 6 — 25 spent


### [I-6-1] Печать Молчания

`MaxRank 1`


Открывает `FROST_SEAL`: **55% SpellPower Magical Damage + Silence 2 sec**, 22 Mana, CD 30 sec.


### [I-6-2] Усиленный Отклик

`MaxRank 2`


**Требует:** `I-2-3`


`Хрустальный Отклик` shield становится **3% / 5% MaxHP** вместо 2%/4% и duration +1 sec. `ICD` не уменьшается.


### [I-6-3] Треснувший Лёд

`MaxRank 2`


**Требует:** `I-2-1`


Когда `FROSTBITE` достигает 3 stacks, следующий `Ice Shard` в течение 5 sec CriticalChance **+5% / +10%**.


### [I-6-4] Стужа Без Паники

`MaxRank 2`


Во время Stun/Silence на тебе входящий Magical Damage **−3% / −6%**. Не уменьшает duration контроля.


## TIER 7 — 30 spent


### [I-7-1] Совершенное Копьё

`MaxRank 2`


**Требует:** `I-3-1`


`Ice Lance`: cooldown **−0.5 / −1 sec**, по цели с 3 Frostbite бонус Damage становится **+35% / +45%**.


### [I-7-2] Зимнее Давление

`MaxRank 2`


**Требует:** `I-2-1`


По цели с 3 `FROSTBITE` её Damage Dealt **−3% / −5%**. Эффект исчезает сразу при потере третьего stack.


### [I-7-3] Холодная Серия

`MaxRank 3`


Три успешных `FROST` ability подряд дают `COLD_SEQUENCE`: следующий `FROST` spell ManaCost **−10% / −20% / −30%** и Damage **+3% / +5% / +7%**. Miss/Interrupt сбрасывает счётчик.


### [I-7-4] Белая Тишина

`MaxRank 1`


**Требует:** `I-6-1`


`FROST_SEAL` после Silence дополнительно снижает AttackSpeed цели на 10% на 4 sec. Это stat debuff.


## TIER 8 — 35 spent


### [I-8-1] Абсолютный Раскол

`MaxRank 1`


**Требует:** `I-4-1`, `I-3-2`


`Ice Fracture` по цели с 3 Frostbite после Stun также накладывает `BRITTLE` на 6 sec.


### [I-8-2] Совершенное Сердце

`MaxRank 2`


**Требует:** `I-5-1`


Во время `Сердца Зимы` CriticalChance FROST **+4% / +8%** и `Ice Lance` cooldown восстанавливается на 50% быстрее.


### [I-8-3] Лёд Не Ломается

`MaxRank 1`


**Требует:** `I-6-2`


Первый раз за CombatSession, когда HP падает ниже 20%, активируется shield **8% MaxHP** на 6 sec. Не предотвращает lethal hit сам по себе.


## TIER 9 — 40 spent


### [I-9-1] ВЛАДЫКА ХОЛОДА

`MaxRank 1`


**Требует:** `I-8-1`, `I-8-2`


Capstone. Требует 40 spent. `FROST` Damage +8%. Max `FROSTBITE` = 4. Четвёртый stack не усиливает AttackSpeed/Accuracy reduction, но является `DEEP_FREEZE` marker: следующий `Ice Lance` consumes все 4 stacks, наносит +60% Damage и Stun 1 sec (`per-target ICD = 12 sec`). Во время `Сердца Зимы` Ice Shard по цели с 3+ stacks имеет CastTime ещё −0.15 sec.


---

# 7. Ключевая механика Огня — Предел Жара

Главная запрошенная burst-механика Огня фиксируется именно так:

```text
Fireball CRIT
→ streak = 1

Fireball CRIT
→ streak = 2

Fireball CRIT
→ streak = 3
→ HEAT_LIMIT
→ streak resets
→ Огненная Комета доступна 8 sec
```

Любой:

```text
Fireball HIT, но не CRIT
```

сбрасывает streak в 0.

`Огненная Комета`:

```text
ManaCost = 0
CastTime = 0.5 sec
Damage = 240% SpellPower
Tag = FIRE
CanCrit = true
```

Название выбрано намеренно не как ещё один «Огненный шар»: игрок должен сразу понимать, что это **особая награда за критическую серию**, а не обычный Fireball.

---

# 8. Слабый защитный Shield

`Хрустальный Отклик` — намеренно **не сильная defensive mechanic**.

```text
Incoming Critical Damage
→ shield 2% / 4% MaxHP
→ 5 sec
→ ICD 12 sec
```

С улучшением:

```text
3% / 5% MaxHP
```

Он:

- помогает пережить следующий небольшой hit;
- создаёт приятный reactive feedback;
- не превращает Mage в Tank;
- не предотвращает lethal damage;
- не конкурирует с полноценными defensive cooldown других классов.

---

# 9. FIRE — балансный intent

Пламя должно быть лучшим по:

- чистому single-target Magical DPS;
- critical burst;
- execute pressure;
- сильным коротким offensive windows;
- Burn synergy.

Цена:

- высокий Mana расход;
- зависимость от Cast;
- меньше control;
- меньше стабильной защиты;
- burst сильнее страдает от interrupt/Silence.

`Предел Жара` не должен быть основным источником всего DPS.

Это редкая, очень приятная награда за успешную критическую серию.

---

# 10. ARCANE — балансный intent

Тайная магия должна быть лучшей по:

- управлению Mana;
- контролируемому burst;
- чередованию способностей;
- MagicPenetration;
- гибридизации с другими ветками.

`ARCANE_CHARGE` является Effect Stack, а не вторым Action Resource.

Это важно:

```text
Mage Resource = Mana
Arcane Charge = temporary Effect state
```

---

# 11. FROST — балансный intent

Лёд должен быть лучшим по:

- предсказуемости;
- control utility;
- enemy AttackSpeed/Accuracy pressure;
- выживаемости;
- sustained DPS в опасном контенте.

Лёд **не использует**:

```text
Slow
Root
Fear
```

`FROSTBITE` уменьшает конкретные Stats цели и поэтому совместим с текущим Combat.

Даже глубокий Frost Mage остаётся Damage class, а не Tank.

---

# 12. Hybrid Builds

## Fire + Arcane

```text
41+ Fire
18 Arcane
```

Получает:
- Mana efficiency;
- MagicPenetration;
- SpellPower;
- хороший burst sustain.

Но не получает полноценный Arcane Charge endgame package.

## Fire + Frost

```text
41+ Fire
18 Frost
```

Получает:
- небольшой reactive shield;
- Frost Accuracy/utility;
- более безопасный solo build.

Но жертвует частью Arcane Mana efficiency.

## Arcane + Fire

Ранний Fire Crit/Mana package позволяет усилить aggressive Arcane build.

## Arcane + Frost

Самый стабильный Mage:
- Mana;
- control;
- shields;
- меньше peak burst.

## Frost + Fire

Frost получает дополнительный offensive pressure, но не превращается в Fire Mage.

---

# 13. Рекомендуемые Level 60 archetypes

```text
🔥 Pure Pyromancer
45 Fire / 14 Arcane

🔥 Glass Cannon
41 Fire / 18 Arcane

🔥 Solo Fire
41 Fire / 18 Frost

🔮 Arcane Master
45 Arcane / 14 Fire

🔮 Battle Arcanist
41 Arcane / 18 Frost

❄️ Frost Controller
45 Frost / 14 Arcane

❄️ Frost Burst Hybrid
41 Frost / 18 Fire
```

---

# 14. UI hooks

Mage UI должен визуально показывать только branch-specific combat state, который реально влияет на решение.

## Fire

```text
Fireball Crit Streak: 0 / 1 / 2 / 3
HEAT_LIMIT available
Burn on target
Combustion duration
```

Когда streak = 2:

- Fireball icon получает лёгкое fiery edge glow;
- UI не закрывает экран огромным эффектом.

При `HEAT_LIMIT`:

- `Огненная Комета` появляется/загорается в skill row;
- короткий haptic feedback;
- 8 sec expiration ring.

## Arcane

Показывать:

```text
ARCANE_CHARGE ● ● ● ○
```

не отдельной resource bar, а маленькими markers над skill row.

## Frost

Показывать на текущей цели:

```text
FROSTBITE ×1 / ×2 / ×3 / ×4
BRITTLE
```

Shield отображается тонким дополнительным сегментом вокруг HP bar, а не второй огромной полосой.

---

# 15. System compatibility

Дерево не вводит новые фундаментальные системы.

Используются существующие:

```text
Stats
Resource
Effect
Damage
Ability
Combat
Talent
```

Новые content tags/states:

```text
FIRE
ARCANE
FROST
BURN
ARCANE_CHARGE
FROSTBITE
BRITTLE
HEAT_LIMIT
```

Все они являются content definitions/tags/effects.

---

# 16. Итоговая идентичность

```text
🔥 ПЛАМЯ
«Я хочу, чтобы всё горело и критовало.»

🔮 ТАЙНАЯ МАГИЯ
«Я хочу идеально управлять Mana и последовательностью заклинаний.»

❄️ ЛЁД
«Я хочу контролировать темп боя и выигрывать за счёт стабильности.»
```

Все три ветки остаются Mage DPS.

Разница создаётся не цветом spell effects, а реальными решениями в ротации.
