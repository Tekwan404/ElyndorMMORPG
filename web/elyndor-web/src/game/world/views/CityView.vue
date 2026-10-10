<script setup lang="ts">
import { defineAsyncComponent, ref, watch } from 'vue'

import cityArt from '@/assets/world/elyndor-city-background.webp'
import guildArt from '@/assets/world/city-guild.webp'
import adventurersArt from '@/assets/world/city-adventurers.webp'
import auctionArt from '@/assets/world/city-auction.webp'
import marketArt from '@/assets/world/city-market.webp'
import bankArt from '@/assets/world/city-bank.webp'
import craftArt from '@/assets/world/city-craft.webp'
import arenaArt from '@/assets/world/city-arena.webp'
import { useGameSessionStore } from '@/stores/gameSession'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useWorldBossStore } from '@/game/worldBoss/worldBossStore'
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { GlyphName } from '@/ui/icons/icon.types'
import { UIButton } from '@/ui/components'

const ForgeView = defineAsyncComponent(() => import('@/game/character/views/ForgeView.vue'))
const AuctionView = defineAsyncComponent(() => import('@/game/economy/views/AuctionView.vue'))
const MailboxView = defineAsyncComponent(() => import('@/game/economy/views/MailboxView.vue'))
const PremiumStoreView = defineAsyncComponent(() => import('@/game/economy/views/PremiumStoreView.vue'))
const MerchantShop = defineAsyncComponent(() => import('@/game/world/components/MerchantShop.vue'))
const AdventurerGuildBoard = defineAsyncComponent(() => import('@/game/world/components/AdventurerGuildBoard.vue'))
const ArenaView = defineAsyncComponent(() => import('@/game/pvp/views/ArenaView.vue'))
const ProfessionView = defineAsyncComponent(() => import('@/game/professions/views/ProfessionView.vue'))

type CityDestination =
  | 'guild' | 'adventurers' | 'auction' | 'market'
  | 'teleport' | 'bank' | 'craft' | 'arena' | 'professions' | 'store' | 'mailbox'

const props = withDefaults(defineProps<{ openAdventurers?: boolean }>(), { openAdventurers: false })
const emit = defineEmits<{
  'open-map': []
  'open-inventory': []
  'open-party': []
  'open-world-boss': []
}>()
const session = useGameSessionStore()
const combat = useCombatSessionStore()
const worldBoss = useWorldBossStore()
const activeDestination = ref<CityDestination | null>(null)
const merchantOpen = ref(false)
const forgeOpen = ref(false)
const adventurersOpen = ref(false)
watch(() => props.openAdventurers, (open) => {
  if (open) adventurersOpen.value = true
}, { immediate: true })

// Original medallions are decorative; destination names stay readable DOM text.
const cityMarkers: readonly {
  id: CityDestination
  label: string
  glyph: GlyphName
  x: number
  y: number
  art: string | null
}[] = [
  { id: 'guild', label: 'Гильдия', glyph: 'shield', x: 50, y: 27, art: guildArt },
  { id: 'adventurers', label: 'Гильдия авантюристов', glyph: 'star', x: 19, y: 35, art: adventurersArt },
  { id: 'auction', label: 'Аукцион', glyph: 'ring', x: 81, y: 35, art: auctionArt },
  { id: 'market', label: 'Рынок', glyph: 'chest', x: 20, y: 60, art: marketArt },
  { id: 'teleport', label: 'Телепорт', glyph: 'staff', x: 50, y: 66, art: null },
  { id: 'bank', label: 'Банк', glyph: 'chest', x: 81, y: 60, art: bankArt },
  { id: 'craft', label: 'Ремесленный квартал', glyph: 'axe', x: 19, y: 85, art: craftArt },
  { id: 'arena', label: 'Арена', glyph: 'helmet', x: 81, y: 85, art: arenaArt },
]

const destinationNames: Record<CityDestination, string> = {
  guild: 'Гильдия',
  adventurers: 'Гильдия авантюристов',
  auction: 'Аукцион',
  market: 'Рынок',
  teleport: 'Телепорт',
  bank: 'Банк',
  craft: 'Ремесленный квартал',
  arena: 'Арена',
  professions: 'Профессии',
  store: 'Магазин',
  mailbox: 'Почта',
}

function openDestination(id: CityDestination): void {
  if (id === 'teleport') {
    emit('open-map')
  } else if (id === 'adventurers') {
    adventurersOpen.value = true
  } else {
    activeDestination.value = id
  }
}

function returnToCity(): void {
  activeDestination.value = null
}

async function startTraining(): Promise<void> {
  if (session.snapshot?.world?.travel || session.mutationPending || combat.isActive || combat.pending) return
  await combat.startTraining()
}
</script>

