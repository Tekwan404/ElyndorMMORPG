Elyndor — Item, Equipment and Inventory System Specification

Document: 13_ITEM_EQUIPMENT_SYSTEM.md
System: Items / Equipment / Inventory
Status: Foundation / Source of Truth
Version: 0.1

1. Назначение

Item System определяет игровые предметы, инвентарь и экипировку персонажа.

Система отвечает за:

Item Definition;
Item Instance;
типы предметов;
стэки;
инвентарь;
слоты экипировки;
equip / unequip;
requirements;
weapon properties;
item stat modifiers;
безопасное получение и удаление предметов;
persistence.

Система не определяет:

loot chance;
кто получает предмет после убийства;
квестовую логику;
экономику;
торговлю;
крафт;
Damage formulas;
Talent rules;
Monster AI.

2. Основной принцип

Предмет существует в двух уровнях:

ItemDefinition — шаблон контента.
ItemInstance — конкретный экземпляр, принадлежащий персонажу или находящийся в reward state.

3. Item Definition

ItemDefinition
  ├── ItemDefinitionId
  ├── Name
  ├── ItemType
  ├── Rarity
  ├── MaxStack
  ├── EquipmentSlot, optional
  ├── WeaponTag, optional
  ├── ArmorTag, optional
  ├── RequiredLevel
  ├── AllowedClassIds / AllowedClassTags, optional
  ├── FixedStatModifiers
  ├── WeaponProfile, optional
  ├── AffixPoolId, optional
  ├── AffixCountProfileId, optional
  ├── SpecialEffectIds, optional
  ├── SetId, optional
  ├── UniqueEquippedGroup, optional
  ├── TradePolicyId, optional
  ├── VendorValueProfileId, optional
  ├── AppearanceProfileId, optional
  ├── Flags
  ├── Version
  └── Metadata

4. Item Instance

ItemInstance
  ├── ItemInstanceId
  ├── ItemDefinitionId
  ├── OwnerCharacterId
  ├── Quantity
  ├── State
  ├── RolledAffixes[]
  ├── AcquiredAt
  ├── SourceType
  ├── SourceId
  ├── BindState
  ├── BoundToCharacterId, optional
  ├── TransactionLockId, optional
  ├── InstanceVersion
  └── InstanceMetadata

Для обычной gear InstanceMetadata должен быть минимальным.

5. Item Types

core ItemType:

WEAPON
ARMOR
ACCESSORY
CONSUMABLE
QUEST
MATERIAL

Consumable/Material могут существовать как предметы, даже если их полноценные gameplay-системы ещё не реализованы.

6. Rarity

Authoritative rarity set:

```text
COMMON
UNCOMMON
RARE
EPIC
LEGENDARY
UNIQUE
```

Rarity влияет на:
- визуальное представление;
- доступный item/stat budget;
- допустимое число affixes;
- доступность SpecialEffect / UniqueRule;
- loot pools.

Rarity сама по себе не умножает Stats автоматически. Конкретную силу определяют ItemDefinition, RewardTier, affixes и special effects.

7. Inventory

Персонаж имеет Inventory.

Inventory хранит ItemInstance, которые не экипированы.

Базовое правило:

Inventory Capacity = 40 slots

Это текущий data-driven default; Capacity может быть изменён отдельным inventory/content profile.

Weight System отсутствует.

8. Inventory Slot

Один inventory slot содержит:

один non-stackable ItemInstance;
или один stack одного ItemDefinition.

9. Stackable Items

MaxStack определяется ItemDefinition.

Equipment:
MaxStack = 1

Материалы/Quest items:
могут иметь MaxStack > 1.

При добавлении stackable item сервер сначала пытается заполнить существующие неполные stacks, затем создаёт новый stack.

10. Inventory Capacity

Если места недостаточно:

операция получения Item не должна молча уничтожать награду.

Reward systems должны поддерживать Pending Reward / Pending Loot state.

Item System возвращает:

SUCCESS
PARTIAL
NO_SPACE

Loot/Quest System решают дальнейшее поведение.

11. Equipment

Equipment — отдельное состояние предметов персонажа.

Экипированный ItemInstance не занимает обычный inventory slot.

При Unequip предмет должен вернуться в Inventory.

Если Inventory заполнен:

Unequip отклоняется, если операция не является заменой предмета в том же атомарном equip transaction.

12. Equipment Slots

Базовое правило:

MAIN_HAND
OFF_HAND
HEAD
CHEST
HANDS
LEGS
FEET
AMULET
RING_1
RING_2

