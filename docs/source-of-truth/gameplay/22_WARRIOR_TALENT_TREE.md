# Elyndor — Warrior Talent Tree — Source of Truth

Текущий исполняемый контракт способностей и изменения от 2026-10-07: [Warrior mechanical contracts](../../development/warrior-mechanical-contracts.md). Базовые «Удар», «Клич ярости» и «Тяжёлый удар» доступны на уровнях 1, 3 и 6 независимо от талантов. При изучении «Сокрушения щитом» изученный «Раскол брони» становится его эффектом при попадании и не требует отдельной кнопки. Точные игровые описания находятся в текущих фрагментах контента Warrior.
**Система:** Talent System (16_TALENT_SYSTEM)  
**Класс:** Warrior (WARRIOR)  
**Level Cap:** 60  
**Talent Points:** 59 (Level 2–60, +1 за уровень)  
**Веток:** 3 — Страж / Берсерк / Командир  
**Tier unlock:** каждые 5 потраченных очков в ветке открывают следующий Tier  
**Tier'ов:** 9 (Tier 1–9, узлы на каждом уровне от 0 до 40 потраченных)

---

## Общая механика

```
MaxRank 1  — уникальный gameplay узел, одноранговый
MaxRank 2  — умеренный stat/ability узел
MaxRank 3  — базовый stat узел
MaxRank 5  — простой scaling узел

Prerequisite указывается явно.
RequiredSpentPoints — минимум очков в этой ветке для доступа к Tier.

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

## Правила баланса дерева v0.2

- Capstone открывается при **40 вложенных очках** в ветку и требует ещё **1 очко** на сам Capstone.
- Минимальная глубокая специализация с Capstone = **41 очко**.
- Оставшиеся **18 очков** на Level 60 могут использоваться для гибридного билда.
- Полностью изучить одну ветку на Level 60 **невозможно**: каждая ветка содержит больше потенциальных rank-points, чем доступные 59 очков.
- Tier открывает доступ к линии, но отдельный сильный талант может дополнительно требовать конкретный `Prerequisite`.
- Prerequisite-цепочки используются для развития конкретной механики: Provoke/Bastion, Wild Strike/Whirlwind/Berserk, Cry/Banner.
- Игрок должен выбирать не только ветку, но и **какие механики внутри ветки довести до максимума**.
- Ни одна ветка не должна быть обязательной для базовой работоспособности Warrior.
- Чистые stat-talents дают заметный, но не доминирующий рост; основная сила дерева должна приходить из gameplay-механик.
- Burst/defensive windows имеют cooldown или internal cooldown и не должны поддерживаться бесконечно через proc chains.

## Союзники и Party Targeting

Таланты ветки **Командир**, использующие «союзник», «все союзники» или `OnAlly...`, работают через Party.

**Party Ally** — персонаж, который одновременно:

- состоит с Warrior в одной Party;
- находится в том же CombatSession;
- является валидной союзной целью конкретного эффекта.

По умолчанию эффекты Командира, действующие на группу, включают самого Warrior.

Если Warrior не состоит в Party, такие эффекты применяются только к нему самому.

Игрок, который случайно присоединился к тому же encounter, но не состоит в Party, не получает групповые баффы Командира и не считается `Party Ally`.

Базовый targeting context:

`SELF_AND_PARTY_MEMBERS_IN_COMBAT`

Базовые party events:

`OnPartyMemberDamaged`
`OnPartyMemberCriticalHit`
`OnPartyMemberDeath`
`OnPartyMemberKill`

Party System является владельцем `PartyId` и membership. Talent / Ability / Effect System используют Party только как авторитетный targeting context.

---

> **Актуальный источник для чисел и рангов:** `content/talents/warrior-guardian.json`, `content/talents/warrior-berserker.json`, `content/talents/warrior-warlord.json`. Ветви Berserker/Warlord ниже содержат исторические проектные описания; при несовпадении приоритет имеет текущий исполняемый контент. Guardian ниже синхронизирован с девятью реальными тирами.

# ВЕТКА I — СТРАЖ (GUARDIAN)

**Исполняемая специализация:** девять тиров, 31 узел, 59 общих очков на 60 уровне. Перераспределено 2026-10-08 (content `0.46.0`, balance `0.36.0`). Текущие значения эффектов и текст описаний берутся непосредственно из `content/talents/warrior-guardian.json`.

**Роль:** танк со щитом, угрозой, блокированием и командной защитой. Ранний Guardian сохраняет Revenge, Shield Block, Provoke и Sunder Armor; основные силовые окна Bastion/Shield Slam расположены на Tier 5. Самая сильная пассивная механика открывается только после 40 очков в Guardian.

| Тир | Вложено очков в ветку | ID | Талант | Предыдущий талант |
|---:|---:|---|---|---|
| 1 | 0 | `G-1-1` | Стойка Стража | — |
| 1 | 0 | `G-1-2` | Специализация на щитах | — |
| 1 | 0 | `G-1-3` | Закалённая броня | — |
| 1 | 0 | `G-1-4` | Мастерство щита | — |
| 1 | 0 | `G-1-5` | Предвидение | — |
| 2 | 5 | `G-2-1` | Последний рубеж | — |
| 2 | 5 | `G-2-2` | Возмездие | — |
| 2 | 5 | `G-2-4` | Непокорность | `G-1-1` |
| 2 | 5 | `G-2-5` | Ярость щита | — |
| 3 | 10 | `G-2-3` | Улучшенное Возмездие | `G-2-2` |
| 3 | 10 | `G-3-1` | Блок щитом | — |
| 3 | 10 | `G-3-3` | Провокация | — |
| 3 | 10 | `G-3-5` | Раскол брони | — |
| 4 | 15 | `G-3-2` | Улучшенный блок щитом | `G-3-1` |
| 4 | 15 | `G-3-4` | Улучшенная провокация | `G-3-3` |
| 4 | 15 | `G-3-6` | Пробитая броня | `G-3-5` |
| 4 | 15 | `G-4-1` | Оглушающий удар | — |
| 5 | 20 | `G-4-2` | Улучшенный удар щитом | — |
| 5 | 20 | `G-4-3` | Одноручная специализация | — |
| 5 | 20 | `G-5-1` | Бастион | — |
| 5 | 20 | `G-6-1` | Сокрушение щитом | — |
| 6 | 25 | `G-4-4` | Тяжёлый щит | `G-3-1` |
| 6 | 25 | `G-4-5` | Мастер провокации | `G-3-3` |
| 6 | 25 | `G-5-3` | Вызывающий клич | — |
| 7 | 30 | `G-5-2` | Укреплённый Бастион | `G-5-1` |
| 7 | 30 | `G-5-4` | Улучшенный последний рубеж | `G-2-1` |
| 7 | 30 | `G-6-3` | Сосредоточенная ярость | — |
| 8 | 35 | `G-5-5` | Держать строй | — |
| 8 | 35 | `G-6-2` | Мастер сокрушения | `G-6-1` |
| 8 | 35 | `G-6-4` | Последняя крепость | — |
| 9 | 40 | `G-6-5` | НЕПОКОЛЕБИМЫЙ СТРАЖ | `G-6-1` |

**Принципы:** ID `G-*` и все значения модификаторов сохранены; цифровой префикс ID исторический и не обозначает текущий тир. `G-6-5` — завершающий талант Tier 9, требующий 40 уже вложенных очков и `G-6-1`; впервые может быть получен на уровне 42. Изменение порогов не очищает изученные ранги; новые вложения очков проходят обычную серверную проверку `TalentRules`.

**Ротация:** Strike/Heavy Blow доступны из базового класса; Revenge после блока; Sunder Armor после изучения Shield Slam становится дополнительным эффектом Shield Slam. Защитные умения и принуждение цели остаются ситуационными решениями.

---
# ВЕТКА II — БЕРСЕРК (BERSERKER)
**Fantasy:** машина разрушения. Чем меньше HP — тем опаснее. Rage тратится быстро, урон огромный.  
**Основные статы:** Strength, AttackPower, CriticalChance, AttackSpeed, ArmorPenetration  
**Ресурс:** Rage генерируется от нанесения урона и критов

---

### TIER 1 (0 spent required)

**[B-1-1] Боевое Безумие** *(Battle Frenzy)*  
`MaxRank 4`  
Увеличивает AttackPower на **2% / 4% / 6% / 9%**.  
*Stat Modifier Talent: AttackPower.*

---

**[B-1-2] Кровожадность** *(Bloodthirst)*  
`MaxRank 3`  
Убийство врага восстанавливает **8 / 12 / 16 Rage** мгновенно.  
*Event-Triggered: OnEnemyKilled → Rage generation.*  
*Proc Safety: CanTriggerFromProc = false (не триггерится от DOT kill).*

---

**[B-1-3] Острые Чувства** *(Keen Senses)*  
`MaxRank 4`  
Увеличивает Accuracy на **1.5% / 3% / 4.5% / 6%**.  
*Stat Modifier: Accuracy.*

---

**[B-1-4] Звериная Сила** *(Savage Strength)*  
`MaxRank 2`  
Strength увеличивается на **3% / 6%**.  
*Stat Modifier Talent: Strength.*

---

### TIER 2 (5 spent required)

**[B-2-1] Ярость Крови** *(Blood Rage)*  
`MaxRank 2`  
При HP ниже **50%**:  
— AttackPower +**7% / 12%**.  
— AttackSpeed +**4% / 8%**.  
*Conditional Stat Modifier: HP threshold-dependent.*

---

**[B-2-2] Дикий Удар** *(Wild Strike)*  
`MaxRank 1`  
**Активная способность.** Стоит **25 Rage**. GCD: STANDARD.  
Наносит физический урон **135% от AttackPower**.  
Cooldown: **6 секунд**.  
*Добавляет AbilityId WILD_STRIKE в KnownAbilities.*

---

**[B-2-3] Неукротимость** *(Unrelenting)*  
`MaxRank 4`  
CriticalChance +**1.5% / 3% / 4.5% / 6%**.  
*Stat Modifier: CriticalChance.*

---

**[B-2-4] Разгон** *(Momentum)*  
`MaxRank 3`  
После расходования **20 или более Rage одной способностью** AttackSpeed увеличивается на **2% / 4% / 6%** на **3 секунды**.  
Повторное срабатывание обновляет Duration, но не создаёт новый stack.  
*Event-Triggered: OnResourceSpent(Rage >= 20) → temporary AttackSpeed Effect.*

---

### TIER 3 (10 spent required)

**[B-3-1] Критический Инстинкт** *(Critical Instinct)*  
`MaxRank 2`  
Критический удар генерирует дополнительно **+4 / +8 Rage**.  
*Event-Triggered: OnCriticalHit → Rage generation.*  
*Proc Safety: InternalCooldown = 1 sec.*

---

**[B-3-2] Вихрь** *(Whirlwind)*  
`MaxRank 1`  
**Активная способность.** Стоит **35 Rage**. GCD: STANDARD.  
Атакует всех врагов в encounter. Урон = **70% от AttackPower** по каждому.  
Cooldown: **10 секунд**.  
*AoE Targeting. Добавляет AbilityId WHIRLWIND.*  
*Prerequisite: [B-2-2] Дикий Удар.*

---

**[B-3-3] Пробивная Ярость** *(Rending Fury)*  
`MaxRank 4`  
Увеличивает ArmorPenetration на **2% / 4% / 6% / 9%**.  
*Stat Modifier: ArmorPenetration.*

---

**[B-3-4] Кровавый След** *(Blood Trail)*  
`MaxRank 2`  
Критический *Дикий Удар* накладывает Кровотечение на **4 секунды**:  
— Rank 1: **4% AttackPower per second = 16% total**;  
— Rank 2: **7% AttackPower per second = 28% total**.  
`TickInterval = 1 sec`.  
*Prerequisite: [B-2-2] Дикий Удар.*  
*Effect Modifier / Event-Triggered: OnWildStrikeCritical → Bleed; snapshot AttackPower.*

---

### TIER 4 (15 spent required)

**[B-4-1] Двойной Удар** *(Double Strike)*  
`MaxRank 1`  
Пассив. **15% шанс** при Auto Attack нанести второй удар немедленно за **45% урона** от первого.  
Второй удар не может критовать.  
Изучение таланта также открывает equipment permission **DUAL_WIELD_ONE_HAND_WEAPON**: Warrior может экипировать второе одноручное оружие в **OFF_HAND**. Щит остаётся доступен по обычным class rules; двуручное оружие по-прежнему занимает обе руки.  
*Event-Triggered: OnAutoAttack → conditional second hit.*  
*Equipment Permission: DUAL_WIELD_ONE_HAND_WEAPON.*  
*Proc Safety: InternalCooldown = 2 sec, CanTriggerFromProc = false.*

---

**[B-4-2] Мастер Вихря** *(Whirlwind Mastery)*  
`MaxRank 2`  
Вихрь наносит **+10% / +20%** урона.  
Cooldown Вихря снижается на **1 / 2 секунды**.  
*Prerequisite: [B-3-2] Вихрь.*  
*Ability Modifier: DamageBonus + CooldownReduction на WHIRLWIND.*

---

**[B-4-3] Пронизывающий Удар** *(Piercing Blow)*  
`MaxRank 2`  
Дикий Удар игнорирует **8% / 15%** Armor цели (ArmorPenetration для этой способности).  
*Prerequisite: [B-2-2] Дикий Удар.*  
*Ability Modifier: conditional ArmorPenetration на WILD_STRIKE.*

---

**[B-4-4] Безрассудство** *(Recklessness)*  
`MaxRank 2`  
При HP ниже **50%** наносимый физический урон увеличивается на **2% / 4%**, но входящий урон также увеличивается на **1% / 2%**.  
*Conditional DamageDealt + DamageTaken Modifier.*

---

### TIER 5 (20 spent required)

**[B-5-1] Берсерк** *(Berserk)*  
`MaxRank 1`  
**Активная способность.** Стоит **50 Rage**. Off-GCD.  
На **8 секунд**: AttackSpeed +**25%**, AttackPower +**15%**, CriticalChance +**8%**.  
В течение действия: нельзя использовать защитные способности.  
Cooldown: **120 секунд**.  
*Добавляет AbilityId BERSERK. Ability с флагом DisablesDefensiveAbilities.*

---

**[B-5-2] Адреналин** *(Adrenaline)*  
`MaxRank 4`  
AttackSpeed +**1.5% / 3% / 4.5% / 6%** постоянно.  
*Stat Modifier: AttackSpeed.*

---

**[B-5-3] Смертельный Критик** *(Lethal Crits)*  
`MaxRank 2`  
CriticalDamage +**8% / 15%** (поверх базового критического множителя).  
*Stat Modifier: CriticalDamage.*

---

**[B-5-4] Неистовство** *(Frenzy)*  
`MaxRank 2`  
Пока активен *Берсерк*, стоимость Rage у атакующих способностей уменьшается на **10% / 20%**.  
Стоимость способности не может стать отрицательной.  
*Prerequisite: [B-5-1] Берсерк.*  
*Conditional Ability ResourceCost Modifier.*

---

### TIER 6 (25 spent required)

**[B-6-1] Жажда Крови** *(Blood Hunger)*  
`MaxRank 2`  
Вампиризм: **2% / 4%** от EffectivePhysicalDamageToHP восстанавливает HP.  
*Vampiric Healing Talent. Использует Damage and Healing System Vampirism rules.*

---

**[B-6-2] Разрушительный Критик** *(Devastating Blow)*  
`MaxRank 1`  
Пассив. Критический Auto Attack накладывает дебафф **Уязвимость** на цель на **8 секунд**:  
— цель получает **+5% физического урона от этого Warrior**.  
*Event-Triggered: OnCriticalAutoAttack → Apply personal Vulnerability Effect keyed by SourceId.*  
*Proc Safety: InternalCooldown = 0, но эффект refreshes, не стакается.*

---

**[B-6-3] Боевой Транс** *(Battle Trance)*  
`MaxRank 2`  
Во время активного *Берсерка* получение урона генерирует **+3 / +5 Rage** дополнительно.  
*Prerequisite: [B-5-1] Берсерк.*  
*Conditional Resource Modifier: active during BERSERK ability.*

---

**[B-6-4] Кровавая Инерция** *(Blood Momentum)*  
`MaxRank 3`  
Пока *Берсерк* не активен, критический удар уменьшает оставшийся Cooldown *Берсерка* на **1 / 2 / 3 секунды**.  
`InternalCooldown = 3 sec`.  
*Prerequisite: [B-5-1] Берсерк.*  
*Event-Triggered Cooldown Reduction.*

---

### TIER 7 (30 spent required)

**[B-7-1] Неостановимая Сила** *(Unstoppable Force)*  
`MaxRank 1`  
При активном Берсерке: Auto Attack имеет **100% шанс** нанести второй удар за **30% урона**.  
*Prerequisite: [B-5-1] Берсерк, [B-4-1] Двойной Удар.*  
*Ability+Event Modifier: overrides Double Strike proc rate during BERSERK.*

---

**[B-7-2] Сила Смерти** *(Death's Strength)*  
`MaxRank 4`  
При HP ниже **25%**: CriticalChance +**3% / 6% / 9% / 12%** дополнительно.  
*Conditional Stat Modifier: HP threshold-dependent CriticalChance.*

---

**[B-7-3] Пронизывающая Ярость** *(Rending Rampage)*  
`MaxRank 2`  
Вихрь накладывает на каждую поражённую цель **Кровотечение** на **6 секунд**:  
— физический DoT на **6 секунд**, `TickInterval = 1 секунда`, всего **6 ticks**.  
— Rank 1: **5% AttackPower per tick = 30% AttackPower total**.  
— Rank 2: **8% AttackPower per tick = 48% AttackPower total**.  
*Prerequisite: [B-3-2] Вихрь.*  
*Effect Modifier: добавляет DoT к WHIRLWIND; snapshot AttackPower фиксируется в момент применения.*

---

**[B-7-4] Палач** *(Executioner)*  
`MaxRank 2`  
Физический урон по целям с HP ниже **20%** увеличивается на **5% / 10%**.  
Не увеличивает True Damage.  
*Conditional Physical Damage Modifier: TargetHP < 20%.*

---

### TIER 8 (35 spent required)

**[B-8-1] Смертельный Вихрь** *(Death Whirlwind)*  
`MaxRank 1`  
После расчёта физического компонента Вихря способность дополнительно наносит **True Damage = 15% от PhysicalDamageBeforeShield** по той же цели.  
`PhysicalDamageBeforeShield` — физический урон Вихря после критического множителя, Armor mitigation и damage modifiers, но до Shield absorption.  
*Prerequisite: [B-4-2] Мастер Вихря, [B-7-3] Пронизывающая Ярость.*  
*Ability Modifier: добавляет True Damage component к WHIRLWIND.*

---

**[B-8-2] Агония Берсерка** *(Berserker's Agony)*  
`MaxRank 2`  
Cooldown *Берсерка* снижается на **15 / 30 секунд**.  
Во время Берсерка: каждое убийство сбрасывает Cooldown *Дикого Удара*.  
*Prerequisite: [B-5-1] Берсерк.*  
*Ability Modifier: CooldownReduction BERSERK + OnKill Cooldown Reset WILD_STRIKE.*

---

**[B-8-3] Последний Вздох** *(Death's Embrace)*  
`MaxRank 1`  
Пассив. При HP ниже **10%**: следующая Auto Attack наносит **200% обычного урона** и гарантированно критует.  
Срабатывает **один раз за Combat Session**.  
*Event-Triggered: OnHPBelowThreshold(10%) → Next Attack Modifier (guaranteed crit + 200% ordinary damage).*  
*Proc Safety: MaxTriggersPerEventChain = 1, one per CombatSession.*

---

### TIER 9 (40 spent required) — CAPSTONE

**[B-9-1] ВОПЛОЩЕНИЕ ЯРОСТИ** *(AVATAR OF RAGE)* ⭐  
`MaxRank 1` — **Capstone**  
**Требует 40 очков в Берсерке.**

Пассив. Ты становишься воплощением разрушения.

**Эффекты:**  
— AttackPower +**10%** постоянно.  
— Берсерк теперь длится **+4 секунды** дольше и снимает все активные **Stun и Silence** с тебя при активации.  
— При нанесении критического удара: **20% шанс** мгновенно сгенерировать **+10 Rage**.  
— Убийство врага во время Берсерка: снижает оставшийся Cooldown Берсерка **на 15 секунд**.

*Composite Talent: Stat Modifier (AP) + Ability Modifier (BERSERK duration + dispel on activation) + Event-Triggered Rage proc + Ability Modifier (conditional CD reset).*  
*Proc Safety: Rage proc — InternalCooldown = 1 sec. CD reset — MaxTriggersPerEventChain = 1 per BERSERK activation.*  
*Дополнительных node prerequisites нет: Capstone требует только 40 вложенных очков в этой ветке.*

---
---

# ВЕТКА III — КОМАНДИР (WARLORD)
**Fantasy:** лидер поля боя. Немного урона, немного защиты. Крики, флаги, команды, баффы союзников. Топ для рейдов и данжей 5+.  
**Основные статы:** Strength, Stamina, Threat generation, party buffs  
**Ресурс:** Rage тратится на крики, генерируется умеренно

---

### TIER 1 (0 spent required)

**[W-1-1] Командный Голос** *(Voice of Command)*  
`MaxRank 3`  
Боевые крики теперь стоят на **5 / 10 / 15 Rage** меньше.  
*Ability Modifier: ResourceCostReduction на все способности с тегом CRY.*

---

**[W-1-2] Вдохновляющее Присутствие** *(Inspiring Presence)*  
`MaxRank 3`  
Ты и Party Allies в том же CombatSession получают **+1% / +2% / +3% к AttackPower**.  
*Passive Party Effect: применяется к SELF_AND_PARTY_MEMBERS_IN_COMBAT.*

---

**[W-1-3] Тактическая Подготовка** *(Tactical Awareness)*  
`MaxRank 2`  
Увеличивает Accuracy на **2% / 4%**.  
*Stat Modifier: Accuracy.*

---

**[W-1-4] Военная Выправка** *(Battle Formation)*  
`MaxRank 3`  
Ты и Party Allies в том же CombatSession получают **+1% / +2% / +3% MagicResistance**.  
*Passive Party Effect: MagicResistance modifier.*

---

### TIER 2 (5 spent required)

**[W-2-1] Боевой Клич** *(Battle Cry)*  
`MaxRank 1`  
**Активная способность.** Off-GCD. Стоит **20 Rage**.  
Ты и Party Allies в том же CombatSession получают **+8% AttackPower** на **20 секунд**.  
Cooldown: **60 секунд**.  
*Добавляет AbilityId BATTLE_CRY. Party buff через Effect System.*

---

**[W-2-2] Щит Товарища** *(Comrade's Shield)*  
`MaxRank 3`  
Когда Party Ally получает урон снижающий его HP ниже **20%**:  
**10% / 15% / 20% шанс** создать на нём щит поглощающий **4% его MaxHP** на **5 секунд**.  
*Event-Triggered: OnAllyHPBelowThreshold(20%) → Apply Shield on ally.*  
*Proc Safety: InternalCooldown = 15 sec per ally target.*

---

**[W-2-3] Сила Строя** *(Formation Strength)*  
`MaxRank 3`  
Stamina +**2% / 4% / 6%**.  
*Stat Modifier: Stamina.*

---

**[W-2-4] Единый Ритм** *(Unified Rhythm)*  
`MaxRank 2`  
*Боевой Клич* дополнительно увеличивает Accuracy получателей на **+2% / +4%** на время действия.  
*Prerequisite: [W-2-1] Боевой Клич.*  
*Ability Modifier: BATTLE_CRY adds Accuracy Effect.*

---

### TIER 3 (10 spent required)

**[W-3-1] Клич Стойкости** *(Endurance Cry)*  
`MaxRank 1`  
**Активная способность.** Off-GCD. Стоит **25 Rage**.  
Ты и Party Allies получают **+6% к MaxHP** (как временный бафф) на **15 секунд**.  
Cooldown: **90 секунд**.  
*Добавляет AbilityId ENDURANCE_CRY.*

---

**[W-3-2] Тактический Удар** *(Tactical Strike)*  
`MaxRank 2`  
Auto Attack имеет **15% / 25% шанс** снизить Cooldown одного случайного крика на **2 / 4 секунды**.  
*Event-Triggered: OnAutoAttack → random CRY Cooldown reduction. InternalCooldown = 1 sec.*

---

**[W-3-3] Стяг Войны** *(War Banner)*  
`MaxRank 1`  
**Активная способность.** Off-GCD. Стоит **30 Rage**.  
Устанавливает *Стяг* на **30 секунд**. Пока Стяг активен:  
— ты и Party Allies в том же CombatSession получают **+4% CriticalChance**.  
— при убийстве врага: все союзники восстанавливают **2% MaxResource** своего Action Resource.  
Cooldown: **120 секунд**.  
*Добавляет AbilityId WAR_BANNER. Persistent Effect с условным ресурс-восстановлением.*

---

**[W-3-4] Знамя Единства** *(Banner of Unity)*  
`MaxRank 3`  
Пока активен любой твой эффект с тегом `BANNER` или `FLAG`, ты и Party Allies получают снижение входящего урона на **1% / 2% / 3%**.  
Несколько твоих Banner/Flag не складывают этот бонус между собой.  
*Conditional Party DamageTaken Modifier.*

---

### TIER 4 (15 spent required)

**[W-4-1] Усиленный Клич** *(Amplified Cry)*  
`MaxRank 3`  
Боевой Клич теперь также увеличивает AttackSpeed получателей на **3% / 5% / 8%**.  
*Prerequisite: [W-2-1] Боевой Клич.*  
*Ability Modifier: добавляет AttackSpeed buff к BATTLE_CRY эффекту.*

---

**[W-4-2] Клич Мести** *(Cry of Vengeance)*  
`MaxRank 1`  
**Активная способность.** Off-GCD. Стоит **35 Rage**.  
На **10 секунд** активирует эффект *Клич Мести*.  
Пока эффект активен, когда Party Ally получает direct damage, Warrior получает **1 stack** (макс **5**).  
`InternalCooldown = 0.5 sec` между получением stacks.  
Следующая Auto Attack Warrior наносит **+35% урона за stack** и потребляет все stacks.  
Cooldown: **30 секунд**.  
*Добавляет AbilityId CRY_OF_VENGEANCE. Timed stack-based Next Attack Modifier.*

---

**[W-4-3] Железная Дисциплина** *(Iron Discipline)*  
`MaxRank 3`  
Ты и Party Allies получают **+1% / +2% / +3% к Dodge**.  
*Passive Party Effect: Dodge modifier на SELF_AND_PARTY_MEMBERS_IN_COMBAT.*

---

**[W-4-4] Эхо Команды** *(Echoing Command)*  
`MaxRank 3`  
Duration положительных эффектов твоих способностей с тегом `CRY` увеличивается на **+1 / +2 / +3 секунды**.  
Не влияет на cooldown.  
*Ability/Effect Duration Modifier.*

---

### TIER 5 (20 spent required)

**[W-5-1] Флаг Победы** *(Victory Flag)*  
`MaxRank 1`  
**Активная способность.** Off-GCD. Стоит **40 Rage**.  
Устанавливает *Флаг Победы* на **18 секунд**. Пока активен:  
— ты и Party Allies получают **+8% ко всему наносимому урону**.  
— первые **4 секунды** после установки вы иммунны к новым **Stun и Silence**. Уже активные control effects не снимаются.  
Cooldown: **180 секунд**.  
*Добавляет AbilityId VICTORY_FLAG. Party offensive window + короткая control protection, а не длительная полная иммунность.*

---

**[W-5-2] Неустрашимость** *(Fearlessness)*  
`MaxRank 3`  
Уменьшает Cooldown *Стяга Войны* и *Флага Победы* на **7 / 14 / 20 секунд**.  
*Prerequisite: [W-3-3] Стяг Войны.*  
*Ability Modifier: CooldownReduction на WAR_BANNER + VICTORY_FLAG.*

---

**[W-5-3] Воля к Победе** *(Will to Win)*  
`MaxRank 3`  
Увеличивает Rage generation от Auto Attack на **8% / 16% / 24%**.  
*Resource Modifier: Rage generation from auto attack.*

---

**[W-5-4] Приказ Наступать** *(Order to Advance)*  
`MaxRank 2`  
Пока активен *Боевой Клич*, ты и Party Allies дополнительно получают **+2% / +4% CriticalChance**.  
*Prerequisite: [W-2-1] Боевой Клич.*  
*Conditional Party Stat Modifier tied to BATTLE_CRY.*

---

### TIER 6 (25 spent required)

**[W-6-1] Клич Исцеления** *(Rally Cry)*  
`MaxRank 1`  
**Активная способность.** Off-GCD. Стоит **30 Rage**.  
Ты и Party Allies восстанавливают **6% MaxHP** мгновенно.  
Cooldown: **120 секунд**.  
*Добавляет AbilityId RALLY_CRY. Party heal через Healing System.*  
*HealingType = Scripted, GeneratesThreat = true.*

---

**[W-6-2] Знамя Стойкости** *(Banner of Endurance)*  
`MaxRank 3`  
Клич Стойкости теперь также даёт **+3% / +5% / +8% к Armor и MagicResistance** получателям.  
*Prerequisite: [W-3-1] Клич Стойкости.*  
*Ability Modifier: добавляет Armor+MR buff к ENDURANCE_CRY.*

---

**[W-6-3] Полководец** *(War Leader)*  
`MaxRank 1`  
Пассив. При каждом использовании крика (любой AbilityId с тегом CRY):  
— генерируется **+10 Rage** мгновенно.  
— с тебя и **одного случайного Party Ally** снимается один активный дебафф категории **Poison**, если он есть.  
*Event-Triggered: OnAbilityUsed(tag=CRY) → Rage generation + limited Party cleanse.*

---

**[W-6-4] Слаженное Снабжение** *(Coordinated Supply)*  
`MaxRank 3`  
Восстановление Action Resource, создаваемое **твоими талантами и способностями Командира**, увеличивается на **10% / 20% / 30%**.  
Не усиливает обычную пассивную регенерацию ресурсов.  
*Resource Restore Modifier; SourceId = self, tags CRY/BANNER/FLAG.*

---

### TIER 7 (30 spent required)

**[W-7-1] Флаг Битвы** *(Battle Standard)*  
`MaxRank 1`  
**Активная способность.** Off-GCD. Стоит **50 Rage**.  
Устанавливает *Флаг Битвы* на **15 секунд**. Пока активен:  
— Auto Attack тебя и Party Allies имеют **+10% шанс** нанести дополнительный физический удар за **25% урона**.  
Cooldown: **150 секунд**.  
*Добавляет AbilityId BATTLE_STANDARD. Party-wide proc Effect; proc-created hits cannot trigger this Effect again.*

---

**[W-7-2] Несломленный Строй** *(Unbroken Formation)*  
`MaxRank 2`  
Когда Party Ally погибает: ты и оставшиеся Party Allies получают **+6% / +10% к AttackPower** и **+4% / +7% к Dodge** на **10 секунд**.  
*Event-Triggered: OnPartyMemberDeath → Apply party buff.*  
*Proc Safety: один раз за Combat Session на каждую смерть союзника.*

---

**[W-7-3] Командный Ритм** *(Command Rhythm)*  
`MaxRank 2`  
Cooldown всех криков снижается на **3 / 6 секунд**.  
*Ability Modifier: глобальный CooldownReduction для всех AbilityId с тегом CRY.*

---

**[W-7-4] Стоять До Конца** *(Hold the Line)*  
`MaxRank 3`  
Party Ally с HP ниже **25%** получает на **2% / 4% / 6%** меньше входящего урона.  
На самого Warrior эффект также действует, если он соответствует условию.  
*Conditional Party DamageTaken Modifier: HP < 25%.*

---

### TIER 8 (35 spent required)

**[W-8-1] Легендарный Клич** *(Legendary Cry)*  
`MaxRank 1`  
Боевой Клич теперь также:  
— восстанавливает **8% MaxRage** тебе и Party Allies-воинам.  
— восстанавливает **6% MaxMana** тебе и Party Allies-магам.  
— восстанавливает **6% MaxEnergy** тебе и Party Allies-разбойникам.  
*Prerequisite: [W-2-1] Боевой Клич, [W-4-1] Усиленный Клич.*  
*Ability Modifier: добавляет class-aware Resource restore к BATTLE_CRY.*

---

**[W-8-2] Непобедимый Авангард** *(Vanguard Unbroken)*  
`MaxRank 1`  
Пока активен *Флаг Победы*:  
— ты и Party Allies не могут быть убиты (HP фиксируется на **1**) в течение первых **2 секунд** действия флага.  
*Prerequisite: [W-5-1] Флаг Победы.*  
*Ability Modifier: применяет Lethal Damage Prevention Effect `CannotReduceHPBelow = 1` на первые 2 sec VICTORY_FLAG.*

---

**[W-8-3] Ярость Командира** *(Warlord's Rage)*  
`MaxRank 3`  
Каждый раз когда Party Ally наносит критический удар:  
Командир получает **+1 / +2 / +3 Rage**.  
*Event-Triggered: OnPartyMemberCriticalHit → Rage generation.*  
*Proc Safety: InternalCooldown = 0.75 sec global for this talent.*

---

**[W-8-4] Главнокомандующий** *(Supreme Commander)*  
`MaxRank 2`  
При активации способности с тегом `FLAG` или `BANNER` оставшийся Cooldown всех твоих `CRY` уменьшается на **3 / 5 секунд**.  
`InternalCooldown = 20 sec`.  
*Event-Triggered Cooldown Reduction.*  
*Prerequisite: [W-3-3] Стяг Войны.*

---

### TIER 9 (40 spent required) — CAPSTONE

**[W-9-1] ПОЛКОВОДЕЦ ЭЛИНДОРА** *(WARLORD OF ELYNDOR)* ⭐  
`MaxRank 1` — **Capstone**  
**Требует 40 очков в Командире.**

Пассив. Ты становишься воплощением воли к победе.

**Эффекты:**  
— Все крики теперь действуют **+5 секунд** дольше.  
— При активации любого флага или крика: ты и Party Allies получают щит поглощающий **4% их MaxHP** на **6 секунд**.  
— Клич Исцеления теперь также восстанавливает **20% MaxRage** тебе.  
— Убийство врага тобой или Party Ally снижает оставшийся Cooldown всех твоих криков на **1 секунду**. `InternalCooldown = 1 sec`.  

*Composite Talent:*  
*— Effect Modifier (CRY/FLAG duration extension)*  
*— Event-Triggered Shield on party (OnAbilityUsed(tag=CRY|FLAG))*  
*— Ability Modifier (RALLY_CRY + Rage restore)*  
*— Event-Triggered CD reduction (OnAllyKill → CRY cooldown -1 sec)*  

*Proc Safety: Shield — InternalCooldown = 2 sec. CD reduction — InternalCooldown = 1 sec.*  
*Дополнительных node prerequisites нет: Capstone требует только 40 вложенных очков в этой ветке.*

---

# СТРУКТУРА ОЧКОВ И ВЫБОРА

Потенциальное количество rank-points в ветках:

```text
Страж:     70 возможных очков
Берсерк:   70 возможных очков
Командир:  70 возможных очков

