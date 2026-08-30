# Elyndor — Currency & Economy System Specification

**Document:** docs/source-of-truth/gameplay/26_CURRENCY_AND_ECONOMY_SYSTEM.md
**System:** Currency / Wallet / Economy / NPC Merchants  
**Status:** Foundation / Source of Truth  
**Version:** 1.0

---

# 1. Назначение

Currency & Economy System является единственным владельцем денежных балансов и экономических операций Elyndor.

Система отвечает за:

- CurrencyDefinition;
- Wallet;
- CurrencyGrant;
- CurrencySpend;
- CurrencyTransfer;
- reservations;
- immutable ledger;
- NPC merchant buy/sell;
- economy sources/sinks;
- idempotency;
- audit;
- inflation telemetry.

Система **не** отвечает за:

- Item stats;
- Loot RNG;
- Quest completion;
- Auction listing lifecycle;
- Crafting recipes;
- Telegram payment verification;
- реальные банковские платежи;
- class balance.

Другие системы могут попросить:

```text
Grant Currency
Spend Currency
Reserve Currency
Transfer Currency
```

но только Economy System изменяет authoritative balance.

---

# 2. Главный принцип

```text
Gameplay Event
→ authoritative owner confirms result
→ Currency Operation
→ Economy validates
→ ledger entry
→ balance update
→ event
```

Клиент никогда не сообщает:

```text
"у меня теперь 500 Gold"
"я уже заплатил"
"верни мне комиссию"
```

Клиент может только запросить действие.

---

# 3. Current Currencies

Текущий базовый набор:

```text
GOLD
CRYSTAL
```

## GOLD

Основная игровая валюта.

Назначение:

- NPC merchants;
- player trade;
- auction;
- crafting fees;
- services;
- talent respec fee, если Economy Profile это включает.

`GOLD`:

```text
Tradeable = true
AuctionCurrency = true
VendorCurrency = true
CanBeEarnedInGame = true
```

## CRYSTAL

Премиальная / редкая валюта.

`CRYSTAL`:

```text
Tradeable = false
AuctionCurrency = false
VendorCurrency = limited
CanBeEarnedInGame = true
CanBePurchasedExternally = true
```

Кристаллы могут:

- редко выдаваться игровыми системами;
- использоваться для cosmetics;
- использоваться для appearance/transmog-related content;
- использоваться для convenience;
- использоваться для некоторых ресурсов, если эти ресурсы доступны обычной игрой.

Кристаллы не должны становиться обязательным эксклюзивным источником боевой силы.

Название `CRYSTAL` является content-facing и может быть переименовано без изменения системной модели.

---

# 4. Telegram Stars

Telegram Stars **не являются CurrencyId Elyndor**.

```text
Telegram Stars
→ external payment confirmation
→ Payment Integration
→ idempotent CRYSTAL CurrencyGrant
```

Economy System не валидирует Telegram payment.

Реальная payment integration проектируется отдельно.

---

# 5. Currency Definition

```text
CurrencyDefinition
├── CurrencyId
├── DisplayName
├── IconId
├── MaxBalance
├── Tradeable
├── AuctionCurrency
├── VendorCurrency
├── CanBeEarnedInGame
├── CanBePurchasedExternally
├── DisplayPrecision
├── Version
└── Metadata
```

Для Gold/Crystal:

```text
DisplayPrecision = 0
```

Дробные валютные значения игроку не используются.

---

# 6. Wallet

Текущая модель:

```text
CharacterWallet
├── CharacterId
├── Balances
├── WalletVersion
└── UpdatedAt
```

Один персонаж имеет один wallet.

Баланс каждой валюты:

```text
CurrencyBalance
├── CurrencyId
├── Amount
└── Version
```

Если позже появится полноценный Account System с несколькими персонажами, account-wide currencies могут быть добавлены отдельным owner scope без изменения Gold-модели.

---

# 7. Currency Amount

Системно использовать integer amount.

```text
Amount >= 0
```

Не использовать floating point для денег.

Баланс:

```text
0 <= Balance <= CurrencyDefinition.MaxBalance
```