Набор является content configuration и может быть расширен.

13. Equip Operation

Equip request:

CharacterId
ItemInstanceId
TargetSlot

Server validation:

персонаж владеет предметом;
предмет существует;
предмет является equipable;
TargetSlot совместим;
RequiredLevel выполнен;
Class requirements выполнены;
WeaponTag разрешён Class System;
ArmorTag разрешён Class System;
персонаж жив, если это требуется;
предмет не заблокирован другой transaction.

14. Atomic Equip

Equip выполняется атомарно.

Если слот занят:

OldItem → Inventory
NewItem → EquipmentSlot

Обе операции либо выполняются вместе, либо не выполняются.

Нельзя получить состояние, где оба предмета потерялись или оба заняли один слот.

15. Equip During Combat

Для текущей системы базовое правило:

смена экипировки во время IN_COMBAT запрещена.

Причины:

предсказуемость Stats;
исключение swap-exploits;
проще Combat Snapshot;
проще UI.

16. Two-Handed Weapons

WeaponProfile может содержать:

HandsRequired = 1 or 2

Two-handed weapon может быть экипирован только в:

MAIN_HAND

Попытка экипировать two-handed weapon непосредственно в OFF_HAND отклоняется сервером.

Если экипируется two-handed weapon:

OFF_HAND должен быть пуст.

Если OFF_HAND занят:

сервер пытается переместить item в Inventory атомарно.

Если места нет:
Equip отклоняется.

17. Off-Hand

OFF_HAND может содержать:

one-hand weapon;
shield-like item, если когда-либо будет добавлен;
focus/offhand accessory.

Block как stat не возвращается автоматически из-за существования shield item.

Если щит появится, его gameplay определяется Item/Effect content, а не обязательным Block stat.

18. Weapon Profile

WeaponProfile
  ├── WeaponTag
  ├── MinWeaponDamage
  ├── MaxWeaponDamage
  ├── BaseAttackInterval
  ├── DamageType
  └── HandsRequired

BaseAttackInterval является источником базовой скорости Auto Attack согласно Combat System.

19. Weapon Damage Roll

Конкретный Auto Attack может получать BaseWeaponDamage из WeaponProfile.

core:

WeaponDamage = server random value between MinWeaponDamage and MaxWeaponDamage

Дальнейший scaling выполняет Damage and Healing System / Combat calculation context.

20. Unarmed

UnarmedProfile является fallback combat profile только для классов, которым Class System разрешает бой без оружия.

ClassDefinition определяет:

AllowUnarmed

Если:

AllowUnarmed = true
AND MAIN_HAND не содержит валидного weapon

Combat System использует UnarmedProfile.

UnarmedProfile имеет:

MinDamage;
MaxDamage;
BaseAttackInterval;
DamageType.

Если:

AllowUnarmed = false
AND MAIN_HAND не содержит валидного weapon

персонаж не выполняет Auto Attack.

Это не запрещает способности, которые Ability System явно разрешает использовать без оружия.

Текущий playable roster:

```text
Warrior → AllowUnarmed = true
Archer  → AllowUnarmed = false
Mage    → AllowUnarmed = false
```

Future classes определяют `AllowUnarmed` в собственном ClassDefinition.

UnarmedProfile не является ItemInstance.

21. Armor Items

Armor item может давать:

Armor;
MagicResistance;
Stamina;
primary attributes;
другие утверждённые Stats.

ArmorTag определяет class requirement.

ArmorTag не задаёт формулу mitigation.

22. Accessories

Accessory может давать Stats без ArmorTag.

Базовые accessory slots:

```text
CLOAK
AMULET
RING_1
RING_2
```

23. Stat Modifiers

ItemDefinition.StatModifiers использует модель Attributes and Stats System.

Пример:

+10 Strength
+5 Stamina
+2% CriticalChance
+20 Armor

Item System не пересчитывает FinalStats самостоятельно.

24. Equipment Stat Pipeline

Equip changed
  ↓
Equipment Source changed
  ↓
Stats cache invalidated
  ↓
FinalStats recalculated
  ↓
Resource maximums re-evaluated if needed

25. CurrentHP при смене Stamina

Если предмет снимается и MaxHP уменьшается:

Resource System применяет свои clamp rules.

Item System не изменяет CurrentHP напрямую.

26. Attack Speed Item Modifiers

Оружие задаёт BaseAttackInterval.

Другие предметы могут давать AttackSpeed modifier.

Stats System вычисляет Final AttackSpeed.

Combat System определяет будущий Auto Attack interval.

27. Class Requirements

