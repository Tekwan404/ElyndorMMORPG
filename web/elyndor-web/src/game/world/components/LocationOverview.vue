<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import { monsterArtUrl } from '@/assets/monsterArt'
import ItemIcon from '@/game/items/components/ItemIcon.vue'
import { locationPresentation } from '@/game/world/locationPresentation'
import {
  loadLocationCatalog,
  type DetailedWorldLocation,
  type WorldLocationResident,
} from '@/game/world/locationDetails'
import { useGameSessionStore } from '@/stores/gameSession'

const session = useGameSessionStore()
const location = ref<DetailedWorldLocation | null>(null)
const loading = ref(false)
const failed = ref(false)

const world = computed(() => session.snapshot?.world ?? null)
const currentLocation = computed(() => world.value?.currentLocation ?? null)
const isTravelling = computed(() => world.value?.travel !== null && world.value?.travel !== undefined)
const presentation = computed(() => locationPresentation(
  currentLocation.value?.id,
  currentLocation.value?.displayName,
))
const residents = computed(() => location.value?.residents ?? [])
const loot = computed(() => location.value?.loot ?? [])
const localContracts = computed(() => {
  const locationId = currentLocation.value?.id
  if (!locationId) return []
  return world.value?.contracts.filter(contract => contract.offerLocationId === locationId) ?? []
})
const levelLabel = computed(() => {
  const value = location.value ?? currentLocation.value
  if (!value) return ''
  return value.minimumLevel === value.maximumLevel
    ? `Ур. ${value.minimumLevel}`
    : `Ур. ${value.minimumLevel}–${value.maximumLevel}`
})
const dangerLabel = computed(() => {
  const danger = (location.value ?? currentLocation.value)?.dangerLevel
  if (danger === 'SAFE') return 'Безопасная зона'
  if (danger === 'DANGEROUS') return 'Высокий риск'
  return presentation.value.dangerLabel
})
const locationBackground = computed(() => {
  const serverArtId = location.value?.artId ?? currentLocation.value?.artId
  return resolveWorldArt(serverArtId) ?? presentation.value.art
})
const activityLabels = computed(() => {
  const value = location.value ?? currentLocation.value
  if (!value) return []

  const activities: string[] = []
  if (value.dangerLevel !== 'SAFE' && residents.value.length > 0) activities.push('Исследование')
  if (value.allowAfk) activities.push('Автоохота')
  if (localContracts.value.length > 0) activities.push(`Контракты · ${localContracts.value.length}`)
  return activities
})

const worldArtModules = import.meta.glob<string>(
  '@/assets/world/*.{png,jpg,jpeg,webp,svg}',
  { eager: true, import: 'default' },
)
const worldArtByStem = new Map<string, string>(
  Object.entries(worldArtModules).map(([path, url]) => {
    const fileName = path.split('/').pop() ?? path
    return [normalizeArtKey(fileName.replace(/\.[^.]+$/, '')), url]
  }),
)

function normalizeArtKey(value: string): string {
  return value.trim().toLowerCase().replace(/[^a-z0-9а-яё]+/gi, '-')
}

function resolveWorldArt(artId: string | null | undefined): string | undefined {
  if (!artId) return undefined
  return worldArtByStem.get(normalizeArtKey(artId))
}

function residentArt(resident: WorldLocationResident): string | undefined {
  return monsterArtUrl(resident.artId, resident.monsterId)
}

function rankLabel(rank: string): string {
  if (rank === 'Boss') return 'Босс'
  if (rank === 'Elite') return 'Элита'
  return 'Обычный'
}

function rarityClass(rarity: string): string {
  return `location-overview__loot--${rarity.toLowerCase()}`
}

watch(
  [() => currentLocation.value?.id, () => session.snapshot?.contentVersion],
  async ([locationId, contentVersion]) => {
    location.value = null
    failed.value = false
    if (!locationId || !contentVersion || isTravelling.value) return

    loading.value = true
    try {
      const catalog = await loadLocationCatalog(contentVersion)
      if (currentLocation.value?.id !== locationId) return
      location.value = catalog.find(entry => entry.id === locationId) ?? null
      failed.value = location.value === null
    } catch {
      failed.value = true
    } finally {
      loading.value = false
    }
  },
  { immediate: true },
)
</script>