---

# 8. Immutable Ledger

Каждое изменение валюты создаёт ledger entry.

```text
CurrencyLedgerEntry
├── LedgerEntryId
├── CharacterId
├── CurrencyId
├── OperationType
├── AmountDelta
├── BalanceBefore
├── BalanceAfter
├── SourceType
├── SourceId
├── IdempotencyKey
├── CreatedAt
└── Metadata
```

Ledger entry после commit не редактируется.

Коррекция выполняется новой компенсирующей операцией.

---

# 9. Operation Types

```text
GRANT
SPEND
TRANSFER_IN
TRANSFER_OUT
RESERVE
RELEASE_RESERVATION
MERCHANT_BUY
MERCHANT_SELL
AUCTION_FEE
AUCTION_SALE
AUCTION_TAX
CRAFTING_FEE
SERVICE_FEE
ADMIN_ADJUSTMENT
EXTERNAL_PURCHASE
```

Это audit classification, а не отдельные wallet механики.

---

# 10. CurrencyGrant

```text
CurrencyGrant
├── GrantId
├── CharacterId
├── CurrencyId
├── Amount
├── SourceType
├── SourceId
└── Metadata
```

Правила:

- `Amount > 0`;
- CurrencyId существует;
- GrantId уникален;
- повтор GrantId не выдаёт деньги второй раз;
- MaxBalance не превышается.

Если MaxBalance достигнут, policy определяется CurrencyDefinition:

```text
REJECT
CLAMP_AND_REPORT
PENDING
```

Для Gold рекомендуется `REJECT/PENDING`, чтобы reward не исчезал молча.

---

# 11. CurrencySpend

```text
CurrencySpend
├── SpendId
├── CharacterId
├── CurrencyId
├── Amount
├── Reason
├── SourceId
└── Metadata
```

Проверки:

```text
Amount > 0
AvailableBalance >= Amount
Currency exists
operation allowed
SpendId not already processed
```

Баланс не может уйти в минус.

---

# 12. Available vs Reserved Balance

Для операций Trade/Auction может использоваться reservation.

```text
AvailableBalance = TotalBalance - ReservedAmount
```

```text
CurrencyReservation
├── ReservationId
├── CharacterId
├── CurrencyId
├── Amount
├── OwnerOperationType
├── OwnerOperationId
├── CreatedAt
├── ExpiresAt, optional
└── State
```

State:

```text
ACTIVE
CONSUMED
RELEASED
EXPIRED
```

Reservation не является новым количеством денег.

---

# 13. Currency Transfer

Player-to-player transfer разрешается только через авторитетную систему-оркестратор.

Текущий consumer:

```text
Trade System
```

Trade вызывает:

```text
Economy.Transfer(...)
```

Economy проверяет:

- CurrencyDefinition.Tradeable;
- sender balance;
- reservation;
- recipient;
- idempotency.

`CRYSTAL` не передаётся между игроками.

---

# 14. Economy Sources

Gold может появляться из:

```text
MONSTER_REWARD
QUEST_REWARD
BOSS_REWARD
WORLD_EVENT_REWARD
DUNGEON_REWARD
AFK_REWARD
MERCHANT_SELL
SCRIPTED_REWARD
ADMIN_DEBUG
```

Auction sale и player trade **не создают Gold**.

Они только перераспределяют уже существующий Gold.

---

# 15. Economy Sinks

Основные Gold sinks:

```text
NPC_MERCHANT_PURCHASE
CRAFTING_FEE
AUCTION_LISTING_FEE
AUCTION_SALE_TAX
TALENT_RESPEC_SERVICE
CITY_SERVICE
```

Не вводить искусственный sink без gameplay причины.

Durability/Repair не является текущим sink, пока durability не утверждена отдельной системой.

---

# 16. Loot Integration

Loot System может сформировать currency reward.

```text
RewardSource
→ Loot/Reward Resolver
→ CurrencyGrant
→ Economy System
```

Loot не мутирует Wallet.

GrantId выводится из:

```text
RewardResolutionId
+ RecipientCharacterId
+ CurrencyId
+ RewardEntryId
```

---

# 17. Quest Integration

QuestRewardProfile может содержать CurrencyRewardEntry.

```text
Quest completion
→ stable CompletionInstanceId
→ CurrencyGrant
```

Повторный Claim не выдаёт Gold второй раз.

---

# 18. Boss / World Event Integration

Boss System не выдаёт валюту напрямую.

```text
BossCompletionId
→ RewardProfile
→ eligible participant
→ CurrencyGrant
```

Eligibility определяется Boss/Loot ParticipationPolicy.

---

# 19. Dungeon Integration

Dungeon может иметь:

- encounter reward;
- boss reward;
- completion reward.

Currency grant обязан использовать:

```text
DungeonCompletionId / EncounterCompletionId
```

для idempotency.

---

# 20. AFK Integration

AFK Reward Profile может включать Gold.

AFK currency rules:

- reward rate data-driven;
- AFK не должен быть лучшим Gold/hour методом;
- CRYSTAL не является обычной AFK reward;
- reward использует stable AfkSessionId.

---

# 21. Merchant System

NPC merchant является частью Economy domain orchestration.

```text
MerchantDefinition
├── MerchantId
├── DisplayName
├── LocationId
├── CatalogId
├── BuyPolicyId
├── SellPolicyId
├── CurrencyId
├── Version
└── Metadata
```

---

# 22. Merchant Catalog

```text
MerchantCatalog
├── CatalogId
├── Offers[]
├── Version
└── Metadata
```

```text
MerchantOffer
├── OfferId
├── ItemDefinitionId
├── Quantity
├── Price
├── CurrencyId
├── RequiredLevel, optional
├── RequiredQuestFlag, optional
├── StockPolicy
├── AvailableFrom, optional
├── AvailableUntil, optional
└── Metadata
```

---

# 23. Merchant Purchase

Pipeline:

```text
Player selects offer
→ Merchant validates availability
→ Economy Spend
→ ItemGrant
→ purchase confirmed
```

Операция должна иметь:

```text
MerchantPurchaseId
```

Если CurrencySpend успешен, а ItemGrant временно не помещается:

- item становится pending;
- Gold не списывается второй раз при retry;
- покупка не теряется.

---

# 24. Selling Items to NPC

ItemDefinition может иметь:

```text
VendorValueProfileId
```

или explicit vendor value.

Продажа:

```text
Validate ItemInstance
→ validate VendorSellAllowed
→ remove item atomically
→ Gold CurrencyGrant
```

Нельзя продавать:

- equipped item без unequip;
- QuestProtected item;
- bound item с VendorSellAllowed = false;
- transaction-locked item;
- AuctionEscrow item.

---

# 25. Vendor Price

Цена покупки и цена продажи не обязаны быть симметричными.

Balance profile:

```text
MerchantPriceProfile
├── BuyPriceMultiplier
├── SellPriceMultiplier
├── MinimumPrice
└── RoundingPolicy
```

Текущий recommended default:

```text
SellPrice ≈ 20–30% базовой NPC BuyPrice
```

Это балансный default, не hardcoded formula.

---

# 26. Buyback

Buyback не обязателен для Item ownership модели, но поддерживается.

```text
MerchantBuybackEntry
├── CharacterId
├── ItemSnapshot/ItemInstanceId
├── SoldAt
├── BuybackPrice
├── ExpiresAt
└── State
```

Для первой реализации можно оставить небольшой список последних продаж.

Buyback price обычно равен полученному SellPrice.

---

# 27. Premium Economy Guardrails

`CRYSTAL` не должен:

- передаваться игроку напрямую;
- использоваться на Auction;
- быть единственным способом получить обязательный combat item;
- покупать эксклюзивный permanent stat, недоступный через игру;
- обходить server-authoritative progression.

Допустимы:

- cosmetics;
- appearance;
- future housing cosmetics;
- convenience;
- ограниченные ускорители;
- ресурсы, которые также добываются игровым путём.

