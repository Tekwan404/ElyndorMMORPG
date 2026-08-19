Elyndor — Damage and Healing System Specification

Document: 09_DAMAGE_AND_HEALING_SYSTEM.md
System: Damage / Healing
Status: Foundation / Source of Truth
Version: 0.2

1. Назначение

Damage and Healing System определяет правила расчёта урона и лечения в Elyndor.

Система охватывает:

типы урона;
типы лечения;
порядок расчёта урона;
порядок расчёта лечения;
критические удары;
промахи и уклонения;
броню и магическое сопротивление;
пробивание брони и пробивание магии;
минимальный урон;
модификаторы урона и лечения;
поглощение урона щитами;
перегрев лечения;
вампиризм / lifesteal;
взаимодействие с DoT/HoT;
взаимодействие с Combat System, Effects System, Resource System и Character System.

Damage and Healing System не определяет:

конкретные значения урона способностей;
конкретные значения лечения способностей;
конкретные коэффициенты масштабирования;
конкретные значения брони и сопротивлений;
конкретные значения Critical Damage;
AI;
лут;
экономику;
классовые деревья;
UI;
визуализацию цифр урона и лечения.

2. Основной принцип

Все расчёты урона и лечения являются серверными.

Клиент может отображать результат, но не может определять:

попал ли удар;
был ли критический удар;
сколько урона было нанесено;
сколько лечения было применено;
поглотил ли щит урон;
умерла ли цель.

Сервер определяет итоговый результат.

3. Effective Damage и Effective Healing

Система оперирует двумя основными результатами:

Effective Damage
Фактический урон, который был применён к HP цели или поглощён щитом.

Effective Healing
Фактическое лечение, которое было применено к HP цели.

Overhealing не является Effective Healing.

Missed, Dodged и Immune атаки не являются Effective Damage.

4. Типы урона

В базовой модели выделяются три типа урона:

Physical Damage
Физический урон.

Magical Damage
Магический урон.

True Damage
Урон, игнорирующий броню и магическое сопротивление.

Physical Damage уменьшается физической бронёй цели.

Magical Damage уменьшается магическим сопротивлением цели.

True Damage не проходит через Armor или MagicResistance mitigation.

Другие типы урона, например Elemental Damage, Holy Damage, Shadow Damage, могут быть добавлены позднее отдельным расширением.

5. Источники урона

Урон может происходить из:

Auto Attack;
Casted Ability;
Instant Ability;
Next Attack Modifier;
DoT effect;
environmental effect, если будет добавлен;
scripted event.

Каждый источник урона должен передавать серверу достаточный контекст для расчёта.

6. Damage Request

Запрос на урон концептуально содержит:

DamageRequest
  ├── SourceId
  ├── TargetId
  ├── AbilityId, optional
  ├── DamageType
  ├── BaseAmount
  ├── SnapshotContext
  ├── CanMiss
  ├── CanBeDodged
  ├── CanCrit
  ├── IgnoreArmor, optional
  ├── IgnoreMagicResistance, optional
  ├── IgnoreShields, optional
  ├── SuppressMinimumDamage, optional
  ├── ThreatContext
  └── Metadata

Конкретная техническая структура будет определена позднее.

7. Damage Resolution Pipeline

Расчёт урона происходит по фиксированному порядку.

Базовый pipeline:

DamageRequest
  ↓
Validate source and target
  ↓
Hit check / Dodge check
  ↓
Critical roll
  ↓
Apply critical multiplier
  ↓
Apply penetration
  ↓
Apply armor or magic resistance mitigation
  ↓
Apply damage modifiers
  ↓
Apply Minimum Damage
  ↓
Apply shield absorption
  ↓
Apply remaining damage to HP
  ↓
Emit damage events

Если любой этап завершается промахом, уклонением или иммунитетом, дальнейший расчёт урона не производится.

Для True Damage:

penetration не применяется;
armor mitigation не применяется;
magic resistance mitigation не применяется.

8. Hit Check

Hit Check определяет, попала ли атака.

По умолчанию все **враждебные атаки** используют Hit Check:

- Auto Attack;
- Physical Ability;
- Magical Ability;
- hostile True Damage Ability.

Ability может пропустить Hit Check только через явный `IgnoresMiss = true`.

Beneficial abilities (heal/buff) Hit Check не используют, если content явно не говорит обратное.

9. Miss Chance

Miss Chance определяет вероятность промаха.

Базовая формула:

```text
LevelGap = max(0, TargetLevel - SourceLevel)
LevelPenalty = min(LevelGap × 1 percentage point, 10%)

EffectiveMissChance = clamp(
    BaseMissChance
    + LevelPenalty
    - SourceAccuracy,
    MinMissChance,
    MaxMissChance
)
```