<template>
  <section
    v-if="currentLocation && !isTravelling"
    class="location-overview"
    :data-location-id="currentLocation.id"
    aria-label="Сведения о локации"
  >
    <div class="location-overview__hero">
      <img class="location-overview__background" :src="locationBackground" alt="" aria-hidden="true" />
      <div class="location-overview__shade" />
      <div class="location-overview__hero-copy">
        <div class="location-overview__eyebrow">
          <span>{{ dangerLabel }}</span>
          <span>{{ levelLabel }}</span>
        </div>
        <h1>{{ location?.displayName ?? currentLocation.displayName }}</h1>
        <p v-if="location?.description || currentLocation.description">
          {{ location?.description ?? currentLocation.description }}
        </p>
        <div v-if="activityLabels.length" class="location-overview__activities" aria-label="Доступные активности">
          <span v-for="activity in activityLabels" :key="activity">{{ activity }}</span>
        </div>
      </div>
    </div>

    <div v-if="loading" class="location-overview__state">Загружаем сведения об области…</div>

    <template v-else-if="location">
      <section v-if="residents.length" class="location-overview__section" data-location-residents>
        <header>
          <div>
            <small>ОБИТАТЕЛИ</small>
            <h2>Кого можно встретить</h2>
          </div>
          <span>{{ residents.length }}</span>
        </header>

        <div class="location-overview__residents">
          <article v-for="resident in residents" :key="resident.monsterId" class="location-overview__resident">
            <div class="location-overview__resident-art">
              <img
                v-if="residentArt(resident)"
                :src="residentArt(resident)"
                :alt="resident.displayName"
                loading="lazy"
              />
              <span v-else aria-hidden="true">?</span>
            </div>
            <div class="location-overview__resident-copy">
              <div>
                <strong>{{ resident.displayName }}</strong>
                <span :data-rank="resident.rank">{{ rankLabel(resident.rank) }} · ур. {{ resident.level }}</span>
              </div>
              <p v-if="resident.description">{{ resident.description }}</p>
              <small>
                +{{ resident.xpReward }} XP
                <template v-if="resident.goldRewardMax > 0"> · {{ resident.goldRewardMin }}–{{ resident.goldRewardMax }} зол.</template>
              </small>
            </div>
          </article>
        </div>
      </section>

      <section v-if="loot.length" class="location-overview__section" data-location-loot>
        <header>
          <div>
            <small>ДОБЫЧА</small>
            <h2>Что здесь встречается</h2>
          </div>
          <span>{{ loot.length }}</span>
        </header>

        <div class="location-overview__loot-grid">
          <article
            v-for="item in loot"
            :key="item.itemId"
            class="location-overview__loot"
            :class="rarityClass(item.rarity)"
            :title="item.description || item.name"
          >
            <div class="location-overview__loot-icon">
              <ItemIcon
                :icon-id="item.iconId"
                :item-id="item.itemId"
                :name="item.name"
                :type="item.type"
                :rarity="item.rarity"
              />
            </div>
            <div>
              <strong>{{ item.name }}</strong>
              <small>{{ item.rarity }}<template v-if="item.requiredLevel > 1"> · ур. {{ item.requiredLevel }}</template></small>
            </div>
          </article>
        </div>
      </section>

      <div v-if="!residents.length && !loot.length" class="location-overview__quiet">
        Здесь нет открытого списка противников или добычи. Доступные действия показаны ниже.
      </div>
    </template>

    <div v-else-if="failed" class="location-overview__state location-overview__state--muted">
      Подробности области недоступны. Основные действия локации остаются доступны ниже.
    </div>
  </section>
</template>

<style scoped>
.location-overview {
  display: grid;
  width: 100%;
  min-width: 0;
  gap: 12px;
}

.location-overview__hero {
  position: relative;
  min-height: clamp(190px, 44vw, 300px);
  overflow: hidden;
  border: 1px solid rgb(205 177 113 / 24%);
  border-radius: 18px;
  background: #0a0d13;
  box-shadow: 0 18px 48px rgb(0 0 0 / 30%);
}

.location-overview__background,
.location-overview__shade {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
}

.location-overview__background {
  object-fit: cover;
  object-position: center;
}

.location-overview__shade {
  background:
    linear-gradient(180deg, rgb(5 7 12 / 8%) 12%, rgb(5 7 12 / 38%) 48%, rgb(5 7 12 / 96%) 100%),
    linear-gradient(90deg, rgb(5 7 12 / 72%), transparent 72%);
}

.location-overview__hero-copy {
  position: absolute;
  z-index: 1;
  right: 0;
  bottom: 0;
  left: 0;
  display: grid;
  gap: 8px;
  padding: clamp(16px, 4vw, 28px);
}

