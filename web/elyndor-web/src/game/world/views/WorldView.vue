<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'

import { gameArt } from '@/assets/gameArt'
import { classLabel, resourceLabel } from '@/game/character/characterPresentation'
import CombatView from '@/game/combat/views/CombatView.vue'
import MerchantShop from '@/game/world/components/MerchantShop.vue'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UICard, UIHealthBar, UIToast } from '@/ui/components'

type CombatResult = 'Victory' | 'Defeat' | 'Cancelled'
interface ForestEncounter {
  id: 'WOLF' | 'FOREST_BOAR' | 'GIANT_SPIDER'
  name: string
  level: number
  description: string
  art: string
}

const STARTER_TOWN_ID = 'STARTER_TOWN'
const WHISPERING_FOREST_ID = 'WHISPERING_FOREST'
const TRAINING_DUMMY_ID = 'TRAINING_DUMMY'
const FOREST_ENCOUNTERS: readonly ForestEncounter[] = [
  { id: 'WOLF', name: 'Волк', level: 3, description: 'Дикий волк вышел на тропу и следит за каждым движением.', art: gameArt.monsters.wolf },
  { id: 'FOREST_BOAR', name: 'Лесной кабан', level: 2, description: 'Тяжёлый кабан роет землю копытом и готовится броситься вперёд.', art: gameArt.monsters.forestBoar },
  { id: 'GIANT_SPIDER', name: 'Гигантский паук', level: 2, description: 'Из корней выполз огромный паук. Он быстрый, но хрупкий.', art: gameArt.monsters.giantSpider },
]

const session = useGameSessionStore()
const combat = useCombatSessionStore()
const selectedEncounter = ref<ForestEncounter | null>(null)
const lastCombatResult = ref<CombatResult | null>(null)
const lastEnemyName = ref<string | null>(null)
const merchantOpen = ref(false)
let encounterCursor = -1
let vitalsRefreshTimer: ReturnType<typeof setInterval> | null = null
let vitalsRefreshPending = false

const world = computed(() => session.snapshot?.world)
const character = computed(() => session.snapshot?.character)
const currentLocationId = computed(() => world.value?.currentLocation.id)
const isStarterTown = computed(() => currentLocationId.value === STARTER_TOWN_ID)
const isWhisperingForest = computed(() => currentLocationId.value === WHISPERING_FOREST_ID)
const locationName = computed(() => isStarterTown.value ? 'Стартовый город' : isWhisperingForest.value ? 'Шепчущий лес' : world.value?.currentLocation.displayName ?? 'Неизвестная область')
const locationDescription = computed(() => isStarterTown.value
  ? 'Безопасный город для отдыха, торговли, тренировки билдов и подготовки к следующему походу.'
  : 'Сумрачный лес старых дорог. Здесь водятся волки, кабаны и гигантские пауки.')
const sceneBackground = computed(() => isWhisperingForest.value ? gameArt.world.forest : gameArt.world.capital)
const recoveryMessage = computed(() => {
  const vitals = character.value?.vitals
  if (!vitals || combat.isActive) return null
  if (isStarterTown.value && vitals.currentHp < vitals.maxHp) return 'Отдых в городе: здоровье восстанавливается по 5 ед. в секунду.'
  if (vitals.resourceType === 'RAGE' && vitals.currentResource > 0) return 'После боя ярость постепенно угасает.'
  return null
})
const needsOutOfCombatRefresh = computed(() => {
  const vitals = character.value?.vitals
  if (!vitals || combat.isActive) return false
  return (isStarterTown.value && vitals.currentHp < vitals.maxHp)
    || (vitals.resourceType === 'RAGE' && vitals.currentResource > 0)
})

function explore(): void {
  if (!isWhisperingForest.value || combat.isActive) return
  lastCombatResult.value = null
  encounterCursor = (encounterCursor + 1) % FOREST_ENCOUNTERS.length
  selectedEncounter.value = FOREST_ENCOUNTERS[encounterCursor] ?? null
}

function locationLabel(id: string, displayName: string): string {
  if (id === STARTER_TOWN_ID) return 'Стартовый город'
  if (id === WHISPERING_FOREST_ID) return 'Шепчущий лес'
  return displayName
}

