<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'

import { gameArt } from '@/assets/gameArt'
import CombatView from '@/game/combat/views/CombatView.vue'
import MerchantShop from '@/game/world/components/MerchantShop.vue'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UICard, UIToast } from '@/ui/components'

type CombatResult = 'Victory' | 'Defeat' | 'Cancelled'

const STARTER_TOWN_ID = 'STARTER_TOWN'

const session = useGameSessionStore()
const combat = useCombatSessionStore()
const lastCombatResult = ref<CombatResult | null>(null)
const lastEnemyName = ref<string | null>(null)
const merchantOpen = ref(false)
let vitalsRefreshTimer: ReturnType<typeof setInterval> | null = null
let vitalsRefreshPending = false

const world = computed(() => session.snapshot?.world)
const activeTravel = computed(() => world.value?.travel ?? null)
const isTravelling = computed(() => activeTravel.value !== null)
const character = computed(() => session.snapshot?.character)
const currentLocationId = computed(() => world.value?.currentLocation.id)
const isStarterTown = computed(() => currentLocationId.value === STARTER_TOWN_ID)
const canExplore = computed(() =>
  !isTravelling.value && world.value?.currentLocation.dangerLevel !== 'SAFE',
)
const locationContracts = computed(() => (world.value?.contracts ?? []).filter((contract) =>
  contract.offerLocationId === currentLocationId.value
  || contract.status === 'ACTIVE'
    && currentLocationId.value === 'BROODMOTHER_LAIR',
))
const locationName = computed(() => world.value?.currentLocation.displayName ?? 'Неизвестная область')
const locationDescription = computed(() =>
  world.value?.currentLocation.description
  || 'Исследуйте текущую область. Для путешествия между областями используйте карту мира.',
)
const sceneBackground = computed(() => {
  if (currentLocationId.value === 'STARTER_TOWN') return gameArt.world.starterTown
  if (currentLocationId.value === 'BROODMOTHER_LAIR') return gameArt.world.ancientRuins
  if (currentLocationId.value === 'BLIGHTED_GROVE') return gameArt.world.caravanRoad
  return gameArt.world.whisperingForest
})
const levelRange = computed(() => {
  const location = world.value?.currentLocation
  if (!location) return ''
  return location.minimumLevel === location.maximumLevel
    ? `ур. ${location.minimumLevel}`
    : `ур. ${location.minimumLevel}–${location.maximumLevel}`
})
const dangerLabel = computed(() => {
  const danger = world.value?.currentLocation.dangerLevel
  if (danger === 'SAFE') return 'БЕЗОПАСНАЯ ЗОНА'
  if (danger === 'DANGEROUS') return 'ВЫСОКИЙ РИСК'
  return 'ОПАСНАЯ ОБЛАСТЬ'
})
const worldErrorMessage = computed(() => {
  const code = session.errorCode
  if (!code) return null
  if (code === 'world_encounter_unavailable') return 'В этой области сейчас не удалось найти противника.'
  if (code === 'world_encounter_location_unavailable') return 'Текущее положение героя не удалось подтвердить.'
  if (code === 'travel_conflict') return 'Мир изменился во время перехода. Попробуйте ещё раз.'
  if (code === 'character_in_combat') return 'Сначала завершите текущий бой.'
  return 'Действие не удалось выполнить.'
})
const recoveryMessage = computed(() => {
  const vitals = character.value?.vitals
  if (!vitals || combat.isActive || isTravelling.value) return null
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

async function acceptContract(contractId: string): Promise<void> {
  if (isTravelling.value || session.mutationPending || combat.isActive) return
  await session.acceptContract(contractId)
}

function contractStatusLabel(status: 'LOCKED' | 'AVAILABLE' | 'ACTIVE' | 'COMPLETED'): string {
  if (status === 'COMPLETED') return 'ВЫПОЛНЕН'
  if (status === 'ACTIVE') return 'ВЗЯТ'
  if (status === 'AVAILABLE') return 'ДОСТУПЕН'
  return 'ЗАКРЫТ'
}

async function explore(): Promise<void> {
  if (!canExplore.value || combat.isActive || session.mutationPending || combat.pending) return

  lastCombatResult.value = null
  const encounter = await session.explore()
  if (!encounter) return

  if (await combat.startCombat(encounter)) {
    lastEnemyName.value = encounter.name
    lastCombatResult.value = null
  }
}

async function startTraining(): Promise<void> {
  if (isTravelling.value || !isStarterTown.value || combat.pending) return
  if (await combat.startTraining()) {
    lastEnemyName.value = 'Тренировочный манекен'
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

watch(isTravelling, travelling => {
  if (travelling) merchantOpen.value = false
})

watch(currentLocationId, (locationId, previousLocationId) => {
  if (locationId !== previousLocationId) {
    merchantOpen.value = false
  }
  if (locationId) void restoreCombat()
}, { immediate: true })

watch(() => combat.snapshot?.status, (status) => {
  if (status === 'Victory' || status === 'Defeat') {
    if (combat.snapshot) lastEnemyName.value = combat.snapshot.enemy.name
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
        <div class="scene-location">
          <div class="scene__eyebrow">
            <span :data-danger="world.currentLocation.dangerLevel">{{ dangerLabel }}</span>
            <span>{{ levelRange }}</span>
          </div>
          <h1>{{ locationName }}</h1>
          <p>{{ locationDescription }}</p>
        </div>
      </div>
    </section>

    <div v-if="session.errorCode" class="world-error" role="alert">
      <strong>{{ worldErrorMessage }}</strong>
      <small>{{ session.errorCode }}</small>
    </div>

    <UICard v-if="lastCombatResult === 'Victory'" class="reward-card">
      <div class="reward-card__heading">
        <small>ПОБЕДА</small>
        <strong>{{ lastEnemyName ?? 'Противник' }} повержен</strong>
      </div>
      <div v-if="combat.reward" class="reward-card__summary">
        <strong>+{{ combat.reward.xpEarned }} опыта · +{{ combat.reward.goldEarned }} золота</strong>
        <p v-if="combat.reward.completedContractIds?.includes('CONTRACT_BROODMOTHER_GATE')" class="contract-completed">
          ✦ Контракт выполнен: Прародительница. Путь в Осквернённую чащу открыт.
        </p>
        <ul v-if="combat.reward.items.length">
          <li v-for="item in combat.reward.items" :key="item.itemId">{{ item.name }} ×{{ item.quantity }}</li>
        </ul>
      </div>
      <UIButton v-if="canExplore" data-explore-after-victory :loading="session.mutationPending" @click="explore">Исследовать дальше</UIButton>
    </UICard>

    <UIToast
      v-if="activeTravel"
      tone="info"
      title="Герой в пути"
      data-location-travel
    >
      Путешествие к {{ activeTravel.targetLocationId }} уже началось. Боевые,
      торговые и контрактные действия станут доступны после прибытия.
    </UIToast>

    <UIToast v-if="lastCombatResult === 'Defeat'" tone="danger" title="Поражение">Вы очнулись в Стартовом городе.</UIToast>
    <UIToast v-if="recoveryMessage" tone="info" title="Восстановление">{{ recoveryMessage }}</UIToast>

    <section
      v-if="canExplore && lastCombatResult !== 'Victory'"
      class="location-activities"
      aria-labelledby="location-activities-title"
    >
      <header class="section-heading">
        <div>
          <small>АКТИВНОСТИ</small>
          <strong id="location-activities-title">Что делать здесь</strong>
        </div>
        <span>ОБЛАСТЬ</span>
      </header>

      <article class="activity-card activity-card--explore">
        <div class="activity-card__icon" aria-hidden="true">⌁</div>
        <div class="activity-card__copy">
          <small>ИССЛЕДОВАНИЕ</small>
          <strong>Осмотреть {{ locationName }}</strong>
          <p>Найдите противника или событие. Результат выбирает сервер из контента текущей локации.</p>
        </div>
        <UIButton data-explore :loading="session.mutationPending" @click="explore">Исследовать</UIButton>
      </article>
    </section>

    <section v-if="locationContracts.length" class="location-contracts" aria-labelledby="contracts-title">
      <header class="section-heading">
        <div>
          <small>КОНТРАКТЫ</small>
          <strong id="contracts-title">Задания области</strong>
        </div>
        <span>{{ locationContracts.length }}</span>
      </header>

      <article
        v-for="contract in locationContracts"
        :key="contract.id"
        class="contract-card"
        :data-contract-id="contract.id"
        :data-contract-status="contract.status"
      >
        <div class="contract-card__icon" aria-hidden="true">✦</div>
        <div class="contract-card__copy">
          <small>{{ contractStatusLabel(contract.status) }} · УР. {{ contract.requiredLevel }}</small>
          <strong>{{ contract.displayName }}</strong>
          <p>{{ contract.description }}</p>
          <div class="contract-card__reward">
            <span>Награда</span>
            <b>+{{ contract.rewardXp }} опыта · +{{ contract.rewardGold }} золота</b>
            <em>Открывает: {{ contract.unlockLocationId === 'BLIGHTED_GROVE' ? 'Осквернённая чаща' : contract.unlockLocationId }}</em>
          </div>
        </div>
        <UIButton
          v-if="contract.status === 'AVAILABLE'"
          data-accept-contract
          :loading="session.mutationPending"
          :disabled="isTravelling || session.mutationPending"
          @click="acceptContract(contract.id)"
        >
          Взять контракт
        </UIButton>
        <span v-else-if="contract.status === 'ACTIVE'" class="contract-card__status contract-card__status--active">
          Убейте цель
        </span>
        <span v-else-if="contract.status === 'COMPLETED'" class="contract-card__status contract-card__status--done">
          Выполнено
        </span>
        <span v-else class="contract-card__status">
          Нужен {{ contract.requiredLevel }} уровень
        </span>
      </article>
    </section>

    <section v-if="isStarterTown" class="town-services">
      <header class="section-heading">
        <div>
          <small>ГОРОДСКИЕ СЕРВИСЫ</small>
          <strong>Стартовый город</strong>
        </div>
        <span data-safe>БЕЗОПАСНО</span>
      </header>

      <div class="service-grid">
        <article class="service-card service-card--training" data-town-service="training">
          <img class="service-card__portrait" :src="gameArt.npc.combatTrainer" alt="Боевой наставник" />
          <div class="service-card__copy">
            <small>ТРЕНИРОВОЧНАЯ ПЛОЩАДКА</small>
            <strong>Манекен</strong>
            <p>Проверьте билд и ротацию без риска, зелий и наград.</p>
          </div>
          <UIButton
            data-start-training
            :disabled="isTravelling"
            :loading="combat.pending"
            @click="startTraining"
          >
            {{ isTravelling ? 'В пути' : 'Тренироваться' }}
          </UIButton>
        </article>

        <article class="service-card service-card--merchant" data-town-service="merchant">
          <img class="service-card__portrait" :src="gameArt.npc.marcus" alt="Маркус" />
          <div class="service-card__copy">
            <small>ТОРГОВЕЦ</small>
            <strong>Маркус</strong>
            <p>Припасы, лечебные зелья и продажа добытых материалов.</p>
          </div>
          <UIButton
            data-open-merchant
            :disabled="isTravelling"
            @click="merchantOpen = true"
          >
            {{ isTravelling ? 'В пути' : 'Торговать' }}
          </UIButton>
        </article>

        <article class="service-card service-card--rest" data-town-service="rest">
          <span class="service-card__icon" aria-hidden="true">✦</span>
          <div class="service-card__copy">
            <small>ОТДЫХ</small>
            <strong>Городская площадь</strong>
            <p>Безопасная зона постепенно восстанавливает здоровье героя.</p>
          </div>
          <span class="service-card__status">Активно</span>
        </article>
      </div>
    </section>

    <MerchantShop :open="merchantOpen" @close="merchantOpen = false" />
  </section>
</template>

<style scoped>
.world {
  display: grid;
  width: min(100%, var(--ui-content-width));
  margin-inline: auto;
  gap: var(--ui-space-3);
  padding: var(--ui-space-3) var(--ui-space-4) var(--ui-space-7);
}

.scene {
  position: relative;
  min-height: 23rem;
  overflow: hidden;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: calc(var(--ui-radius-lg) + 2px);
  background-color: var(--ui-color-surface-1);
  background-position: center;
  background-size: cover;
  box-shadow: var(--ui-shadow-inset), 0 18px 42px rgb(0 0 0 / 26%);
}

.scene::after {
  position: absolute;
  inset: 0;
  border: 1px solid rgb(255 255 255 / 3%);
  border-radius: inherit;
  content: '';
  pointer-events: none;
}

.scene__shade {
  position: absolute;
  inset: 0;
  background:
    linear-gradient(180deg, rgb(2 4 8 / 4%) 0%, rgb(4 7 13 / 22%) 38%, rgb(4 7 13 / 94%) 84%),
    linear-gradient(90deg, rgb(3 5 10 / 30%), transparent 45%);
}

.scene__content {
  position: relative;
  z-index: 1;
  display: grid;
  min-height: 23rem;
  align-items: end;
  padding: var(--ui-space-5);
}

.scene-location {
  display: grid;
  gap: var(--ui-space-2);
  text-shadow: 0 2px 8px rgb(0 0 0 / 60%);
}

.scene__eyebrow {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--ui-space-2);
  margin-bottom: 2px;
}

.scene__eyebrow span {
  padding: 4px 7px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-round);
  background: rgb(5 8 14 / 58%);
  color: var(--ui-color-text-muted);
  font-size: .59rem;
  font-weight: 700;
  letter-spacing: .08em;
  text-transform: uppercase;
  backdrop-filter: blur(8px);
}

.scene__eyebrow span:first-child[data-danger='SAFE'] {
  border-color: rgb(79 185 150 / 35%);
  color: #84d5bb;
}

.scene__eyebrow span:first-child[data-danger='ADVENTURE'] {
  border-color: rgb(208 164 88 / 38%);
  color: #e1bd78;
}

.scene__eyebrow span:first-child[data-danger='DANGEROUS'] {
  border-color: rgb(216 95 114 / 42%);
  color: #ef8fa0;
}

.scene-location h1,
.scene-location p,
.scene-encounter h2,
.scene-encounter p {
  margin: 0;
}

.scene-location h1 {
  max-width: 90%;
  font-family: var(--ui-font-display);
  font-size: clamp(1.8rem, 8vw, 2.55rem);
  line-height: 1.02;
  letter-spacing: -.02em;
}

.scene-location > p,
.scene-encounter p {
  max-width: 38rem;
  color: #c4cad8;
  font-size: var(--ui-font-size-sm);
  line-height: 1.55;
}

.world-error {
  display: grid;
  gap: 2px;
  margin: 0;
  padding: var(--ui-space-3) var(--ui-space-4);
  border: 1px solid rgb(216 95 114 / 38%);
  border-left: 3px solid var(--ui-color-danger);
  border-radius: var(--ui-radius-md);
  background: linear-gradient(90deg, rgb(216 95 114 / 9%), var(--ui-color-surface-1));
  color: var(--ui-color-text-secondary);
}

.world-error strong {
  color: #ef9bab;
  font-size: var(--ui-font-size-sm);
}

.world-error small {
  color: var(--ui-color-text-muted);
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  font-size: .6rem;
}

.reward-card {
  display: grid;
  gap: var(--ui-space-3);
  border-color: color-mix(in srgb, var(--ui-color-success) 35%, var(--ui-color-border));
  background:
    linear-gradient(135deg, rgb(79 185 150 / 8%), transparent 48%),
    var(--ui-gradient-panel);
}

.reward-card__heading {
  display: grid;
  gap: var(--ui-space-1);
}

.reward-card__heading small {
  color: var(--ui-color-success);
  font-weight: 700;
  letter-spacing: .08em;
}

.reward-card__summary > strong {
  color: #83d2b8;
}

.contract-completed {
  margin: var(--ui-space-2) 0 0;
  padding: 7px 9px;
  border: 1px solid rgb(146 136 255 / 24%);
  border-radius: var(--ui-radius-sm);
  background: rgb(146 136 255 / 6%);
  color: #cbc7ff;
  font-size: .64rem;
}

.reward-card ul {
  margin: var(--ui-space-2) 0 0;
  padding-left: 1.2rem;
}

.section-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ui-space-3);
  padding: var(--ui-space-3) var(--ui-space-4);
  border-bottom: 1px solid rgb(255 255 255 / 6%);
  background: rgb(255 255 255 / 1.5%);
}

.section-heading > div {
  display: grid;
  gap: 2px;
}

.section-heading small {
  color: #aaa3ff;
  font-size: .56rem;
  font-weight: 800;
  letter-spacing: .09em;
}

.section-heading strong {
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-md);
}

.section-heading > span {
  display: grid;
  min-width: 1.6rem;
  min-height: 1.6rem;
  place-items: center;
  padding-inline: 5px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-round);
  color: var(--ui-color-text-muted);
  font-size: .54rem;
  font-weight: 700;
}

.section-heading > span[data-safe] {
  border-color: rgb(79 185 150 / 28%);
  color: #84d5bb;
}

.location-activities,
.location-contracts,
.town-services,
.location-routes {
  overflow: hidden;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-lg);
  background:
    linear-gradient(180deg, rgb(13 18 30 / 82%), rgb(6 9 16 / 88%));
  box-shadow: var(--ui-shadow-inset);
}