Текущие default values:

```text
BaseMissChance = 5%
LevelPenaltyPerLevel = 1 percentage point
MaxLevelPenalty = 10%
MinMissChance = 0%
MaxMissChance = 30%
```

Accuracy уменьшает вероятность промаха.

Пример:

BaseMissChance = 5%
Source Accuracy = 3%
EffectiveMissChance = 2%

10. Dodge Check

Dodge Check определяет, уклонилась ли цель от атаки.

Dodge применяется только если атака может быть уклонена.

По умолчанию:

Auto Attack может быть уклонён.
Физические способности могут быть уклонены, если это определено способностью.
Магические способности по умолчанию не могут быть уклонены.
True Damage может быть уклонён только если это явно разрешено источником.

DodgeChance = Target Dodge

Если результат меньше 0, используется 0.

Если результат больше 100%, используется 100%.

11. Порядок Hit и Dodge

Если атака может промахнуться и может быть уклонена:

сначала проверяется Miss;
затем проверяется Dodge.

Пример:

EffectiveMissChance = 2%
Target Dodge = 10%

Roll:
  0–2% → Miss
  2–12% → Dodge
  12–100% → Hit

Если атака не может промахнуться, проверяется только Dodge.

Если атака не может быть уклонена, проверяется только Miss.

12. Immunity

Если цель имеет иммунитет к типу урона или к конкретному источнику:

урон не наносится;
критический удар не проверяется;
броня и сопротивление не применяются;
щиты не поглощают урон;
Threat по умолчанию не генерируется;
Minimum Damage не применяется.

Immunity определяется Effects System, Combat System или правилами цели.

13. Critical Strikes

Если атака попала и может быть критической, выполняется Critical roll.

CriticalChance = Source CriticalChance

Если результат меньше 0, используется 0.

Если результат больше 100%, используется 100%.

Если roll успешен:

атака считается критической;
базовый урон умножается на CriticalDamageMultiplier.

14. Critical Damage Multiplier

CriticalDamageMultiplier определяет, насколько сильнее критический удар.

Authoritative representation:

```text
Base CriticalDamage = 100% bonus
CriticalDamageMultiplier = 1 + FinalCriticalDamage
```

Базово:

```text
FinalCriticalDamage = 100%
CriticalDamageMultiplier = 2.0
```

Например talent `+15% CriticalDamage`:

```text
FinalCriticalDamage = 115%
CriticalDamageMultiplier = 2.15
```

Это полностью совпадает с Attributes and Stats System.

15. Критический удар, пробивание и броня

Критический множитель применяется до брони и сопротивления.

Penetration не умножает критический урон напрямую.

Penetration уменьшает эффективную броню или эффективное магическое сопротивление цели, после чего уже рассчитывается mitigation.

Порядок:

Base Damage
  ↓
Critical multiplier
  ↓
Penetration reduces Armor / MagicResistance
  ↓
Mitigation formula
  ↓
Damage modifiers

Пример Critical + Armor Penetration:

Дано:

Base Physical Damage = 100
Critical = yes
CriticalDamageMultiplier = 2.0
Target Armor = 100
Armor Penetration = 20%
Damage modifiers = none
Shield = none

Pipeline:

Critical Damage = 100 × 2.0 = 200
EffectiveArmor = 100 × (1 - 0.20) = 80
Mitigation = 200 × 100 / (100 + 80) = 111.11
ModifiedDamage = 111.11
EffectiveDamageToHP = 111.11

Итог:

цель получает примерно 111 физического урона.

Критический множитель увеличил урон до mitigation.
Penetration уменьшил броню до mitigation.
Penetration не умножал критический урон отдельно.

16. Armor и Physical Damage

Armor уменьшает входящий физический урон.

Базовая формула mitigation:

PhysicalDamageAfterArmor = PhysicalDamage × 100 / (100 + EffectiveArmor)

EffectiveArmor не может быть меньше 0.

Current data-driven default:

ArmorMitigationConstant = 100

17. Magic Resistance и Magical Damage

Magic Resistance уменьшает входящий магический урон.

Базовая формула mitigation:

MagicDamageAfterResistance = MagicalDamage × 100 / (100 + EffectiveMagicResistance)

EffectiveMagicResistance не может быть меньше 0.

Current data-driven default:

MagicMitigationConstant = 100

18. Armor Penetration

Armor Penetration уменьшает эффективность физической брони цели.

Базовая модель:

EffectiveArmor = Target Armor × (1 - ArmorPenetration)

ArmorPenetration выражается как доля или процент.

Если ArmorPenetration = 20%, то:

EffectiveArmor = Target Armor × 0.8