async function travelTo(locationId: string): Promise<void> {
  await session.travel(locationId)
  selectedEncounter.value = null
  lastCombatResult.value = null
  lastEnemyName.value = null
}

async function startEncounterCombat(): Promise<void> {
  if (!selectedEncounter.value || combat.pending) return
  const encounter = selectedEncounter.value
  if (await combat.startCombat(encounter.id)) {
    lastEnemyName.value = encounter.name
    selectedEncounter.value = null
    lastCombatResult.value = null
  }
}

async function startTraining(): Promise<void> {
  if (!isStarterTown.value || combat.pending) return
  if (await combat.startCombat(TRAINING_DUMMY_ID)) {
    lastEnemyName.value = 'Тренировочный манекен'
    selectedEncounter.value = null
    lastCombatResult.value = null
  }
}

async function restoreCombat(): Promise<void> {
  try {
    await combat.connect()
    await combat.resume()
  } catch {
    // Мир остаётся доступным при временной ошибке realtime.
  }
}

async function refreshOutOfCombatVitals(): Promise<void> {
  if (vitalsRefreshPending || combat.isActive || session.mutationPending) return
  vitalsRefreshPending = true
  try { await session.refreshSnapshot() } catch { /* background refresh */ }
  finally { vitalsRefreshPending = false }
}

function syncVitalsRefreshTimer(enabled: boolean): void {
  if (enabled && vitalsRefreshTimer === null) {
    vitalsRefreshTimer = setInterval(() => void refreshOutOfCombatVitals(), 1000)
  } else if (!enabled && vitalsRefreshTimer !== null) {
    clearInterval(vitalsRefreshTimer)
    vitalsRefreshTimer = null
  }
}

function localizedEnemyName(id: string, fallback: string): string {
  if (id === TRAINING_DUMMY_ID) return 'Тренировочный манекен'
  return FOREST_ENCOUNTERS.find((item) => item.id === id)?.name ?? fallback
}

watch(currentLocationId, (locationId, previousLocationId) => {
  if (locationId !== previousLocationId) {
    selectedEncounter.value = null
    merchantOpen.value = false
    encounterCursor = -1
  }
  if (locationId === WHISPERING_FOREST_ID || locationId === STARTER_TOWN_ID) void restoreCombat()
}, { immediate: true })

watch(() => combat.snapshot?.status, (status) => {
  if (status === 'Victory' || status === 'Defeat') {
    selectedEncounter.value = null
    if (combat.snapshot) lastEnemyName.value = localizedEnemyName(combat.snapshot.enemy.definitionId, combat.snapshot.enemy.name)
    lastCombatResult.value = status
    void session.refreshSnapshot()
  }
})

watch(needsOutOfCombatRefresh, syncVitalsRefreshTimer, { immediate: true })
onBeforeUnmount(() => syncVitalsRefreshTimer(false))
</script>

