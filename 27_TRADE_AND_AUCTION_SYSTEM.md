# Elyndor — Trade & Auction System Specification

**Document:** 27_TRADE_AND_AUCTION_SYSTEM.md  
**System:** Direct Trade / Auction House  
**Status:** Foundation / Source of Truth  
**Version:** 1.0

---

# 1. Назначение

Trade & Auction System отвечает за безопасную передачу предметов и Gold между игроками.

Система разделена на:

```text
Direct Trade
Auction House
```

Система не владеет:

- Item stats;
- Item ownership как конечным состоянием;
- Wallet balance;
- Loot RNG;
- Currency definitions.

Она оркестрирует операции через:

```text
Item System
Economy System
```

---

# 2. Главный принцип

Никакой trade/auction request клиента не считается фактом передачи.

```text
Client intent
→ server validation
→ asset lock / escrow
→ atomic commit
→ Item/Economy owner mutation
→ result event
```

---

# 3. Item Trade Policy

ItemDefinition должен иметь trade policy.

```text
ItemTradePolicy
├── Tradeable
├── Auctionable
├── VendorSellAllowed
├── BindRule
└── Metadata
```

BindRule:

```text
NONE
BIND_ON_EQUIP
BIND_ON_PICKUP
CHARACTER_BOUND
```

Текущее ItemInstance может хранить:

```text
BindState
BoundToCharacterId, optional
```

---

# 4. Bind Rules

## NONE

Предмет остаётся tradeable согласно policy.

## BIND_ON_EQUIP

До первого Equip предмет:

- можно trade;
- можно auction.

После Equip:

```text
BindState = CHARACTER_BOUND
```

## BIND_ON_PICKUP

После получения конкретным игроком:

```text
BindState = CHARACTER_BOUND
```

## CHARACTER_BOUND

Нельзя:

- direct trade;
- auction.

Vendor sell зависит от `VendorSellAllowed`.

---

# 5. Quest Items

`QuestProtected = true` по умолчанию:

```text
Tradeable = false
Auctionable = false
```

Quest System остаётся владельцем objective logic.

---

# 6. Direct Trade Eligibility

Direct Trade начинается между двумя персонажами.

Условия по умолчанию:

```text
both alive
not IN_COMBAT
same LocationId
not already in another TradeSession
not in incompatible transition state
```

Party membership не требуется.

Dungeon может запрещать trade через ActivityPolicy.

---

# 7. Trade Session

```text
TradeSession
├── TradeSessionId
├── CharacterAId
├── CharacterBId
├── State
├── OfferA
├── OfferB
├── OfferRevision
├── ConfirmedRevisionA, optional
├── ConfirmedRevisionB, optional
├── CreatedAt
├── SuspendedUntil, optional
└── Version
```

State:

```text
OPEN
CONFIRMING
SUSPENDED
COMPLETED
CANCELLED
EXPIRED
```

---

# 8. Trade Offer

```text
TradeOffer
├── ItemEntries[]
├── GoldAmount
└── Metadata
```

Current currency:

```text
GOLD
```

`CRYSTAL` в direct trade запрещён.

---

# 9. Item Lock

Когда ItemInstance добавлен в TradeOffer:

- ownership не меняется;
- item получает transaction lock;
- его нельзя equip;
- destroy;
- sell;
- auction;
- использовать как crafting ingredient.

Lock owner:

```text
TradeSessionId
```

Удаление предмета из offer освобождает lock.

---

# 10. Stack Items

Если в trade передаётся часть stack:

Item System выполняет atomic split.

Получившийся ItemInstance/stack блокируется TradeSession.

Клиент не создаёт split instance самостоятельно.

---

# 11. Gold Reservation

До первого confirmation Gold может только отображаться в offer.

При переходе к final confirmation:

```text
Economy.ReserveGold
```

Reservation:

```text
OwnerOperationId = TradeSessionId
```

Если offer меняется:

- confirmations сбрасываются;
- reservation recalculated/released.

---

# 12. Offer Revision

Любое изменение:

- item added;
- item removed;
- quantity changed;
- Gold changed

увеличивает:

```text
OfferRevision
```

И автоматически:

```text
ConfirmedRevisionA = null
ConfirmedRevisionB = null
```

Это защищает от схемы:

> игрок подтвердил одно, а второй незаметно изменил offer.

---

# 13. Confirmation

Игрок подтверждает конкретный:

```text
TradeSessionId + OfferRevision
```

Trade commit выполняется только если:

```text
ConfirmedRevisionA == OfferRevision
AND
ConfirmedRevisionB == OfferRevision
```

---

# 14. Final Validation

Перед commit сервер повторно проверяет:

- оба персонажа существуют;
- item ownership;
- item locks;
- bind/trade policy;
- Gold reservations;
- destination inventory capacity;
- session state;
- offer revision.

Нельзя полагаться на validation, сделанную 20 секунд назад.

---

# 15. Atomic Trade Commit

