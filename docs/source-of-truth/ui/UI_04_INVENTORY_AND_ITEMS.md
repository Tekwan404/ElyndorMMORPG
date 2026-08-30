# Elyndor — UI/UX Specification 04
# Inventory & Item Interaction

**Document:** docs/source-of-truth/ui/UI_04_INVENTORY_AND_ITEMS.md
**Status:** Approved foundation  
**Platform:** Telegram Mini App  
**Orientation:** Mobile Portrait First  
**Depends on:**  
- `docs/source-of-truth/ui/UI_01_GLOBAL_GAME_SHELL.md`
- `docs/source-of-truth/ui/UI_03_HERO.md`
- `docs/source-of-truth/gameplay/13_ITEM_EQUIPMENT_SYSTEM.md`
- `docs/source-of-truth/gameplay/14_LOOT_SYSTEM.md`
- `docs/source-of-truth/gameplay/26_CURRENCY_AND_ECONOMY_SYSTEM.md`
- `docs/source-of-truth/gameplay/27_TRADE_AND_AUCTION_SYSTEM.md`
- `docs/source-of-truth/gameplay/29_CRAFTING_AND_PROFESSION_SYSTEM.md`

---

# 1. Назначение

Inventory — часть Hero flow.

Он отвечает за:

- просмотр предметов;
- фильтрацию;
- сортировку;
- item inspect;
- экипировку;
- использование расходников;
- защиту предметов;
- массовый выбор;
- item comparison;
- NEW state;
- stack quantity;
- переход в merchant/auction/crafting context.

Inventory не является отдельной root-вкладкой нижней навигации.

Путь:

```text
ГЕРОЙ
→ ИНВЕНТАРЬ
```

---

# 2. Основная композиция

Mobile-first:

```text
[GLOBAL HUD]

[Hero Tabs]

ИНВЕНТАРЬ         31 / 40

[ ФИЛЬТРЫ ]
[ СОРТИРОВКА ]

[ item ][ item ][ item ][ item ]
[ item ][ item ][ item ][ item ]
[ item ][ item ][ item ][ item ]
...

[context / selection bar]
```

Базовая сетка:

```text
4 предмета в ряд
```

---

# 3. Почему 4 колонки

Для 360–430 CSS px:

- иконки остаются крупными;
- rarity frame читается;
- quantity badge читается;
- NEW/lock markers помещаются;
- удобнее tap;
- визуально соответствует MMORPG.

5 колонок допускаются только на заметно более широком viewport, но canonical mobile layout = 4.

---

# 4. Capacity

Current default:

```text
Inventory Capacity = 40
```

В UI:

```text
31 / 40
```

При приближении к лимиту:

```text
38 / 40
```

может получить warning accent.

Полный:

```text
40 / 40
ИНВЕНТАРЬ ЗАПОЛНЕН
```

---

# 5. Inventory Full UX

Если inventory full:

- новые обычные item grants не должны исчезать;
- pending reward использует Item/Loot rules;
- UI показывает clear warning;
- player получает CTA освободить место.

Пример:

```text
ИНВЕНТАРЬ ЗАПОЛНЕН

Освободите место, чтобы забрать награду.

[ ОТКРЫТЬ ИНВЕНТАРЬ ]
```

---

# 6. Filters

Основные:

```text
ВСЕ
ЭКИПИРОВКА
РАСХОДНИКИ
МАТЕРИАЛЫ
КВЕСТОВЫЕ
```

Дополнительные contextual filters могут появляться позже:

```text
WEAPONS
ARMOR
ACCESSORIES
CRAFTING
NEW
LOCKED
```

Но не перегружать first row.

---

# 7. Filter Presentation

Recommended:

```text
horizontal chip row
```

На маленьком телефоне:

```text
horizontal scroll
```

Активный filter визуально выделен.

---

# 8. Sorting

Sort button:

```text
СОРТИРОВКА
```

Options:

```text
НОВЫЕ
РЕДКОСТЬ
УРОВЕНЬ
ТИП
```