Если ArmorPenetration превышает 100%, используется 100%.

EffectiveArmor не может быть меньше 0.

Пример:

Target Armor = 200
Armor Penetration = 25%

EffectiveArmor = 200 × 0.75 = 150

19. Magic Penetration

Magic Penetration уменьшает эффективность магического сопротивления цели.

Базовая модель:

EffectiveMagicResistance = Target MagicResistance × (1 - MagicPenetration)

MagicPenetration выражается как доля или процент.

Если MagicPenetration = 20%, то:

EffectiveMagicResistance = Target MagicResistance × 0.8

Если MagicPenetration превышает 100%, используется 100%.

EffectiveMagicResistance не может быть меньше 0.

Пример:

Target MagicResistance = 150
Magic Penetration = 30%

EffectiveMagicResistance = 150 × 0.7 = 105

20. True Damage

True Damage — это тип урона, который игнорирует обычную mitigation через Armor и MagicResistance.

Базовые правила True Damage:

True Damage не уменьшается Armor.
True Damage не уменьшается MagicResistance.
ArmorPenetration не применяется к True Damage.
MagicPenetration не применяется к True Damage.
True Damage может быть критическим, если источник это разрешает.
True Damage может быть уклонён или промахнуться только если источник явно это разрешает.
True Damage проходит через Damage Modifiers только если модификатор явно влияет на True Damage.
True Damage может быть поглощён щитом, если источник не имеет IgnoreShields и щит способен поглощать True Damage.
Minimum Damage применяется к True Damage, если запрос не помечен как SuppressMinimumDamage.

Пример:

Base True Damage = 100
Target Armor = 500
Target MagicResistance = 500
Critical = no
Damage modifiers = none
Shield = none

Pipeline:

Hit = yes
Mitigation ignored
ModifiedDamage = 100
EffectiveDamageToHP = 100

Итог:

цель получает 100 True Damage.

21. Minimum Damage

Minimum Damage предотвращает ситуацию, при которой успешная атака наносит ничтожно малый урон, например 0.001.

Базовое правило:

Если атака попала.
Если цель не имеет immunity.
Если BaseAmount > 0.
Если запрос не помечен как SuppressMinimumDamage.

Тогда итоговый урон после mitigation и damage modifiers не может быть меньше MinimumDamage.

Current data-driven default:

MinimumDamage = 1

Minimum Damage применяется после damage modifiers и до shield absorption.

Формула:

MinimumDamageAmount = max(MinimumDamage, ModifiedDamage)

Затем MinimumDamageAmount проходит через shield absorption.

Пример без щита:

ModifiedDamage после mitigation и modifiers = 0.4
MinimumDamage = 1

MinimumDamageAmount = 1
Shield = 0
EffectiveDamageToHP = 1

Пример со щитом:

ModifiedDamage после mitigation и modifiers = 0.4
MinimumDamage = 1
Shield = 5

MinimumDamageAmount = 1
AbsorbedDamage = 1
EffectiveDamageToHP = 0

Minimum Damage не обязан пробивать щит.

Он гарантирует, что успешный удар не превратится в ноль до shield absorption.

22. Damage Modifiers

После mitigation могут применяться модификаторы урона.

Примеры:

+10% damage dealt;
-15% damage taken;
+20% physical damage;
+25% magical damage;
+10% true damage, если явно указано;
next attack deals bonus damage;
target takes increased damage.

Модификаторы предоставляются Effects System, Ability System или Combat System.

Порядок применения модификаторов:

MitigatedDamage
  ↓
+ Flat Damage Modifiers
  ↓
+ Percent Damage Modifiers
  ↓
× Multiplicative Damage Modifiers
  ↓
Clamp min 0
  ↓
ModifiedDamage

Damage Modifiers применяются после armor/resistance mitigation и до shield absorption.

Это означает:

Armor/Resistance mitigation
  ↓
Damage Modifiers, including damage dealt and damage taken
  ↓
Minimum Damage
  ↓
Shield absorption
  ↓
Apply to HP

Debuff вроде:

Target takes -20% damage

уменьшает урон до щита.

Пример:

MitigatedDamage = 100
Damage taken modifier = -20%
ModifiedDamage = 80
Shield = 30
Absorbed = 30
EffectiveDamageToHP = 50

Если конкретный модификатор должен применяться до mitigation или после shield, это должно быть явно указано в источнике модификатора.

По умолчанию:

Physical Damage modifiers применяются к Physical Damage.
Magical Damage modifiers применяются к Magical Damage.
True Damage modifiers применяются к True Damage только если явно указано.
Generic damage modifiers применяются ко всем типам, если явно указано.

23. Shield Absorption

Если цель имеет активный shield, урон может быть поглощён.