Item может использовать:

AllowedClassIds;
WeaponTag;
ArmorTag.

Рекомендуется предпочитать tag-based permissions через Class System.

AllowedClassIds использовать только для действительно class-specific items.

28. Level Requirements

RequiredLevel проверяется при Equip.

Предмет может находиться в Inventory ниже RequiredLevel.

29. Item Ownership

Один ItemInstance имеет не более одного OwnerCharacterId.

Клиент не может менять OwnerCharacterId.

30. Item State

core ItemState:

INVENTORY
EQUIPPED
PENDING_REWARD
AUCTION_ESCROW
DESTROYED

DESTROYED является terminal state для удалённого item instance.

31. Destroy Item

Игрок может удалить обычный Item только через подтверждённое серверное действие.

Для текущей системы нельзя уничтожать:

equipped item без unequip;
quest-protected item;
item, locked transaction.

32. Quest Item

QUEST item может иметь:

QuestProtected = true

Quest System может использовать ItemObtained/ItemRemoved events.

Item System не знает, какой objective выполнен.

33. Item Acquisition

Item может появиться через:

Loot System;
Quest Reward;
starting class equipment;
Merchant Purchase;
Player Trade;
Auction Purchase;
Crafting Result;
scripted grant;
admin/debug grant.

Каждый grant должен иметь SourceType + SourceId.

34. Item Grant Idempotency

ItemGrant должен иметь уникальный GrantId.

Повторная обработка одного GrantId не создаёт дубликат.

35. Item Removal

Removal request должен указывать:

ItemInstanceId / stack;
Quantity;
Reason;
SourceId.

Количество не может стать отрицательным.

36. Equipment Requirements and Talents

Talent может:

изменять Stats;
модифицировать способность;
требовать определённый WeaponTag.

Talent System проверяет equipment condition динамически.

Item System не активирует Talent самостоятельно.

37. Affixes and Item Variability

Item System архитектурно поддерживает random affixes.

Предмет может использовать один из content profiles:

```text
FIXED
AFFIXED
LEGENDARY
UNIQUE
SET_PIECE
```

Random affix не обязателен для каждого предмета.

Affix roll выполняется только если:
- ItemDefinition имеет `AffixPoolId`;
- source/reward tier разрешает вариативность;
- Rarity profile задаёт допустимое число affixes.

Сгенерированные affixes сохраняются в ItemInstance и никогда не перебрасываются из-за reconnect/restart.

Legendary и Unique используют те же Item/Effect механики, но могут иметь `SpecialEffectIds` и `UniqueEquippedGroup`.

38. Item Power

Не требуется универсальный ItemPower score как источник механики.

Если UI позже покажет Gear Score, он является derived display value.

39. Persistence

Сохраняются:

ItemInstance;
OwnerCharacterId;
Quantity;
State;
Equipment slot;
inventory placement;
acquisition source.

ItemDefinition является content data.

40. Transaction Safety

Следующие операции должны быть atomic:

Item Grant;
Equip;
Unequip;
Stack Merge;
Item Remove;
Reward Claim.

41. Restart Recovery

После restart:

inventory восстанавливается;
equipment восстанавливается;
Stats пересчитываются;
PENDING_REWARD не теряется;
незавершённые item transactions не должны создавать duplication.

42. Events

Item System эмитит:

ItemGranted
ItemObtained
ItemRemoved
ItemEquipped
ItemUnequipped
InventoryFull
ItemDestroyed
EquipmentChanged

Event payload содержит:

CharacterId;
ItemDefinitionId;
ItemInstanceId;
Quantity;
SourceType;
SourceId.

43. Quest Integration

Quest System может слушать:

ItemObtained;
ItemRemoved;
ItemEquipped.

Collect objective не должен зависеть только от события, если objective требует «иметь N предметов сейчас».

Quest System может запросить авторитетный current inventory count.

44. Loot Integration

Loot System создаёт ItemGrant.

Item System возвращает:

granted;
pending/no-space;
rejected.

Loot System сохраняет reward state до успешного получения.

45. Equipment Philosophy

Первый тест должен давать игроку заметный upgrade.

Новый предмет должен быть понятен:

больше damage;
больше survivability;
другая secondary stat;
подходящий class identity.

Не нужен огромный список почти одинаковых предметов.

46. Item Invariants

INVARIANT-01
ItemDefinition и ItemInstance являются разными сущностями.

INVARIANT-02
Клиент не может создавать ItemInstance.

INVARIANT-03
Клиент не может изменять ownership.