Дополнительно позже:

```text
NAME
GEAR SCORE
VENDOR VALUE
```

---

# 9. Default Sorting

Recommended default:

```text
NEW first
then ItemType / Rarity
```

Но конкретный default можно сделать persistent user preference.

---

# 10. Item Grid Cell

Каждая ячейка показывает:

```text
item icon
rarity frame
quantity, if stackable
NEW marker
lock marker
optional equipped marker
```

---

# 11. Rarity

Rarity:

```text
COMMON
UNCOMMON
RARE
EPIC
LEGENDARY
UNIQUE
```

Не полагаться только на цвет.

Использовать:

```text
frame style
name treatment
border detail
glow strength
rarity label in details
```

---

# 12. Stack Quantity

Stack item:

```text
x37
```

Badge располагается поверх item icon, обычно снизу справа.

Не выводить:

```text
x1
```

если это не нужно.

---

# 13. Equipped Indicator

Если ItemInstance экипирован:

```text
E
```

или маленький equipment glyph.

Но equipped item обычно не находится в ordinary Inventory state.

Если UI объединит inventory/equipment view, marker помогает.

Canonical ItemState owner остаётся Item System.

---

# 14. NEW State

Новый item:

```text
NEW
```

Legendary/Unique:

- stronger rarity glow;
- subtle pulse;
- special NEW frame.

NEW снимается после первого meaningful inspect/ack.

---

# 15. Legendary / Unique Discovery

При первом открытии такого предмета:

```text
LEGENDARY ITEM
```

может быть короткая premium reveal animation.

Отключается Reduced Motion / UI effects settings.

---

# 16. No Long Press

Long press не используется как обязательный interaction.

Причина:

- плохо discoverable;
- нестабильно в embedded browser/mobile context;
- конфликтует с browser/OS gestures;
- хуже accessibility.

Все ключевые действия доступны обычным tap.

---

# 17. Tap Item

Tap item:

```text
→ Item Bottom Sheet
```

Bottom sheet не требует перехода на новый full screen.

---

# 18. Item Bottom Sheet — Common Structure

```text
[ICON] ITEM NAME
RARITY

Item Type
Required Level
Slot, if equipment

Stats
Effects
Set
Binding
Trade state
Appearance

Context actions
```

---

# 19. Equipment Item Actions

Для equipment:

```text
[ СРАВНИТЬ ]
[ ЭКИПИРОВАТЬ ]
```

Если уже equipped:

```text
[ СНЯТЬ ]
```

---

# 20. Consumable Item Actions

Для usable consumable:

```text
[ ИСПОЛЬЗОВАТЬ ]
```

UI показывает:

```text
effect summary
cooldown category
combat restriction
quantity
```

---

# 21. Material Item Actions

Material:

```text
information only
```

Optional contexts:

```text
[ РЕЦЕПТЫ ]
```

позже, если Crafting UI это поддерживает.

Material нельзя "использовать" без соответствующего gameplay action.

---

# 22. Quest Item Actions

Quest item:

```text
QuestProtected = true
```

Нельзя:

```text
продать
удалить
auction
trade
craft consume вне quest rule
```

UI не показывает опасные CTA.

---

# 23. Delete Item

Для обычного allowed item:

```text
[ УДАЛИТЬ ]
```

только внутри secondary actions / danger zone.

Не ставить Delete рядом с Equip.

---

# 24. Delete Confirmation

Если item valuable:

```text
Удалить предмет?

Epic Staff
Это действие нельзя отменить.

[ ОТМЕНА ]
[ УДАЛИТЬ ]
```

Legendary/Unique требуют усиленное подтверждение.

---

# 25. Locked Item

Игрок может поставить:

```text
🔒
```

Lock защищает от accidental:

```text
delete
vendor sell
bulk action
craft consumption
```

если соответствующий gameplay policy это допускает.

Это user safety lock, не Trade transaction lock.

---

# 26. User Lock vs Transaction Lock

Не смешивать:

```text
UserProtected = true
```

