<script setup lang="ts">
import MoneyAmount from '@/ui/components/MoneyAmount.vue'
import { canAffordMoney } from '@/shared/money'
import { computed, onBeforeUnmount, ref, watch } from 'vue'

import {
  EQUIPMENT_SLOT_TO_EQUIPPED_KEY,
  type InventoryItem,
  type MerchantBuybackItem,
  type MerchantItem,
  type MerchantSnapshot,
} from '@/api/contracts'
import { gameArt } from '@/assets/gameArt'
import { consumableActionLabel } from '@/game/items/consumablePresentation'
import ItemIcon from '@/game/items/components/ItemIcon.vue'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UIModal } from '@/ui/components'
import IconGenerator from '@/ui/icons/IconGenerator.vue'

const props = defineProps<{ open: boolean }>()
const emit = defineEmits<{ close: [] }>()
const MERCHANT_ID = 'MARCUS_SUPPLIES'

type MerchantFilter = 'all' | MerchantItem['type']
type SortMode = 'recommended' | 'price-asc' | 'price-desc' | 'rarity'
type EquipmentSubcategory = 'all' | 'weapon' | 'armor' | 'accessory'
type ActiveTab = 'buy' | 'sell' | 'buyback'
type StatKey =
  | 'strength' | 'agility' | 'intellect' | 'stamina'
  | 'maxHp' | 'attackPower' | 'spellPower' | 'criticalChance'
  | 'armor' | 'magicResistance' | 'dodge' | 'attackSpeed'

const session = useGameSessionStore()
const merchant = ref<MerchantSnapshot | null>(null)
const loading = ref(false)
const activeTab = ref<ActiveTab>('buy')
const selectedOfferId = ref<string | null>(null)
const searchQuery = ref('')
const activeFilter = ref<MerchantFilter>('all')
const equipmentSubcategory = ref<EquipmentSubcategory>('all')
const onlyAffordable = ref(false)
const onlyForClass = ref(false)
const sortMode = ref<SortMode>('recommended')
const selectedQuantity = ref(1)
const selectedSellIds = ref<string[]>([])
const reaction = ref('Осмотрись. Хорошее снаряжение само себя не купит.')
const purchasePulseId = ref<string | null>(null)
let pulseTimer: ReturnType<typeof setTimeout> | null = null

const merchantFilters = ['all', 'Consumable', 'Equipment', 'Material'] as const
const buyPending = computed(() => session.isMutationPending('merchant:buy'))
const sellPending = computed(() => session.isMutationDomainPending('merchant:sell-'))
const buybackPending = computed(() => session.isMutationPending('merchant:buyback'))
const characterClassId = computed(() => session.snapshot?.character?.classId ?? '')
const characterLevel = computed(() => session.snapshot?.character?.level ?? 1)
const walletGold = computed(() => Number(merchant.value?.gold ?? session.snapshot?.character?.gold ?? 0))
const buybackItems = computed(() => merchant.value?.buybackItems ?? [])

const sellableItems = computed(() => session.snapshot?.character?.inventory.items
  .filter(item => !item.equippedSlot && !item.isLocked && item.sellPriceGold > 0) ?? [])
const protectedItemsCount = computed(() => session.snapshot?.character?.inventory.items
  .filter(item => !item.equippedSlot && item.isLocked && item.sellPriceGold > 0).length ?? 0)
const affordableOfferCount = computed(() =>
  merchant.value?.items.filter(item => isAffordable(item)).length ?? 0,
)

const visibleOffers = computed(() => {
  const query = searchQuery.value.trim().toLocaleLowerCase()
  const offers = merchant.value?.items.filter(item => {
    const matchesType = activeFilter.value === 'all' || item.type === activeFilter.value
    const matchesQuery = !query
      || item.name.toLocaleLowerCase().includes(query)
      || item.description.toLocaleLowerCase().includes(query)
    const matchesBudget = !onlyAffordable.value || isAffordable(item)
    const matchesClass = !onlyForClass.value || item.type !== 'Equipment' || isForCurrentClass(item)
    const matchesEquipmentGroup = activeFilter.value !== 'Equipment'
      || equipmentSubcategory.value === 'all'
      || equipmentGroup(item) === equipmentSubcategory.value
    return matchesType && matchesQuery && matchesBudget && matchesClass && matchesEquipmentGroup
  }) ?? []

  if (sortMode.value === 'price-asc') return [...offers].sort((a, b) => a.buyPriceGold - b.buyPriceGold)
  if (sortMode.value === 'price-desc') return [...offers].sort((a, b) => b.buyPriceGold - a.buyPriceGold)
  if (sortMode.value === 'rarity') return [...offers].sort((a, b) => rarityRank(b.rarity) - rarityRank(a.rarity))
  return [...offers].sort((a, b) => recommendationScore(b) - recommendationScore(a))
})

const selectedOffer = computed(() =>
  visibleOffers.value.find(item => item.definitionId === selectedOfferId.value)
    ?? visibleOffers.value[0]
    ?? null,
)

const recommendedOffers = computed(() =>
  [...(merchant.value?.items ?? [])]
    .filter(item => isAffordable(item))
    .sort((a, b) => recommendationScore(b) - recommendationScore(a))
    .slice(0, 3),
)

const selectedOfferMaxQuantity = computed(() => {
  if (!selectedOffer.value) return 1
  return selectedOffer.value.type === 'Equipment' ? 1 : 20
})
const selectedTotalPrice = computed(() =>
  (selectedOffer.value?.buyPriceGold ?? 0) * selectedQuantity.value,
)
const selectedCanAfford = computed(() =>
  !!selectedOffer.value && canAffordMoney(merchant.value?.gold ?? 0, selectedTotalPrice.value),
)
const maxAffordableQuantity = computed(() => {
  if (!selectedOffer.value || selectedOffer.value.buyPriceGold <= 0) return 1
  return Math.max(
    1,
    Math.min(
      selectedOfferMaxQuantity.value,
      Math.floor(walletGold.value / selectedOffer.value.buyPriceGold),
    ),
  )
})
const selectedComparisonRows = computed(() =>
  selectedOffer.value ? comparisonRows(selectedOffer.value) : [],
)
const selectedSellItems = computed(() =>
  sellableItems.value.filter(item => selectedSellIds.value.includes(item.id)),
)
const selectedSellValue = computed(() =>
  selectedSellItems.value.reduce((sum, item) => sum + item.sellPriceGold * item.quantity, 0),
)

watch(() => props.open, open => {
  if (!open) return
  activeTab.value = 'buy'
  searchQuery.value = ''
  activeFilter.value = 'all'
  equipmentSubcategory.value = 'all'
  onlyAffordable.value = false
  onlyForClass.value = false
  sortMode.value = 'recommended'
  selectedQuantity.value = 1
  selectedSellIds.value = []
  reaction.value = 'Осмотрись. Хорошее снаряжение само себя не купит.'
  void loadMerchant()
}, { immediate: true })

watch(selectedOfferId, () => {
  selectedQuantity.value = 1
})

onBeforeUnmount(() => {
  if (pulseTimer) clearTimeout(pulseTimer)
})

async function loadMerchant(): Promise<void> {
  loading.value = true
  try {
    merchant.value = await session.getMerchant(MERCHANT_ID)
    if (
      merchant.value
      && !merchant.value.items.some(item => item.definitionId === selectedOfferId.value)
    ) {
      selectedOfferId.value = merchant.value.items[0]?.definitionId ?? null
    }
  } finally {
    loading.value = false
  }
}

function selectOffer(item: MerchantItem): void {
  selectedOfferId.value = item.definitionId
}

function filterLabel(filter: MerchantFilter): string {
  if (filter === 'Equipment') return 'Снаряжение'
  if (filter === 'Consumable') return 'Расходники'
  if (filter === 'Material') return 'Материалы'
  return 'Всё'
}

function itemTypeLabel(item: MerchantItem): string {
  if (item.type === 'Consumable') return 'Расходник'
  if (item.type === 'Equipment') return 'Снаряжение'
  return 'Материал'
}

function rarityLabel(item: { rarity: MerchantItem['rarity'] }): string {
  if (item.rarity === 'Uncommon') return 'Необычный'
  if (item.rarity === 'Rare') return 'Редкий'
  if (item.rarity === 'Epic') return 'Эпический'
  if (item.rarity === 'Legendary') return 'Легендарный'
  if (item.rarity === 'Unique') return 'Уникальный'
  return 'Обычный'
}

