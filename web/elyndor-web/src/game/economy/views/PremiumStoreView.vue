<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'

import type { PremiumStoreSnapshot } from '@/api/contracts'
import { createAppearancePreviewController } from '@/game/character/appearancePreviewController'
import PremiumStoreProductCard from '@/game/economy/components/PremiumStoreProductCard.vue'
import CharacterSkinStoreView from '@/game/economy/views/CharacterSkinStoreView.vue'
import ItemIcon from '@/game/items/components/ItemIcon.vue'
import {
  buildPremiumStoreProducts,
  PREMIUM_CURRENCY,
  PREMIUM_STORE_TABS,
  purchaseLabel,
  type PremiumStoreCategory,
  type PremiumStoreProduct,
} from '@/game/economy/premiumStoreCatalog'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UILoadingState } from '@/ui/components'

const session = useGameSessionStore()
const store = ref<PremiumStoreSnapshot | null>(null)
const skinBalance = ref<number | null>(null)
const error = ref<string | null>(null)
const promoCode = ref('')
const activeCategory = ref<PremiumStoreCategory>('recommended')
const selectedProduct = ref<PremiumStoreProduct | null>(null)
const currencySheetOpen = ref(false)
const previewCosmeticId = ref<string | null>(null)
const equippedCosmeticId = ref<string | null>(null)
const promoPending = computed(() => session.isMutationDomainPending('premium:promo:'))
const selectedProductPending = computed(() => Boolean(
  selectedProduct.value?.sku
  && session.isMutationPending(`premium:buy:${selectedProduct.value.sku}`),
))

const previewController = createAppearancePreviewController({
  capture: () => previewCosmeticId.value,
  applyPreview: (cosmeticId) => { previewCosmeticId.value = cosmeticId },
  restore: (snapshot) => { previewCosmeticId.value = snapshot },
})

const products = computed(() => store.value ? buildPremiumStoreProducts(store.value).map((product) => ({
  ...product,
  equipped: Boolean(product.cosmeticId && equippedCosmeticId.value === product.cosmeticId),
})) : [])

const featured = computed(() => products.value.find((product) => product.featured))
const spatialFeatured = computed(() => products.value.find((product) => product.id === 'wanderer-spatial-ring'))
const forgeSupplies = computed(() => products.value.filter((product) => ['enhancement-ore-20', 'reforge-stones-10', 'forge-scrap-20'].includes(product.id)))
const popular = computed(() => products.value.filter((product) => ['rename-character', 'profile-frame', 'battle-entry-effect'].includes(product.id)))
const bundles = computed(() => products.value.filter((product) => product.type === 'bundle'))
const categoryProducts = computed(() => products.value.filter((product) => product.category === activeCategory.value))

function formatBalance(value: number): string {
  return new Intl.NumberFormat('ru-RU').format(value)
}

function updateSkinBalance(balance: number): void {
  skinBalance.value = balance
  if (store.value) store.value = { ...store.value, crystalBalance: balance }
}

async function load(): Promise<void> {
  error.value = null
  try {
    store.value = await session.getPremiumStore()
    skinBalance.value = null
  } catch {
    error.value = 'Не удалось загрузить лавку.'
  }
}

function openProduct(product: PremiumStoreProduct): void {
  previewController.close()
  selectedProduct.value = product
}

function closeProduct(): void {
  previewController.close()
  selectedProduct.value = null
}

function preview(product: PremiumStoreProduct): void {
  if (!product.previewable || !product.cosmeticId) return
  previewController.preview(product.cosmeticId)
}

function equipOwned(product: PremiumStoreProduct): void {
  if (!product.owned || !product.cosmeticId) return
  equippedCosmeticId.value = product.cosmeticId
  previewController.close()
}

async function purchase(product: PremiumStoreProduct): Promise<void> {
  if (product.owned && !product.repeatable) {
    equipOwned(product)
    return
  }
  if (!product.backendBacked || !product.sku || !product.canPurchase) return
  const result = await session.buyPremiumStoreOffer(product.sku)
  if (!result) {
    error.value = 'Покупка не выполнена.'
    return
  }
  await load()
  const refreshed = products.value.find((item) => item.id === product.id || item.sku === product.sku)
  selectedProduct.value = refreshed ?? null
  if (refreshed?.cosmeticId && refreshed.owned) equippedCosmeticId.value = refreshed.cosmeticId
}

