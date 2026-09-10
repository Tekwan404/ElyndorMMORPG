# Item System: Phase 0 Audit

**Аудит на:** `cb9b04c` (актуальный `main` на начало фазы)
**Scope:** только карта существующей item/economy архитектуры. Новые игровые механики, миграции и UI в этой фазе не добавлялись.

## Already exists

### Static content and item definitions

- `ItemDefinition` в `src/Elyndor.Core/Items/ItemModels.cs` — единственный статический контракт предмета: `ItemType`, `ItemRarity`, level/slot/class restrictions, set, categories, icon/appearance, fixed и range-статы, procedural-affix параметры.
- Контент предметов, сетов, лута, торговцев и itemization собирается из `content/` через `GameContentPackage`, `CategoryContentComposer` и валидируется `ContentValidationPipeline`. Индексы контента — `GameContentIndexes`.
- `content/itemization/system.json` уже хранит server-side формулы Item Power, rarity/slot multipliers, веса статов, affix pools, quality profiles и reforge costs. Административный content pipeline уже поддерживает draft → validate → revision → publish; в Admin V2 есть structured editor для `items`, `lootTables`, `merchants`.

### Item instances, rolls and quality

- `CharacterItem` в `src/Elyndor.Core/Items/CharacterInventoryModels.cs` — canonical persistent instance (`game.character_items`). Это не копия `ItemDefinition`: в нём хранятся владелец, количество, definition version, lock, bind/source metadata и generated-instance state.
- `ItemRolledAffix` (`game.character_item_affixes`) хранит rolled stat, диапазон, step, tier, guaranteed/reforge state и ordinal. `ItemInstancePersistenceFactory` — единая точка materialize/create для merchant, combat reward и pending loot.
- Процедурная экипировка уже считает `ActualItemPower`, `MinimumTemplateItemPower`, `MaxTemplateItemPower`, `RollQuality`, `Stars`, `IsPerfect`, prefix/suffix/display name. Расчёт живёт в `ItemInstanceGenerator`; RNG детерминирован seed от source operation через `ItemGenerationKey`.
- `RollQuality` — canonical quality score (0–100), а не отдельное недостающее поле. `Stars` вычисляются из realized potential: 1 (<30%), 2 (≥30%), 3 (≥50%), 4 (≥70%), 5 (≥90%). `IsPerfect` требует всех affix на максимум и max Item Power. Rarity остаётся полем template и генератор/рефорж его не изменяют.
- Неprocedural и legacy equipment продолжает использовать существующий `PrimaryStatRanges`/`ItemInstanceStatRoller`; обычные stackable materials и consumables не получают generated state, звёзды или Item Power.

### Inventory, equipment, loot and safety

- `InventoryEquipmentService` и `InventorySnapshotReader` — canonical inventory/equipment read/mutation layer. Он уже поддерживает equip/unequip, class/category/level restrictions, dual wield, sets, use, pending loot и capacity.
- Durable `IsLocked` уже есть на item instance и защищает merchant/quest consumption. `TransactionLockId` защищает instance во время reforge. `SetItemLockAsync` использует `CharacterMutation` для replay protection.
- `CombatRewardService`, `CombatLootRollService`, `PendingLootItem` и `CombatRewardGrant` уже используют тот же item-instance factory. Generated item state переживает pending loot и claim.
- Обычный merchant за Gold уже существует (`MerchantService`): buy/sell атомарны, используют row lock на персонаже, `CharacterMutation` (mutation id + request fingerprint) и PostgreSQL transaction.

### Existing reforge foundation

- `ItemReforgeService` и `item_reforge_operations` уже реализуют server-authoritative reforge одного **не-guaranteed** affix slot generated, unequipped item instance.
- Стоимость берётся из `ItemReforgeCostProfileDefinition` в content, а не из controller. Операция расходует Gold/material/catalyst, создаёт server-calculated proposal, блокирует item и позволяет принять или оставить текущий roll.
- Повтор `operationId` возвращает прежний результат; conflicting payload отклоняется. Spend, item lock и persisted proposal находятся в одной PostgreSQL transaction. Это нужно расширять, а не заменять в Phase 3.

### Client contracts and art

- API уже отдаёт generated fields в `GeneratedItemSummaryResponse`; frontend тип `GeneratedItemSummary` уже содержит Item Power, RollQuality, Stars, IsPerfect и affixes.
- Уже есть `itemArt.ts`, `IconGenerator` и SVG glyph system. Для следующих UI-фаз использовать их вместо emoji/новой icon infrastructure.