.location-overview__eyebrow,
.location-overview__activities {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.location-overview__eyebrow span,
.location-overview__activities span {
  padding: 4px 8px;
  border: 1px solid rgb(218 185 108 / 28%);
  border-radius: 999px;
  background: rgb(8 11 17 / 64%);
  color: #dbc38c;
  font-size: 11px;
  line-height: 1.2;
  backdrop-filter: blur(8px);
}

.location-overview h1,
.location-overview h2,
.location-overview p {
  margin: 0;
}

.location-overview h1 {
  max-width: 20ch;
  color: #f6edd8;
  font-family: var(--ui-font-family-display, inherit);
  font-size: clamp(25px, 6.4vw, 38px);
  line-height: 1;
  text-shadow: 0 3px 18px rgb(0 0 0 / 72%);
}

.location-overview__hero-copy > p {
  max-width: 64ch;
  color: rgb(235 231 218 / 82%);
  font-size: 13px;
  line-height: 1.45;
}

.location-overview__section {
  display: grid;
  gap: 10px;
  padding: 13px;
  border: 1px solid rgb(205 177 113 / 15%);
  border-radius: 15px;
  background: linear-gradient(180deg, rgb(20 24 34 / 94%), rgb(11 14 21 / 96%));
}

.location-overview__section > header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: 12px;
}

.location-overview__section header > div {
  display: grid;
  gap: 2px;
}

.location-overview__section header small {
  color: #a98b51;
  font-size: 9px;
  font-weight: 800;
  letter-spacing: .14em;
}

.location-overview__section h2 {
  color: #eee5d2;
  font-size: 15px;
}

.location-overview__section header > span {
  color: #8e887c;
  font-size: 11px;
}

.location-overview__residents {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
}

.location-overview__resident {
  display: grid;
  grid-template-columns: 72px minmax(0, 1fr);
  min-width: 0;
  overflow: hidden;
  border: 1px solid rgb(255 255 255 / 7%);
  border-radius: 12px;
  background: rgb(4 7 12 / 55%);
}

.location-overview__resident-art {
  display: grid;
  min-height: 92px;
  place-items: end center;
  overflow: hidden;
  background: radial-gradient(circle at 50% 68%, rgb(126 101 58 / 18%), transparent 58%);
}

.location-overview__resident-art img {
  width: 100%;
  height: 100%;
  max-height: 104px;
  object-fit: contain;
  object-position: center bottom;
}

.location-overview__resident-art > span {
  align-self: center;
  color: rgb(255 255 255 / 18%);
  font-size: 28px;
}

.location-overview__resident-copy {
  display: grid;
  align-content: center;
  gap: 5px;
  min-width: 0;
  padding: 9px 10px;
}

.location-overview__resident-copy > div {
  display: grid;
  gap: 2px;
}

.location-overview__resident-copy strong,
.location-overview__loot strong {
  overflow: hidden;
  color: #ece5d7;
  font-size: 12px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.location-overview__resident-copy span,
.location-overview__resident-copy small,
.location-overview__loot small {
  color: #948e82;
  font-size: 10px;
}

.location-overview__resident-copy span[data-rank='Boss'] { color: #d78a7f; }
.location-overview__resident-copy span[data-rank='Elite'] { color: #c9a66c; }

.location-overview__resident-copy p {
  display: -webkit-box;
  overflow: hidden;
  color: #aaa497;
  font-size: 10px;
  line-height: 1.35;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}

.location-overview__loot-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 7px;
}

.location-overview__loot {
  display: grid;
  grid-template-columns: 42px minmax(0, 1fr);
  align-items: center;
  gap: 7px;
  min-width: 0;
  padding: 7px;
  border: 1px solid rgb(255 255 255 / 7%);
  border-radius: 10px;
  background: rgb(4 7 12 / 48%);
}

.location-overview__loot > div:last-child {
  display: grid;
  min-width: 0;
  gap: 2px;
}

.location-overview__loot-icon {
  width: 42px;
  height: 42px;
  padding: 3px;
  border: 1px solid rgb(255 255 255 / 12%);
  border-radius: 8px;
  background: #080b11;
}

.location-overview__loot--legendary strong,
.location-overview__loot--unique strong { color: #e9b56a; }
.location-overview__loot--epic strong { color: #bd91e8; }
.location-overview__loot--rare strong { color: #83a9e7; }
.location-overview__loot--uncommon strong { color: #82bd7d; }

.location-overview__state,
.location-overview__quiet {
  padding: 12px;
  border: 1px solid rgb(255 255 255 / 7%);
  border-radius: 12px;
  background: rgb(13 17 24 / 78%);
  color: #aaa497;
  font-size: 12px;
  text-align: center;
}

.location-overview__state--muted {
  opacity: .72;
}

@media (max-width: 620px) {
  .location-overview__residents { grid-template-columns: minmax(0, 1fr); }
  .location-overview__loot-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
}

@media (max-width: 390px) {
  .location-overview__hero { min-height: 176px; border-radius: 14px; }
  .location-overview__hero-copy { padding: 14px; }
  .location-overview__hero-copy > p { font-size: 12px; }
  .location-overview__resident { grid-template-columns: 64px minmax(0, 1fr); }
  .location-overview__loot-grid { grid-template-columns: minmax(0, 1fr); }
}

@media (min-width: 760px) {
  .location-overview__loot-grid { grid-template-columns: repeat(4, minmax(0, 1fr)); }
}
</style>