function rarityRank(rarity: MerchantItem['rarity']): number {
  if (rarity === 'Unique') return 6
  if (rarity === 'Legendary') return 5
  if (rarity === 'Epic') return 4
  if (rarity === 'Rare') return 3
  if (rarity === 'Uncommon') return 2
  return 1
}

function isAffordable(item: MerchantItem): boolean {
  return canAffordMoney(merchant.value?.gold ?? 0, item.buyPriceGold)
}

function inventoryCount(item: MerchantItem): number {
  return session.snapshot?.character?.inventory.items
    .filter(entry => entry.definitionId === item.definitionId)
    .reduce((sum, entry) => sum + entry.quantity, 0) ?? 0
}

function classPrimaryStat(): 'strength' | 'agility' | 'intellect' | null {
  if (characterClassId.value === 'WARRIOR') return 'strength'
  if (characterClassId.value === 'ARCHER') return 'agility'
  if (characterClassId.value === 'MAGE') return 'intellect'
  return null
}

function isForCurrentClass(item: MerchantItem): boolean {
  if (item.type !== 'Equipment' || !item.stats) return true
  const key = classPrimaryStat()
  if (!key) return true
  const primary = {
    strength: item.stats.strength,
    agility: item.stats.agility,
    intellect: item.stats.intellect,
  }
  const peak = Math.max(primary.strength, primary.agility, primary.intellect)
  return peak <= 0 || primary[key] >= peak
}

function equipmentGroup(item: MerchantItem): EquipmentSubcategory {
  if (item.weaponCategory || item.slot === 'MainHand' || item.slot === 'OffHand') return 'weapon'
  if (item.slot === 'Amulet' || item.slot === 'Ring1' || item.slot === 'Ring2') return 'accessory'
  return 'armor'
}

function equippedForOffer(item: MerchantItem): InventoryItem | null {
  if (!item.slot) return null
  const equipped = session.snapshot?.character?.inventory.equipped
  if (!equipped) return null
  const key = EQUIPMENT_SLOT_TO_EQUIPPED_KEY[item.slot]
  return equipped[key] ?? null
}

const statDefinitions: ReadonlyArray<{ key: StatKey; label: string }> = [
  { key: 'strength', label: 'Сила' },
  { key: 'agility', label: 'Ловкость' },
  { key: 'intellect', label: 'Интеллект' },
  { key: 'stamina', label: 'Выносливость' },
  { key: 'maxHp', label: 'Здоровье' },
  { key: 'attackPower', label: 'Сила атаки' },
  { key: 'spellPower', label: 'Сила заклинаний' },
  { key: 'armor', label: 'Броня' },
  { key: 'magicResistance', label: 'Сопротивление' },
  { key: 'criticalChance', label: 'Крит. шанс' },
  { key: 'dodge', label: 'Уклонение' },
  { key: 'attackSpeed', label: 'Скорость атаки' },
]

function comparisonRows(item: MerchantItem) {
  if (item.type !== 'Equipment' || !item.stats) return []
  const equipped = equippedForOffer(item)
  return statDefinitions
    .map(({ key, label }) => {
      const next = Number(item.stats?.[key] ?? 0)
      const current = Number(equipped?.stats?.[key] ?? 0)
      return { key, label, next, current, delta: next - current }
    })
    .filter(row => row.next !== 0 || row.current !== 0)
}

function hasPositiveComparison(item: MerchantItem): boolean {
  return comparisonRows(item).some(row => row.delta > 0)
}

function recommendationScore(item: MerchantItem): number {
  let score = 0
  if (isAffordable(item)) score += 10
  if (item.type === 'Consumable' && item.definitionId === 'SMALL_HEALING_POTION' && inventoryCount(item) < 5) score += 40
  if (item.type === 'Equipment' && isForCurrentClass(item)) score += 20
  if (item.type === 'Equipment' && hasPositiveComparison(item)) score += 25
  if ((item.requiredLevel ?? 1) <= characterLevel.value) score += 5
  return score
}

function recommendationReason(item: MerchantItem): string {
  if (item.definitionId === 'SMALL_HEALING_POTION' && inventoryCount(item) < 5) return 'Запас заканчивается'
  if (item.type === 'Equipment' && hasPositiveComparison(item)) return 'Есть прирост характеристик'
  if (item.type === 'Equipment' && isForCurrentClass(item)) return 'Подходит вашему классу'
  return 'Полезно в дороге'
}

function offerBadges(item: MerchantItem): string[] {
  const badges: string[] = []
  if ((item.requiredLevel ?? 1) > characterLevel.value) badges.push('Ур. ' + item.requiredLevel)
  if (item.type === 'Equipment' && isForCurrentClass(item)) badges.push('Для моего класса')
  if (item.type === 'Equipment' && hasPositiveComparison(item)) badges.push('Есть прирост')
  if (inventoryCount(item) > 0) badges.push('В сумке: ' + inventoryCount(item))
  return badges.slice(0, 2)
}

function setBuyQuantity(quantity: number): void {
  selectedQuantity.value = Math.max(1, Math.min(selectedOfferMaxQuantity.value, quantity))
}

function pulse(itemId: string): void {
  purchasePulseId.value = itemId
  if (pulseTimer) clearTimeout(pulseTimer)
  pulseTimer = setTimeout(() => {
    purchasePulseId.value = null
  }, 360)
}

async function buy(item: MerchantItem, quantity = 1): Promise<void> {
  const updated = await session.buyMerchantItem(MERCHANT_ID, item.definitionId, quantity)
  if (!updated) return
  merchant.value = updated
  reaction.value = quantity > 1
    ? 'Вот это запас. ' + quantity + ' шт. — хватит на дорогу.'
    : 'Хороший выбор. В дороге пригодится.'
  pulse(item.definitionId)
  selectedQuantity.value = 1
}

async function buySelected(): Promise<void> {
  if (!selectedOffer.value) return
  await buy(selectedOffer.value, selectedQuantity.value)
}

function inventoryItemTypeLabel(item: InventoryItem): string {
  if (item.type === 'Equipment') return 'ЭКИПИРОВКА'
  if (item.type === 'Consumable') return 'РАСХОДНИК'
  return 'МАТЕРИАЛ'
}

function isValuable(item: InventoryItem): boolean {
  return rarityRank(item.rarity) >= rarityRank('Rare')
    || (item.generatedItem?.stars ?? 0) > 0
    || (item.reforgeCount ?? 0) > 0
}

async function performSell(item: InventoryItem, quantity: number): Promise<boolean> {
  const updated = await session.sellMerchantItem(MERCHANT_ID, item.id, quantity)
  if (!updated) return false
  merchant.value = updated
  return true
}

async function sell(item: InventoryItem, quantity: number): Promise<void> {
  if (isValuable(item)) {
    const confirmed = window.confirm(
      'Продать ценный предмет «' + item.name + '» за ' + (item.sellPriceGold * quantity) + ' золота?',
    )
    if (!confirmed) return
  }
  if (await performSell(item, quantity)) {
    reaction.value = 'Сделка есть сделка. Если передумаешь — загляни во «Выкуп».'
    selectedSellIds.value = selectedSellIds.value.filter(id => id !== item.id)
  }
}

function toggleSellSelection(itemId: string): void {
  selectedSellIds.value = selectedSellIds.value.includes(itemId)
    ? selectedSellIds.value.filter(id => id !== itemId)
    : [...selectedSellIds.value, itemId]
}

function selectAllSellable(): void {
  selectedSellIds.value = selectedSellIds.value.length === sellableItems.value.length
    ? []
    : sellableItems.value.map(item => item.id)
}

async function sellSelected(): Promise<void> {
  const items = selectedSellItems.value
  if (!items.length) return
  if (items.some(isValuable)) {
    const confirmed = window.confirm(
      'В выбранных предметах есть Rare+ или улучшенные вещи. Продать '
      + items.length + ' позиций за ' + selectedSellValue.value + ' золота?',
    )
    if (!confirmed) return
  }
  let sold = 0
  for (const item of items) {
    if (!await performSell(item, item.quantity)) break
    sold += 1
  }
  selectedSellIds.value = []
  if (sold > 0) reaction.value = 'Принял ' + sold + ' позиций. Освободил тебе место в сумке.'
}

async function buyback(item: MerchantBuybackItem): Promise<void> {
  const updated = await session.buybackMerchantItem(MERCHANT_ID, item.characterItemId)
  if (!updated) return
  merchant.value = updated
  reaction.value = 'Передумал? Бывает. Забирай — вещь всё ещё твоя.'
}
</script>