Shield absorption происходит после damage modifiers и Minimum Damage.

Базовый порядок:

MinimumDamageAmount
  ↓
Check active shields
  ↓
Absorb damage
  ↓
Remaining damage applies to HP

Если несколько щитов активны:

по умолчанию newest shield absorbs first;
конкретные правила определяются Effects System.

Пример:

MinimumDamageAmount = 300
Active Shield = 200

AbsorbedDamage = 200
EffectiveDamageToHP = 100

Если щит поглощает весь урон:

AbsorbedDamage = MinimumDamageAmount
EffectiveDamageToHP = 0

Если источник имеет IgnoreShields = true:

щиты не поглощают урон;
урон сразу применяется к HP, если нет другого правила.

24. Applying Damage

Оставшийся урон применяется к CurrentHP.

NewCurrentHP = CurrentHP - EffectiveDamageToHP

NewCurrentHP не может быть меньше 0.

Если NewCurrentHP = 0:

цель считается мёртвой;
Character System переводит цель в DEAD;
запускаются правила смерти и респауна.

25. Threat Relevant Damage

Для Threat System используется ThreatRelevantDamage.

ThreatRelevantDamage = EffectiveDamageToHP + AbsorbedDamage

Примеры:

Урон 100, броня уменьшила урон, HP потерял 60:
ThreatRelevantDamage = 60

Урон 100, щит поглотил 100, HP потерял 0:
ThreatRelevantDamage = 100

Урон 100, цель immune:
ThreatRelevantDamage = 0

Урон 100, атака dodged:
ThreatRelevantDamage = 0

Конкретные правила Threat определяются Combat System.

26. Damage Result

Результат урона концептуально содержит:

DamageResult
  ├── DamageRequestId
  ├── SourceId
  ├── TargetId
  ├── DamageType
  ├── HitResult
  ├── IsCritical
  ├── BaseAmount
  ├── CriticalAmount
  ├── MitigatedAmount
  ├── ModifiedAmount
  ├── MinimumDamageApplied
  ├── AbsorbedAmount
  ├── EffectiveAmount
  ├── ThreatRelevantAmount
  ├── AppliedAt
  └── Metadata

HitResult может быть:

Hit;
Miss;
Dodge;
Immune.

27. Пример физического урона

Дано:

Base Physical Damage = 100
Critical = no
Target Armor = 50
Armor Penetration = 0%
Damage modifiers = none
Shield = none

Pipeline:

Hit = yes
Critical = no
EffectiveArmor = 50
Mitigation = 100 × 100 / (100 + 50) = 66.67
ModifiedDamage = 66.67
MinimumDamageAmount = 66.67
AbsorbedDamage = 0
EffectiveDamageToHP = 66.67

Итог:

цель получает примерно 67 физического урона.

28. Пример магического урона

Дано:

Base Magical Damage = 200
Critical = yes
CriticalDamageMultiplier = 2.0
Target MagicResistance = 100
Magic Penetration = 20%
Damage modifiers = none
Shield = none

Pipeline:

Hit = yes
Critical = yes
CriticalDamage = 200 × 2.0 = 400
EffectiveMagicResistance = 100 × 0.8 = 80
Mitigation = 400 × 100 / (100 + 80) = 222.22
ModifiedDamage = 222.22
MinimumDamageAmount = 222.22
AbsorbedDamage = 0
EffectiveDamageToHP = 222.22

Итог:

цель получает примерно 222 магического урона.

29. Типы лечения

В базовой модели лечение может быть:

Direct Healing
Мгновенное восстановление HP.

Healing over Time (HoT)
Периодическое восстановление HP.

Vampiric Healing
Лечение, полученное из нанесённого урона.

Scripted Healing
Лечение по специальному скриптовому правилу.

Другие типы, например:

Resurrection Healing;
Revive over time;
Damage-to-healing conversion с особыми правилами;

могут быть добавлены позднее отдельным расширением.

30. HealingType

HealingRequest содержит HealingType.

HealingType определяет категорию лечения и специальные правила.

Базовые типы:

Normal Healing
Обычное прямое лечение.

HoT Healing
Периодическое лечение из HoT эффекта.

Vampiric Healing
Лечение от вампиризма / lifesteal.

Scripted Healing
Лечение из скриптового события.

HealingType влияет на:

разрешён ли critical heal;
генерирует ли лечение Threat;
использует ли лечение snapshot;
применяются ли специальные правила.

HealingType не означает, что лечение уменьшается Armor или MagicResistance.

По умолчанию:

лечение не уменьшается бронёй;
лечение не уменьшается магическим сопротивлением;
Physical Healing как отдельный тип не вводится.

