# Elyndor — Crafting & Profession System Specification

**Document:** 29_CRAFTING_AND_PROFESSION_SYSTEM.md  
**System:** Professions / Recipes / Crafting  
**Status:** Foundation / Source of Truth  
**Version:** 1.0

---

# 1. Назначение

Crafting & Profession System определяет развитие ремесленных профессий и создание игровых предметов из ресурсов.

Текущие профессии:

```text
BLACKSMITHING
ALCHEMY
COOKING
```

Система отвечает за:

- ProfessionDefinition;
- CharacterProfessionState;
- profession XP/level;
- RecipeDefinition;
- recipe unlock;
- crafting validation;
- ingredient consumption;
- crafting operation;
- station requirements;
- crafting completion;
- profession progression.

Система не отвечает за:

- Item stats;
- Item RNG implementation;
- Wallet balance;
- Auction listing;
- Quest completion;
- Effect formulas.

---

# 2. Главный принцип

```text
Known Recipe
+ Profession
+ Required Station
+ Ingredients
+ optional Gold Fee
→ CraftOperation
→ Item System / Item Generator
→ Result
→ Profession XP
```

---

# 3. Professions

## BLACKSMITHING

Fantasy:

- оружие;
- металлическая броня;
- отдельные компоненты/материалы.

## ALCHEMY

Fantasy:

- potions;
- elixirs;
- magical reagents;
- consumable utility.

## COOKING

Fantasy:

- food;
- напитки;
- временные food buffs;
- подготовка пищи.

---

# 4. Profession Limit

Текущая система **не вводит искусственный лимит "выбери только две профессии"**.

Персонаж может изучать все текущие профессии.

Причины:

- проще для первого полноценного gameplay loop;
- меньше необратимых ошибок игрока;
- экономика всё равно специализируется через rare recipes/material cost/time;
- ограничение можно добавить позднее через ProfessionPolicy без переделки Recipe/Craft модели.

---

# 5. Profession Definition

```text
ProfessionDefinition
├── ProfessionId
├── Name
├── MaxProfessionLevel
├── XPProfileId
├── StationTags[]
├── RecipeCategories[]
├── Version
└── Metadata
```

Current:

```text
MaxProfessionLevel = 60
```

Это data-driven default.

---

# 6. Character Profession State

```text
CharacterProfessionState
├── CharacterId
├── ProfessionId
├── ProfessionLevel
├── CurrentProfessionXP
├── LifetimeProfessionXP
├── LearnedRecipeIds[]
├── StateVersion
└── UpdatedAt
```

Каждая профессия прогрессирует независимо.

---

# 7. Profession Level

```text
1..60
```

Profession XP curve хранится:

```text
ProfessionXPProfile
```

Не hardcode'ить формулу в C#.

---

# 8. Character Level Requirement

Recipe может иметь:

```text
RequiredCharacterLevel
RequiredProfessionLevel
```

Profession level сам по себе не обязан быть <= Character Level.

Сильный результат контролируется recipe requirements.

---

# 9. Recipe Definition

```text
RecipeDefinition
├── RecipeId
├── ProfessionId
├── Name
├── RequiredProfessionLevel
├── RequiredCharacterLevel, optional
├── IngredientEntries[]
├── CurrencyCost, optional
├── RequiredStationTag, optional
├── CraftDuration
├── ResultProfile
├── ProfessionXPReward
├── UnlockPolicy
├── Repeatable
├── Version
└── Metadata
```

---

# 10. Ingredient Entry

```text
RecipeIngredient
├── ItemDefinitionId / ItemTag
├── Quantity
├── Consume
└── Metadata
```

Current recipes используют:

```text
Consume = true
```

Tool items могут позже использовать `Consume = false`.

---

# 11. Recipe Result

```text
CraftResultProfile
├── OutputItemDefinitionId
├── Quantity
├── ItemGenerationProfileId, optional
├── RarityProfileId, optional
├── BindOverride, optional
└── Metadata
```

Crafting не создаёт ItemInstance напрямую.

```text
Crafting
→ ItemGrant / ItemGenerator
→ Item System
```

---

# 12. Fixed vs Generated Craft

Recipe может создавать:

## Fixed

```text
specific ItemDefinition
```

## Generated

```text
Base Item
+ Recipe Result Tier
+ allowed AffixPool
+ Rarity profile
```

Generated craft использует тот же Item Generation pipeline, что и Loot.

Не создавать отдельную crafting-only affix engine.

---

# 13. No Crafting Luck Stat

Текущий Stats System не содержит:

```text
CraftingLuck
ProfessionLuck
QualityChance
```

Не добавлять hidden stat ради crafting.

Если recipe имеет RNG:

- RNG находится в CraftResultProfile;
- сервер authoritative;
- вероятность data-driven.

---

# 14. Craft Failure

Базово:

```text
CraftFailureChance = 0
```

Если игрок заплатил корректные ресурсы и операция валидна, craft создаёт результат.