<template>
  <UIModal :open="open" title="Лавка Маркуса" @close="emit('close')">
    <section class="merchant">
      <header
        class="merchant__npc"
        :style="{ '--merchant-scene': 'url(' + gameArt.world.capital + ')' }"
      >
        <div class="merchant__npc-shade" />
        <div class="merchant__portrait">
          <img :src="gameArt.npc.marcus" alt="Торговец Маркус" />
        </div>

        <div class="merchant__identity">
          <span class="merchant__eyebrow">СТАРТОВЫЙ ГОРОД · ЛАВКА ПРИПАСОВ</span>
          <h2>{{ merchant?.name ?? 'Торговец Маркус' }}</h2>
          <p>{{ merchant?.description ?? 'Припасы, снаряжение и полезные мелочи перед дорогой.' }}</p>
          <div class="merchant__service">
            <span>⚒ Проверенный товар</span>
            <span>◆ Честная цена</span>
          </div>
        </div>

        <div class="merchant__wallet">
          <small>Ваш кошелёк</small>
          <strong><MoneyAmount :amount="merchant?.gold ?? session.snapshot?.character?.gold ?? 0" /></strong>
          <span>{{ affordableOfferCount }} товаров по карману</span>
        </div>
      </header>

      <div class="merchant-reaction" data-merchant-reaction>
        <span aria-hidden="true">“</span>
        <p>{{ reaction }}</p>
        <small>— Маркус</small>
      </div>

      <nav class="merchant-tabs" aria-label="Разделы торговца">
        <button
          type="button"
          data-merchant-tab="buy"
          :class="{ active: activeTab === 'buy' }"
          @click="activeTab = 'buy'"
        >
          Купить
        </button>
        <button
          type="button"
          data-merchant-tab="sell"
          :class="{ active: activeTab === 'sell' }"
          @click="activeTab = 'sell'"
        >
          Продать
        </button>
        <button
          type="button"
          data-merchant-tab="buyback"
          :class="{ active: activeTab === 'buyback' }"
          @click="activeTab = 'buyback'"
        >
          Выкуп <span v-if="buybackItems.length">· {{ buybackItems.length }}</span>
        </button>
      </nav>

      <section v-if="activeTab === 'buy'" class="merchant-panel merchant-panel--buy">
        <header class="merchant-panel__heading">
          <div>
            <small>ВИТРИНА МАРКУСА</small>
            <strong>Выберите товар для следующего похода</strong>
          </div>
          <span>{{ visibleOffers.length }} / {{ merchant?.items.length ?? 0 }}</span>
        </header>

        <div v-if="recommendedOffers.length" class="merchant-recommendations">
          <small>МАРКУС СОВЕТУЕТ</small>
          <div>
            <button
              v-for="item in recommendedOffers"
              :key="item.definitionId"
              type="button"
              @click="selectOffer(item)"
            >
              <ItemIcon
                :icon-id="item.iconId"
                :item-id="item.definitionId"
                :name="item.name"
                :type="item.type"
                :rarity="item.rarity"
              />
              <span>
                <b>{{ item.name }}</b>
                <em>{{ recommendationReason(item) }}</em>
              </span>
            </button>
          </div>
        </div>

        <div class="merchant-filter" aria-label="Фильтры витрины">
          <label class="merchant-filter__search">
            <span class="sr-only">Поиск товара</span>
            <span aria-hidden="true">⌕</span>
            <input v-model="searchQuery" type="search" placeholder="Поиск по витрине…" />
          </label>

          <div class="merchant-filter__row">
            <div class="merchant-filter__chips" role="group" aria-label="Категория товара">
              <button
                v-for="filter in merchantFilters"
                :key="filter"
                type="button"
                :data-merchant-filter="filter"
                :class="{ active: activeFilter === filter }"
                @click="activeFilter = filter; equipmentSubcategory = 'all'"
              >{{ filterLabel(filter) }}</button>
            </div>

            <label class="merchant-filter__sort">
              <span class="sr-only">Сортировка</span>
              <select v-model="sortMode" data-merchant-sort>
                <option value="recommended">Рекомендовано</option>
                <option value="price-asc">Сначала дешевле</option>
                <option value="price-desc">Сначала дороже</option>
                <option value="rarity">По редкости</option>
              </select>
            </label>
          </div>

          <div v-if="activeFilter === 'Equipment'" class="merchant-filter__subcategories">
            <button type="button" :class="{ active: equipmentSubcategory === 'all' }" @click="equipmentSubcategory = 'all'">Всё снаряжение</button>
            <button type="button" :class="{ active: equipmentSubcategory === 'weapon' }" @click="equipmentSubcategory = 'weapon'">Оружие</button>
            <button type="button" :class="{ active: equipmentSubcategory === 'armor' }" @click="equipmentSubcategory = 'armor'">Броня</button>
            <button type="button" :class="{ active: equipmentSubcategory === 'accessory' }" @click="equipmentSubcategory = 'accessory'">Аксессуары</button>
          </div>

          <div class="merchant-filter__toggles">
            <button
              type="button"
              class="merchant-filter__affordable"
              data-merchant-affordable
              :aria-pressed="onlyAffordable"
              :class="{ active: onlyAffordable }"
              @click="onlyAffordable = !onlyAffordable"
            >
              <span class="merchant-filter__check">{{ onlyAffordable ? '✓' : '' }}</span>
              Только то, что могу купить
              <b>{{ affordableOfferCount }}</b>
            </button>

            <button
              type="button"
              class="merchant-filter__affordable"
              data-merchant-class
              :aria-pressed="onlyForClass"
              :class="{ active: onlyForClass }"
              @click="onlyForClass = !onlyForClass"
            >
              <span class="merchant-filter__check">{{ onlyForClass ? '✓' : '' }}</span>
              Для моего класса
              <b>{{ characterClassId || '—' }}</b>
            </button>
          </div>
        </div>

        <div v-if="visibleOffers.length" class="merchant-buy">
          <div class="merchant-showcase">
            <div class="merchant-showcase__rail">
              <span>ТОВАРЫ НА ПРИЛАВКЕ</span>
              <i aria-hidden="true" />
            </div>

            <div class="merchant-shelf" aria-label="Товары торговца">
              <article
                v-for="item in visibleOffers"
                :key="item.definitionId"
                class="offer-card"
                role="button"
                tabindex="0"
                :class="{
                  active: selectedOffer?.definitionId === item.definitionId,
                  'is-unaffordable': !isAffordable(item),
                  'purchase-pulse': purchasePulseId === item.definitionId,
                }"
                :data-rarity="item.rarity"
                :data-merchant-offer="item.definitionId"
                @click="selectOffer(item)"
                @keydown.enter="selectOffer(item)"
              >
                <span class="offer-card__visual">
                  <span class="offer-card__icon">
                    <ItemIcon
                      :icon-id="item.iconId"
                      :item-id="item.definitionId"
                      :name="item.name"
                      :type="item.type"
                      :rarity="item.rarity"
                    />
                  </span>
                  <span class="offer-card__rarity">{{ rarityLabel(item) }}</span>
                </span>

                <span class="offer-card__copy">
                  <small>{{ itemTypeLabel(item) }}</small>
                  <strong>{{ item.name }}</strong>
                  <span class="offer-card__badges">
                    <i v-for="badge in offerBadges(item)" :key="badge">{{ badge }}</i>
                  </span>
                  <span class="offer-card__description">{{ item.description }}</span>
                </span>

                <span class="offer-card__footer">
                  <b><MoneyAmount :amount="item.buyPriceGold" /></b>
                  <button
                    type="button"
                    class="offer-card__quick-buy"
                    :disabled="buyPending || !isAffordable(item)"
                    @click.stop="buy(item, 1)"
                  >
                    Купить
                  </button>
                </span>
              </article>
            </div>
          </div>

          <article
            v-if="selectedOffer"
            class="merchant-detail"
            data-merchant-detail
            :data-rarity="selectedOffer.rarity"
          >
            <div class="merchant-detail__banner">
              <small>ВЫБРАННЫЙ ТОВАР</small>
              <span>{{ rarityLabel(selectedOffer) }}</span>
            </div>

            <div class="merchant-detail__identity">
              <span class="merchant-detail__icon" :data-rarity="selectedOffer.rarity">
                <ItemIcon
                  :icon-id="selectedOffer.iconId"
                  :item-id="selectedOffer.definitionId"
                  :name="selectedOffer.name"
                  :type="selectedOffer.type"
                  :rarity="selectedOffer.rarity"
                  loading="eager"
                />
              </span>
              <div>
                <small>{{ itemTypeLabel(selectedOffer) }}</small>
                <h3>{{ selectedOffer.name }}</h3>
                <span v-if="inventoryCount(selectedOffer) > 0">Уже в сумке: {{ inventoryCount(selectedOffer) }}</span>
                <span v-if="selectedOffer.requiredLevel">Требуется уровень: {{ selectedOffer.requiredLevel }}</span>
              </div>
            </div>

            <p>{{ selectedOffer.description }}</p>

            <div
              v-if="selectedOffer.type === 'Consumable' && selectedOffer.consumableActions.length"
              class="merchant-detail__effect"
            >
              <div>
                <span>Эффект</span>
                <strong>{{ selectedOffer.consumableActions.map(consumableActionLabel).join(' · ') }}</strong>
              </div>
              <small v-if="selectedOffer.consumableCooldownSeconds">
                Перезарядка {{ selectedOffer.consumableCooldownSeconds }} сек.
              </small>
            </div>

            <div v-if="selectedOffer.type === 'Equipment'" class="merchant-compare">
              <header>
                <span>Сравнение с надетым</span>
                <small>{{ equippedForOffer(selectedOffer)?.name ?? 'Слот пуст' }}</small>
              </header>
              <div v-if="selectedComparisonRows.length">
                <p v-for="row in selectedComparisonRows" :key="row.key">
                  <span>{{ row.label }}</span>
                  <em>{{ row.current }}</em>
                  <b :class="{ positive: row.delta > 0, negative: row.delta < 0 }">
                    {{ row.delta > 0 ? '+' : '' }}{{ row.delta }}
                  </b>
                </p>
              </div>
              <small v-else>У предмета нет сравнимых базовых характеристик.</small>
            </div>

            <div class="merchant-detail__quote">
              <span aria-hidden="true">“</span>
              <p>{{ reaction }}</p>
              <small>— Маркус</small>
            </div>

            <div v-if="selectedOffer.type !== 'Equipment'" class="merchant-quantity">
              <span>Количество</span>
              <div class="merchant-quantity__stepper">
                <button type="button" @click="setBuyQuantity(selectedQuantity - 1)">−</button>
                <b data-buy-quantity>{{ selectedQuantity }}</b>
                <button type="button" @click="setBuyQuantity(selectedQuantity + 1)">+</button>
              </div>
              <div class="merchant-quantity__quick">
                <button
                  v-for="qty in [1, 5, 10]"
                  :key="qty"
                  type="button"
                  :disabled="qty > selectedOfferMaxQuantity"
                  @click="setBuyQuantity(qty)"
                >
                  {{ qty }}
                </button>
                <button type="button" @click="setBuyQuantity(maxAffordableQuantity)">MAX</button>
              </div>
              <small>После покупки: {{ inventoryCount(selectedOffer) + selectedQuantity }} в сумке</small>
            </div>

            <footer class="merchant-detail__purchase">
              <div>
                <small>Итого</small>
                <strong><MoneyAmount :amount="selectedTotalPrice" /></strong>
                <span :class="{ danger: !selectedCanAfford }">
                  {{ selectedCanAfford ? 'Можно купить сейчас' : 'Недостаточно золота' }}
                </span>
              </div>
              <UIButton
                data-buy-selected
                :loading="buyPending"
                :disabled="buyPending || !selectedCanAfford"
                @click="buySelected"
              >
                Купить {{ selectedQuantity > 1 ? '×' + selectedQuantity : '' }} ·
                <MoneyAmount :amount="selectedTotalPrice" />
              </UIButton>
            </footer>
          </article>
        </div>

        <div v-if="loading" class="merchant-empty">
          <span class="merchant-empty__mark">◆</span>
          <p>Маркус раскладывает товар…</p>
        </div>
        <div v-else-if="merchant && merchant.items.length === 0" class="merchant-empty">
          <span class="merchant-empty__mark">◇</span>
          <p>На прилавке пока ничего нет.</p>
        </div>
        <div v-else-if="merchant && visibleOffers.length === 0" class="merchant-empty">
          <span class="merchant-empty__mark">⌕</span>
          <p>По этим фильтрам ничего не нашлось.</p>
          <button
            type="button"
            @click="searchQuery = ''; activeFilter = 'all'; onlyAffordable = false; onlyForClass = false"
          >
            Сбросить фильтры
          </button>
        </div>
      </section>

      <section v-else-if="activeTab === 'sell'" class="merchant-panel merchant-panel--sell">
        <header class="merchant-panel__heading">
          <div>
            <small>СДАТЬ МАРКУСУ</small>
            <strong>Отметьте вещи и продайте пачкой</strong>
          </div>
          <span>{{ sellableItems.length }} предметов</span>
        </header>

        <div v-if="sellableItems.length" class="sell-toolbar">
          <button type="button" @click="selectAllSellable">
            {{ selectedSellIds.length === sellableItems.length ? 'Снять выбор' : 'Выбрать всё' }}
          </button>
          <span>Выбрано: {{ selectedSellItems.length }}</span>
          <b><MoneyAmount :amount="selectedSellValue" /></b>
          <UIButton :disabled="sellPending || !selectedSellItems.length" @click="sellSelected">
            Продать выбранное
          </UIButton>
        </div>

        <p v-if="protectedItemsCount > 0" class="protected-hint">
          <IconGenerator :config="{ id: 'merchant-locked-items', glyph: 'lock', category: 'utility', state: 'locked' }" />
          Защищённые предметы скрыты из продажи: {{ protectedItemsCount }}
        </p>

        <div v-if="sellableItems.length" class="sell-list">
          <article
            v-for="item in sellableItems"
            :key="item.id"
            class="sell-card"
            :class="{ selected: selectedSellIds.includes(item.id) }"
            :data-sell-item="item.id"
          >
            <label class="sell-card__select">
              <input
                type="checkbox"
                :checked="selectedSellIds.includes(item.id)"
                @change="toggleSellSelection(item.id)"
              />
              <span />
            </label>

            <span class="sell-card__icon" :data-rarity="item.rarity">
              <ItemIcon
                :icon-id="item.iconId"
                :item-id="item.id"
                :name="item.name"
                :type="item.type"
                :equipment-slot="item.slot"
                :rarity="item.rarity"
              />
            </span>

            <div class="sell-card__copy">
              <small>{{ inventoryItemTypeLabel(item) }}</small>
              <strong>{{ item.name }}</strong>
              <span>В сумке: {{ item.quantity }}</span>
              <em v-if="isValuable(item)">⚠ Ценный предмет — потребуется подтверждение</em>
            </div>

            <div class="sell-card__price">
              <small>за штуку</small>
              <strong><MoneyAmount :amount="item.sellPriceGold" /></strong>
            </div>

            <div class="sell-card__actions">
              <UIButton variant="ghost" :disabled="sellPending" @click="sell(item, 1)">
                Продать 1
              </UIButton>
              <UIButton :disabled="sellPending" @click="sell(item, item.quantity)">
                {{ item.quantity > 1 ? 'Продать всё' : 'Продать' }} ·
                <MoneyAmount :amount="item.sellPriceGold * item.quantity" />
              </UIButton>
            </div>
          </article>
        </div>

        <div v-else class="merchant-empty">
          <span class="merchant-empty__mark">◇</span>
          <p>В сумке пока нет вещей, которые Маркус готов купить.</p>
        </div>
      </section>

      <section v-else class="merchant-panel merchant-panel--buyback">
        <header class="merchant-panel__heading">
          <div>
            <small>ОБРАТНЫЙ ВЫКУП</small>
            <strong>Последние 12 проданных позиций</strong>
          </div>
          <span>{{ buybackItems.length }} предметов</span>
        </header>

        <div v-if="buybackItems.length" class="buyback-list">
          <article
            v-for="item in buybackItems"
            :key="item.characterItemId"
            class="buyback-card"
            :data-buyback-item="item.characterItemId"
          >
            <span class="sell-card__icon" :data-rarity="item.rarity">
              <ItemIcon
                :icon-id="item.iconId"
                :item-id="item.definitionId"
                :name="item.name"
                :type="item.type"
                :rarity="item.rarity"
              />
            </span>

            <div class="buyback-card__copy">
              <small>{{ rarityLabel(item) }}</small>
              <strong>{{ item.name }}</strong>
              <span>
                Количество: {{ item.quantity }}
                <template v-if="item.enhancementLevel"> · +{{ item.enhancementLevel }}</template>
              </span>
            </div>

            <div class="buyback-card__price">
              <small>Вернуть за</small>
              <b><MoneyAmount :amount="item.buybackPriceGold" /></b>
            </div>

            <UIButton
              :loading="buybackPending"
              :disabled="buybackPending || !canAffordMoney(merchant?.gold ?? 0, item.buybackPriceGold)"
              @click="buyback(item)"
            >
              Выкупить
            </UIButton>
          </article>
        </div>

        <div v-else class="merchant-empty">
          <span class="merchant-empty__mark">↶</span>
          <p>Выкуп пуст. Здесь появятся последние проданные Маркусу вещи.</p>
        </div>
      </section>
    </section>
  </UIModal>