## Partial

| Area | Current state | Consequence for later phases |
| --- | --- | --- |
| Quality UI | API/DTO и persistent fields готовы, но player Vue views не читают `generatedItem`; reusable `ItemQualityStars` отсутствует. | Phase 1 должна быть UI-first поверх current DTO/calculation, без новой quality schema. |
| Inventory details | Показывает effective stats, comparison, set, level requirements, NEW/lock; не показывает Item Power, stars, percent RollQuality, perfect state, affix ranges/tier. | Extend `InventoryView.vue` and its item rendering; retain current comparison flow. |
| Reforge UX | Backend endpoints и `gameSession` client methods есть, но ни один player component их не вызывает. | Phase 4 должна построить mobile flow поверх current pending/roll/decide contract, а не второй Forge API. |
| Reforge scope | Есть reroll only one selected non-guaranteed affix; reforge currently rejects equipped items and uses legacy `SPIDER_SILK` / `SPIDER_VENOM_SAC` cost profile. | Phase 2/3 decide how to introduce the single Reforge Stone and salvage through the existing inventory/mutation pattern; do not silently repurpose catalyst semantics. |
| Enhancement | `EnhancementLevel` is persisted but has no mutation/service/API/UI behavior. | Phase 5 can claim this field only after reconciling it with canonical stars/quality; it must not make UI ★★★★★ inconsistent with affix rolls. |
| Admin V2 | Items, merchant and loot have forms plus package validation; no dedicated Forge/salvage/store/promo editor exists. | Phase 9 extends the existing content package/Admin V2 workflow. |
| Economy | Character Gold and merchant mutations exist, but not a generic economy wallet. | Do not treat `Character.Gold` or `CharacterMutation` as a Crystal ledger. |

## Missing

- Salvage action, salvage preview/yield profile, Reforge Stone item/resource and anti-loop economy validation.
- Star-quality upgrade operation; catalyst policy marking dungeon/boss catalysts as premium-shop forbidden.
- Crystal balance, immutable premium ledger, payment grant API, refund/reconciliation and concurrency-safe premium spend.
- Server-defined premium SKU/store, premium eligibility policy, purchase UI and promo-code entities/redemption history.
- Forge/salvage/star-upgrade Admin V2 forms and economy observability.
- Runtime crafting/profession implementation. `29_CRAFTING_AND_PROFESSION_SYSTEM.md` is a design contract only; no related DbSet/entity/service/API exists.
- A player-facing `ItemQualityStars` component and Forge screen.

## Canonical models to extend

| Concern | Canonical model / service | Extension rule |
| --- | --- | --- |
| Static item and balance data | `ItemDefinition`, `ItemizationDefinition`, `GameContentPackage`, `content/itemization/system.json` | Add content only through the existing package/composer/validator/Admin publication path. |
| Concrete owned item | `CharacterItem` + `ItemRolledAffix` | Preserve instance identity and generated metadata; use `ItemInstancePersistenceFactory` for new sources. |
| Quality / stars / Item Power | `GeneratedItemInstance` + `ItemInstanceGenerator.Recalculate` | Reuse `RollQuality`, `Stars`, `IsPerfect`, Item Power formula. Do not add parallel quality/star fields. |
| Inventory/equipment/lock | `InventoryEquipmentService` + `CharacterMutation` | Add item mutations here or in a focused service using the same transaction/idempotency conventions; do not bypass equipment restrictions or locks. |
| Reforge operation | `ItemReforgeService` + `ItemReforgeOperation` | Extend its content-driven cost and durable proposal pattern; retain retry/replay and item transaction lock semantics. |
| Loot/rewards | `CombatRewardService`, `PendingLootItem`, `CombatRewardGrant` | Route generated rewards through `ItemInstancePersistenceFactory`; preserve pending-loot/claim idempotency. |
| Existing Gold merchant | `MerchantService` + `CharacterMutation` | Reuse only for normal Gold/item exchange. Crystal needs its own future canonical wallet/ledger. |
| Item client rendering | `InventoryItemResponse` / `GeneratedItemSummaryResponse`, `web/elyndor-web/src/api/contracts.ts`, `InventoryView.vue` | Render current fields; keep item art and SVG glyph helpers. |

## Phase boundary decision

Phase 1 does **not** need a migration or a new calculation engine. Its smallest complete slice is: expose the already persisted/generated quality data in the existing inventory grid and detail view, add focused tests for the current boundaries, and leave consumables/materials without stars. Forge, salvage, Crystals and premium flows remain out of scope until their numbered phases.