.contract-card {
  display: grid;
  grid-template-columns: 3.2rem minmax(0, 1fr) auto;
  align-items: center;
  gap: var(--ui-space-3);
  padding: var(--ui-space-4);
  background:
    radial-gradient(circle at 8% 50%, rgb(146 136 255 / 10%), transparent 9rem),
    linear-gradient(90deg, rgb(146 136 255 / 5%), transparent 70%);
}

.contract-card__icon {
  display: grid;
  width: 3.1rem;
  height: 3.1rem;
  place-items: center;
  border: 1px solid rgb(146 136 255 / 26%);
  border-radius: var(--ui-radius-md);
  background: rgb(5 8 14 / 82%);
  color: #b8b2ff;
  font-size: 1.2rem;
}

.contract-card__copy {
  display: grid;
  min-width: 0;
  gap: 3px;
}

.contract-card__copy small {
  color: #aaa3ff;
  font-size: .53rem;
  font-weight: 800;
  letter-spacing: .07em;
}

.contract-card__copy strong {
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-sm);
}

.contract-card__copy p {
  margin: 0;
  color: var(--ui-color-text-muted);
  font-size: .66rem;
  line-height: 1.4;
}

.contract-card__reward {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 8px;
  margin-top: 4px;
  font-size: .58rem;
}