<template>
  <section class="city-hub" aria-label="Город" data-city-hub>
    <div v-if="!activeDestination" class="city-map" :class="{ 'city-map--boss-active': worldBoss.active && worldBoss.active.currentHealth > 0 }" data-city-map>
      <img class="city-map__art" :src="cityArt" alt="Стартовый город — улицы, здания и площади" />
      <div class="city-map__top-shade" aria-hidden="true" />
      <div class="city-map__heading">
        <small>ELYNDOR · БЕЗОПАСНАЯ ЗОНА</small>
        <h1>Стартовый город</h1>
      </div>
      <button
        v-if="worldBoss.active && worldBoss.active.currentHealth > 0"
        type="button"
        class="city-map__boss"
        data-city-world-boss
        @click="emit('open-world-boss')"
      >Мировой босс</button>
      <button
        v-for="marker in cityMarkers"
        :key="marker.id"
        type="button"
        class="city-marker"
        :class="{ 'city-marker--wide': marker.id === 'adventurers' || marker.id === 'craft' }"
        :data-city-marker="marker.id"
        :style="{ left: marker.x + '%', top: marker.y + '%' }"
        :aria-label="'Открыть: ' + marker.label"
        @click="openDestination(marker.id)"
      >
        <span v-if="marker.art" class="city-marker__medallion" aria-hidden="true">
          <img :src="marker.art" alt="" draggable="false" />
        </span>
        <span v-else class="city-marker__seal" aria-hidden="true">
          <IconGenerator :config="{ id: 'city-' + marker.id, glyph: marker.glyph, category: 'utility' }" />
        </span>
        <span class="city-marker__name">{{ marker.label }}</span>
        <span class="city-marker__arrow" aria-hidden="true" />
      </button>
    </div>

    <div v-else class="city-interior" data-city-interior>
      <header class="city-interior__header">
        <UIButton variant="ghost" data-city-back @click="returnToCity">‹ Город</UIButton>
        <h2>{{ destinationNames[activeDestination] }}</h2>
      </header>

      <AuctionView v-if="activeDestination === 'auction'" />
      <div v-else-if="activeDestination === 'arena'" class="city-interior__arena">
        <UIButton data-city-training :loading="combat.lifecyclePending" :disabled="session.mutationPending || combat.pending || combat.isActive" @click="startTraining">Тренировка на манекене</UIButton>
        <ArenaView />
      </div>
      <PremiumStoreView v-else-if="activeDestination === 'store'" />
      <MailboxView v-else-if="activeDestination === 'mailbox'" />
      <ProfessionView v-else-if="activeDestination === 'professions'" />

      <div v-else-if="activeDestination === 'market'" class="city-interior__choices">
        <p>Торговые ряды. Припасы, товары за кристаллы и доставка покупок.</p>
        <UIButton data-city-merchant @click="merchantOpen = true">Лавка Маркуса</UIButton>
        <UIButton variant="secondary" data-city-store @click="activeDestination = 'store'">Магазин за кристаллы</UIButton>
        <UIButton variant="secondary" data-city-mailbox @click="activeDestination = 'mailbox'">Почта</UIButton>
      </div>

      <div v-else-if="activeDestination === 'craft'" class="city-interior__choices">
        <p>Выделка кожи и изготовление снаряжения — в кожевенной мастерской. Улучшение и перековка готовых вещей — в кузнице.</p>
        <UIButton data-city-forge @click="forgeOpen = true">Кузница</UIButton>
        <UIButton variant="secondary" data-city-professions @click="activeDestination = 'professions'">Кожевенная мастерская</UIButton>
      </div>

      <div v-else-if="activeDestination === 'bank'" class="city-interior__choices">
        <p>Банковское хранилище ещё не открыто. Пространственные артефакты и доступные слоты сейчас находятся в инвентаре.</p>
        <UIButton data-city-inventory @click="emit('open-inventory')">Открыть инвентарь</UIButton>
      </div>

      <div v-else-if="activeDestination === 'guild'" class="city-interior__choices">
        <p>Гильдейское здание готовится к открытию. Управлять текущей группой можно уже сейчас.</p>
        <UIButton data-city-party @click="emit('open-party')">Моя группа</UIButton>
      </div>
    </div>

    <MerchantShop v-if="merchantOpen" :open="merchantOpen" @close="merchantOpen = false" />
    <ForgeView v-if="forgeOpen" @close="forgeOpen = false" />
    <AdventurerGuildBoard
      v-if="adventurersOpen"
      :open="adventurersOpen"
      :location-id="session.snapshot?.world?.currentLocation.id ?? ''"
      @close="adventurersOpen = false"
    />
  </section>
</template>