async function redeem(): Promise<void> {
  if (!promoCode.value.trim()) return
  const result = await session.redeemPromoCode(promoCode.value)
  if (!result) {
    error.value = 'Промокод не удалось применить.'
    return
  }
  promoCode.value = ''
  await load()
}

function selectCategory(category: PremiumStoreCategory): void {
  activeCategory.value = category
}

onMounted(load)
onBeforeUnmount(() => previewController.close())
</script>

<template>
  <section class="premium-store">
    <header class="store-header">
      <div>
        <small>ELYNDOR</small>
        <h1>ЛАВКА</h1>
      </div>
      <div class="currency-balance" :title="PREMIUM_CURRENCY.displayName">
        <span aria-hidden="true">{{ PREMIUM_CURRENCY.symbol }}</span>
        <strong>{{ formatBalance(skinBalance ?? store?.crystalBalance ?? 0) }}</strong>
        <button type="button" aria-label="Пополнить Осколки Эфира" @click="currencySheetOpen = true">+</button>
      </div>
    </header>

    <nav class="store-tabs" aria-label="Категории лавки">
      <button
        v-for="tab in PREMIUM_STORE_TABS"
        :key="tab.id"
        type="button"
        :class="{ active: activeCategory === tab.id }"
        @click="selectCategory(tab.id)"
      >
        {{ tab.label }}
      </button>
    </nav>

    <CharacterSkinStoreView v-if="activeCategory === 'skins'" @balance="updateSkinBalance" />
    <UILoadingState v-else-if="!store && !error" state="loading" title="Открываем лавку" />
    <div v-else-if="error && !store" class="store-error">
      <strong>{{ error }}</strong>
      <UIButton variant="secondary" @click="load">Повторить</UIButton>
    </div>

    <template v-else-if="store">
      <main v-if="activeCategory === 'recommended'" class="store-home">
        <PremiumStoreProductCard v-if="featured" :product="featured" layout="hero" @open="openProduct" />

        <section v-if="spatialFeatured" class="store-section">
          <div class="section-heading">
            <div><small>ЕДИНОЕ ХРАНИЛИЩЕ</small><h2>Пространственные артефакты</h2></div>
            <button type="button" @click="selectCategory('artifacts')">Все</button>
          </div>
          <PremiumStoreProductCard :product="spatialFeatured" layout="compact" @open="openProduct" />
          <p class="section-note">Артефакт увеличивает общую вместимость текущего инвентаря. Отдельные сумки не создаются.</p>
        </section>

        <section v-if="forgeSupplies.length" class="store-section forge-section" data-forge-supplies>
          <div class="section-heading">
            <div><small>УСИЛЕНИЕ СНАРЯЖЕНИЯ</small><h2>Кузнечные припасы</h2></div>
            <button type="button" @click="selectCategory('convenience')">Все</button>
          </div>
          <p class="forge-section__lead">Материалы доставляются прямо в инвентарь. Цена и количество подтверждаются сервером.</p>
          <div class="forge-grid">
            <PremiumStoreProductCard v-for="product in forgeSupplies" :key="product.id" :product="product" layout="compact" @open="openProduct" />
          </div>
        </section>

        <section class="store-section">
          <div class="section-heading"><div><small>ЧАСТО ВЫБИРАЮТ</small><h2>Популярное</h2></div></div>
          <div class="popular-grid">
            <PremiumStoreProductCard v-for="product in popular" :key="product.id" :product="product" layout="compact" @open="openProduct" />
          </div>
        </section>

        <section v-if="bundles.length" class="store-section">
          <div class="section-heading"><div><small>ТЕМАТИЧЕСКИЕ КОЛЛЕКЦИИ</small><h2>Наборы</h2></div></div>
          <PremiumStoreProductCard v-for="product in bundles" :key="product.id" :product="product" layout="bundle" @open="openProduct" />
        </section>

        <details class="promo-panel">
          <summary>Есть промокод?</summary>
          <form class="promo-form" @submit.prevent="redeem">
            <input v-model="promoCode" aria-label="Промокод" autocomplete="off" maxlength="64" placeholder="ВВЕДИТЕ КОД" :disabled="promoPending" />
            <UIButton type="submit" variant="secondary" :disabled="!promoCode.trim() || promoPending" :loading="promoPending">Применить</UIButton>
          </form>
        </details>
      </main>

      <main v-else class="category-view">
        <div class="category-title">
          <small>{{ activeCategory === 'cosmetics' ? 'ВНЕШНИЙ ВИД БЕЗ ХАРАКТЕРИСТИК' : activeCategory === 'artifacts' ? 'ПОЛЕЗНЫЕ АРТЕФАКТЫ' : activeCategory === 'convenience' ? 'QOL И РАСХОДНИКИ' : 'ПЕРСОНАЛЬНЫЕ УСЛУГИ' }}</small>
          <h2>{{ PREMIUM_STORE_TABS.find((tab) => tab.id === activeCategory)?.label }}</h2>
        </div>
        <div class="category-grid" :class="{ 'category-grid--cosmetics': activeCategory === 'cosmetics' }">
          <PremiumStoreProductCard
            v-for="product in categoryProducts"
            :key="product.id"
            :product="product"
            :layout="activeCategory === 'cosmetics' ? 'hero' : 'card'"
            @open="openProduct"
          />
        </div>
        <p v-if="!categoryProducts.length" class="empty-category">В этой категории пока нет доступных товаров.</p>
      </main>
    </template>

    <p v-if="error && store" class="inline-error">{{ error }}</p>

    <div v-if="selectedProduct" class="sheet-backdrop" role="presentation" @click.self="closeProduct">
      <section class="product-sheet" role="dialog" aria-modal="true" :aria-label="selectedProduct.title">
        <button class="sheet-close" type="button" aria-label="Закрыть" @click="closeProduct">×</button>
        <div class="product-preview" :class="[`product-preview--${selectedProduct.artwork}`, { 'is-previewing': previewCosmeticId === selectedProduct.cosmeticId }]">
          <span v-if="selectedProduct.badge" class="detail-badge">{{ selectedProduct.badge === 'new' ? 'НОВОЕ' : selectedProduct.badge === 'popular' ? 'ПОПУЛЯРНО' : 'ЛИМИТИРОВАННО' }}</span>
          <ItemIcon
            v-if="selectedProduct.iconId && selectedProduct.itemDefinitionId"
            class="product-preview__item"
            :icon-id="selectedProduct.iconId"
            :item-id="selectedProduct.itemDefinitionId"
            :name="selectedProduct.title"
            :type="selectedProduct.type === 'spatial-artifact' ? 'SpatialArtifact' : selectedProduct.type === 'consumable' ? 'Material' : 'Premium'"
            :rarity="selectedProduct.rarity ?? 'Common'"
            decorative
          />
          <div v-else class="preview-figure" aria-hidden="true" />
          <small v-if="previewCosmeticId === selectedProduct.cosmeticId">РЕЖИМ ПРИМЕРКИ</small>
        </div>
        <div class="product-detail">
          <small>{{ selectedProduct.subtitle }}</small>
          <h2>{{ selectedProduct.title }}</h2>
          <p>{{ selectedProduct.description }}</p>
          <p v-if="selectedProduct.inventoryCapacity" class="effect-line">+{{ selectedProduct.inventoryCapacity }} ячеек к единому инвентарю</p>
          <ul v-if="selectedProduct.bundle" class="bundle-list">
            <li v-for="itemId in selectedProduct.bundle" :key="itemId">{{ products.find((item) => item.id === itemId)?.title ?? itemId }}</li>
          </ul>
          <div class="detail-actions">
            <UIButton v-if="selectedProduct.previewable && selectedProduct.cosmeticId" variant="secondary" @click="preview(selectedProduct)">ПРИМЕРИТЬ</UIButton>
            <UIButton
              :disabled="(!selectedProduct.backendBacked && !selectedProduct.owned) || (!selectedProduct.canPurchase && !selectedProduct.owned) || selectedProductPending"
              :loading="selectedProductPending"
              @click="purchase(selectedProduct)"
            >
              {{ selectedProduct.backendBacked || selectedProduct.owned ? purchaseLabel(selectedProduct) : `${PREMIUM_CURRENCY.symbol} ${selectedProduct.price} — ПРИОБРЕСТИ` }}
            </UIButton>
          </div>
          <small v-if="!selectedProduct.backendBacked" class="foundation-note">Витринный контент подготовлен; покупка будет активна после подключения соответствующего domain-товара или сервиса.</small>
        </div>
      </section>
    </div>

    <div v-if="currencySheetOpen" class="sheet-backdrop" role="presentation" @click.self="currencySheetOpen = false">
      <section class="currency-sheet" role="dialog" aria-modal="true" aria-label="Осколки Эфира">
        <button class="sheet-close" type="button" aria-label="Закрыть" @click="currencySheetOpen = false">×</button>
        <small>ПРЕМИАЛЬНАЯ ВАЛЮТА</small>
        <h2>{{ PREMIUM_CURRENCY.displayName }}</h2>
        <div class="ether-mark">{{ PREMIUM_CURRENCY.symbol }}</div>
        <p>Баланс: <strong>{{ formatBalance(skinBalance ?? store?.crystalBalance ?? 0) }}</strong></p>
        <p class="foundation-note">Экран готов как точка входа для purchase packs. Реальный billing в этой задаче не подключается.</p>
      </section>
    </div>
  </section>