Конкретные monetization offers являются content/business layer и не hardcode'ятся Economy System.

---

# 28. Talent Respec Integration

Talent System определяет возможность respec.

Economy может определить стоимость услуги.

```text
ValidateRespec
→ Spend Gold
→ TalentRespec transaction
```

Если Talent operation не выполняется:

- Spend должен быть компенсирован либо transaction не должен commit'иться.

Loadout switching между двумя сохранёнными билдами сам по себе не обязан стоить валюту.

---

# 29. Economy Transaction Boundary

Операции с item + currency должны использовать application transaction/outbox.

Пример Merchant Buy:

```text
1. lock operation id
2. validate Gold
3. persist spend
4. persist ItemGrant/pending state
5. commit
6. publish events
```

Не использовать distributed eventual consistency там, где обе сущности находятся в одной PostgreSQL базе и могут быть изменены одной transaction.

---

# 30. Concurrency

Wallet mutation использует optimistic/pessimistic concurrency policy.

Два одновременных spend не могут оба увидеть один и тот же доступный balance и уйти в минус.

Обязательны:

- WalletVersion/concurrency token;
- transaction;
- retry only for safe conflicts.

---

# 31. Idempotency

Все durable операции имеют stable key:

```text
CurrencyGrantId
CurrencySpendId
CurrencyTransferId
MerchantPurchaseId
MerchantSellId
ReservationId
```

Повтор HTTP/SignalR request не создаёт повторное движение денег.

---

# 32. Restart Recovery

После server restart:

- balances загружаются из persistent state;
- ledger сохраняется;
- ACTIVE reservations проверяются по ExpiresAt;
- committed transaction не повторяется;
- pending reward grants безопасно продолжаются;
- merchant purchases не дублируются.

---

# 33. Events

Economy System эмитит:

```text
CurrencyGranted
CurrencySpent
CurrencyTransferred
CurrencyReservationCreated
CurrencyReservationReleased
WalletBalanceChanged
MerchantPurchaseCompleted
MerchantSaleCompleted
EconomyOperationRejected
```

Event payload содержит stable operation id.

---

# 34. Analytics

Минимально собирать:

```text
GoldMinted
GoldBurned
GoldTransferred
GoldMedianBalance
GoldP95Balance
MerchantSpend
MerchantSellIncome
AuctionFees
AuctionTaxes
CraftingFees
CrystalEarnedInGame
CrystalGrantedExternally
```

Главный показатель инфляции:

```text
NetGoldCreation = GoldMinted - GoldBurned
```

Trade/Auction transfer volume не считать minting.

---

# 35. Admin Tools

Admin operation:

```text
AdjustCurrency
```

требует:

- privileged role;
- CharacterId;
- CurrencyId;
- signed reason/comment;
- stable AdminOperationId;
- immutable audit entry.

Никакого прямого UPDATE wallet balance из admin UI.

---

# 36. Balance Profiles

Числа хранятся data-driven:

```text
CurrencyBalanceProfile
MerchantPriceProfile
EconomySourceProfile
EconomySinkProfile
PremiumOfferProfile
```

Никакие economy multipliers не должны быть разбросаны по C#.

---

# 37. UI Contract

Wallet summary:

```text
Gold
Crystal
```

Merchant UI получает:

- offers;
- player balance;
- canBuy;
- buy price;
- sell price;
- requirements;
- item comparison context.

Frontend не рассчитывает финальную цену как authoritative result.

---

# 38. Invariants

1. Wallet balance изменяет только Economy System.
2. Currency amount — integer.
3. Balance не может быть отрицательным.
4. Один idempotency key не создаёт вторую операцию.
5. Gold tradeable; Crystal non-tradeable.
6. Auction использует только разрешённую CurrencyDefinition.
7. Player trade не создаёт новую валюту.
8. Merchant sale не создаёт item duplication.
9. Loot/Quest/Boss/Dungeon/AFK не мутируют wallet напрямую.
10. Real-money payment не подтверждается Economy System.
11. Admin adjustment всегда audit'ится.
12. Economy numbers являются Balance Profiles.
