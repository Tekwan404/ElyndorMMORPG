<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'

import { gameArt } from '@/assets/gameArt'
import { classLabel, resourceLabel } from '@/game/character/characterPresentation'
import CombatView from '@/game/combat/views/CombatView.vue'
import MerchantShop from '@/game/world/components/MerchantShop.vue'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UICard, UIPanel, UIToast, UIHealthBar } from '@/ui/components'

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
  ? 'Безопасный город для отдыха, торговли и подготовки к следующему походу.'
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

async function startEncounterCombat(): Promise<void> {
  if (!selectedEncounter.value || combat.pending) return
  const encounter = selectedEncounter.value
  if (await combat.startCombat(encounter.id)) {
    lastEnemyName.value = encounter.name
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
  return FOREST_ENCOUNTERS.find((item) => item.id === id)?.name ?? fallback
}

watch(currentLocationId, (locationId, previousLocationId) => {
  if (locationId !== previousLocationId) {
    selectedEncounter.value = null
    merchantOpen.value = false
    encounterCursor = -1
  }
  if (locationId === WHISPERING_FOREST_ID) void restoreCombat()
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
      <div class="scene__copy">
        <small>{{ world.currentLocation.dangerLevel === 'SAFE' ? 'БЕЗОПАСНАЯ ЗОНА' : 'ОПАСНАЯ ОБЛАСТЬ' }}</small>
        <h1>{{ locationName }}</h1>
        <p>{{ locationDescription }}</p>
      </div>
    </section>

    <section class="hero-hud">
      <div><strong>{{ character.name }}</strong><small>{{ classLabel(character.classId) }} · уровень {{ character.level }}</small></div>
      <div class="hero-hud__gold">● {{ character.gold }} золота</div>
      <div class="hero-hud__bars">
        <UIHealthBar label="Здоровье" :value="character.vitals.currentHp" :max="character.vitals.maxHp" />
        <UIHealthBar :label="resourceLabel(character.vitals.resourceType)" :tone="character.vitals.resourceType === 'RAGE' ? 'rage' : character.vitals.resourceType === 'MANA' ? 'mana' : 'focus'" :value="character.vitals.currentResource" :max="character.vitals.maxResource" />
      </div>
    </section>

    <UIToast v-if="recoveryMessage" tone="info" title="Восстановление">{{ recoveryMessage }}</UIToast>
    <UIToast v-if="lastCombatResult === 'Victory'" tone="success" title="Победа">{{ lastEnemyName ?? 'Противник' }} повержен.</UIToast>
    <UICard v-if="lastCombatResult === 'Victory' && combat.reward" class="reward-card">
      <strong>+{{ combat.reward.xpEarned }} опыта · +{{ combat.reward.goldEarned }} золота</strong>
      <ul v-if="combat.reward.items.length"><li v-for="item in combat.reward.items" :key="item.itemId">{{ item.name }} ×{{ item.quantity }}</li></ul>
    </UICard>
    <UIToast v-if="lastCombatResult === 'Defeat'" tone="danger" title="Поражение">Вы очнулись в Стартовом городе.</UIToast>

    <UICard v-if="selectedEncounter" class="encounter">
      <img :src="selectedEncounter.art" :alt="selectedEncounter.name" />
      <div><small>ОБНАРУЖЕН ПРОТИВНИК</small><h2>{{ selectedEncounter.name }}</h2><p>Уровень {{ selectedEncounter.level }} · {{ selectedEncounter.description }}</p></div>
      <div class="actions"><UIButton :loading="combat.pending" @click="startEncounterCombat">Вступить в бой</UIButton><UIButton variant="ghost" @click="selectedEncounter = null">Уйти</UIButton></div>
    </UICard>

    <section v-else-if="isWhisperingForest" class="primary-action">
      <div><small>ИССЛЕДОВАНИЕ</small><strong>Осмотреть лесные тропы</strong><p>Ищите противников ради опыта, золота, материалов и частей комплекта Следопыта.</p></div>
      <UIButton @click="explore">Исследовать</UIButton>
    </section>

    <section v-else-if="isStarterTown" class="town-grid">
      <UICard class="town-service">
        <small>ТОРГОВЕЦ</small><h2>Маркус</h2><p>Лечебные припасы, покупка зелий и скупка добытых материалов.</p>
        <UIButton @click="merchantOpen = true">Открыть лавку</UIButton>
      </UICard>
      <UICard class="town-service">
        <small>ОТДЫХ</small><h2>Городская площадь</h2><p>Здесь здоровье постепенно восстанавливается. Подготовьте экипировку и таланты.</p>
      </UICard>
    </section>

    <UIPanel v-if="!selectedEncounter">
      <template #title>Куда отправиться</template>
      <div class="paths">
        <UIButton v-for="location in world.outgoingTransitions" :key="location.id" variant="secondary" :disabled="session.mutationPending" @click="session.travel(location.id)">
          {{ location.id === STARTER_TOWN_ID ? 'Стартовый город' : location.id === WHISPERING_FOREST_ID ? 'Шепчущий лес' : location.displayName }}
        </UIButton>
      </div>
    </UIPanel>

    <MerchantShop :open="merchantOpen" @close="merchantOpen = false" />
  </section>
</template>

<style scoped>
.world{display:grid;width:min(100%,var(--ui-content-width));margin-inline:auto;gap:var(--ui-space-4);padding:var(--ui-space-4) var(--ui-space-4) var(--ui-space-7)}
.scene{position:relative;min-height:20rem;overflow:hidden;border:1px solid var(--ui-color-border-strong);border-radius:var(--ui-radius-lg);background-position:center;background-size:cover}.scene__shade{position:absolute;inset:0;background:linear-gradient(180deg,rgb(5 7 14 / 20%),rgb(5 7 14 / 88%))}.scene__copy{position:absolute;right:0;bottom:0;left:0;display:grid;gap:var(--ui-space-1);padding:var(--ui-space-5)}.scene__copy h1,.scene__copy p{margin:0}.scene__copy small{color:var(--ui-color-primary);font-weight:700}.scene__copy p{max-width:36rem;color:var(--ui-color-text-secondary)}
.hero-hud{display:grid;grid-template-columns:auto auto;gap:var(--ui-space-3);align-items:start;padding:var(--ui-space-3);border:1px solid var(--ui-color-border);border-radius:var(--ui-radius-md);background:var(--ui-color-surface-2)}.hero-hud>div:first-child{display:grid}.hero-hud small{color:var(--ui-color-text-muted)}.hero-hud__gold{justify-self:end;color:#e8c866;font-weight:700}.hero-hud__bars{grid-column:1/-1;display:grid;gap:var(--ui-space-2)}
.primary-action,.town-service{display:grid;gap:var(--ui-space-2)}.primary-action{grid-template-columns:1fr auto;align-items:center;padding:var(--ui-space-4);border:1px solid var(--ui-color-border);border-radius:var(--ui-radius-lg);background:var(--ui-color-surface-2)}.primary-action div{display:grid;gap:var(--ui-space-1)}.primary-action p,.town-service p{margin:0;color:var(--ui-color-text-muted)}
.town-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:var(--ui-space-3)}.town-service small,.encounter small{color:var(--ui-color-primary);font-weight:700}.town-service h2,.encounter h2{margin:0}
.encounter{display:grid;grid-template-columns:8rem 1fr;gap:var(--ui-space-3);align-items:center}.encounter img{width:100%;max-height:8rem;object-fit:contain}.encounter p{margin:0;color:var(--ui-color-text-muted)}.actions{grid-column:1/-1;display:flex;gap:var(--ui-space-2)}
.reward-card strong{color:var(--ui-color-success)}.reward-card ul{margin:var(--ui-space-2) 0 0;padding-left:1.2rem}.paths{display:flex;flex-wrap:wrap;gap:var(--ui-space-2)}
@media(max-width:520px){.world{padding-inline:var(--ui-space-3)}.scene{min-height:17rem}.town-grid{grid-template-columns:1fr}.primary-action{grid-template-columns:1fr}.encounter{grid-template-columns:6rem 1fr}.hero-hud{grid-template-columns:1fr}.hero-hud__gold{justify-self:start}}
</style>