Это уменьшает frustration и упрощает экономический баланс.

---

# 15. Recipe Unlock

Recipe может открываться через:

```text
PROFESSION_LEVEL
QUEST
LOOT
MERCHANT
SCRIPTED
DEFAULT
```

Unlock event должен быть idempotent.

---

# 16. Recipe Knowledge

LearnedRecipeId хранится в Profession State.

Игрок не теряет выученный рецепт после logout/restart.

Duplicate recipe unlock:

```text
no duplicate state
no duplicate reward
```

---

# 17. Crafting Station

```text
CraftingStationDefinition
├── StationId
├── StationTag
├── LocationId
├── AllowedProfessionIds[]
├── InteractionProfile
└── Metadata
```

Current tags:

```text
BLACKSMITH_FORGE
ALCHEMY_TABLE
KITCHEN
```

---

# 18. City Integration

Стартовые station locations логично размещать в городе:

```text
Forge
Alchemy Lab
Kitchen/Tavern
```

UI города открывает соответствующий Crafting screen.

---

# 19. Home Cooking

Повар в будущем может использовать:

```text
HOME_KITCHEN
```

Но Crafting System **не зависит от Housing System**.

Пока Housing не существует:

```text
KITCHEN
```

может находиться в городе/таверне.

Когда Housing появится, Home Kitchen просто становится дополнительным station с подходящим tag.

---

# 20. Craft Request

```text
StartCraft
├── CraftRequestId
├── CharacterId
├── RecipeId
├── Quantity
├── StationId
└── ClientRequestMetadata
```

Server validation:

- character state;
- recipe known;
- ProfessionLevel;
- CharacterLevel;
- station;
- ingredients;
- Gold;
- queue capacity;
- recipe active version.

---

# 21. Character State Restrictions

Crafting запрещён:

```text
IN_COMBAT
DEAD
RESPAWNING
TRAVELLING
```

Dungeon/World Event может запрещать crafting через activity policy.

---

# 22. Material Validation

Ingredient count всегда читает Item System.

Client не сообщает:

```text
"у меня есть 10 Iron"
```

Сервер проверяет inventory.

---

# 23. Currency Cost

Recipe может иметь Gold fee.

```text
Crafting
→ Economy Spend
```

Crafting System не мутирует Wallet.

`CRYSTAL` не используется как обязательная базовая crafting fee.

---

# 24. Craft Operation

```text
CraftOperation
├── CraftOperationId
├── CharacterId
├── RecipeId
├── Quantity
├── State
├── StartedAt
├── CompletesAt
├── IngredientConsumptionId
├── CurrencySpendId, optional
├── ResultGrantIds[]
├── ProfessionXPGrantId
├── Version
└── Metadata
```

State:

```text
VALIDATING
ACTIVE
COMPLETED
RESULT_PENDING
FAILED
CANCELLED_BEFORE_START
```

---

# 25. Ingredient Consumption

После успешной validation и перед ACTIVE:

```text
consume ingredients
spend fee
persist CraftOperation
```

Все действия входят в одну transaction/application operation.

Не оставлять:

```text
Gold spent
but ingredients not consumed
```

или наоборот.

---

# 26. Craft Duration

Recipe:

```text
CraftDuration = 0
```

означает instant craft.

Timed craft:

```text
CompletesAt = StartedAt + CraftDuration
```

Time System является owner времени.

Quartz может разбудить processing, но authoritative completion определяется `ServerTime >= CompletesAt`.

---

# 27. Craft Queue

Текущий default:

```text
MaxConcurrentCraftOperations = 1
```

Это Balance/Profile value.

Engine допускает изменение capacity позже.

Не использовать premium purchase как обязательное условие для нормального crafting.

---

# 28. Offline Crafting

Timed Craft продолжается после logout.

После reconnect:

```text
ServerTime >= CompletesAt
→ resolve result
```

Это не AFK Farming.

Crafting не симулирует combat.

---

# 29. Cancellation

После того как:

- ingredients consumed;
- Gold spent;
- CraftOperation ACTIVE

обычный player cancel отсутствует.

Это делает economy rules однозначными.

До commit validation player может закрыть UI/отменить request без потери ресурсов.

Если позже нужна cancellation/refund — отдельная explicit policy.

---

# 30. Result Grant

Когда craft completed:

```text
CraftOperationId
→ ResultGrantId
→ Item System
```

Если inventory заполнен:

```text
State = RESULT_PENDING
```

Результат не теряется.

После получения всех outputs:

```text
State = COMPLETED
```

---

# 31. Profession XP

Profession XP выдаётся только после подтверждённого craft completion.

```text
ProfessionXPGrantId
```

обеспечивает idempotency.

Если output pending из-за inventory:

рекомендуется profession XP всё равно считать earned после durable result creation, но не выдавать второй раз при claim.

---

# 32. Profession XP Reward

Recipe имеет:

```text
ProfessionXPReward
```

Возможен diminishing profile для слишком низкоуровневых recipes:

```text
normal XP
reduced XP
0 XP
```

Числа data-driven.

---

# 33. Blacksmithing Outputs

Typical categories:

```text
WEAPON
ARMOR
METAL_COMPONENT
```

Кузнец может создавать equipment, которое:

- имеет RequiredLevel;
- имеет Rarity;
- может иметь affixes;
- использует обычный ItemTradePolicy.

Никакого отдельного "crafted power formula" вне Item Budget System.

---

# 34. Alchemy Outputs

Typical:

```text
POTION
ELIXIR
REAGENT
```

Consumable ItemDefinition определяет:

- use action;
- EffectId/AbilityId;
- cooldown category;
- restrictions.

Alchemy не применяет Effect напрямую.

---

# 35. Cooking Outputs

Typical:

```text
FOOD
DRINK
MEAL
```

Food может:

- восстановить ресурс;
- дать временный buff;
- требовать out-of-combat use.

Effect/Item/Ability System определяют actual gameplay result.

Cooking только создаёт item.

---

# 36. Item Trade Integration

Crafted item может:

```text
Tradeable
Auctionable
BindOnEquip
BindOnPickup
```

Это определяется ItemDefinition/CraftResultProfile.

Crafting System не переопределяет trade rules произвольно.

---

# 37. Auction Integration

Crafted items могут формировать значимую часть Auction economy.

Auction SearchSnapshot может показывать:

```text
CrafterName, optional cosmetic metadata
Profession source
Affixes
Rarity
```

`CrafterName` не влияет на stats.

---

# 38. Merchant Integration

Merchant может продавать:

- базовые recipes;
- ингредиенты;
- profession tools/consumables.

Rare recipes лучше распределять между:

- quest;
- boss;
- dungeon;
- world drop;
- reputation/future content.

---

# 39. Loot Integration

Materials и recipes могут выпадать через Loot System.

Loot не изменяет ProfessionState напрямую.

Recipe item/use или unlock event передаётся Crafting System.

---

# 40. Quest Integration

Quest objectives могут включать:

```text
CRAFT_ITEM
REACH_PROFESSION_LEVEL
LEARN_RECIPE
```

Quest слушает подтверждённые Craft/Profession events.

Crafting System не меняет quest progress напрямую.

---

# 41. Economy Integration

Crafting создаёт:

## Sinks
- ingredient consumption;
- Gold fee.

## Value Creation
- new items;
- consumables;
- equipment.

Crafting не mint'ит Gold.

---

# 42. Server Restart

После restart:

- ProfessionState восстанавливается;
- Known Recipes сохраняются;
- ACTIVE CraftOperation проверяется по CompletesAt;
- consumed ingredients не возвращаются автоматически;
- completed result не генерируется повторно;
- pending item grant продолжается idempotently.

---

# 43. Content Versioning

CraftOperation должен сохранить:

```text
AcceptedRecipeVersion
ResultProfileVersion
```

Если recipe patch произошёл посередине timed craft:

текущая operation завершается по snapshot/version, принятому при старте.

Нельзя менять стоимость/выход уже оплаченного craft задним числом.

---

# 44. Concurrency

Нельзя одновременно потратить один и тот же stack ingredients двумя craft requests.

Item consumption и CraftOperation start — transaction.

---

# 45. Idempotency

Stable ids:

```text
CraftRequestId
CraftOperationId
IngredientConsumptionId
CurrencySpendId
ResultGrantId
ProfessionXPGrantId
RecipeUnlockId
```

Retry безопасен.

---

# 46. Analytics

Собирать:

```text
CraftCount by Recipe
CraftCount by Profession
MaterialConsumption
GoldCraftingSink
MostCraftedItems
ProfessionLevelDistribution
RecipeUnlockSource
AuctionVolumeOfCraftedItems
```

---

# 47. UI Contract

Profession screen:

```text
Profession name
Profession level
XP bar
Recipe categories
Known recipes
Locked recipe requirements
```

Craft screen:

```text
Recipe icon/name
result preview
required materials
owned / required quantity
Gold fee
station requirement
craft time
Craft button
pending result
```

---

# 48. Future Gathering

Текущая система не требует отдельной Gathering profession.

Materials могут приходить из:

- monsters;
- world loot;
- quests;
- dungeon;
- merchant;
- future gathering nodes.

Если Gathering System появится, он выдаёт materials через Item System и не меняет Crafting engine.

---

# 49. Invariants

1. Crafting не создаёт ItemInstance напрямую.
2. Crafting не меняет Wallet напрямую.
3. Ingredients проверяются сервером.
4. Ingredient consumption и fee не могут расходиться.
5. Craft retry не создаёт duplicate output.
6. Craft result не теряется при full inventory.
7. Profession XP не дублируется.
8. Recipe version snapshot сохраняется для active craft.
9. No CraftingLuck active stat.
10. No random craft failure by default.
11. Все три current professions могут быть изучены одним персонажем.
12. Housing не является обязательной dependency для Cooking.