Доступно персонажу на Level 60:
59 Talent Points
```

Следствие:

- полностью закрыть любую ветку невозможно;
- Capstone требует минимум 41 очко в основной ветке;
- после Capstone остаётся максимум 18 очков на вторую/третью ветку;
- даже глубокий билд должен отказаться от части узлов основной ветки;
- `Prerequisite` заставляет инвестировать именно в выбранную механику, а не просто набрать любое количество очков в нижних Tier.

---

# КЛЮЧЕВЫЕ PREREQUISITE-ЦЕПОЧКИ

## Правило Capstone

Все три Capstone требуют **40 spent points в своей ветке**, но не заставляют брать один конкретный Tier 8 node. Это сохраняет разные глубокие билды внутри одной специализации.


## Страж

### Provoke / Threat

```text
G-2-2 Провокатор
    ↓
G-4-2 Мастер Провокации
```

### Bastion

```text
G-5-1 Бастион
    ↓
G-7-1 Бессмертный Воин
    ↓
G-8-2 Нерушимый Оплот
```

### Shield

```text
G-6-1 Живой Щит
    ↓
G-6-4 Усиленные Барьеры
```

## Берсерк

### Wild Strike

```text
B-2-2 Дикий Удар
    ├── B-3-4 Кровавый След
    └── B-4-3 Пронизывающий Удар