INVARIANT-04
Equip validation выполняется сервером.

INVARIANT-05
Equip является atomic operation.

INVARIANT-06
Смена equipment в Combat запрещена для core.

INVARIANT-07
Weapon BaseAttackInterval приходит из WeaponProfile.

INVARIANT-08
Item Stats проходят через Attributes and Stats System.

INVARIANT-09
Item System не определяет Damage mitigation formulas.

INVARIANT-10
Item System не определяет Loot chance.

INVARIANT-11
Reward не должен теряться только из-за полного Inventory.

INVARIANT-12
ItemGrant должен быть idempotent.

INVARIANT-13
Equipment items MaxStack = 1.

INVARIANT-14
FinalStats пересчитываются после EquipmentChanged.

INVARIANT-15
Durability отсутствует в core.

47. Out of Scope

Этот документ пока не определяет:

durability;
repair;
sockets;
gems;
enchanting;
random affixes;
procedural items;
item sets;
transmog;
auction;
player trade;
mail;
bank;
crafting;
salvage;
vendor economy;
bind on pickup;
bind on equip;
item degradation;
weight;
gear score mechanics;
PvP equipment normalization;
конкретный полный список items;
UI inventory drag-and-drop.

---

# Source of Truth Revision v2

- Item architecture сразу поддерживает fixed items, random affixes, set pieces, legendary effects и unique rules; контент может вводиться поэтапно.
- Rarity set: COMMON, UNCOMMON, RARE, EPIC, LEGENDARY, UNIQUE.
- Официальные equipment slots включают CLOAK.
- Block/Parry не возвращаются из-за Shield item.
- Обычный gear не содержит прямых `PHYSICAL_PET/SPIRIT_PET Damage/Crit/AttackSpeed` процентов.
- RequiredLevel обязателен для экипируемых предметов.
- Set bonuses реализуются data-driven через SetDefinition.


## Full ItemDefinition fields

```text
ItemDefinition
├── ItemDefinitionId
├── Name
├── ItemType
├── Rarity
├── RequiredLevel
├── AllowedClassIds / ClassTags
├── EquipmentSlot
├── WeaponTag / ArmorTag
├── FixedStatModifiers
├── AffixPoolId, optional
├── AffixCountProfile, optional
├── SpecialEffectIds, optional
├── SetId, optional
├── UniqueEquippedGroup, optional
├── TradePolicyId, optional
├── VendorValueProfileId, optional
├── AppearanceProfileId, optional
├── VisualPriority, optional
├── WeaponProfile, optional
├── Flags
└── Version
```

## Equipment Slots

```text
MAIN_HAND
OFF_HAND
HEAD
CHEST
HANDS
LEGS
FEET
CLOAK
AMULET
RING_1
RING_2
```

Лук/арбалет может блокировать обычный OFF_HAND и использовать отдельный `QUIVER` как item tag/content concept; отдельный slot вводится только если это понадобится UI/балансу.

## Item generation

```text
Base Item
+ RequiredLevel / RewardTier budget
+ Rarity budget
+ Fixed modifiers
+ Rolled Affixes (если definition разрешает)
+ Legendary Effect / Unique Rule (если применимо)
```

Один и тот же Legendary Effect не может бесконтрольно складываться; для этого используется `UniqueEquippedGroup`.

---

# 48. Visual Equipment / Appearance

Экипируемый предмет может иметь отдельный визуальный профиль.

Gameplay и presentation разделены:

```text
Item stats / requirements / effects
!=
Item appearance
```

Базовый контракт:

```text
ItemDefinition
└── AppearanceProfileId, optional
```

`AppearanceProfileId` не изменяет Stats, Damage, Resource или требования экипировки.

## 48.1. Видимые equipment slots

Внешний вид персонажа может собираться по слоям:

```text
Base Character
+ Race/Gender body
+ HEAD
+ CHEST
+ HANDS
+ LEGS
+ FEET
+ CLOAK
+ MAIN_HAND
+ OFF_HAND
```

`AMULET`, `RING_1`, `RING_2` по умолчанию не требуют отдельного отображения на модели персонажа.

## 48.2. Rarity и уникальный внешний вид

Любой предмет технически может иметь `AppearanceProfileId`.

Контентный приоритет:

```text
COMMON / UNCOMMON / RARE
→ могут использовать общие appearance families

EPIC
→ чаще получает отдельные детали/варианты

LEGENDARY / UNIQUE
→ должен иметь возможность получить уникальный узнаваемый appearance
```

Legendary/Unique не обязаны всегда иметь уникальную модель, но engine и content schema это поддерживают.