и:

```text
TransactionLockId
```

UserProtected — preference игрока.

TransactionLock — authoritative operation state.

---

# 27. Favorite

Можно визуально объединить lock/favorite concept или разделить позже.

Current recommendation:

```text
🔒 Защитить
```

одной функции достаточно.

Не создавать сразу и star, и lock, и pin.

---

# 28. Vendor Selling

Продажа предметов **не происходит прямо из обычного Inventory**.

Player:

```text
City
→ Merchant
→ Sell
```

Merchant screen использует inventory selection mode.

Это сохраняет понятный world context.

---

# 29. Merchant Sell Context

Когда Inventory открыт как part of Merchant flow:

```text
SELL MODE
```

Можно выбирать только:

```text
VendorSellAllowed
unlocked
not QuestProtected
not equipped
not transaction locked
```

---

# 30. Auction Context

Auction Listing flow:

```text
City
→ Auction
→ Выставить
→ Inventory selection mode
```

Фильтр автоматически исключает:

```text
bound
QuestProtected
Auctionable=false
locked
equipped
```

---

# 31. Crafting Context

Crafting может открыть:

```text
ingredient inspection
```

Но recipes сами выбирают required materials.

Player не должен вручную перетаскивать 15 ingredients в slots, если recipe уже знает их.

---

# 32. Bulk Selection

Inventory поддерживает multi-select.

Activation:

```text
[ ВЫБРАТЬ ]
```

или contextual action.

Не long press.

---

# 33. Bulk Selection State

```text
ВЫБРАНО: 6

[Удалить]
[Снять выбор]
```

В Merchant Sell Mode:

```text
ВЫБРАНО: 6
Продажа: 342 Gold

[ ПРОДАТЬ ]
```

---

# 34. Bulk Safety

Bulk action автоматически исключает:

```text
QuestProtected
UserProtected
Equipped
TransactionLocked
AuctionEscrow
```

Если selected item becomes invalid:

```text
remove from selection
show reason
```

---

# 35. Bulk Delete

Bulk Delete разрешён только ordinary items.

Confirmation:

```text
Удалить 6 предметов?

Rare x1
Common x5

[ ОТМЕНА ]
[ УДАЛИТЬ ]
```

Legendary/Unique рекомендуется не включать в bulk delete вообще.

---

# 36. Item Comparison

Equipment item может быть compared с current equipped slot.

Пример:

```text
CURRENT                NEW

+18 Intellect       → +30
+6 Critical         → +10
+22 Stamina         → +14
```

Delta:

```text
+12 Intellect
+4 Critical
-8 Stamina
```

---

# 37. Compare Access

Compare доступен:

```text
Item Bottom Sheet
Filtered inventory after empty slot tap
Merchant item
Auction item
Loot result
```

Один reusable compare component.

---

# 38. Gear Score Comparison

Можно показывать:

```text
Gear Score
+24
```

Но это secondary summary.

Special effects и set changes должны оставаться видны.

---

# 39. Set Comparison

Если новый item изменяет set state:

```text
Iron Guardian
2/5 → 3/5

4p bonus still locked
```

или:

```text
4/5 → 3/5
4p bonus LOST
```

Это critical comparison.

---

# 40. Legendary Effect Comparison

Если меняется Legendary effect:

отдельный block:

```text
LEGENDARY EFFECT LOST
...
LEGENDARY EFFECT GAINED
...
```

---

# 41. Appearance Preview

Equipment item может иметь:

```text
[ ПРИМЕРИТЬ ]
```

Preview-only.

Не меняет:

```text
Stats
Equipment
ItemState
```

Полезно особенно для Legendary/Unique.

---

# 42. Filter From Equipment Slot

Hero:

```text
tap empty HEAD
```

Inventory opens:

```text
filter = HEAD
compatibility = current character
```

Header:

```text
ВЫБЕРИТЕ ШЛЕМ
```

Back:

```text
→ Hero Character
```

---

# 43. Compatible Filter

Frontend отображает server/read-model compatibility.