</template>

<style scoped>
.merchant {
  --merchant-gold: #d8ad63;
  --merchant-gold-soft: #f0d99f;
  --merchant-wood: #241811;
  --merchant-iron: #15191f;
  --merchant-burgundy: #4b2027;

  display: grid;
  overflow: hidden;
  border: 1px solid rgb(201 167 101 / 30%);
  border-radius: var(--ui-radius-lg);
  background:
    radial-gradient(circle at 88% 2%, rgb(117 53 42 / 15%), transparent 14rem),
    linear-gradient(180deg, #12151a, #080a0e 58%, #090a0d);
  box-shadow:
    inset 0 1px 0 rgb(255 244 213 / 4%),
    0 1.1rem 2.6rem rgb(0 0 0 / 38%);
}

.merchant__npc {
  --merchant-scene: none;

  position: relative;
  display: grid;
  grid-template-columns: 6.2rem minmax(0, 1fr) auto;
  align-items: end;
  gap: var(--ui-space-3);
  min-height: 10.5rem;
  padding: var(--ui-space-4);
  overflow: hidden;
  border-bottom: 1px solid rgb(216 173 99 / 24%);
  background:
    var(--merchant-scene) center 44% / cover,
    #16181c;
}

.merchant__npc::after {
  position: absolute;
  right: 0;
  bottom: 0;
  left: 0;
  height: 4px;
  background:
    linear-gradient(90deg, transparent, rgb(216 173 99 / 36%) 20%, rgb(216 173 99 / 62%) 50%, rgb(216 173 99 / 36%) 80%, transparent);
  content: '';
}

.merchant__npc-shade {
  position: absolute;
  inset: 0;
  background:
    linear-gradient(180deg, rgb(3 4 6 / 18%), rgb(5 6 8 / 90%)),
    linear-gradient(90deg, rgb(7 7 8 / 18%), transparent 58%, rgb(44 17 18 / 26%));
  pointer-events: none;
}

.merchant__portrait,
.merchant__identity,
.merchant__wallet {
  position: relative;
  z-index: 1;
}

.merchant__portrait {
  width: 6.2rem;
  height: 7.8rem;
  overflow: hidden;
  border: 1px solid rgb(216 173 99 / 42%);
  border-radius: 2.8rem 2.8rem var(--ui-radius-md) var(--ui-radius-md);
  background: #17130f;
  box-shadow:
    0 0 0 3px rgb(0 0 0 / 34%),
    0 .7rem 1.5rem rgb(0 0 0 / 45%);
}

.merchant__portrait img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  object-position: center 18%;
}

