# Elyndor — Content & Balance Profiles

**Status:** Game Content / Engineering Source of Truth  
**Purpose:** единый владелец data-driven gameplay definitions и числовых balance profiles.

---

# 1. Основной принцип

Game systems определяют **правила**, а content/balance files определяют **конкретные игровые данные и числа**.

Пример:

```text
Damage System owns:
как работает CriticalDamage

Balance Profile owns:
BaseCriticalDamage = 100% bonus

Class System owns:
как ClassDefinition выбирает growth profile

Class content owns:
WarriorGrowthProfileId
```

Не хранить игровые коэффициенты россыпью по C# `if`/constants.

---

# 2. Где живёт content

Repository:

```text
/content
├── balance/
├── classes/
├── abilities/
├── talents/
├── companions/
├── items/
├── item-sets/
├── affixes/
├── monsters/
├── ai/
├── locations/
├── quests/
├── loot/
├── bosses/
└── world-events/
```

Базовый формат: **JSON**.

Причины:
- легко читать C# и TypeScript tooling;
- хорошо diff'ается в Git;
- легко валидировать JSON Schema/application validator;
- не нужен отдельный CMS/DB editor на старте.

---

# 3. Stable IDs

Content использует стабильные string IDs:

```text
WARRIOR
ARCHER
MAGE
ARCANE_ARROW
HUNTER_MARK
BASTION
WOLF_PREDATOR_01
SET_RANGER
```

DisplayName/localization может меняться без изменения ID.

Удалённый ID нельзя незаметно переиспользовать для другой сущности.

---

# 4. ContentVersion

Каждый опубликованный набор content имеет:

```text
ContentVersion
BalanceVersion
PublishedAt
```

Server при startup загружает и валидирует один active content package.

---

# 5. Balance Profiles

Balance Profile — versioned data, а не отдельная gameplay system.

Примеры:

```text
CombatBalanceProfile
ResourceBalanceProfile
ClassBaseStatProfile
ClassLevelGrowthProfile
ItemStatBudgetProfile
LootBalanceProfile
BossParticipationProfile
AfkRewardProfile
CurrencyBalanceProfile
MerchantPriceProfile
AuctionFeeProfile
AuctionDurationProfile
DungeonDifficultyProfile
DungeonLockoutProfile
ProfessionXPProfile
CraftingCostProfile
```

---

# 6. Current Combat defaults

Authoritative значения берутся из соответствующих systems.

Сводно:

```text
BaseMissChance = 5%
LevelPenaltyPerLevel = 1 percentage point
MaxLevelPenalty = 10%
MinMissChance = 0%
MaxMissChance = 30%
BaseCriticalDamageBonus = 100%   # ordinary crit = 2.0x
CriticalHealMultiplier = 1.5
ArmorMitigationConstant = 100
MagicMitigationConstant = 100
GCD = 1.5 sec
AbilityQueueWindow = 0.5 sec
```

Если этот файл и system document расходятся, **system document имеет приоритет до следующей согласованной revision**.

---

# 7. Current Resource profiles

```text
MANA
Max = 100
Start = 100
Respawn = 100
CombatRegen = 4/sec
OutOfCombatRegen = 12/sec

RAGE
Max = 100
Start = 0
Respawn = 0
AutoAttackHit = +10
DirectDamageTaken = +5
OutOfCombatDecay = 5/sec after 5 sec

FOCUS
Max = 100
Start = 100
Respawn = 100
CombatRegen = 8/sec
OutOfCombatRegen = 12/sec

ENERGY
Max = 100
Start = 100
Respawn = 100
CombatRegen = 10/sec
OutOfCombatRegen = 10/sec
```

Energy profile нужен будущему Rogue, но уже валиден архитектурно.

---

# 8. Content snapshot во время боя

`CombatSession` при создании сохраняет:

```text
ContentVersion
BalanceVersion
```

Derived combat definitions, которые могут повлиять на уже начавшийся бой, не должны внезапно измениться посередине session.

Если сервер загружает новый balance package:
- новые CombatSession используют новую version;
- текущие session завершаются на snapshot/version, с которой начались;
- либо deployment выполняется через controlled restart, где active combat использует restart policy.

На первом сервере предпочтительнее **controlled restart**, а не сложный live hot-reload combat content.

---

# 9. Persistent content references

Persistent state хранит IDs/versions, а не копию всей definition без причины.