Не вычислять самостоятельно полный Equip validation.

Possible tags:

```text
CAN_EQUIP
LEVEL_TOO_LOW
WRONG_CLASS
WRONG_WEAPON_TYPE
LOCKED
```

---

# 44. Incompatible Items

В contextual slot selection рекомендуется:

```text
hide incompatible by default
```

Optional toggle:

```text
Показать всё
```

---

# 45. Search

Text search можно добавить позже.

Для 40-slot inventory search не является обязательной первой функцией.

Фильтров и сортировки достаточно.

---

# 46. Inventory Categories

Current categories:

## Equipment

```text
Weapon
Armor
Accessory
```

## Consumables

```text
Potion
Food
Drink
Elixir
other usable item
```

## Materials

```text
Crafting Material
Reagent
Component
```

## Quest

```text
QuestProtected
```

---

# 47. Item Details — Trade State

Показывать понятную строку:

```text
Можно передать
Привязывается при экипировке
Привязан
Нельзя выставить на аукцион
```

Не показывать raw enum:

```text
BIND_ON_EQUIP
```

игроку.

---

# 48. Item Details — Vendor

Если item has value:

```text
Цена продажи торговцу:
120 Gold
```

Но final merchant price authoritative и может зависеть от Merchant/Economy profile.

В ordinary Inventory это preview.

---

# 49. Item Details — Source

Optional lore/debug-facing:

```text
Получено:
Древние шахты
```

Не обязательно показывать technical SourceId.

---

# 50. Item Details — Appearance

Если есть unique visual:

```text
Уникальный внешний вид
```

Optional:

```text
[ ПРИМЕРИТЬ ]
```

---

# 51. Item Details — Crafter

Crafted item позже может показывать:

```text
Изготовил: PlayerName
```

presentation-only.

---

# 52. Sorting Persistence

User sort/filter preference может сохраняться в session.

При contextual flow:

```text
empty slot
merchant sell
auction create
```

context filter имеет приоритет над обычной сохранённой сортировкой.

---

# 53. New Item Priority

Default inventory open:

NEW items можно поднять выше остальных.

Но пользователь может выбрать другую сортировку.

---

# 54. Inventory During Travel

Inventory доступен во время Travel.

Разрешены:

```text
inspect
sort/filter
equip
use allowed non-local items
```

Если consumable требует local/combat state — server validation решает.

---

# 55. Inventory During Combat

Bottom navigation скрыта.

Обычный Inventory screen не открывается.

Combat-specific consumables доступны только через Combat UI, если Ability/Item rules разрешают.

---

# 56. Inventory During Dungeon

Может быть доступен вне active combat.

Но Equipment change зависит от Item/Activity policy.

Current default допускает inventory inspect вне Combat.

---

# 57. Pending Rewards

Если reward не помещается:

Inventory может иметь отдельный banner:

```text
НЕПОЛУЧЕННЫЕ НАГРАДЫ: 2

[ ОСВОБОДИТЬ МЕСТО ]
```

После освобождения Item/Loot owner завершает claim.

---

# 58. Pending Auction Delivery

Купленный auction item при full inventory:

```text
Pending Delivery
```

может появиться в same pending-reward banner.

Не использовать fake mailbox dependency.

---

# 59. Item Count

Inventory capacity считается по Item System rules:

- stack занимает slot;
- stack merge учитывается;
- equipped items не обязательно занимают inventory slots в current state.

UI показывает authoritative count.

---

# 60. Stack Merge

Stack merge происходит автоматически по Item System rules.

UI не требует drag-and-drop.

---

# 61. Drag & Drop

Drag-and-drop не является обязательной механикой.

На mobile он создаёт:

- accidental moves;
- touch conflicts;
- implementation complexity.

Grid order может быть automatic/sorted.

---

# 62. Manual Reordering

Не нужно в первой версии.

Inventory — gameplay collection, а не desktop bag puzzle.

---

# 63. Item Use

Consumable flow:

```text
tap item
→ details
→ Использовать
→ server validation
→ effect/result
→ quantity update
```

---

# 64. Use Quantity

Stack consumable:

по умолчанию `Использовать` = 1.

Если future item supports bulk use:

separate quantity selector.

---

# 65. Cooldown State

Consumable on cooldown:

```text
[ ИСПОЛЬЗОВАТЬ ] disabled
Откат: 00:32
```

---

# 66. Full-screen Rare Inspect

Legendary/Unique item details могут иметь optional expanded view:

```text
[ ПОДРОБНЕЕ ]
```

для:

- large art;
- lore;
- appearance;
- full effect description.

Не делать full screen обязательным для каждого Common material.

---

# 67. Notifications

Inventory tab может получить badge:

```text
NEW
```

или count:

```text
3
```

если появились новые items.

---

# 68. Loot Result → Inventory

Victory Screen:

```text
Rare Sword
```

tap:

```text
→ Item Details
```

Continue:

```text
→ Location
```

Item уже находится в authoritative inventory/pending state.

---

# 69. Rarity Reveal

Recommended:

```text
Rare → subtle
Epic → stronger
Legendary/Unique → premium
```

Не заставлять игрока смотреть длинную animation после каждого drop.

---

# 70. Loading

Inventory load:

```text
grid skeleton
capacity skeleton
```

Не блокировать entire Hero.

---

# 71. Incremental Updates

При:

```text
ItemGranted
ItemRemoved
ItemEquipped
ItemUnequipped
StackChanged
```

обновлять affected cells, а не reload entire inventory.

---

# 72. Reconnect

После reconnect:

```text
authoritative inventory snapshot
→ reconcile ItemInstanceId
```

Не доверять stale local quantity.

---

# 73. Transaction Locks

Если item участвует в:

```text
Trade
Auction
Crafting operation
```

cell показывает contextual lock marker.

Tap объясняет:

```text
Предмет используется в обмене
```

или другой reason.

---

# 74. Quest Item Visual

Quest item имеет отдельный small quest glyph.

Не обязательно отдельная rarity.

---

# 75. User-Protected Item Visual

```text
🔒
```

виден прямо на cell.

---

# 76. Accessibility

- tap area 44–48 CSS px минимум;
- quantity читается;
- rarity не только color;
- text contrast высокий;
- destructive action визуально отделён.

---

# 77. Visual Reference

Основной:

```text
reference/UI_03-04_HERO_INVENTORY.png
```

Дополнительно:

```text
reference/UI_01-02_GLOBAL_SHELL_WORLD.png
reference/UI_03-04_HERO_INVENTORY.png
```

Берём:

- насыщенные item icons;
- аккуратную сетку;
- rarity frames;
- dark fantasy panels;
- gold/arcane detail.

Не копируем случайные AI-generated labels.

---

# 78. Approved Decisions

Зафиксировано:

1. Canonical mobile grid = 4 items per row.
2. Current capacity = 40.
3. Capacity отображается `used / max`.
4. Filters:
   - All;
   - Equipment;
   - Consumables;
   - Materials;
   - Quest.
5. Sort:
   - New;
   - Rarity;
   - Level;
   - Type.
6. Tap opens bottom sheet.
7. Long press не является interaction.
8. Equipment: Compare / Equip.
9. Consumables: Use.
10. Materials: info-only by default.
11. Selling выполняется через Merchant.
12. User item protection `🔒` поддерживается.
13. Stack quantity видна на icon.
14. Multi-select поддерживается.
15. Legendary/Unique NEW state имеет заметный visual.
16. Quest items нельзя вручную продавать/удалять.
17. Drag-and-drop/manual bag ordering не нужны в первой версии.
18. Empty equipment slot opens filtered Inventory.

---

# 79. Next Specification

Следующий:

```text
docs/source-of-truth/ui/UI_05_CHARACTER_STATS.md
```

После него:

```text
docs/source-of-truth/ui/UI_06_TALENTS.md
docs/source-of-truth/ui/UI_07_COMPANION.md
```
