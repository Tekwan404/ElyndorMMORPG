<script setup lang="ts">
import ItemIcon from '@/game/items/components/ItemIcon.vue'
import type { PremiumStoreProduct } from '@/game/economy/premiumStoreCatalog'
import { PREMIUM_CURRENCY } from '@/game/economy/premiumStoreCatalog'

const props = withDefaults(defineProps<{
  product: PremiumStoreProduct
  layout?: 'hero' | 'card' | 'compact' | 'bundle'
}>(), { layout: 'card' })

const emit = defineEmits<{ open: [product: PremiumStoreProduct] }>()

const badgeLabel: Record<string, string> = {
  new: 'НОВОЕ',
  popular: 'ПОПУЛЯРНО',
  limited: 'ЛИМИТИРОВАННО',
}
</script>

<template>
  <button class="store-product" :class="[`store-product--${layout}`, `store-product--art-${product.artwork}`]" type="button" :data-product-id="product.id" @click="emit('open', props.product)">
    <span v-if="product.badge" class="store-product__badge">{{ badgeLabel[product.badge] }}</span>
    <span class="store-product__art" aria-hidden="true">
      <span v-if="product.quantity && product.quantity > 1" class="store-product__quantity">×{{ product.quantity }}</span>
      <ItemIcon
        v-if="product.iconId && product.itemDefinitionId"
        :icon-id="product.iconId"
        :item-id="product.itemDefinitionId"
        :name="product.title"
        :type="product.type === 'spatial-artifact' ? 'SpatialArtifact' : product.type === 'consumable' ? 'Material' : 'Premium'"
        :rarity="product.rarity ?? 'Common'"
        decorative
      />
      <span v-else class="store-product__glyph" />
    </span>
    <span class="store-product__copy">
      <strong>{{ product.title }}</strong>
      <small v-if="product.subtitle" class="store-product__subtitle">{{ product.subtitle }}</small>
      <small v-if="layout !== 'compact'" class="store-product__description">{{ product.description }}</small>
      <small v-if="product.unitPriceLabel" class="store-product__unit">{{ product.unitPriceLabel }}</small>
    </span>
    <span class="store-product__footer">
      <span v-if="product.equipped" class="store-product__state">ИСПОЛЬЗУЕТСЯ</span>
      <span v-else-if="product.owned && !product.repeatable" class="store-product__state">КУПЛЕНО</span>
      <span v-else class="store-product__cta">Подробнее</span>
      <span class="store-product__price">{{ PREMIUM_CURRENCY.symbol }} {{ product.price }}</span>
    </span>
  </button>
</template>