```

### Whirlwind

```text
B-2-2 Дикий Удар
    ↓
B-3-2 Вихрь
    ↓
B-4-2 Мастер Вихря
    ↓
B-7-3 Пронизывающая Ярость
    ↓
B-8-1 Смертельный Вихрь
```

### Berserk

```text
B-5-1 Берсерк
    ├── B-5-4 Неистовство
    ├── B-6-3 Боевой Транс
    ├── B-6-4 Кровавая Инерция
    └── B-7-1 Неостановимая Сила
            ↓
       B-8-2 Агония Берсерка
```

## Командир

### Battle Cry

```text
W-2-1 Боевой Клич
    ├── W-2-4 Единый Ритм
    ├── W-4-1 Усиленный Клич
    │       ↓
    │   W-8-1 Легендарный Клич
    └── W-5-4 Приказ Наступать
```

### Endurance Cry

```text
W-3-1 Клич Стойкости
    ↓
W-6-2 Знамя Стойкости
```

### Banner / Flag

```text
W-3-3 Стяг Войны
    ├── W-5-2 Неустрашимость
    ├── W-8-4 Главнокомандующий
    └── W-5-1 Флаг Победы
            ↓
       W-8-2 Непобедимый Авангард
```

---

# ПРИМЕРЫ LEVEL 60 БИЛДОВ

## 1. Main Tank — 45 Страж / 14 Командир

Берёт:

- Guardian Capstone;
- Bastion-line;
- Provoke-line;
- основную Armor/Stamina mitigation;
- ранние Party defensive talents Командира.

Назначение:
**лучший стабильный tank для boss/dungeon.**

## 2. Offensive Tank — 41 Страж / 18 Берсерк

Берёт Guardian Capstone, но часть дорогих defensive side-nodes пропускает.

18 очков Берсерка используются для:

- AttackPower;
- Accuracy;
- Crit;
- раннего Wild Strike / Rage utility.

Назначение:
**танк с большим личным damage, но слабее pure defensive build.**

## 3. Berserker DPS — 45 Берсерк / 14 Страж

Берёт:

- Berserk-line;
- Wild Strike;
- большую часть Whirlwind-line;
- low-HP modifiers;
- Berserker Capstone.

Guardian даёт раннюю выживаемость.

Назначение:
**основной physical DPS Warrior.**

## 4. AoE Berserker — 43 Берсерк / 16 Командир

Приоритет:

- Whirlwind;
- Bleed;
- Death Whirlwind;
- Rage generation;
- часть ранних Commander resource/buff talents.

Назначение:
**многоцелевой damage и dungeon clear.**

## 5. Party Commander — 45 Командир / 14 Страж

Берёт:

- Battle Cry line;
- Endurance Cry;
- Banner/Flag line;
- Rally Cry;
- Commander Capstone.

Страж усиливает личную survivability.

Назначение:
**главный support Warrior для Party.**

## 6. Dungeon Hybrid — 31 Страж / 28 Командир

Без Capstone.

Получает:

- Bastion;
- Provoke improvements;
- Battle Cry;
- Endurance Cry;
- War Banner;
- Victory Flag;
- часть Party mitigation.

Назначение:
**универсальный Warrior для небольших групп.**

---

# ФИНАЛЬНЫЙ БАЛАНСНЫЙ ПРОФИЛЬ

## Страж

Должен выигрывать у других Warrior builds по:

- Effective HP;
- стабильности mitigation;
- Threat;
- контролю цели через Provoke;
- survivability во время длинного boss fight.

Не должен выигрывать по:

- burst damage;
- sustained personal DPS;
- силе Party buffs.

Главный power window:

`BASTION`

Passive mitigation намеренно распределена между большим количеством узлов, чтобы нельзя было получить весь defensive package за 41 очко.

## Берсерк

Должен выигрывать по:

- personal physical DPS;
- burst;
- execute pressure;
- AoE при инвестиции в Whirlwind-line.

Цена:

- низкий HP является частью fantasy;
- `Berserk` запрещает defensive abilities;
- часть талантов увеличивает входящий урон;
- Vampirism ограничен EffectivePhysicalDamageToHP;
- True Damage не должен становиться основной частью damage profile.

Три возможных поднаправления внутри ветки:

1. `Wild Strike / single target`;
2. `Whirlwind / AoE + Bleed`;
3. `Berserk / low-HP burst`.

59 очков недостаточно, чтобы максимально закрыть все три.

## Командир

Должен выигрывать по:

- Party utility;
- Party survival;
- временным offensive windows;
- ресурсной поддержке;
- адаптивности в 3–5 player PvE.

Не должен:

- давать постоянный uptime сильнейших group buffs;
- превосходить Berserker по personal DPS;
- превосходить Guardian по tank survivability.

Все Party effects:

`SELF_AND_PARTY_MEMBERS_IN_COMBAT`

Случайные участники encounter вне Party не получают эффекты.

Три поднаправления:

1. `CRY`;
2. `BANNER / FLAG`;
3. `Party Protection / Resource Support`.

---

# PROC / COOLDOWN SAFETY

Дополнительный attack/damage proc по умолчанию:

```text
CanTriggerFromProc = false
```

если талант явно не говорит обратное.

Cooldown reduction:

- не может уменьшить remaining cooldown ниже 0;
- выполняется сервером;
- использует InternalCooldown, если trigger может происходить массово;
- один proc-event не может рекурсивно вызвать самого себя.

Party procs используют авторитетный Party membership snapshot в момент события.

---

# CORE REQUIREMENT — LETHAL DAMAGE PREVENTION

Таланты:

- `G-7-2 Щит Вечности`;
- `W-8-2 Непобедимый Авангард`;

требуют универсальную механику:

```text
Incoming Damage
    ↓
Calculated HP result <= 0?
    ↓
Check active Lethal Damage Prevention
    ├── yes → apply prevention rule
    │          ↓
    │       HP remains > 0 / fixed at 1
    └── no  → CurrentHP = 0
               ↓
             DEAD
```

Lethal Damage Prevention является частью общего Damage/Effect pipeline и не реализуется отдельным `if Warrior`.

---

# FINAL DESIGN STATUS

Warrior Talent Tree рассчитан на:

```text
Level Cap: 60
Talent Points: 59
Branches: 3
Tier count: 9
Capstone threshold: 40 spent + 1 capstone point
Party targeting: SELF + Party members in same CombatSession
```

Роли:

```text
Страж    → Tank
Берсерк  → Physical DPS
Командир → Party Support / Hybrid
```

Дерево намеренно содержит больше доступных rank-points, чем игрок может потратить.

**Главное правило финального дизайна: игрок выбирает не только специализацию, но и конкретный путь внутри специализации.**