Если понадобится физическое лечение или лечение, уменьшаемое защитными статами, это будет добавлено отдельным расширением.

31. Источники лечения

Лечение может происходить из:

Instant healing ability;
Casted healing ability;
HoT effect;
Vampirism / Life Steal;
scripted event;
world effect, если будет добавлен.

32. Vampirism / Life Steal

Vampirism — это лечение, получаемое из нанесённого урона.

Базовая модель:

DamageResult resolved
  ↓
If source has VampirismPercent > 0
  ↓
Calculate VampiricHealingAmount
  ↓
Create HealingRequest
  ↓
Resolve healing

Базовая формула:

VampiricHealingAmount = EffectiveDamageToHP × VampirismPercent

По умолчанию:

VampirismPercent = 0%

Vampiric Healing:

не критический по умолчанию;
использует обычные правила Effective Healing;
может быть overhealing;
применяется только если цель жива;
не применяется, если урон был Miss, Dodge или Immune.

По умолчанию Vampiric Healing не генерирует дополнительный Threat.

Причина:

урон уже сгенерировал Threat через ThreatRelevantDamage;
дополнительное healing threat от вампиризма приводило бы к двойному учёту.

Если конкретная способность или эффект явно определяют, что Vampiric Healing генерирует Threat, это указывается отдельно.

Пример:

EffectiveDamageToHP = 120
VampirismPercent = 10%

VampiricHealingAmount = 12

Если источник имеет 100 HP и MaxHP 100:

EffectiveHealing = 0
Overhealing = 12

33. Healing Request

Запрос на лечение концептуально содержит:

HealingRequest
  ├── SourceId
  ├── TargetId
  ├── AbilityId, optional
  ├── HealingType
  ├── BaseAmount
  ├── SnapshotContext
  ├── CanCrit
  ├── GeneratesThreat
  ├── ThreatContext
  └── Metadata

По умолчанию:

Normal Healing:
  CanCrit = false, если не разрешено
  GeneratesThreat = true

HoT Healing:
  CanCrit = false, если эффект не разрешает
  GeneratesThreat = true

Vampiric Healing:
  CanCrit = false
  GeneratesThreat = false

Scripted Healing:
  правила определяются скриптом.

Конкретная техническая структура будет определена позднее.

34. Healing Resolution Pipeline

Расчёт лечения происходит по фиксированному порядку.

Базовый pipeline:

HealingRequest
  ↓
Validate source and target
  ↓
Check target alive
  ↓
Critical heal roll, if allowed
  ↓
Apply CriticalHealMultiplier, if critical
  ↓
Apply healing modifiers
  ↓
Clamp min 0
  ↓
Calculate EffectiveHealing
  ↓
Apply EffectiveHealing to HP
  ↓
Calculate Overhealing
  ↓
Emit healing events

Если цель мертва:

лечение по умолчанию не применяется;
воскрешение не является частью Damage and Healing System.

35. Critical Healing Order

Порядок критического лечения фиксирован.

BaseHealing
  ↓
Critical roll
  ↓
If critical:
  BaseHealing × CriticalHealMultiplier
  ↓
Apply Healing Modifiers
  ↓
Clamp min 0
  ↓
ModifiedHealing

Критический множитель применяется до healing modifiers.

Это согласуется с damage pipeline, где критический множитель применяется до mitigation и последующих модификаторов.

Пример:

Base Healing = 300
Critical heal = yes
CriticalHealMultiplier = 1.5
Healing done modifier = +20%

Pipeline:

CriticalHealing = 300 × 1.5 = 450
ModifiedHealing = 450 × 1.2 = 540

Если цель missing 400 HP:

EffectiveHealing = 400
Overhealing = 140

36. Healing Modifiers

К лечению могут применяться модификаторы.

Примеры:

+10% healing done;
+15% healing received;
-20% healing received;
HoT healing increased;
next heal has bonus amount.

Порядок применения модификаторов:

CriticalHealingAmount или BaseHealing, если крита не было
  ↓
+ Flat Healing Modifiers
  ↓
+ Percent Healing Modifiers
  ↓
× Multiplicative Healing Modifiers
  ↓
Clamp min 0
  ↓
ModifiedHealing

ModifiedHealing не может быть меньше 0.

Если конкретный модификатор должен применяться до critical roll, это должно быть явно указано в источнике модификатора.

37. Critical Healing

По умолчанию лечение не может быть критическим.

Критическое лечение возможно только если это явно разрешено:

способностью;
эффектом;
талантом;
другой системой.

Если критическое лечение разрешено:

используется CriticalChance источника;
критический множитель лечения определяется CriticalHealMultiplier.

По умолчанию:

CriticalHealMultiplier = 1.5

Конкретное значение может быть изменено.