<template>
  <CombatView v-if="combat.isActive" @leave="lastCombatResult = 'Cancelled'" />

  <section v-else-if="world && character" class="world">
    <section class="scene" :style="{ backgroundImage: `url(${sceneBackground})` }">
      <div class="scene__shade" />
      <div class="scene__content">
        <div v-if="selectedEncounter" class="scene-encounter" data-world-encounter>
          <img :src="selectedEncounter.art" :alt="selectedEncounter.name" />
          <div class="scene-encounter__copy">
            <small>ОБНАРУЖЕН ПРОТИВНИК</small>
            <h2>{{ selectedEncounter.name }}</h2>
            <p>Уровень {{ selectedEncounter.level }} · {{ selectedEncounter.description }}</p>
          </div>
          <div class="scene__actions">
            <UIButton data-start-encounter :loading="combat.pending" @click="startEncounterCombat">Вступить в бой</UIButton>
            <UIButton variant="ghost" @click="selectedEncounter = null">Уйти</UIButton>
          </div>
        </div>

        <div v-else class="scene-location">
          <small>{{ world.currentLocation.dangerLevel === 'SAFE' ? 'БЕЗОПАСНАЯ ЗОНА' : 'ОПАСНАЯ ОБЛАСТЬ' }}</small>
          <h1>{{ locationName }}</h1>
          <p>{{ locationDescription }}</p>

          <div v-if="isWhisperingForest && lastCombatResult !== 'Victory'" class="scene__primary-action">
            <UIButton data-explore @click="explore">Исследовать</UIButton>
          </div>

          <nav class="scene__travel" aria-label="Переходы между локациями">
            <small>ПУТЕШЕСТВИЕ</small>
            <div v-if="world.outgoingTransitions.length" class="scene__travel-actions">
              <UIButton
                v-for="location in world.outgoingTransitions"
                :key="location.id"
                :data-travel="location.id"
                :aria-label="`Отправиться: ${locationLabel(location.id, location.displayName)}`"
                variant="secondary"
                :loading="session.mutationPending"
                @click="travelTo(location.id)"
              >
                {{ locationLabel(location.id, location.displayName) }}
              </UIButton>
            </div>
            <p v-else class="scene__no-paths" role="status">Пути не найдены. Исследуйте текущую область.</p>
          </nav>
        </div>
      </div>
    </section>

    <p v-if="session.errorCode" class="world-error" role="alert">{{ session.errorCode }}</p>

    <UICard v-if="lastCombatResult === 'Victory'" class="reward-card">
      <div class="reward-card__heading">
        <small>ПОБЕДА</small>
        <strong>{{ lastEnemyName ?? 'Противник' }} повержен</strong>
      </div>
      <div v-if="combat.reward" class="reward-card__summary">
        <strong>+{{ combat.reward.xpEarned }} опыта · +{{ combat.reward.goldEarned }} золота</strong>
        <ul v-if="combat.reward.items.length">
          <li v-for="item in combat.reward.items" :key="item.itemId">{{ item.name }} ×{{ item.quantity }}</li>
        </ul>
      </div>
      <UIButton v-if="isWhisperingForest" data-explore-after-victory @click="explore">Исследовать дальше</UIButton>
    </UICard>

    <UIToast v-if="lastCombatResult === 'Defeat'" tone="danger" title="Поражение">Вы очнулись в Стартовом городе.</UIToast>
    <UIToast v-if="recoveryMessage" tone="info" title="Восстановление">{{ recoveryMessage }}</UIToast>

    <section class="hero-hud">
      <div>
        <strong>{{ character.name }}</strong>
        <small>{{ classLabel(character.classId) }} · уровень {{ character.level }}</small>
      </div>
      <div class="hero-hud__gold">● {{ character.gold }} золота</div>
      <div class="hero-hud__bars">
        <UIHealthBar label="Здоровье" :value="character.vitals.currentHp" :max="character.vitals.maxHp" />
        <UIHealthBar :label="resourceLabel(character.vitals.resourceType)" :tone="character.vitals.resourceType === 'RAGE' ? 'rage' : character.vitals.resourceType === 'MANA' ? 'mana' : 'focus'" :value="character.vitals.currentResource" :max="character.vitals.maxResource" />
      </div>
    </section>

    <section v-if="isStarterTown" class="town-grid">
      <UICard class="town-service town-service--training">
        <small>ТРЕНИРОВОЧНАЯ ПЛОЩАДКА</small>
        <h2>Тренировочный манекен</h2>
        <p>Проверяйте билды, криты, DoT и ротацию без риска, расхода зелий и наград.</p>
        <UIButton data-start-training :loading="combat.pending" @click="startTraining">Тренироваться</UIButton>
      </UICard>
      <UICard class="town-service">
        <small>ТОРГОВЕЦ</small>
        <h2>Маркус</h2>
        <p>Лечебные припасы, покупка зелий и скупка добытых материалов.</p>
        <UIButton @click="merchantOpen = true">Открыть лавку</UIButton>
      </UICard>
      <UICard class="town-service">
        <small>ОТДЫХ</small>
        <h2>Городская площадь</h2>
        <p>Здесь здоровье постепенно восстанавливается. Подготовьте экипировку и таланты.</p>
      </UICard>
    </section>

    <MerchantShop :open="merchantOpen" @close="merchantOpen = false" />
  </section>
</template>

