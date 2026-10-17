<script setup lang="ts">
import { computed, shallowRef } from 'vue'
import type { WorldPvpOffer } from '../useWorldPvpProgression'

const props = defineProps<{
  offers: WorldPvpOffer[]
  fieldHonor: number
  bloodyTokens: number
  characterLevel: number
  loading: boolean
  buying: boolean
}>()
const emit = defineEmits<{ buy: [offer: WorldPvpOffer, tokens: boolean] }>()
const affordableOnly = shallowRef(false)
const canUse = (offer: WorldPvpOffer) => props.characterLevel >= offer.requiredLevel
const affordable = (offer: WorldPvpOffer) =>
  canUse(offer) && (props.fieldHonor >= offer.fieldHonorPrice || props.bloodyTokens >= offer.tokenPrice)
const visibleOffers = computed(() =>
  affordableOnly.value ? props.offers.filter(affordable) : props.offers)
</script>

<template>
  <section class="war-shop" aria-label="Награды войны" data-world-pvp-shop>
    <header class="war-shop__header">
      <div><small>АРСЕНАЛ ГИЛЬДИИ</small><h4>Награды войны</h4></div>
      <label class="war-shop__filter">
        <input v-model="affordableOnly" type="checkbox">
        Только доступные
      </label>
    </header>
    <p class="war-shop__hint">Снаряжение под твой класс. Цена указана в полевой чести или кровавых жетонах.</p>
    <ul v-if="visibleOffers.length" class="war-shop__list">
      <li v-for="offer in visibleOffers" :key="offer.id" class="war-shop__item">
        <div class="war-shop__item-head">
          <strong>{{ offer.name }}</strong>
          <span>Ур. {{ offer.requiredLevel }}</span>
        </div>
        <small v-if="!canUse(offer)" class="war-shop__requirement">
          Будет доступно на {{ offer.requiredLevel }} уровне
        </small>
        <div class="war-shop__actions">
          <button type="button" data-buy-honor
            :disabled="loading || buying || !canUse(offer) || fieldHonor < offer.fieldHonorPrice"
            :aria-label="'Купить ' + offer.name + ' за ' + offer.fieldHonorPrice + ' чести'"
            @click="emit('buy', offer, false)">
            {{ offer.fieldHonorPrice }} чести
          </button>
          <button type="button"
            :disabled="loading || buying || !canUse(offer) || bloodyTokens < offer.tokenPrice"
            :aria-label="'Купить ' + offer.name + ' за ' + offer.tokenPrice + ' жетонов'"
            @click="emit('buy', offer, true)">
            {{ offer.tokenPrice }} жет.
          </button>
        </div>
      </li>
    </ul>
    <p v-else class="war-shop__empty" role="status">
      {{ affordableOnly ? 'Пока не хватает валюты или уровня для покупки. Отключи фильтр, чтобы увидеть награды.'
        : 'Сейчас нет наград для твоего класса.' }}
    </p>
  </section>
</template>

<style scoped>
.war-shop { display: grid; gap: 10px; min-width: 0; }
.war-shop__header { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: 10px; }
.war-shop__header small { color: #c6a46f; font-size: .65rem; font-weight: 800; letter-spacing: .1em; }
.war-shop h4 { margin: 4px 0 0; color: #f0e2c5; font-size: 1rem; }
.war-shop__filter { display: flex; min-height: 44px; align-items: center; gap: 7px; color: #d6c8b0; font-size: .76rem; cursor: pointer; }
.war-shop__filter input { width: 18px; height: 18px; accent-color: #bb9456; }
.war-shop__hint { margin: 0; color: #ac9f8b; font-size: .78rem; line-height: 1.45; }
.war-shop__list { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 220px), 1fr)); gap: 8px; margin: 0; padding: 0; list-style: none; }
.war-shop__item { display: grid; align-content: space-between; gap: 8px; padding: 12px; border: 1px solid rgb(183 145 81 / 23%); border-radius: 10px; background: #141921; }
.war-shop__item-head { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: baseline; gap: 4px 9px; }
.war-shop__item-head strong { color: #f0e4d1; font-size: .82rem; overflow-wrap: anywhere; }
.war-shop__item-head span { color: #bdac92; font-size: .73rem; }
.war-shop__requirement { color: #dfb19b; font-size: .72rem; }
.war-shop__actions { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 6px; }
.war-shop__actions button { min-height: 44px; padding: 7px 4px; border: 1px solid #8d7047; border-radius: 8px; background: #42301d; color: #ffdfad; font: inherit; font-size: .76rem; cursor: pointer; }
.war-shop__actions button:last-child { border-color: #8a575a; background: #352128; color: #f3c9cf; }
.war-shop__actions button:disabled { opacity: .43; cursor: not-allowed; }
.war-shop__actions button:focus-visible, .war-shop__filter input:focus-visible { outline: 2px solid #e6c481; outline-offset: 2px; }
.war-shop__empty { margin: 0; padding: 12px; color: #b5a791; font-size: .8rem; line-height: 1.45; }
</style>