38. Effective Healing

Effective Healing — это фактически применённое лечение.

EffectiveHealing = min(ModifiedHealing, MaxHP - CurrentHP)

Если ModifiedHealing больше недостающего HP:

применяется только недостающее HP;
остаток становится Overhealing.

39. Overhealing

Overhealing — это часть лечения, которая превысила недостающее HP.

Overhealing = ModifiedHealing - EffectiveHealing

Overhealing:

не применяется к HP;
не сохраняет избыточное здоровье;
не генерирует Threat;
может логироваться для аналитики.

Пример:

CurrentHP = 700
MaxHP = 1000
ModifiedHealing = 400

MissingHP = 300
EffectiveHealing = 300
Overhealing = 100

40. Applying Healing

Новое значение HP:

NewCurrentHP = CurrentHP + EffectiveHealing

NewCurrentHP не может превышать MaxHP.

Если цель уже имеет полное HP:

EffectiveHealing = 0
Overhealing = ModifiedHealing

41. Threat Relevant Healing

Для Threat System используется ThreatRelevantHealing.

ThreatRelevantHealing = EffectiveHealing

Overhealing не генерирует Threat.

Пример:

ModifiedHealing = 400
EffectiveHealing = 300
Overhealing = 100

ThreatRelevantHealing = 300

Vampiric Healing по умолчанию:

ThreatRelevantHealing = 0

если источник явно не определяет другое.

Конкретные правила Threat определяются Combat System.

42. Healing Result

Результат лечения концептуально содержит:

HealingResult
  ├── HealingRequestId
  ├── SourceId
  ├── TargetId
  ├── HealingType
  ├── IsCritical
  ├── BaseAmount
  ├── CriticalAmount
  ├── ModifiedAmount
  ├── EffectiveAmount
  ├── OverhealingAmount
  ├── ThreatRelevantAmount
  ├── AppliedAt
  └── Metadata

43. Пример лечения

Дано:

Base Healing = 300
Critical heal = no
Healing modifiers = +20% healing done
Target CurrentHP = 800
Target MaxHP = 1000

Pipeline:

ModifiedHealing = 300 × 1.2 = 360
MissingHP = 200
EffectiveHealing = 200
Overhealing = 160

Итог:

цель восстанавливает 200 HP;
160 лечения является overhealing;
ThreatRelevantHealing = 200.

44. DoT Interaction

Damage over Time эффекты используют Damage and Healing System для каждого tick.

Базовые правила:

DoT tick может быть критическим, если это разрешено эффектом.
DoT tick может игнорировать Hit Check и Dodge Check, если эффект не указывает другое.
DoT tick использует snapshot source параметров согласно Effects System.
Target Armor и MagicResistance применяются в момент tick по текущим значениям цели, а не по snapshot момента применения эффекта.
Minimum Damage может применяться к DoT tick, если эффект не помечен как SuppressMinimumDamage.

По умолчанию:

DoT tick не проверяет Miss.
DoT tick не проверяет Dodge.
DoT tick может проверить Critical, если эффект разрешает.
DoT tick проходит через armor/resistance mitigation, если тип урона требует mitigation.
Mitigation использует текущие значения Armor/MagicResistance цели в момент tick.

Если конкретный DoT эффект требует snapshot target mitigation в момент применения, это должно быть явно указано в правилах эффекта.

Конкретные правила DoT snapshot для source параметров определяются Effects System.

45. HoT Interaction

Healing over Time эффекты используют Damage and Healing System для каждого tick.

Базовые правила:

HoT tick применяет лечение к цели.
HoT tick не может вылечить мёртвую цель.
HoT tick учитывает недостающее HP в момент tick.
HoT tick может быть критическим, если это разрешено эффектом.
Overhealing от HoT tick не генерирует Threat.

По умолчанию:

HoT tick не критический.
HoT tick использует snapshot source параметров согласно Effects System.

Конкретные правила HoT snapshot определяются Effects System.

46. Interaction with Combat System

Combat System вызывает Damage and Healing System для:

auto attacks;
abilities;
DoT/HoT ticks;
vampirism triggers;
environmental combat damage, если будет добавлен;
scripted combat events.

Damage and Healing System не решает:

кто атакует;
какая цель выбирается;
когда начинается бой;
когда завершается бой;
как генерируется Threat.

Она только рассчитывает результат конкретного урона или лечения.

47. Interaction with Threat System

Damage and Healing System передаёт Combat System:

ThreatRelevantDamage;
ThreatRelevantHealing;
SourceId;
TargetId;
DamageType или HealingType;
AppliedAt.

Combat System самостоятельно применяет Threat rules.

Базовое правило:

Threat генерируется только от Effective Damage и Effective Healing.