<style scoped>
.store-product{position:relative;display:grid;width:100%;min-width:0;overflow:hidden;padding:0;border:1px solid rgb(196 161 92 / 28%);border-radius:14px;background:linear-gradient(145deg,rgb(12 18 31 / 96%),rgb(7 10 19 / 98%));color:var(--ui-color-text-primary);text-align:left;box-shadow:0 10px 30px rgb(0 0 0 / 18%);cursor:pointer;touch-action:manipulation;transition:border-color .18s ease,transform .18s ease,box-shadow .18s ease}.store-product:active{transform:scale(.985)}.store-product:focus-visible{outline:2px solid #d4b269;outline-offset:2px}.store-product__badge{position:absolute;z-index:3;top:10px;left:10px;padding:5px 8px;border:1px solid rgb(214 178 101 / 45%);border-radius:999px;background:rgb(18 13 24 / 88%);color:#f1cf83;font-size:10px;font-weight:900;letter-spacing:.09em}.store-product__art{position:relative;display:grid;place-items:center;min-height:132px;overflow:hidden;background:radial-gradient(circle at 50% 35%,rgb(122 83 188 / 24%),transparent 47%),linear-gradient(180deg,rgb(20 29 47 / 65%),rgb(8 11 19 / 10%))}.store-product__art::after{content:"";position:absolute;inset:auto 12% 0;height:1px;background:linear-gradient(90deg,transparent,rgb(213 177 99 / 60%),transparent)}.store-product__quantity{position:absolute;z-index:2;right:9px;bottom:9px;display:grid;place-items:center;min-width:38px;min-height:26px;padding:2px 7px;border:1px solid rgb(222 181 99 / 42%);border-radius:999px;background:rgb(5 8 15 / 82%);color:#f1d790;font-size:12px;font-weight:900}.store-product__art :deep(img){width:min(58%,118px);height:min(58%,118px);object-fit:contain;filter:drop-shadow(0 10px 18px rgb(0 0 0 / 48%))}.store-product__glyph{width:78px;height:96px;border:1px solid rgb(217 177 96 / 38%);border-radius:48% 48% 38% 38%;background:radial-gradient(circle at 50% 25%,rgb(244 170 82 / 88%) 0 5%,transparent 6%),linear-gradient(160deg,rgb(135 54 45 / 52%),rgb(48 29 76 / 42%));box-shadow:0 0 34px rgb(132 70 186 / 14%)}.store-product--art-spatial-ring .store-product__glyph{width:88px;height:88px;border:11px solid #b68b47;border-radius:50%;background:radial-gradient(circle,rgb(108 66 171 / 58%) 0 34%,transparent 36%);box-shadow:inset 0 0 13px rgb(255 228 170 / 35%),0 0 24px rgb(112 72 173 / 20%)}.store-product--art-reforge .store-product__glyph{width:68px;height:68px;clip-path:polygon(50% 0,100% 37%,82% 100%,18% 100%,0 37%);background:linear-gradient(145deg,#8e75b8,#40345f)}.store-product--art-enhancement-ore .store-product__art{background:radial-gradient(circle at 50% 43%,rgb(224 136 57 / 34%),transparent 44%),linear-gradient(180deg,rgb(39 27 24 / 88%),rgb(9 11 18 / 24%))}.store-product--art-forge-scrap .store-product__art{background:radial-gradient(circle at 50% 43%,rgb(127 146 166 / 24%),transparent 45%),linear-gradient(180deg,rgb(23 30 38 / 85%),rgb(9 11 18 / 24%))}.store-product--art-profile-frame .store-product__glyph{width:76px;height:92px;border:7px double #ae874a;border-radius:10px;background:radial-gradient(circle,rgb(111 70 149 / 34%),transparent 68%)}.store-product--art-battle-entry .store-product__glyph{width:90px;height:90px;border:0;border-radius:50%;background:radial-gradient(circle,transparent 0 26%,rgb(150 91 213 / 54%) 28% 31%,transparent 33% 48%,rgb(197 153 79 / 48%) 50% 53%,transparent 55%)}.store-product--art-service .store-product__glyph{width:70px;height:84px;border-radius:50% 50% 14px 14px;background:linear-gradient(180deg,rgb(183 148 86 / 52%),rgb(45 37 62 / 58%))}.store-product--art-ash-border .store-product__glyph{width:120px;height:74px;border-radius:12px;background:radial-gradient(circle at 25% 55%,rgb(202 102 63 / 60%),transparent 26%),radial-gradient(circle at 73% 40%,rgb(122 80 173 / 65%),transparent 28%),linear-gradient(145deg,#39231f,#171525)}.store-product__copy{display:grid;gap:4px;padding:12px 13px 6px}.store-product__copy strong{font-size:14px;line-height:1.25}.store-product__subtitle,.store-product__unit{color:#d7b66f;font-size:12px;font-weight:700}.store-product__description{display:-webkit-box;overflow:hidden;color:var(--ui-color-text-muted);font-size:11px;line-height:1.35;-webkit-line-clamp:2;-webkit-box-orient:vertical}.store-product__unit{color:#9e91b9}.store-product__footer{display:flex;align-items:center;justify-content:space-between;gap:8px;min-height:46px;padding:7px 13px 11px}.store-product__price{margin-left:auto;color:#f0d792;font-size:14px;font-weight:900;white-space:nowrap}.store-product__state{color:#9eb7a1;font-size:10px;font-weight:900;letter-spacing:.06em}.store-product__cta{color:#8f8998;font-size:10px;font-weight:800}.store-product--hero{grid-template-columns:minmax(0,1fr);min-height:330px}.store-product--hero .store-product__art{min-height:205px;background:radial-gradient(circle at 55% 34%,rgb(180 74 47 / 34%),transparent 38%),radial-gradient(circle at 45% 48%,rgb(113 65 169 / 18%),transparent 50%),linear-gradient(160deg,#16101b,#09101a 72%)}.store-product--hero .store-product__glyph{width:104px;height:150px}.store-product--hero .store-product__copy strong{font-size:21px}.store-product--hero .store-product__description{font-size:12px;-webkit-line-clamp:1}.store-product--compact{grid-template-columns:96px minmax(0,1fr);grid-template-areas:"art copy" "art footer";min-height:124px}.store-product--compact .store-product__art{grid-area:art;min-height:124px}.store-product--compact .store-product__art :deep(img){width:72px;height:72px}.store-product--compact .store-product__glyph{width:50px;height:58px}.store-product--compact .store-product__copy{grid-area:copy;padding:12px 10px 0}.store-product--compact .store-product__footer{grid-area:footer;padding:4px 10px 10px}.store-product--bundle .store-product__art{min-height:158px}.store-product--bundle .store-product__glyph{width:150px;height:84px}@media (hover:hover){.store-product:hover{border-color:rgb(217 178 96 / 52%);box-shadow:0 14px 34px rgb(0 0 0 / 28%)}}@media (min-width:700px){.store-product--hero{grid-template-columns:48% 52%;grid-template-areas:"copy art" "footer art";min-height:300px}.store-product--hero .store-product__art{grid-area:art;height:100%;min-height:300px}.store-product--hero .store-product__copy{grid-area:copy;align-self:end;padding:38px 22px 8px}.store-product--hero .store-product__footer{grid-area:footer;padding:8px 22px 28px}}
</style>