<style scoped>
.city-hub {
  width: 100%;
  min-width: 0;
  height: 100%;
  background: #090d15;
}
.city-map {
  position: relative;
  width: 100%;
  height: 100%;
  min-height: 340px;
  isolation: isolate;
  overflow: hidden;
  background: #101620;
}
.city-map__art {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: cover;
  object-position: center 42%;
}
.city-map__top-shade {
  position: absolute;
  inset: 0;
  background: linear-gradient(180deg, #080b15d9, transparent 20%, transparent 85%, #080b1570),
    linear-gradient(90deg, #080b1533, transparent 30%, transparent 70%, #080b1533);
  pointer-events: none;
}
.city-map__heading {
  position: absolute;
  top: 13px;
  left: 50%;
  width: max-content;
  text-align: center;
  transform: translateX(-50%);
  z-index: 1;
  display: grid;
  gap: 4px;
  color: #f6e9cf;
  text-shadow: 0 2px 6px black;
  pointer-events: none;
}
.city-map__heading small {
  color: #d2b574;
  font-size: 0.6rem;
  letter-spacing: 0.14em;
}
.city-map--boss-active .city-map__heading {
  left: 12px;
  width: calc(100% - 132px);
  text-align: left;
  transform: none;
}
.city-map--boss-active .city-map__heading small {
  font-size: 0.52rem;
  letter-spacing: 0.05em;
}
.city-map__boss {
  position: absolute;
  z-index: 2;
  top: 12px;
  right: 10px;
  min-height: 34px;
  padding: 4px 9px;
  border: 1px solid #d5a865;
  border-radius: 6px;
  background: #1a1010ee;
  color: #f8ca8f;
  font: 700 0.7rem Georgia, serif;
}
.city-map__heading h1 {
  margin: 0;
  font: 700 clamp(1rem, 4vw, 1.3rem) Georgia, serif;
}
.city-marker {
  position: absolute;
  z-index: 2;
  display: grid;
  width: clamp(78px, 22vw, 96px);
  min-height: 78px;
  align-content: center;
  justify-items: center;
  padding: 0;
  transform: translate(-50%, -50%);
  border: 0;
  background: transparent;
  color: #ffebba;
  cursor: pointer;
  filter: drop-shadow(0 3px 5px #000b);
  -webkit-tap-highlight-color: transparent;
}
.city-marker:focus-visible {
  outline: 2px solid #f9d079;
  outline-offset: 4px;
  border-radius: 12px;
}
.city-marker--wide {
  width: clamp(100px, 30vw, 116px);
}
.city-marker__medallion {
  position: relative;
  display: block;
  width: 52px;
  height: 52px;
  overflow: hidden;
  filter: drop-shadow(0 2px 3px #000b);
}
.city-marker__medallion img {
  position: absolute;
  top: -2px;
  left: 50%;
  width: 68px;
  max-width: none;
  height: auto;
  transform: translateX(-50%);
  pointer-events: none;
}
.city-marker__seal {
  display: grid;
  width: 49px;
  height: 49px;
  margin: 4px 0 5px;
  place-items: center;
  border: 2px solid #b68b45;
  border-radius: 50%;
  background: radial-gradient(circle at 45% 32%, #6d2918, #1d0910 75%);
  box-shadow: inset 0 0 0 2px #f0c16b, 0 0 0 2px #241813, 0 0 16px #f0ae4c66;
}
.city-marker__seal :deep(.icon-generator) {
  width: 72%;
  height: 72%;
  border: 0;
  background: transparent;
  box-shadow: none;
  color: #f4d17c;
}
.city-marker__seal :deep(.icon-generator__glyph) {
  color: #f4d17c;
}
.city-marker__name {
  position: relative;
  z-index: 1;
  width: 100%;
  min-width: 0;
  max-width: 100%;
  box-sizing: border-box;
  white-space: normal;
  margin-top: -3px;
  padding: 3px 7px 4px;
  border: 1px solid #b68b45;
  border-radius: 2px;
  background: linear-gradient(#28201b, #090a10);
  box-shadow: inset 0 1px #e1bd7299, 0 2px 5px #000b;
  color: #fff2d3;
  font: 600 clamp(0.68rem, 2.6vw, 0.8rem)/1.1 Georgia, serif;
  text-align: center;
  text-wrap: balance;
}
.city-marker__arrow {
  width: 0;
  height: 0;
  border-right: 4px solid transparent;
  border-top: 7px solid #f4ba54;
  border-left: 4px solid transparent;
  filter: drop-shadow(0 0 4px #ffab38);
}
@media (hover: hover) {
  .city-marker:hover { filter: drop-shadow(0 0 7px #e7b65499); }
}
.city-marker:active {
  transform: translate(-50%, -50%) scale(0.96);
}
.city-interior {
  min-height: 100%;
  padding: var(--ui-space-3);
  background: var(--ui-gradient-panel);
}
.city-interior__header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: var(--ui-space-3);
  padding-bottom: 8px;
  border-bottom: 1px solid var(--ui-color-border-strong);
}
.city-interior__header h2 {
  margin: 0;
  color: var(--ui-color-gold);
  font: 700 1.1rem Georgia, serif;
}
.city-interior__arena {
  display: grid;
  gap: var(--ui-space-3);
}
.city-interior__choices {
  display: grid;
  justify-items: stretch;
  gap: var(--ui-space-3);
  padding: var(--ui-space-4);
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-lg);
  background: var(--ui-color-surface-1);
}
.city-interior__choices p {
  margin: 0 0 5px;
  color: var(--ui-color-text-secondary);
  line-height: 1.5;
}
@media (prefers-reduced-motion: reduce) {
  .city-marker { transition: none; }
}
</style>