.contract-card__reward span,
.contract-card__reward em {
  color: var(--ui-color-text-muted);
  font-style: normal;
}

.contract-card__reward b {
  color: var(--ui-color-gold);
}

.contract-card__status {
  padding: 6px 9px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-round);
  color: var(--ui-color-text-muted);
  font-size: .58rem;
  font-weight: 700;
  white-space: nowrap;
}

.contract-card__status--active {
  border-color: rgb(146 136 255 / 30%);
  color: #c2bdff;
}

.contract-card__status--done {
  border-color: rgb(79 185 150 / 30%);
  color: #84d5bb;
}

.activity-card {
  display: grid;
  grid-template-columns: 3.2rem minmax(0, 1fr) auto;
  align-items: center;
  gap: var(--ui-space-3);
  padding: var(--ui-space-4);
}

.activity-card--explore {
  background:
    radial-gradient(circle at 8% 50%, rgb(208 164 88 / 9%), transparent 9rem),
    linear-gradient(90deg, rgb(208 164 88 / 4%), transparent 70%);
}

.service-card__portrait {
  width: 3.1rem;
  height: 3.1rem;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: var(--ui-radius-md);
  object-fit: cover;
  object-position: top center;
  box-shadow: 0 .35rem .9rem rgb(0 0 0 / 32%);
}