</template>

<style scoped>
.premium-store{display:grid;gap:14px;width:100%;min-width:0;max-width:920px;margin:0 auto;padding:2px 0 24px;overflow-x:clip}.store-header{display:flex;align-items:center;justify-content:space-between;gap:12px;padding:8px 2px 2px}.store-header>div:first-child{display:grid;gap:1px}.store-header small,.section-heading small,.category-title small,.currency-sheet>small{color:#8b819e;font-size:10px;font-weight:900;letter-spacing:.16em}.store-header h1{margin:0;color:#e9d8ad;font-family:Georgia,'Times New Roman',serif;font-size:22px;letter-spacing:.12em}.currency-balance{display:flex;align-items:center;gap:7px;min-height:44px;padding:5px 6px 5px 12px;border:1px solid rgb(195 155 77 / 35%);border-radius:999px;background:linear-gradient(100deg,rgb(46 30 67 / 58%),rgb(11 16 27 / 86%));color:#f0d48c}.currency-balance>span{color:#b995df;font-size:18px}.currency-balance strong{font-size:14px;white-space:nowrap}.currency-balance button{display:grid;place-items:center;width:34px;height:34px;border:1px solid rgb(214 177 98 / 50%);border-radius:50%;background:#2a2135;color:#f1d697;font-size:22px;line-height:1;cursor:pointer}.store-tabs{display:flex;gap:5px;width:100%;overflow-x:auto;padding:1px 2px 6px;scrollbar-width:none;overscroll-behavior-inline:contain}.store-tabs::-webkit-scrollbar{display:none}.store-tabs button{flex:0 0 auto;min-height:44px;padding:0 14px;border:1px solid transparent;border-radius:999px;background:transparent;color:#918b9d;font:inherit;font-size:12px;font-weight:800;white-space:nowrap;cursor:pointer}.store-tabs button.active{border-color:rgb(208 169 88 / 36%);background:linear-gradient(180deg,rgb(67 47 80 / 58%),rgb(20 21 33 / 72%));color:#ead39c}.store-home,.category-view{display:grid;gap:20px;min-width:0}.store-section{display:grid;gap:9px;min-width:0}.section-heading{display:flex;align-items:end;justify-content:space-between;gap:10px;padding:0 2px}.section-heading>div,.category-title{display:grid;gap:2px}.section-heading h2,.category-title h2,.product-detail h2,.currency-sheet h2{margin:0;color:#eee5d2;font-family:Georgia,'Times New Roman',serif;font-size:19px;font-weight:700}.section-heading button{min-width:44px;min-height:44px;border:0;background:transparent;color:#b99b61;font:inherit;font-size:11px;font-weight:800;cursor:pointer}.section-note{margin:-1px 4px 0;color:#85808e;font-size:11px;line-height:1.45}.forge-section{position:relative;padding:14px 12px 12px;border:1px solid rgb(193 133 68 / 28%);border-radius:16px;background:radial-gradient(circle at 12% 0,rgb(199 105 44 / 14%),transparent 35%),linear-gradient(150deg,rgb(27 21 23 / 82%),rgb(8 12 20 / 86%));box-shadow:inset 0 1px rgb(255 214 139 / 5%)}.forge-section::before{content:"";position:absolute;inset:0 auto 0 0;width:2px;border-radius:16px;background:linear-gradient(transparent,#d18946,transparent)}.forge-section__lead{margin:0;color:#a59b91;font-size:11px;line-height:1.45}.forge-grid,.popular-grid,.category-grid{display:grid;grid-template-columns:1fr;gap:8px;min-width:0}.promo-panel{padding:10px 12px;border:1px solid rgb(157 134 91 / 20%);border-radius:12px;background:rgb(8 12 21 / 54%);color:#a79eae;font-size:12px}.promo-panel summary{min-height:36px;cursor:pointer;font-weight:800}.promo-form{display:grid;grid-template-columns:minmax(0,1fr) auto;gap:7px;padding-top:8px}.promo-form input{min-width:0;min-height:44px;padding:0 11px;border:1px solid rgb(157 134 91 / 28%);border-radius:9px;background:rgb(0 0 0 / 22%);color:var(--ui-color-text-primary);font:inherit}.category-title{padding:5px 2px 0}.category-title h2{font-size:24px}.empty-category,.inline-error{margin:0;padding:14px;border:1px solid rgb(170 130 80 / 20%);border-radius:10px;color:#8f8998;text-align:center}.store-error{display:grid;gap:10px;justify-items:center;padding:28px 18px;border:1px solid rgb(184 84 84 / 32%);border-radius:12px;color:var(--ui-color-danger)}.inline-error{color:var(--ui-color-danger)}.sheet-backdrop{position:fixed;z-index:80;inset:0;display:flex;align-items:flex-end;justify-content:center;padding:18px 10px max(10px,env(safe-area-inset-bottom));background:rgb(2 5 10 / 76%);backdrop-filter:blur(4px)}.product-sheet,.currency-sheet{position:relative;width:min(100%,620px);max-height:min(88dvh,760px);overflow-y:auto;border:1px solid rgb(211 171 91 / 38%);border-radius:20px 20px 12px 12px;background:linear-gradient(165deg,#111728,#080b13 72%);box-shadow:0 -18px 60px rgb(0 0 0 / 48%)}.sheet-close{position:absolute;z-index:5;top:10px;right:10px;display:grid;place-items:center;width:44px;height:44px;border:1px solid rgb(223 194 139 / 22%);border-radius:50%;background:rgb(6 9 16 / 65%);color:#d9c8a6;font-size:25px;cursor:pointer}.product-preview{position:relative;display:grid;place-items:center;min-height:260px;overflow:hidden;background:radial-gradient(circle at 50% 38%,rgb(121 70 177 / 22%),transparent 44%),linear-gradient(180deg,#171927,#0c101a)}.product-preview.is-previewing{background:radial-gradient(circle at 50% 38%,rgb(190 96 63 / 33%),transparent 40%),radial-gradient(circle at 50% 50%,rgb(121 70 177 / 20%),transparent 58%),linear-gradient(180deg,#171927,#0c101a)}.product-preview>small{position:absolute;bottom:10px;color:#e1bd73;font-size:10px;font-weight:900;letter-spacing:.15em}.product-preview__item{width:148px;height:148px;filter:drop-shadow(0 20px 32px rgb(0 0 0 / 52%))}.product-preview__item :deep(img){width:100%;height:100%;object-fit:contain}.preview-figure{width:116px;height:176px;border:1px solid rgb(225 183 96 / 36%);border-radius:52% 52% 35% 35%;background:radial-gradient(circle at 50% 19%,#e2ad7b 0 8%,transparent 9%),linear-gradient(155deg,rgb(153 63 48 / 72%),rgb(58 37 91 / 72%));box-shadow:0 18px 45px rgb(0 0 0 / 42%)}.product-preview--spatial-ring .preview-figure{width:128px;height:128px;border:17px solid #b68b47;border-radius:50%;background:radial-gradient(circle,rgb(110 64 174 / 58%) 0 34%,transparent 36%);box-shadow:inset 0 0 17px rgb(255 230 178 / 32%),0 0 38px rgb(113 72 175 / 24%)}.product-preview--ash-border .preview-figure{width:220px;height:120px;border-radius:15px;background:radial-gradient(circle at 25% 55%,rgb(202 102 63 / 60%),transparent 25%),radial-gradient(circle at 73% 40%,rgb(122 80 173 / 65%),transparent 28%),linear-gradient(145deg,#39231f,#171525)}.detail-badge{position:absolute;z-index:2;top:14px;left:14px;padding:5px 8px;border:1px solid rgb(214 178 101 / 45%);border-radius:999px;background:rgb(18 13 24 / 82%);color:#f1cf83;font-size:10px;font-weight:900;letter-spacing:.09em}.product-detail{display:grid;gap:9px;padding:18px 16px 20px}.product-detail>small:first-child{color:#c1a567;font-size:11px;font-weight:800}.product-detail h2{font-size:23px}.product-detail p{margin:0;color:#aaa4b0;font-size:13px;line-height:1.5}.product-detail .effect-line{color:#d5b570;font-weight:800}.bundle-list{display:grid;gap:5px;margin:0;padding:10px 10px 10px 28px;border:1px solid rgb(195 155 77 / 18%);border-radius:10px;color:#a9a1af;font-size:12px}.detail-actions{display:grid;grid-template-columns:1fr;gap:8px;margin-top:5px}.detail-actions :deep(.ui-button){min-height:48px}.foundation-note{color:#716d7b!important;font-size:10px!important;line-height:1.4!important}.currency-sheet{display:grid;justify-items:center;gap:9px;padding:28px 20px;text-align:center}.currency-sheet h2{font-size:25px}.ether-mark{display:grid;place-items:center;width:74px;height:74px;margin:8px 0;border:1px solid rgb(188 146 223 / 45%);border-radius:50%;background:radial-gradient(circle,rgb(115 73 168 / 44%),rgb(17 18 30 / 65%));color:#c8a4e4;font-size:34px}.currency-sheet p{margin:0;color:#aaa3b1;font-size:13px}@media (min-width:390px){.forge-grid{grid-template-columns:repeat(2,minmax(0,1fr))}.forge-grid>:first-child{grid-column:1/-1}.popular-grid{grid-template-columns:repeat(2,minmax(0,1fr))}.popular-grid :deep(.store-product--compact){grid-template-columns:1fr;grid-template-areas:"art" "copy" "footer"}.popular-grid :deep(.store-product--compact .store-product__art){min-height:106px}.popular-grid :deep(.store-product--compact .store-product__copy){padding-top:9px}}@media (min-width:700px){.premium-store{padding-inline:10px}.forge-grid{grid-template-columns:repeat(3,minmax(0,1fr))}.forge-grid>:first-child{grid-column:auto}.category-grid{grid-template-columns:repeat(2,minmax(0,1fr))}.category-grid--cosmetics{grid-template-columns:1fr}.detail-actions{grid-template-columns:1fr 1.5fr}.sheet-backdrop{align-items:center}.product-sheet{display:grid;grid-template-columns:48% 52%;border-radius:20px}.product-preview{min-height:440px}.product-detail{align-content:center;padding:30px 24px}.currency-sheet{border-radius:20px}}
</style>