Miss, Dodge, Immune и Overhealing не генерируют Threat.

Vampiric Healing по умолчанию не генерирует дополнительный Threat.

48. Interaction with Effects System

Effects System предоставляет:

damage modifiers;
healing modifiers;
shields;
DoT effects;
HoT effects;
immunities;
conditional modifiers.

Damage and Healing System использует эти данные в pipeline.

Damage and Healing System не хранит эффекты.

49. Interaction with Resource System

Resource System предоставляет:

CurrentHP;
MaxHP;
правила изменения HP;
правила смерти;
правила респауна.

Damage and Healing System изменяет CurrentHP только через разрешённые методы Resource System.

Примеры методов:

ApplyDamageToHP
ApplyHealingToHP

Конкретная техническая реализация методов определяется позднее.

50. Interaction with Character System

Character System использует результат урона для определения:

жив ли персонаж;
перешёл ли персонаж в DEAD;
нужно ли запустить respawn;
нужно ли остановить AFK Farming;
нужно ли остановить Travel;
нужно ли очистить Activity State.

Если CurrentHP достигает 0:

Damage and Healing System сообщает EffectiveDamageApplied;
Character System переводит персонажа в DEAD.

51. AFK Farming Interaction

AFK Farming по умолчанию не использует полный Damage and Healing pipeline.

Если AFK Farming является passive bonus mode:

реальные DamageRequest и HealingRequest не создаются для каждого расчётного убийства;
DoT/HoT ticks не симулируются;
Threat не генерируется;
смерть по умолчанию не происходит;
vampirism не применяется.

Если будущая система вводит combat-like AFK или risk-AFK, её взаимодействие с Damage and Healing System должно быть описано отдельно.

52. Offline Combat Interaction

Offline combat может использовать Damage and Healing System.

Если персонаж участвует в offline combat:

урон рассчитывается серверно;
лечение рассчитывается серверно;
DoT/HoT ticks обрабатываются по Server Time;
vampirism применяется, если он разрешён источником;
смерть обрабатывается обычными правилами.

Offline status не меняет формулы урона и лечения.

53. Events

Damage and Healing System может эмитить события.

Damage events:

DamageRequested
DamageResolved
DamageDealt
DamageTaken
DamageCritical
DamageMissed
DamageDodged
DamageImmune
DamageAbsorbed
MinimumDamageApplied
DamageKilledTarget

Healing events:

HealingRequested
HealingResolved
HealingDone
HealingReceived
HealingCritical
OverhealingOccurred
HealingTargetFull
VampiricHealingOccurred

События должны быть серверно-авторитетными.

54. Event Delivery Rules

Combat System и Resource System могут получать все боевые damage/healing events.

Threat System получает только ThreatRelevantDamage и ThreatRelevantHealing.

Quest System по умолчанию не должен получать каждый DamageDealt или HealingDone event.

Quest System может получать:

DamageKilledTarget;
HealingCompleted objective-related events;
другие события, если objective явно требует tracking.

Analytics и debug tools могут получать расширенный лог, если это разрешено серверной политикой.

55. Persistence

Damage and Healing System не обязана хранить каждый урон как постоянную игровую сущность.

Однако сервер должен сохранять:

CurrentHP;
MaxHP;
состояние щитов через Effects System;
состояние DoT/HoT через Effects System;
результат смерти, если смерть произошла.

Отдельные damage/healing events могут логироваться для:

debug;
combat reports;
analytics;
anti-cheat;
auditing.

Конкретная модель логов определяется отдельно.

56. Restart Recovery

После server restart:

CurrentHP восстанавливается из persisted state;
active DoT/HoT effects проверяются по Server Time;
active shields проверяются по Server Time;
expired effects удаляются;
damage/healing events, не завершённые до restart, не должны дублироваться.

Если смерть была зафиксирована до restart:

смерть не должна теряться.

Если смерть не была зафиксирована до crash:

персонаж может быть восстановлен в последнем безопасном состоянии.

57. Damage and Healing Invariants

INVARIANT-01
Сервер является источником истины для урона и лечения.

INVARIANT-02
Клиент не может определять результат урона или лечения.

INVARIANT-03
Effective Damage не может быть отрицательным.

INVARIANT-04
Effective Healing не может быть отрицательным.

INVARIANT-05
CurrentHP не может быть меньше 0.

INVARIANT-06
CurrentHP не может превышать MaxHP.

INVARIANT-07
Missed, Dodged и Immune атаки не наносят Effective Damage.

INVARIANT-08
Критический удар проверяется только если атака попала и может быть критической.

INVARIANT-09
Critical Damage multiplier применяется до armor/resistance mitigation и damage modifiers.