.activity-card__icon,
.service-card__icon {
  display: grid;
  width: 3.1rem;
  height: 3.1rem;
  place-items: center;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: var(--ui-radius-md);
  background:
    radial-gradient(circle at 50% 25%, rgb(146 136 255 / 10%), transparent 62%),
    rgb(5 8 14 / 82%);
  color: #aaa3ff;
  font-size: 1.25rem;
}

.activity-card__copy,
.service-card__copy {
  display: grid;
  min-width: 0;
  gap: 2px;
}

.activity-card__copy small,
.service-card__copy small {
  color: #aaa3ff;
  font-size: .53rem;
  font-weight: 800;
  letter-spacing: .07em;
}

.activity-card__copy strong,
.service-card__copy strong {
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-sm);
}

.activity-card__copy p,
.service-card__copy p {
  margin: 0;
  color: var(--ui-color-text-muted);
  font-size: .66rem;
  line-height: 1.4;
}

.service-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1px;
  background: rgb(255 255 255 / 6%);
}

.service-card {
  position: relative;
  display: grid;
  min-height: 12rem;
  align-content: start;
  gap: var(--ui-space-3);
  padding: var(--ui-space-4);
  overflow: hidden;
  background: linear-gradient(160deg, rgb(13 19 31 / 100%), rgb(5 8 14 / 100%));
}