.merchant__identity {
  display: grid;
  min-width: 0;
  gap: 4px;
  padding-bottom: 2px;
  text-shadow: 0 2px 8px rgb(0 0 0 / 82%);
}

.merchant__eyebrow,
.merchant__identity small,
.merchant__wallet small,
.merchant-panel__heading small,
.offer-card small,
.merchant-detail small,
.sell-card small {
  color: #988f80;
  font-size: .52rem;
  font-weight: 850;
  letter-spacing: .095em;
  text-transform: uppercase;
}

.merchant__eyebrow {
  color: #c8a96d;
}

.merchant__identity h2,
.merchant__identity p,
.merchant__service {
  margin: 0;
}

.merchant__identity h2 {
  color: #f1e5cc;
  font-family: var(--ui-font-display);
  font-size: 1.55rem;
  line-height: 1;
}

.merchant__identity p {
  max-width: 34rem;
  color: #c5c0b5;
  font-size: .7rem;
  line-height: 1.45;
}

.merchant__service {
  display: flex;
  flex-wrap: wrap;
  gap: 5px 10px;
  margin-top: 3px;
  color: #bca878;
  font-size: .55rem;
}

.merchant__wallet {
  display: grid;
  justify-items: end;
  gap: 2px;
  min-width: 7rem;
  padding: 9px 11px;
  border: 1px solid rgb(216 173 99 / 24%);
  border-radius: var(--ui-radius-md);
  background: rgb(7 8 10 / 78%);
  box-shadow: inset 0 1px 0 rgb(255 255 255 / 3%);
  white-space: nowrap;
  backdrop-filter: blur(8px);
}

.merchant__wallet strong {
  color: var(--merchant-gold-soft);
  font-size: .88rem;
  font-variant-numeric: tabular-nums;
}

.merchant__wallet span {
  color: #8f968d;
  font-size: .52rem;
}

.merchant-tabs {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  border-bottom: 1px solid rgb(216 173 99 / 16%);
  background:
    linear-gradient(180deg, rgb(44 31 22 / 76%), rgb(20 18 16 / 72%));
}

.merchant-tabs button {
  position: relative;
  min-height: 3rem;
  border: 0;
  border-right: 1px solid rgb(216 173 99 / 10%);
  background: transparent;
  color: #938b80;
  font: inherit;
  font-size: .67rem;
  font-weight: 850;
  letter-spacing: .035em;
  text-transform: uppercase;
}

.merchant-tabs button:last-child {
  border-right: 0;
}

.merchant-tabs button.active {
  background: rgb(216 173 99 / 6%);
  color: #f0d79c;
}

.merchant-tabs button.active::after {
  position: absolute;
  right: 18%;
  bottom: -1px;
  left: 18%;
  height: 2px;
  background: var(--merchant-gold);
  box-shadow: 0 0 10px rgb(216 173 99 / 42%);
  content: '';
}

.merchant-tabs button:disabled {
  opacity: .25;
}

.merchant-panel {
  display: grid;
}

.merchant-panel__heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ui-space-3);
  padding: 12px var(--ui-space-4) 10px;
  border-bottom: 1px solid rgb(255 255 255 / 5%);
}

.merchant-panel__heading > div {
  display: grid;
  gap: 2px;
}

.merchant-panel__heading strong {
  color: #ddd5c7;
  font-family: var(--ui-font-display);
  font-size: .9rem;
}

.merchant-panel__heading > span {
  padding: 3px 7px;
  border: 1px solid rgb(216 173 99 / 15%);
  border-radius: var(--ui-radius-round);
  color: #9e9485;
  font-size: .56rem;
}

.merchant-filter {
  display: grid;
  gap: 8px;
  padding: 10px var(--ui-space-4);
  border-bottom: 1px solid rgb(255 255 255 / 5%);
  background:
    linear-gradient(180deg, rgb(7 9 12 / 82%), rgb(11 10 9 / 54%));
}

.merchant-filter__search {
  display: grid;
  grid-template-columns: 1.4rem minmax(0, 1fr);
  align-items: center;
  gap: 4px;
  min-height: var(--ui-touch-target);
  padding: 0 10px;
  border: 1px solid rgb(216 173 99 / 20%);
  border-radius: var(--ui-radius-md);
  background: rgb(8 10 13 / 88%);
  color: var(--merchant-gold);
}

.merchant-filter__search input {
  width: 100%;
  min-width: 0;
  border: 0;
  outline: 0;
  background: transparent;
  color: var(--ui-color-text-primary);
  font: inherit;
  font-size: .7rem;
}

.merchant-filter__search input::placeholder {
  color: #77756f;
}

.merchant-filter__row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 7px;
}

.merchant-filter__chips {
  display: flex;
  gap: 5px;
  overflow-x: auto;
  scrollbar-width: none;
}

.merchant-filter__chips::-webkit-scrollbar {
  display: none;
}

.merchant-filter__chips button,
.merchant-filter__affordable,
.merchant-filter__sort select {
  min-height: 2rem;
  border: 1px solid rgb(216 173 99 / 16%);
  border-radius: var(--ui-radius-round);
  background: rgb(255 255 255 / 1.5%);
  color: #979187;
  font: inherit;
  font-size: .58rem;
}

.merchant-filter__chips button {
  flex: 0 0 auto;
  padding: 4px 9px;
  white-space: nowrap;
}