В одной application/database transaction:

```text
A items → B
B items → A
A Gold → B
B Gold → A
release locks
consume reservations
TradeSession = COMPLETED
```

Если один обязательный шаг не выполняется:

```text
commit не происходит
```

---

# 16. Inventory Capacity

Direct Trade не должен завершаться, если после обмена получатель не может вместить предметы.

До commit рассчитывается destination inventory result с учётом:

- outgoing items;
- incoming stacks;
- possible stack merge;
- free slots.

---

# 17. Disconnect

Transient disconnect не обязан мгновенно отменять trade.

Policy:

```text
OPEN/CONFIRMING
→ participant disconnect
→ SUSPENDED
→ reconnect before SuspendedUntil → restore
→ timeout → CANCELLED
```

Recommended default:

```text
SuspendGrace = 60 sec
```

При cancel:

- item locks released;
- Gold reservations released.

---

# 18. Trade Cancel

Любой участник может Cancel до commit.

После `COMPLETED` отмена невозможна.

Rollback уже завершённой сделки возможен только как admin compensation, не как обычный player action.

---

# 19. Trade Events

```text
TradeOpened
TradeOfferChanged
TradeConfirmationChanged
TradeSuspended
TradeCancelled
TradeCompleted
```

---

# 20. Auction House Model

Текущий Auction House работает как:

```text
BUYOUT-ONLY MARKET
```

Игрок:

```text
выставляет предмет
→ назначает фиксированную цену
→ другой игрок покупает
```

Ставки/bidding не являются текущей игровой механикой.

Это осознанное решение, а не урезанная архитектура.

В будущем `ListingMode` может расшириться без изменения Item ownership/escrow модели.

---

# 21. Auction Listing

```text
AuctionListing
├── ListingId
├── SellerCharacterId
├── ItemInstanceId / StackQuantity
├── ItemDefinitionId
├── Quantity
├── PriceCurrencyId
├── TotalPrice
├── ListingFee
├── State
├── CreatedAt
├── ExpiresAt
├── BuyerCharacterId, optional
├── SoldAt, optional
├── ListingVersion
└── SearchSnapshot
```

State:

```text
ACTIVE
PURCHASE_PENDING
SOLD
CANCELLED
EXPIRED
RETURN_PENDING
SETTLED
```

---

# 22. Auction Currency

Current:

```text
GOLD
```

Auction validates:

```text
CurrencyDefinition.AuctionCurrency = true
```

`CRYSTAL` нельзя использовать.

---

# 23. Auction Escrow

При создании listing:

```text
ItemInstance
INVENTORY
→ AUCTION_ESCROW
```

Ownership логически остаётся за seller до продажи, но предмет:

- отсутствует в inventory;
- нельзя equip;
- нельзя trade;
- нельзя craft;
- нельзя уничтожить.

Auction listing становится единственным разрешённым operation owner для этого item.

---

# 24. Listing Creation

Pipeline:

```text
validate item
→ validate Auctionable
→ validate price
→ calculate listing fee
→ Spend Gold listing fee
→ move item to Auction Escrow
→ create ACTIVE listing
```

Операция имеет stable:

```text
CreateListingId
```

Повтор request не создаёт второй listing и не списывает fee повторно.

---

# 25. Listing Duration

Content profile поддерживает:

```text
12 hours
24 hours
48 hours
```

Recommended default UI:

```text
24h
48h
```

Конкретные варианты являются Economy/Auction Balance Profile.

---

# 26. Listing Fee

Listing fee — Gold sink.

Recommended current balance default:

```text
ListingFeeRate = 1%
MinimumListingFee = 1 Gold
```

Fee списывается при публикации и не возвращается при обычной отмене/истечении.

Числа data-driven.

---

# 27. Sale Tax

После успешной продажи:

```text
SaleTaxRate = 5%
```

Recommended balance default.

Seller payout:

```text
SellerPayout = TotalPrice - SaleTax
```

Tax является Gold sink.

---

# 28. Auction Purchase

Buyer request:

```text
BuyListing(ListingId, ExpectedListingVersion)
```

Server:

1. lock listing;
2. verify `ACTIVE`;
3. verify not self-buy;
4. verify price/version;
5. reserve/spend buyer Gold;
6. transfer item ownership;
7. create seller payout;
8. apply tax;
9. set SOLD/SETTLED;
10. emit result.

---

# 29. Double Buy Protection

Два покупателя не могут купить один listing.

Использовать:

- database transaction;
- row/version lock;
- unique settlement operation.

Только одна transition:

```text
ACTIVE → PURCHASE_PENDING
```

может победить.

---

# 30. Buyer Inventory Full

Auction purchase не должен теряться из-за полного inventory.

После успешной покупки:

- ownership переходит buyer;
- Item System может создать `PENDING_REWARD/PENDING_DELIVERY`;
- buyer забирает item после освобождения места.