## 48.3. Equipment state → appearance

Server остаётся источником истины для того, **что экипировано**.

Frontend строит внешний вид из подтверждённого Equipment State:

```text
Equipped ItemInstance
→ ItemDefinition
→ AppearanceProfileId
→ Character renderer
```

Клиент не может визуально подменить экипировку и тем самым изменить gameplay state.

## 48.4. Visual conflicts

Если два appearance layer конфликтуют, применяется data-driven presentation policy.

Примеры:

```text
full helmet may hide hair
two-handed weapon may hide OFF_HAND visual
hood may hide some hair styles
large cloak may use compatible chest variant
```

Это presentation rule, не Equipment validation rule, если gameplay явно не требует иного.

## 48.5. Transmog-ready architecture

Архитектура поддерживает будущий cosmetic override:

```text
EquippedItemDefinitionId
→ gameplay source

DisplayedAppearanceProfileId
→ cosmetic source
```

Это позволяет в будущем носить сильный предмет, но отображать уже открытый внешний вид другого предмета.

Transmog:
- не меняет Item stats;
- не меняет Rarity;
- не меняет requirements;
- не меняет ownership;
- не должен храниться как подмена Equipped Item.

До отдельного Cosmetic/Transmog System используется обычный `AppearanceProfileId` экипированного предмета.

## 48.6. Initial implementation priority

Для первого полноценного визуального equipment pass:

```text
1. MAIN_HAND / weapon
2. HEAD
3. CHEST
4. CLOAK
5. HANDS
6. LEGS
7. FEET
8. OFF_HAND
```

Оружие, шлем, нагрудник и плащ дают наибольшую визуальную отдачу и должны быть реализованы первыми.

## 48.7. Appearance invariants

1. Appearance не влияет на combat formulas.
2. Equipment System остаётся owner экипированного ItemInstance.
3. Character renderer не является owner gameplay equipment state.
4. Legendary/Unique могут иметь уникальный appearance.
5. Accessories не обязаны иметь body-layer visual.
6. Cosmetic override не заменяет EquippedItemId.
7. Race/Gender влияют только на совместимый presentation variant, а не на power.

# 49. Economy / Trade / Crafting Integration

## 49.1. Trade Policy

ItemDefinition хранит один authoritative `TradePolicyId`.

```text
ItemTradePolicy
├── Tradeable
├── Auctionable
├── VendorSellAllowed
└── BindRule
```

BindRule:

```text
NONE
BIND_ON_EQUIP
BIND_ON_PICKUP
CHARACTER_BOUND
```

Trade/Auction System оркестрирует передачу, но Item System остаётся owner ItemInstance и binding state.

## 49.2. Bind State

```text
BindState
UNBOUND
CHARACTER_BOUND
```

`BIND_ON_EQUIP` переводит ItemInstance в `CHARACTER_BOUND` при первом подтверждённом Equip.

`BIND_ON_PICKUP` применяет bind при ItemGrant конкретному персонажу.

Bound item не может быть передан/выставлен, если policy не разрешает это явно.

## 49.3. Transaction Lock

ItemInstance может иметь:

```text
TransactionLockId
```

Lock используется Trade и другими атомарными операциями.

Locked item нельзя:
- equip/unequip;
- destroy;
- vendor sell;
- auction;
- consume as crafting ingredient.

Lock не меняет ownership.

## 49.4. Auction Escrow

Auction listing переводит item:

```text
INVENTORY → AUCTION_ESCROW
```

Предмет в escrow:
- не находится в обычном inventory;
- не может быть использован;
- остаётся связан с seller/listing до settlement;
- переходит buyer только через подтверждённый Auction purchase.

## 49.5. Vendor Value

ItemDefinition может иметь:

```text
VendorValueProfileId
```

или explicit base vendor value.

Economy System рассчитывает окончательную цену NPC sell/buy.
Item System не хранит Gold.

## 49.6. Crafting Result

Crafting System:
- не создаёт ItemInstance напрямую;
- вызывает ItemGrant/ItemGenerator;
- передаёт CraftOperationId как SourceId;
- использует те же Rarity/Affix/Trade/Bind rules.

## 49.7. New item invariants

1. ItemInstance не может одновременно быть `EQUIPPED` и `AUCTION_ESCROW`.
2. Transaction-locked item не мутируется сторонней операцией.
3. BindState изменяет только Item System по подтверждённой policy.
4. Economy/Trade/Crafting не редактируют item stats.
5. Auction escrow не является вторым владельцем ItemInstance.