.service-card::after {
  position: absolute;
  right: -2.5rem;
  bottom: -3rem;
  width: 9rem;
  height: 9rem;
  border-radius: 50%;
  background: radial-gradient(circle, rgb(146 136 255 / 8%), transparent 68%);
  content: '';
  pointer-events: none;
}

.service-card--training {
  background:
    linear-gradient(150deg, rgb(86 76 168 / 12%), transparent 58%),
    linear-gradient(160deg, rgb(13 19 31 / 100%), rgb(5 8 14 / 100%));
}

.service-card--merchant {
  background:
    linear-gradient(150deg, rgb(208 164 88 / 9%), transparent 58%),
    linear-gradient(160deg, rgb(13 19 31 / 100%), rgb(5 8 14 / 100%));
}

.service-card--rest {
  grid-column: 1 / -1;
  grid-template-columns: 3.1rem minmax(0, 1fr) auto;
  align-items: center;
  min-height: auto;
  background:
    linear-gradient(90deg, rgb(79 185 150 / 7%), transparent 64%),
    rgb(7 11 18);
}

.service-card :deep(.ui-button) {
  position: relative;
  z-index: 1;
  width: 100%;
  align-self: end;
  margin-top: auto;
}

.service-card__status {
  position: relative;
  z-index: 1;
  padding: 5px 9px;
  border: 1px solid rgb(79 185 150 / 25%);
  border-radius: var(--ui-radius-round);
  color: #84d5bb;
  font-size: .57rem;
  font-weight: 700;
}