.merchant-filter__chips button.active,
.merchant-filter__affordable.active {
  border-color: rgb(216 173 99 / 52%);
  background: rgb(216 173 99 / 10%);
  color: #f0d79c;
}

.merchant-filter__sort select {
  min-width: 8.2rem;
  padding: 0 8px;
  outline: 0;
}

.merchant-filter__sort option {
  background: #111318;
  color: #ddd;
}

.merchant-filter__affordable {
  display: grid;
  grid-template-columns: 1rem minmax(0, 1fr) auto;
  align-items: center;
  gap: 7px;
  width: 100%;
  padding: 4px 9px;
  border-radius: var(--ui-radius-md);
  text-align: left;
}

.merchant-filter__affordable b {
  color: #c2a86f;
  font-size: .58rem;
}

.merchant-filter__check {
  display: grid;
  width: 1rem;
  height: 1rem;
  place-items: center;
  border: 1px solid rgb(216 173 99 / 28%);
  border-radius: 3px;
  color: #f0d79c;
  font-size: .65rem;
}

.merchant-buy {
  display: grid;
  grid-template-columns: minmax(0, 1.45fr) minmax(13.5rem, .75fr);
  min-height: 24rem;
}

.merchant-showcase {
  min-width: 0;
  border-right: 1px solid rgb(216 173 99 / 13%);
  background:
    linear-gradient(90deg, rgb(55 35 22 / 8%), transparent 35%),
    #090b0e;
}

.merchant-showcase__rail {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr);
  align-items: center;
  gap: 8px;
  padding: 8px 11px 3px;
  color: #786f62;
  font-size: .48rem;
  font-weight: 850;
  letter-spacing: .1em;
}

.merchant-showcase__rail i {
  height: 1px;
  background: linear-gradient(90deg, rgb(216 173 99 / 18%), transparent);
}

.merchant-shelf {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  align-content: start;
  gap: 8px;
  max-height: 27rem;
  padding: 8px 10px 12px;
  overflow-y: auto;
  scrollbar-width: thin;
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}

.offer-card {
  position: relative;
  display: grid;
  grid-template-columns: 3.4rem minmax(0, 1fr);
  gap: 8px;
  min-width: 0;
  min-height: 7.1rem;
  padding: 9px;
  overflow: hidden;
  border: 1px solid rgb(176 164 142 / 14%);
  border-radius: var(--ui-radius-md);
  background:
    linear-gradient(135deg, rgb(255 255 255 / 2.5%), transparent 48%),
    rgb(17 19 22 / 94%);
  color: #c9c2b7;
  font: inherit;
  text-align: left;
  box-shadow: inset 0 1px 0 rgb(255 255 255 / 2.5%);
}

.offer-card::after {
  position: absolute;
  right: 8px;
  bottom: 0;
  left: 8px;
  height: 2px;
  background: rgb(216 173 99 / 7%);
  content: '';
}

.offer-card:hover,
.offer-card.active {
  border-color: rgb(216 173 99 / 46%);
  background:
    linear-gradient(135deg, rgb(216 173 99 / 9%), transparent 48%),
    rgb(22 22 21 / 96%);
}

.offer-card.active {
  box-shadow:
    inset 0 0 0 1px rgb(216 173 99 / 12%),
    0 .4rem 1rem rgb(0 0 0 / 16%);
}

.offer-card.is-unaffordable {
  opacity: .68;
}

.offer-card__visual {
  display: grid;
  align-content: start;
  gap: 4px;
}

.offer-card__icon {
  display: grid;
  width: 3.4rem;
  height: 3.4rem;
  place-items: center;
  overflow: hidden;
  border: 1px solid #55504a;
  border-radius: var(--ui-radius-md);
  background: #080a0d;
  box-shadow: inset 0 0 12px rgb(0 0 0 / 60%);
}

.offer-card__rarity {
  overflow: hidden;
  color: #8e8980;
  font-size: .48rem;
  line-height: 1.1;
  text-align: center;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.offer-card__copy {
  display: grid;
  min-width: 0;
  align-content: start;
  gap: 2px;
}

.offer-card__copy small {
  color: #8e8578;
}

.offer-card__copy strong {
  display: -webkit-box;
  overflow: hidden;
  color: #e5dfd4;
  font-size: .69rem;
  line-height: 1.22;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}

.offer-card__description {
  display: -webkit-box;
  overflow: hidden;
  color: #85847f;
  font-size: .55rem;
  line-height: 1.35;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}

.offer-card__footer {
  grid-column: 1 / -1;
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: 6px;
  margin-top: auto;
  padding-top: 6px;
  border-top: 1px solid rgb(255 255 255 / 5%);
}

.offer-card__footer b {
  color: #e8c77e;
  font-size: .67rem;
  font-style: normal;
}

.offer-card__footer em {
  color: #798377;
  font-size: .49rem;
  font-style: normal;
  text-align: right;
}

.offer-card__footer .offer-card__shortfall {
  color: #a87670;
}

.offer-card[data-rarity='Uncommon'] .offer-card__icon,
.merchant-detail__icon[data-rarity='Uncommon'] {
  border-color: rgb(87 177 115 / 58%);
}

.offer-card[data-rarity='Rare'] .offer-card__icon,
.merchant-detail__icon[data-rarity='Rare'] {
  border-color: rgb(75 130 201 / 66%);
}

.offer-card[data-rarity='Epic'] .offer-card__icon,
.merchant-detail__icon[data-rarity='Epic'] {
  border-color: rgb(137 94 201 / 72%);
}

.offer-card[data-rarity='Legendary'] .offer-card__icon,
.merchant-detail__icon[data-rarity='Legendary'] {
  border-color: rgb(211 139 62 / 76%);
}

.offer-card[data-rarity='Unique'] .offer-card__icon,
.merchant-detail__icon[data-rarity='Unique'] {
  border-color: rgb(218 188 92 / 82%);
}

.merchant-detail {
  position: sticky;
  top: 0;
  display: grid;
  align-content: start;
  gap: var(--ui-space-3);
  min-width: 0;
  padding: 13px;
  background:
    radial-gradient(circle at 50% 0, rgb(91 43 34 / 14%), transparent 12rem),
    linear-gradient(180deg, rgb(28 24 21 / 96%), rgb(12 13 15 / 98%));
}

.merchant-detail__banner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 7px;
  padding-bottom: 6px;
  border-bottom: 1px solid rgb(216 173 99 / 12%);
}

.merchant-detail__banner span {
  color: #c9a96d;
  font-size: .55rem;
  font-weight: 800;
  text-transform: uppercase;
}

.merchant-detail__identity {
  display: grid;
  grid-template-columns: 4.6rem minmax(0, 1fr);
  align-items: center;
  gap: var(--ui-space-3);
}

.merchant-detail__icon {
  display: grid;
  width: 4.6rem;
  height: 4.6rem;
  place-items: center;
  overflow: hidden;
  border: 1px solid #5b554e;
  border-radius: var(--ui-radius-md);
  background: #080a0d;
  box-shadow:
    inset 0 0 15px rgb(0 0 0 / 60%),
    0 .4rem 1rem rgb(0 0 0 / 24%);
}