<style scoped>
.world{display:grid;width:min(100%,var(--ui-content-width));margin-inline:auto;gap:var(--ui-space-4);padding:var(--ui-space-4) var(--ui-space-4) var(--ui-space-7)}
.scene{position:relative;min-height:22rem;overflow:hidden;border:1px solid var(--ui-color-border-strong);border-radius:var(--ui-radius-lg);background-position:center;background-size:cover}.scene__shade{position:absolute;inset:0;background:linear-gradient(180deg,rgb(5 7 14 / 12%),rgb(5 7 14 / 92%))}.scene__content{position:relative;z-index:1;display:grid;min-height:22rem;align-items:end;padding:var(--ui-space-5)}.scene-location{display:grid;gap:var(--ui-space-2)}.scene-location h1,.scene-location p,.scene-encounter h2,.scene-encounter p{margin:0}.scene-location>small,.scene-encounter small,.scene__travel>small{color:var(--ui-color-primary);font-weight:700}.scene-location>p,.scene-encounter p{max-width:38rem;color:var(--ui-color-text-secondary)}.scene__primary-action{display:flex;margin-top:var(--ui-space-2)}
.scene__travel{display:grid;gap:var(--ui-space-2);margin-top:var(--ui-space-3);padding-top:var(--ui-space-3);border-top:1px solid var(--ui-color-border)}.scene__travel-actions{display:flex;flex-wrap:wrap;gap:var(--ui-space-2)}.scene__no-paths{margin:0;color:var(--ui-color-text-muted)}
.scene-encounter{display:grid;grid-template-columns:minmax(7rem,10rem) 1fr;gap:var(--ui-space-4);align-items:center}.scene-encounter img{width:100%;max-height:12rem;object-fit:contain;filter:drop-shadow(0 .5rem 1rem rgb(0 0 0 / 50%))}.scene-encounter__copy{display:grid;gap:var(--ui-space-1)}.scene__actions{grid-column:1/-1;display:flex;gap:var(--ui-space-2)}
.world-error{margin:0;padding:var(--ui-space-3) var(--ui-space-4);border:1px solid var(--ui-color-danger);border-radius:var(--ui-radius-md);color:var(--ui-color-danger);background:var(--ui-color-surface-2)}
.reward-card{display:grid;gap:var(--ui-space-3)}.reward-card__heading{display:grid;gap:var(--ui-space-1)}.reward-card__heading small{color:var(--ui-color-success);font-weight:700}.reward-card__summary>strong{color:var(--ui-color-success)}.reward-card ul{margin:var(--ui-space-2) 0 0;padding-left:1.2rem}
.hero-hud{display:grid;grid-template-columns:auto auto;gap:var(--ui-space-3);align-items:start;padding:var(--ui-space-3);border:1px solid var(--ui-color-border);border-radius:var(--ui-radius-md);background:var(--ui-color-surface-2)}.hero-hud>div:first-child{display:grid}.hero-hud small{color:var(--ui-color-text-muted)}.hero-hud__gold{justify-self:end;color:#e8c866;font-weight:700}.hero-hud__bars{grid-column:1/-1;display:grid;gap:var(--ui-space-2)}
.town-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:var(--ui-space-3)}.town-service{display:grid;gap:var(--ui-space-2)}.town-service small{color:var(--ui-color-primary);font-weight:700}.town-service h2,.town-service p{margin:0}.town-service p{color:var(--ui-color-text-muted)}.town-service--training{grid-column:1/-1;border-color:color-mix(in srgb,var(--ui-color-primary) 45%,var(--ui-color-border));background:linear-gradient(135deg,color-mix(in srgb,var(--ui-color-primary) 8%,var(--ui-color-surface-1)),var(--ui-color-surface-1))}
@media(max-width:520px){.world{padding-inline:var(--ui-space-3)}.scene,.scene__content{min-height:19rem}.scene__content{padding:var(--ui-space-4)}.scene-encounter{grid-template-columns:6.5rem 1fr;gap:var(--ui-space-3)}.scene-encounter img{max-height:9rem}.scene__actions,.scene__travel-actions{display:grid;grid-template-columns:1fr}.town-grid{grid-template-columns:1fr}.town-service--training{grid-column:auto}.hero-hud{grid-template-columns:1fr}.hero-hud__gold{justify-self:start}}
</style>