Пример:

```text
ItemInstance
ItemDefinitionId
RolledAffixes
InstanceVersion
```

Если historical calculation/audit требует snapshot, он хранится явно в audit/reward/combat metadata.

---

# 10. Validation before server start

Server/CI обязан отклонить content package при:

- duplicate ID;
- missing referenced ID;
- talent prerequisite cycle;
- prerequisite на более высокий Tier;
- неизвестном Stat/Resource/Effect/TargetType;
- `PHYSICAL_PET` talent, ошибочно targeting `SPIRIT_PET`;
- unsupported `Slow/Root/Fear/Charm`;
- активном `Spirit/Block/Parry/CastSpeed` stat;
- отрицательном RequiredLevel;
- invalid rarity;
- item affix вне разрешённого pool;
- loot entry на отсутствующий ItemDefinition;
- class ability на отсутствующий AbilityDefinition;
- SetDefinition с отсутствующей piece;
- duplicate unique group conflict, если rule запрещает configuration.

---

# 11. Talent validation

Для каждого дерева CI проверяет:

```text
TalentId unique
MaxRank >= 1
Tier valid
RequiredSpentPoints valid
Prerequisite exists
Prerequisite tier <= child tier
No prerequisite cycle
Branch capacity > 59
Capstone threshold = 40
```

Текущая цель:

```text
Warrior: 70 possible rank-points per branch
Archer:  69 possible rank-points per branch
Available at Level 60: 59
```

---

# 12. Formula versioning

Если меняется математический смысл поля, а не только число, увеличивается schema/content version.

Пример:

```text
CriticalDamage
old semantics: final multiplier
new semantics: bonus beyond normal hit
```

Нельзя тихо оставить старые persisted values с новым смыслом.

---

# 13. Item Stat Budget

`ITEM_STAT_BUDGET` является отдельным balance profile внутри content layer.

Он должен определять относительную цену:
- Primary Attribute;
- Stamina;
- AttackPower/SpellPower;
- Accuracy;
- CriticalChance;
- CriticalDamage;
- AttackSpeed;
- Armor/MagicResistance;
- ArmorPenetration/MagicPenetration;
- MaxResource / regeneration.

До утверждения конкретной budget formula `24_EQUIPMENT_SETS_LEVEL_5_30` является **ручным authored content** и не используется как доказательство универсальной стоимости одного stat относительно другого.

Перед массовой procedural generation gear этот profile обязателен.

---

# 14. Class numerical profiles

Class System уже определяет структуру:

```text
BaseStatProfileId
LevelGrowthProfileId
```

Текущие prototype-значения Warrior/Archer/Mage для validation slice Level 1–10
утверждены в `content/package.json` начиная с `BalanceVersion = 0.2.0`.
Они являются playtest-профилем и не считаются финальным балансом Level 1–60.
Изменение этих чисел выполняется content pass без перекомпиляции gameplay Core.

---

# 15. No database editing as primary workflow

На первом этапе не строим admin CMS для balance.

Workflow:

```text
edit JSON
→ validator/tests
→ Git diff/review
→ package version
→ deploy/test
→ telemetry/playtest
→ balance patch
```

Позже admin/editor может генерировать те же validated definitions.

---

# 16. Invariants

1. Game rules живут в system docs/Core; числа контента — в versioned content.
2. Content ID стабилен.
3. Server никогда не стартует с невалидным required content package.
4. Активный CombatSession не меняет balance semantics посередине боя.
5. Persistent player state не зависит от клиента.
6. Balance patch не должен требовать перекомпиляции Core, если меняются только data values.
7. Любой random roll использует server `IGameRandom` и сохраняет результат, если он влияет на persistent reward/state.

---

# 17. Economy / Dungeon / Crafting Profiles

Новые versioned balance profiles:

```text
CurrencyBalanceProfile
MerchantPriceProfile
EconomySourceProfile
EconomySinkProfile

AuctionFeeProfile
AuctionDurationProfile

DungeonDifficultyProfile
DungeonLockoutProfile
DungeonRewardProfile

ProfessionXPProfile
CraftingCostProfile
CraftResultProfile
```

Правило остаётся прежним:

```text
gameplay code owns rules
profile owns numbers/content values
```

Auction fee, sale tax, dungeon timers, profession XP и crafting fees не hardcode'ятся в handlers/services.