.offer-card__icon :deep(img),
.merchant-detail__icon :deep(img) {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.merchant-detail__identity h3 {
  margin: 2px 0;
  color: #eee5d3;
  font-family: var(--ui-font-display);
  font-size: 1.08rem;
  line-height: 1.12;
}

.merchant-detail__identity > div > span {
  color: #879087;
  font-size: .55rem;
}

.merchant-detail > p {
  margin: 0;
  color: #aaa59c;
  font-size: .66rem;
  line-height: 1.5;
}

.merchant-detail__effect {
  display: grid;
  gap: 4px;
  padding: 9px;
  border: 1px solid rgb(76 157 111 / 18%);
  border-radius: var(--ui-radius-md);
  background: rgb(49 104 73 / 7%);
}

.merchant-detail__effect > div {
  display: grid;
  gap: 2px;
}

.merchant-detail__effect span {
  color: #7f9487;
  font-size: .52rem;
}

.merchant-detail__effect strong {
  color: #8ac6a0;
  font-size: .66rem;
}

.merchant-detail__effect > small {
  color: #737d76;
}

.merchant-detail__quote {
  position: relative;
  display: grid;
  gap: 2px;
  padding: 8px 9px 8px 20px;
  border-left: 2px solid rgb(216 173 99 / 30%);
  background: rgb(216 173 99 / 3%);
}

.merchant-detail__quote > span {
  position: absolute;
  top: 3px;
  left: 7px;
  color: rgb(216 173 99 / 52%);
  font-family: Georgia, serif;
  font-size: 1.4rem;
}

.merchant-detail__quote p {
  margin: 0;
  color: #9c9488;
  font-size: .58rem;
  font-style: italic;
  line-height: 1.35;
}

.merchant-detail__quote small {
  color: #7f776c;
  font-size: .48rem;
  text-align: right;
}

.merchant-detail__purchase {
  display: grid;
  gap: 9px;
  margin-top: auto;
  padding-top: 10px;
  border-top: 1px solid rgb(216 173 99 / 13%);
}

.merchant-detail__purchase > div {
  display: grid;
  grid-template-columns: 1fr auto;
  align-items: end;
  gap: 1px 7px;
}

.merchant-detail__purchase > div > small,
.merchant-detail__purchase > div > span {
  grid-column: 1;
}

.merchant-detail__purchase strong {
  grid-column: 2;
  grid-row: 1 / 3;
  color: var(--merchant-gold-soft);
  font-size: 1rem;
}

.merchant-detail__purchase span {
  color: #7f9782;
  font-size: .52rem;
}

.merchant-detail__purchase span.danger {
  color: #a87670;
}

.merchant-detail__purchase :deep(.ui-button) {
  width: 100%;
}

.protected-hint {
  display: flex;
  align-items: center;
  gap: 6px;
  margin: 0;
  padding: var(--ui-space-2) var(--ui-space-4);
  border-bottom: 1px solid rgb(255 255 255 / 5%);
  background: rgb(146 136 255 / 4%);
  color: #b9b4e8;
  font-size: .62rem;
}

.sell-list {
  display: grid;
  padding: 6px 0;
}

.sell-card {
  display: grid;
  grid-template-columns: 3rem minmax(0, 1fr) auto;
  align-items: center;
  gap: var(--ui-space-3);
  margin: 0 8px;
  padding: var(--ui-space-3);
  border-bottom: 1px solid rgb(216 173 99 / 9%);
}

.sell-card__icon {
  display: grid;
  width: 3rem;
  height: 3rem;
  place-items: center;
  overflow: hidden;
  border: 1px solid rgb(216 173 99 / 20%);
  border-radius: var(--ui-radius-md);
  background: #090b0e;
}

.sell-card__icon :deep(img) {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.sell-card__copy {
  display: grid;
  min-width: 0;
  gap: 1px;
}

.sell-card__copy strong {
  overflow: hidden;
  color: #ddd6ca;
  font-size: .7rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.sell-card__copy span {
  color: #8a8780;
  font-size: .58rem;
}

.sell-card__price {
  display: grid;
  justify-items: end;
  gap: 1px;
  white-space: nowrap;
}

.sell-card__price strong {
  color: #e0bd76;
  font-size: .7rem;
}

.sell-card__actions {
  grid-column: 1 / -1;
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--ui-space-2);
}

.sell-card__actions :deep(.ui-button) {
  width: 100%;
}

.merchant-empty {
  display: grid;
  justify-items: center;
  gap: 5px;
  min-height: 10rem;
  place-content: center;
  padding: var(--ui-space-4);
  color: #87827a;
  text-align: center;
}

.merchant-empty__mark {
  color: #b79459;
  font-size: 1.15rem;
}

.merchant-empty p {
  margin: 0;
  font-size: .68rem;
}

.merchant-empty button {
  margin-top: 4px;
  border: 0;
  background: transparent;
  color: #d2b477;
  font: inherit;
  font-size: .6rem;
  text-decoration: underline;
}

@media (max-width: 620px) {
  .merchant__npc {
    grid-template-columns: 5.2rem minmax(0, 1fr);
    align-items: end;
    min-height: 9rem;
    padding: 12px;
  }

  .merchant__portrait {
    width: 5.2rem;
    height: 6.6rem;
  }

  .merchant__identity h2 {
    font-size: 1.25rem;
  }

  .merchant__identity p {
    display: -webkit-box;
    overflow: hidden;
    -webkit-box-orient: vertical;
    -webkit-line-clamp: 2;
  }

  .merchant__service {
    display: none;
  }

  .merchant__wallet {
    grid-column: 1 / -1;
    grid-template-columns: 1fr auto;
    align-items: center;
    justify-items: stretch;
    min-width: 0;
    padding: 6px 9px;
  }

  .merchant__wallet strong {
    grid-column: 2;
    grid-row: 1 / 3;
    justify-self: end;
  }

  .merchant__wallet span {
    grid-column: 1;
  }

  .merchant-panel__heading {
    padding-inline: 12px;
  }

  .merchant-filter {
    padding-inline: 12px;
  }

  .merchant-filter__row {
    grid-template-columns: 1fr;
  }

  .merchant-filter__sort select {
    width: 100%;
    min-height: 2.2rem;
    border-radius: var(--ui-radius-md);
  }

  .merchant-buy {
    grid-template-columns: 1fr;
  }

  .merchant-showcase {
    border-right: 0;
    border-bottom: 1px solid rgb(216 173 99 / 13%);
  }

  .merchant-shelf {
    max-height: 21rem;
  }

  .merchant-detail {
    position: static;
    min-height: 16rem;
  }
}

@media (max-width: 390px) {
  .merchant-shelf {
    gap: 6px;
    padding-inline: 8px;
  }

  .offer-card {
    grid-template-columns: 3rem minmax(0, 1fr);
    min-height: 6.6rem;
    padding: 7px;
  }

  .offer-card__icon {
    width: 3rem;
    height: 3rem;
  }

  .offer-card__description {
    -webkit-line-clamp: 1;
  }

  .offer-card__footer {
    align-items: center;
  }

  .offer-card__footer em {
    max-width: 5rem;
  }
}

@media (max-width: 340px) {
  .merchant-shelf {
    grid-template-columns: 1fr;
  }

  .sell-card {
    grid-template-columns: 2.7rem minmax(0, 1fr);
  }

  .sell-card__price {
    grid-column: 1 / -1;
    grid-template-columns: 1fr auto;
    justify-items: stretch;
  }

  .sell-card__price strong {
    justify-self: end;
  }
}

.merchant-reaction {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 7px;
  padding: 7px var(--ui-space-4);
  border-bottom: 1px solid rgb(216 173 99 / 10%);
  background: linear-gradient(90deg, rgb(216 173 99 / 5%), transparent 65%);
  color: #9e968a;
}

.merchant-reaction > span {
  color: rgb(216 173 99 / 52%);
  font-family: Georgia, serif;
  font-size: 1.25rem;
}

.merchant-reaction p {
  margin: 0;
  font-size: .58rem;
  font-style: italic;
  line-height: 1.3;
}

.merchant-reaction small {
  color: #7f776c;
  font-size: .48rem;
  white-space: nowrap;
}

.merchant-recommendations {
  display: grid;
  gap: 6px;
  padding: 9px var(--ui-space-4);
  border-bottom: 1px solid rgb(216 173 99 / 9%);
  background: rgb(216 173 99 / 2.5%);
}

.merchant-recommendations > small {
  color: #a99164;
  font-size: .5rem;
  font-weight: 850;
  letter-spacing: .1em;
}

.merchant-recommendations > div {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 6px;
}

.merchant-recommendations button {
  display: grid;
  grid-template-columns: 2.1rem minmax(0, 1fr);
  align-items: center;
  gap: 6px;
  min-width: 0;
  padding: 6px;
  border: 1px solid rgb(216 173 99 / 14%);
  border-radius: var(--ui-radius-md);
  background: rgb(255 255 255 / 1.5%);
  color: #bbb3a6;
  font: inherit;
  text-align: left;
}

.merchant-recommendations button :deep(img) {
  width: 2.1rem;
  height: 2.1rem;
  border-radius: 5px;
  object-fit: cover;
}

.merchant-recommendations button span {
  display: grid;
  min-width: 0;
}

.merchant-recommendations button b {
  overflow: hidden;
  color: #d9d1c2;
  font-size: .58rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.merchant-recommendations button em {
  color: #8e9a86;
  font-size: .48rem;
  font-style: normal;
}

.merchant-filter__toggles {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 7px;
}

.merchant-filter__subcategories {
  display: flex;
  gap: 5px;
  overflow-x: auto;
  scrollbar-width: none;
}

.merchant-filter__subcategories::-webkit-scrollbar {
  display: none;
}

.merchant-filter__subcategories button {
  flex: 0 0 auto;
  min-height: 1.8rem;
  padding: 3px 8px;
  border: 1px solid rgb(216 173 99 / 12%);
  border-radius: var(--ui-radius-round);
  background: transparent;
  color: #817c74;
  font: inherit;
  font-size: .53rem;
}

.merchant-filter__subcategories button.active {
  border-color: rgb(216 173 99 / 40%);
  background: rgb(216 173 99 / 8%);
  color: #dac38f;
}

.offer-card {
  cursor: pointer;
}

.offer-card__badges {
  display: flex;
  flex-wrap: wrap;
  gap: 3px;
  min-height: .9rem;
}

.offer-card__badges i {
  padding: 2px 4px;
  border: 1px solid rgb(216 173 99 / 15%);
  border-radius: 3px;
  background: rgb(216 173 99 / 5%);
  color: #aa9977;
  font-size: .43rem;
  font-style: normal;
  line-height: 1;
}

.offer-card__quick-buy {
  min-height: 1.7rem;
  padding: 3px 7px;
  border: 1px solid rgb(216 173 99 / 28%);
  border-radius: var(--ui-radius-sm);
  background: rgb(216 173 99 / 8%);
  color: #e2c37f;
  font: inherit;
  font-size: .51rem;
  font-weight: 800;
}

.offer-card__quick-buy:disabled {
  opacity: .38;
}

.offer-card.purchase-pulse {
  animation: merchant-purchase-pulse 360ms ease-out;
}

@keyframes merchant-purchase-pulse {
  0% { box-shadow: 0 0 0 0 rgb(216 173 99 / 0%); }
  35% { box-shadow: 0 0 0 2px rgb(216 173 99 / 45%), 0 0 1.2rem rgb(216 173 99 / 28%); }
  100% { box-shadow: 0 0 0 0 rgb(216 173 99 / 0%); }
}

.merchant-compare {
  display: grid;
  gap: 6px;
  padding: 8px 9px;
  border: 1px solid rgb(111 142 173 / 16%);
  border-radius: var(--ui-radius-md);
  background: rgb(76 103 128 / 6%);
}

.merchant-compare header {
  display: flex;
  justify-content: space-between;
  gap: 8px;
}

.merchant-compare header span {
  color: #aebdca;
  font-size: .55rem;
  font-weight: 800;
}

.merchant-compare header small {
  overflow: hidden;
  max-width: 55%;
  color: #777f86;
  font-size: .48rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.merchant-compare > div {
  display: grid;
  gap: 3px;
}

.merchant-compare p {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 3rem 3rem;
  gap: 5px;
  margin: 0;
  color: #8f9498;
  font-size: .52rem;
}

.merchant-compare p em {
  color: #777d82;
  font-style: normal;
  text-align: right;
}

.merchant-compare p b {
  color: #aaa;
  font-weight: 800;
  text-align: right;
}

.merchant-compare p b.positive {
  color: #75b98d;
}

.merchant-compare p b.negative {
  color: #bd7771;
}

.merchant-quantity {
  display: grid;
  grid-template-columns: 1fr auto;
  align-items: center;
  gap: 6px;
  padding: 8px 9px;
  border: 1px solid rgb(216 173 99 / 12%);
  border-radius: var(--ui-radius-md);
  background: rgb(216 173 99 / 3%);
}

.merchant-quantity > span {
  color: #a29a8e;
  font-size: .56rem;
  font-weight: 800;
}

.merchant-quantity__stepper {
  display: grid;
  grid-template-columns: 1.8rem 2rem 1.8rem;
  align-items: center;
  overflow: hidden;
  border: 1px solid rgb(216 173 99 / 22%);
  border-radius: var(--ui-radius-sm);
}

.merchant-quantity__stepper button,
.merchant-quantity__quick button {
  border: 0;
  background: rgb(255 255 255 / 2%);
  color: #ceb57d;
  font: inherit;
  font-size: .65rem;
}

.merchant-quantity__stepper button {
  min-height: 1.8rem;
}

.merchant-quantity__stepper b {
  color: #e3d8c4;
  font-size: .62rem;
  text-align: center;
}

.merchant-quantity__quick {
  grid-column: 1 / -1;
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 4px;
}

.merchant-quantity__quick button {
  min-height: 1.65rem;
  border: 1px solid rgb(216 173 99 / 12%);
  border-radius: 4px;
}

.merchant-quantity > small {
  grid-column: 1 / -1;
  color: #757d73;
  font-size: .48rem;
}

.sell-toolbar {
  display: grid;
  grid-template-columns: auto 1fr auto auto;
  align-items: center;
  gap: 8px;
  padding: 8px var(--ui-space-4);
  border-bottom: 1px solid rgb(216 173 99 / 10%);
  background: rgb(216 173 99 / 3%);
}

.sell-toolbar > button {
  border: 0;
  background: transparent;
  color: #c1a56e;
  font: inherit;
  font-size: .55rem;
  text-decoration: underline;
}

.sell-toolbar > span {
  color: #88827a;
  font-size: .55rem;
}

.sell-toolbar > b {
  color: #d8b874;
  font-size: .65rem;
}

.sell-card {
  grid-template-columns: 1.4rem 3rem minmax(0, 1fr) auto;
}

.sell-card.selected {
  background: rgb(216 173 99 / 4%);
}

.sell-card__select {
  display: grid;
  place-items: center;
}

.sell-card__select input {
  position: absolute;
  opacity: 0;
  pointer-events: none;
}

.sell-card__select span {
  width: 1rem;
  height: 1rem;
  border: 1px solid rgb(216 173 99 / 28%);
  border-radius: 3px;
  background: #0b0d10;
}

.sell-card__select input:checked + span {
  background:
    linear-gradient(135deg, transparent 38%, #e2c77f 39% 52%, transparent 53%) center / 70% 70% no-repeat,
    rgb(216 173 99 / 12%);
  border-color: rgb(216 173 99 / 60%);
}

.sell-card__copy em {
  color: #ba7f75;
  font-size: .48rem;
  font-style: normal;
}

.buyback-list {
  display: grid;
  gap: 7px;
  padding: 9px;
}

.buyback-card {
  display: grid;
  grid-template-columns: 3rem minmax(0, 1fr) auto auto;
  align-items: center;
  gap: 9px;
  padding: 9px;
  border: 1px solid rgb(216 173 99 / 13%);
  border-radius: var(--ui-radius-md);
  background: rgb(255 255 255 / 1.5%);
}

.buyback-card__copy {
  display: grid;
  min-width: 0;
  gap: 2px;
}

.buyback-card__copy small {
  color: #928979;
  font-size: .49rem;
  text-transform: uppercase;
}

.buyback-card__copy strong {
  overflow: hidden;
  color: #ddd4c4;
  font-size: .67rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.buyback-card__copy span {
  color: #7f817c;
  font-size: .52rem;
}

.buyback-card__price {
  display: grid;
  justify-items: end;
  gap: 2px;
  white-space: nowrap;
}

.buyback-card__price small {
  color: #7c756c;
  font-size: .48rem;
}

.buyback-card__price b {
  color: #d7b874;
  font-size: .66rem;
}

@media (max-width: 620px) {
  .merchant-recommendations > div {
    grid-template-columns: 1fr;
  }

  .merchant-recommendations button {
    grid-template-columns: 2rem minmax(0, 1fr);
  }

  .merchant-filter__toggles {
    grid-template-columns: 1fr;
  }

  .merchant-detail__purchase {
    position: sticky;
    z-index: 3;
    bottom: 0;
    margin: 0 -13px -13px;
    padding: 10px 13px calc(10px + var(--ui-safe-area-bottom));
    background: linear-gradient(180deg, rgb(12 13 15 / 92%), #0a0b0d);
    box-shadow: 0 -.5rem 1rem rgb(0 0 0 / 24%);
    backdrop-filter: blur(8px);
  }

  .sell-toolbar {
    grid-template-columns: 1fr auto;
  }

  .sell-toolbar > span {
    grid-column: 1;
  }

  .sell-toolbar :deep(.ui-button) {
    grid-column: 1 / -1;
    width: 100%;
  }

  .sell-card {
    grid-template-columns: 1.2rem 2.7rem minmax(0, 1fr);
  }

  .sell-card__price {
    grid-column: 2 / -1;
  }

  .buyback-card {
    grid-template-columns: 2.7rem minmax(0, 1fr) auto;
  }

  .buyback-card > :deep(.ui-button) {
    grid-column: 1 / -1;
    width: 100%;
  }
}

@media (prefers-reduced-motion: reduce) {
  .offer-card.purchase-pulse {
    animation: none;
  }
}
</style>