INVARIANT-10
Penetration уменьшает EffectiveArmor или EffectiveMagicResistance, а не сам урон.

INVARIANT-11
Armor Penetration не может сделать EffectiveArmor отрицательным.

INVARIANT-12
Magic Penetration не может сделать EffectiveMagicResistance отрицательным.

INVARIANT-13
True Damage игнорирует Armor и MagicResistance.

INVARIANT-14
True Damage проходит через damage modifiers только если модификатор явно влияет на True Damage.

INVARIANT-15
Minimum Damage применяется после mitigation и damage modifiers, но до shield absorption.

INVARIANT-16
Minimum Damage не обязан пробивать щит.

INVARIANT-17
Damage Modifiers применяются после mitigation и до shield absorption.

INVARIANT-18
Shield absorption происходит после damage modifiers и Minimum Damage.

INVARIANT-19
Overhealing не применяется к HP.

INVARIANT-20
Overhealing не генерирует Threat.

INVARIANT-21
Лечение не может воскресить мёртвую цель по умолчанию.

INVARIANT-22
Critical Healing multiplier применяется до healing modifiers.

INVARIANT-23
HealingType не уменьшает лечение через Armor или MagicResistance.

INVARIANT-24
Vampiric Healing рассчитывается из EffectiveDamageToHP по умолчанию.

INVARIANT-25
Vampiric Healing не генерирует дополнительный Threat по умолчанию.

INVARIANT-26
ThreatRelevantDamage включает урон, применённый к HP, и урон, поглощённый щитом.

INVARIANT-27
ThreatRelevantHealing равен EffectiveHealing, если источник явно не определяет другое.

INVARIANT-28
DoT/HoT используют правила snapshot из Effects System.

INVARIANT-29
AFK Farming по умолчанию не использует полный Damage and Healing pipeline.

INVARIANT-30
Offline combat использует Damage and Healing System серверно.

INVARIANT-31
DoT tick применяет Target Armor и MagicResistance по текущим значениям цели в момент tick, если эффект явно не определяет snapshot target mitigation.

58. Default Balance Values

Следующие значения являются текущими authoritative defaults. Они хранятся в versioned Balance Profile и могут быть изменены только осознанным balance patch:

MinimumDamage = 1
BaseMissChance = 5%
LevelPenaltyPerLevel = 1 percentage point
MaxLevelPenalty = 10%
CriticalDamageMultiplier = 2.0
CriticalHealMultiplier = 1.5
ArmorMitigationConstant = 100
MagicMitigationConstant = 100
VampirismPercent default = 0%
MinMissChance = 0%
MaxMissChance = 30%
MinDodgeChance = 0%
MaxDodgeChance = 100%
MinCriticalChance = 0%
MaxCriticalChance = 100%
MinArmorPenetration = 0%
MaxArmorPenetration = 100%
MinMagicPenetration = 0%
MaxMagicPenetration = 100%

59. Out of Scope

Этот документ пока не определяет:

конкретные формулы способностей;
конкретные weapon damage values;
конкретные ability coefficients;
конкретные значения Armor и MagicResistance;
конкретные значения Accuracy и Dodge;
конкретные значения CriticalChance и CriticalDamage;
конкретные значения Healing coefficients;
конкретные значения VampirismPercent для способностей и эффектов;
lifesteal caps;
Elemental Damage schools;
Holy Damage;
Shadow Damage;
Physical Healing как отдельный тип лечения;
healing reduction by armor;
healing reduction by magic resistance;
damage reflection;
damage transfer;
resurrection;
revive mechanics;
environmental damage formulas;
PvP damage modifiers;
boss-specific damage rules;
damage logs retention policy;
anti-cheat thresholds;
UI;
floating combat text;
визуализацию критических ударов.

---

# Source of Truth Revision v2

- После Shield absorption и расчёта resulting HP, но до DEAD transition выполняется Lethal Damage Prevention check.
- Пример эффектов: `SetHPToPercent`, `CannotReduceHPBelow = 1`, `OncePerCombatSession`.
- True Damage игнорирует Armor/MagicResistance, но не игнорирует Shields или generic DamageTaken modifiers без explicit flag.
- Lethal prevention реализуется общим pipeline, а не class-specific if/else.


## Lethal Damage Prevention — authoritative order

```text
Resolve Hit
→ Resolve Critical
→ Base Damage
→ Damage Modifiers
→ Armor / MagicResistance (если применимо)
→ Shields
→ Effective damage candidate
→ ResultingHP <= 0?
   ├─ no  → Apply HP
   └─ yes → Lethal Damage Prevention Check
             ├─ matched → consume/apply prevention → HP remains > 0
             └─ none    → CurrentHP = 0 → DEAD
```