@media (max-width: 520px) {
  .world {
    padding: var(--ui-space-3);
    padding-bottom: var(--ui-space-6);
  }

  .scene,
  .scene__content {
    min-height: 21rem;
  }

  .scene__content {
    padding: var(--ui-space-4);
  }

  .scene-location h1 {
    max-width: 100%;
  }

  .scene-encounter {
    grid-template-columns: 6.5rem 1fr;
    gap: var(--ui-space-3);
    padding: var(--ui-space-3);
  }

  .scene-encounter img {
    max-height: 9rem;
  }

  .activity-card {
    grid-template-columns: 2.8rem minmax(0, 1fr);
    gap: var(--ui-space-2);
    padding: var(--ui-space-3);
  }

  .activity-card__icon {
    width: 2.8rem;
    height: 2.8rem;
  }

  .activity-card :deep(.ui-button) {
    grid-column: 1 / -1;
    width: 100%;
  }

  .service-grid {
    grid-template-columns: 1fr;
  }

  .service-card,
  .service-card--rest {
    grid-column: auto;
    grid-template-columns: 3rem minmax(0, 1fr);
    min-height: auto;
    align-items: center;
    gap: var(--ui-space-3);
    padding: var(--ui-space-3);
  }

  .service-card :deep(.ui-button),
  .service-card__status {
    grid-column: 1 / -1;
    width: 100%;
  }

  .service-card__status {
    box-sizing: border-box;
    text-align: center;
  }

}
</style>