Seller получает payout только после durable подтверждения ownership transfer/pending delivery.

---

# 31. Seller Payout

Payout идёт через Economy System.

```text
AuctionSettlementId
→ Gold CurrencyGrant
```

Повтор settlement не выдаёт Gold дважды.

Если wallet cap временно блокирует grant:

```text
Settlement = PAYOUT_PENDING
```

Listing не становится повторно продаваемым.

---

# 32. Expiration

Когда:

```text
ServerTime >= ExpiresAt
```

ACTIVE listing:

```text
→ EXPIRED
→ item return
```

Если inventory seller заполнен:

```text
RETURN_PENDING
```

Никакой mailbox не обязателен.

---

# 33. Cancellation

Seller может отменить ACTIVE listing.

Нельзя отменить:

```text
PURCHASE_PENDING
SOLD
SETTLED
```

Listing fee не возвращается.

Item возвращается через Item System.

---

# 34. Search Snapshot

Auction хранит read/search snapshot:

```text
ItemType
EquipmentSlot
Rarity
RequiredLevel
AllowedClassTags
KeyStats
ItemDefinitionId
Affix summary
TotalPrice
```

Search snapshot не является owner ItemDefinition.

Он служит индексом для поиска.

---

# 35. Auction Search

Минимальные filters:

```text
text/name
ItemType
EquipmentSlot
Rarity
RequiredLevel range
Class compatibility
Price range
Affix/stat tags
```

Sort:

```text
PRICE_ASC
PRICE_DESC
TIME_LEFT
NEWEST
REQUIRED_LEVEL
```

---

# 36. Self Purchase

Seller не может купить собственный listing.

Это не создаёт полезной экономики и усложняет fee/tax semantics.

---

# 37. Bound Items

Auction rejects:

```text
BindState = CHARACTER_BOUND
Auctionable = false
QuestProtected = true
```

BIND_ON_EQUIP item можно auction только пока он ещё UNBOUND.

---

# 38. Legendary / Unique

Legendary/Unique могут быть tradeable или bound — это ItemDefinition policy.

Rarity сама по себе не означает automatic bind.

Это позволяет создавать:

- tradeable world-drop legendary;
- bind-on-pickup boss legendary;
- unique crafted item.

---

# 39. Crafting Integration

Crafted item использует обычный ItemTradePolicy.

Crafting System не решает отдельно, можно ли выставить предмет.

---

# 40. Audit

Для каждой операции хранить:

```text
TradeSessionId / ListingId
seller
buyer
item instance
quantity
Gold
fee
tax
timestamps
result
```

История нужна для:

- bug investigation;
- dupe investigation;
- economy analytics;
- admin compensation.

---

# 41. Restart Recovery

Direct Trade:

- OPEN/SUSPENDED sessions восстанавливаются или expire;
- locks/reservations сверяются;
- incomplete commit не должен создавать half-trade.

Auction:

- ACTIVE listings сохраняются;
- expired timestamps пересчитываются;
- PURCHASE_PENDING settlement безопасно продолжается idempotently;
- SOLD listing не возвращается в ACTIVE.

---

# 42. Security Invariants

1. Client не меняет Item ownership.
2. Client не меняет Wallet.
3. Item нельзя одновременно иметь в Inventory и AuctionEscrow.
4. Item не может находиться в двух TradeSession.
5. Offer change сбрасывает confirmations.
6. Direct Trade commit атомарен.
7. Только Gold участвует в текущем player trade/auction.
8. Crystal non-tradeable.
9. Listing buy выполняется один раз.
10. Listing fee/tax idempotent.
11. Bound/QuestProtected item не обходит policy.
12. Full inventory не приводит к потере купленного auction item.

---

# 43. UI Contract

## Trade

Экран показывает:

- имя второго игрока;
- свои items;
- items второго игрока;
- Gold обеих сторон;
- status подтверждения;
- предупреждение при изменении offer;
- Confirm;
- Cancel.

## Auction

Вкладки:

```text
ПОИСК
МОИ ЛОТЫ
ВЫСТАВИТЬ
```

Карточка:

- icon;
- name;
- rarity;
- level;
- key stats;
- price;
- time left.

Frontend показывает server-provided final fee/tax preview.

---

# 44. Balance Profiles

```text
TradeBalanceProfile
AuctionFeeProfile
AuctionDurationProfile
AuctionSearchProfile
```

Fee/tax/durations не hardcode'ятся.

---

# 45. Events

```text
TradeCompleted
TradeCancelled
AuctionListingCreated
AuctionListingCancelled
AuctionListingExpired
AuctionItemSold
AuctionItemPurchased
AuctionSellerPaid
AuctionItemReturned
```

---

# 46. Invariants

Trade/Auction являются оркестраторами.

Authoritative ownership:

```text
Items → Item System
Currency → Economy System
Trade state → Trade System
Auction listing/escrow lifecycle → Auction System
```

Ни один модуль не дублирует owner state другого.
