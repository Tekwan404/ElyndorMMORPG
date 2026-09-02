<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'

import { gameArt } from '@/assets/gameArt'
import { classLabel, resourceLabel } from '@/game/character/characterPresentation'
import CombatView from '@/game/combat/views/CombatView.vue'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UICard, UILoadingState, UIPanel, UIToast, UIHealthBar } from '@/ui/components'

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
  {
    id: 'WOLF',
    name: 'Волк',
    level: 3,
    description: 'Дикий волк вышел на тропу и внимательно следит за каждым движением.',
    art: gameArt.monsters.wolf,
  },
  {
    id: 'FOREST_BOAR',
    name: 'Лесной кабан',
    level: 2,
    description: 'Тяжёлый кабан роет землю копытом и готовится броситься вперёд.',
    art: gameArt.monsters.forestBoar,
  },
  {
    id: 'GIANT_SPIDER',
    name: 'Гигантский паук',
    level: 2,
    description: 'Из тёмных корней выполз огромный паук. Он быстрый, но заметно хрупче других зверей.',
    art: gameArt.monsters.giantSpider,
  },
]

const session = useGameSessionStore()
const combat = useCombatSessionStore()
const selectedEncounter = ref<ForestEncounter | null>(null)
const lastCombatResult = ref<CombatResult | null>(null)
const lastEnemyName = ref<string | null>(null)
let encounterCursor = -1
let vitalsRefreshTimer: ReturnType<typeof setInterval> | null = null
let vitalsRefreshPending = false

const world = computed(() => session.snapshot?.world)
const character = computed(() => session.snapshot?.character)
const currentLocationId = computed(() => world.value?.currentLocation.id)
const isStarterTown = computed(() => currentLocationId.value === STARTER_TOWN_ID)
const isWhisperingForest = computed(() => currentLocationId.value === WHISPERING_FOREST_ID)
const needsOutOfCombatRefresh = computed(() => {
  const vitals = character.value?.vitals
  if (!vitals || combat.isActive) return false

  const hpRecovering = isStarterTown.value && vitals.currentHp < vitals.maxHp
  const resourceRecovering = vitals.resourceType === 'RAGE'
    ? vitals.currentResource > 0
    : vitals.currentResource < vitals.maxResource
  return hpRecovering || resourceRecovering
})
const recoveryMessage = computed(() => {
  const vitals = character.value?.vitals
  if (!vitals || combat.isActive) return null
  if (isStarterTown.value && vitals.currentHp < vitals.maxHp) {
    return 'Вы отдыхаете в городе. Здоровье восстанавливается по 5 ед. в секунду.'
  }
  if (vitals.resourceType === 'RAGE' && vitals.currentResource > 0) {
    return 'После боя ярость постепенно угасает.'
  }
  return null
})
const sceneBackground = computed(() => {
  const dangerLevel = world.value?.currentLocation.dangerLevel
  if (dangerLevel === 'DANGEROUS') return gameArt.world.ruins
  if (dangerLevel === 'ADVENTURE') return gameArt.world.forest
  return gameArt.world.capital
})
const locationName = computed(() => {
  if (currentLocationId.value === STARTER_TOWN_ID) return 'Стартовый город'
  if (currentLocationId.value === WHISPERING_FOREST_ID) return 'Шепчущий лес'
  return world.value?.currentLocation.displayName ?? 'Неизвестная область'
})
const locationDescription = computed(() => {
  if (currentLocationId.value === STARTER_TOWN_ID) {
    return 'Безопасное место для отдыха и подготовки. Здесь герой постепенно восстанавливает здоровье.'
  }
  if (currentLocationId.value === WHISPERING_FOREST_ID) {
    return 'Сумрачный лес вокруг старых дорог. Здесь можно встретить волков, кабанов и гигантских пауков.'
  }
  return 'Неизведанная область мира Элиндора.'
})
const dangerLabel = computed(() => {
  const danger = world.value?.currentLocation.dangerLevel
  if (danger === 'SAFE') return 'Безопасная зона'
  if (danger === 'ADVENTURE') return 'Опасная область'
  if (danger === 'DANGEROUS') return 'Высокая опасность'
  return 'Неизвестная опасность'
})

function transitionName(locationId: string, fallback: string): string {
  if (locationId === STARTER_TOWN_ID) return 'Стартовый город'
  if (locationId === WHISPERING_FOREST_ID) return 'Шепчущий лес'
  return fallback
}

function dangerText(level: string): string {
  if (level === 'SAFE') return 'безопасно'
  if (level === 'ADVENTURE') return 'опасно'
  if (level === 'DANGEROUS') return 'очень опасно'
  return level
}

function explore(): void {
  if (!isWhisperingForest.value || combat.isActive) return
  lastCombatResult.value = null
  encounterCursor = (encounterCursor + 1) % FOREST_ENCOUNTERS.length
  selectedEncounter.value = FOREST_ENCOUNTERS[encounterCursor] ?? null
}

async function startEncounterCombat(): Promise<void> {
  if (!isWhisperingForest.value || combat.pending || !selectedEncounter.value) return
  const encounter = selectedEncounter.value
  const started = await combat.startCombat(encounter.id)
  if (started) {
    lastEnemyName.value = encounter.name
    selectedEncounter.value = null
    lastCombatResult.value = null
  }
}

function cancelEncounter(): void {
  selectedEncounter.value = null
}

function handleCombatLeft(): void {
  lastCombatResult.value = 'Cancelled'
}

async function restoreCombat(): Promise<void> {
  try {
    await combat.connect()
    await combat.resume()
    if (combat.snapshot) {
      lastEnemyName.value = localizedEnemyName(
        combat.snapshot.enemy.definitionId,
        combat.snapshot.enemy.name,
      )
    }
  } catch {
    // Навигация по миру должна оставаться доступной при временной ошибке realtime-канала.
  }
}

async function refreshOutOfCombatVitals(): Promise<void> {
  if (vitalsRefreshPending || combat.isActive || session.mutationPending) return
  vitalsRefreshPending = true
  try {
    await session.refreshSnapshot()
  } catch {
    // Фоновое обновление восстановления не должно блокировать экран мира.
  } finally {
    vitalsRefreshPending = false
  }
}

function syncVitalsRefreshTimer(enabled: boolean): void {
  if (enabled && vitalsRefreshTimer === null) {
    vitalsRefreshTimer = setInterval(() => void refreshOutOfCombatVitals(), 1000)
    return
  }
  if (!enabled && vitalsRefreshTimer !== null) {
    clearInterval(vitalsRefreshTimer)
    vitalsRefreshTimer = null
  }
}

function localizedEnemyName(definitionId: string, fallback: string): string {
  return FOREST_ENCOUNTERS.find((encounter) => encounter.id === definitionId)?.name ?? fallback
}

watch(
  currentLocationId,
  (locationId, previousLocationId) => {
    if (locationId !== previousLocationId) {
      selectedEncounter.value = null
      encounterCursor = -1
      const isDefeatRespawn = lastCombatResult.value === 'Defeat' && locationId === STARTER_TOWN_ID
      if (!isDefeatRespawn) {
        lastCombatResult.value = null
        lastEnemyName.value = null
      }
    }
    if (locationId === WHISPERING_FOREST_ID) {
      void restoreCombat()
    }
  },
  { immediate: true },
)

watch(
  () => combat.snapshot?.status,
  (status) => {
    if (status === 'Victory' || status === 'Defeat') {
      selectedEncounter.value = null
      if (combat.snapshot) {
        lastEnemyName.value = localizedEnemyName(
          combat.snapshot.enemy.definitionId,
          combat.snapshot.enemy.name,
        )
      }
      lastCombatResult.value = status
      void session.refreshSnapshot()
    }
  },
)

watch(needsOutOfCombatRefresh, syncVitalsRefreshTimer, { immediate: true })
onBeforeUnmount(() => syncVitalsRefreshTimer(false))
</script>

<template>
  <CombatView v-if="combat.isActive" @leave="handleCombatLeft" />

  <section v-else-if="world && character" class="world">
    <section class="scene" :style="{ backgroundImage: `url(${sceneBackground})` }">
      <div class="scene__shade" />
      <header class="scene__header">
        <span class="danger-badge" :data-danger="world.currentLocation.dangerLevel">{{ dangerLabel }}</span>
        <small>Рекомендуемый уровень: {{ world.currentLocation.recommendedLevel }}</small>
      </header>
      <div class="scene__copy">
        <p>Текущая локация</p>
        <h1>{{ locationName }}</h1>
        <span>{{ locationDescription }}</span>
      </div>
    </section>

    <section class="hero-hud">
      <div class="hero-hud__identity">
        <strong>{{ character.name }}</strong>
        <small>{{ classLabel(character.classId) }} · уровень {{ character.level }}</small>
      </div>
      <div class="hero-hud__bars">
        <UIHealthBar label="Здоровье" :value="character.vitals.currentHp" :max="character.vitals.maxHp" />
        <UIHealthBar
          :label="resourceLabel(character.vitals.resourceType)"
          :tone="character.vitals.resourceType === 'RAGE' ? 'rage' : character.vitals.resourceType === 'MANA' ? 'mana' : 'focus'"
          :value="character.vitals.currentResource"
          :max="character.vitals.maxResource"
        />
      </div>
    </section>

    <UIToast v-if="recoveryMessage" tone="info" title="Восстановление" data-world-recovery>
      {{ recoveryMessage }}
    </UIToast>

    <UIToast v-if="lastCombatResult === 'Victory'" tone="success" title="Победа" data-combat-result>
      {{ lastEnemyName ?? 'Противник' }} повержен. Вы снова можете продолжить исследование.
    </UIToast>
    <UICard v-if="lastCombatResult === 'Victory' && combat.reward" class="reward-card" data-victory-reward>
      <div class="reward-card__heading">
        <div><small>Награда за победу</small><h2>+{{ combat.reward.xpEarned }} опыта</h2></div>
        <b v-if="combat.reward.leveledUp">Новый уровень: {{ combat.reward.currentLevel }}</b>
      </div>
      <ul v-if="combat.reward.items.length">
        <li v-for="item in combat.reward.items" :key="item.itemId">
          <span>{{ item.name }}</span><b>×{{ item.quantity }}</b>
          <small>{{ item.type === 'Equipment' ? 'Снаряжение' : 'Материал' }}</small>
        </li>
      </ul>
      <p v-else>В этот раз дополнительных предметов не выпало.</p>
    </UICard>
    <UIToast v-if="lastCombatResult === 'Defeat'" tone="danger" title="Поражение" data-combat-result>
      {{ lastEnemyName ?? 'Противник' }} оказался сильнее. Вы очнулись в Стартовом городе с восстановленным здоровьем.
    </UIToast>
    <UIToast v-else-if="lastCombatResult === 'Cancelled'" tone="info" title="Бой прерван" data-combat-result>
      Вы покинули бой и вернулись к исследованию.
    </UIToast>

    <UICard v-if="selectedEncounter" class="encounter" data-world-encounter :data-monster-id="selectedEncounter.id">
      <div class="encounter__art">
        <img :src="selectedEncounter.art" :alt="selectedEncounter.name" />
      </div>
      <div class="encounter__copy">
        <p class="encounter__eyebrow">Обнаружен противник</p>
        <h2>{{ selectedEncounter.name }}</h2>
        <p class="encounter__level">Уровень {{ selectedEncounter.level }}</p>
        <p>{{ selectedEncounter.description }}</p>
      </div>
      <div class="encounter__actions">
        <UIButton data-start-encounter :loading="combat.pending" :disabled="combat.pending" @click="startEncounterCombat">
          Вступить в бой
        </UIButton>
        <UIButton variant="ghost" :disabled="combat.pending" @click="cancelEncounter">Уйти незамеченным</UIButton>
      </div>
    </UICard>

    <section v-else-if="isWhisperingForest" class="primary-action">
      <div>
        <small>Исследование области</small>
        <strong>Осмотреть лесные тропы</strong>
        <p>Найдите противника, чтобы получить опыт, материалы и шанс на снаряжение.</p>
      </div>
      <UIButton data-explore :disabled="combat.pending" @click="explore">Исследовать</UIButton>
    </section>

    <section v-else-if="isStarterTown" class="primary-action primary-action--safe">
      <div>
        <small>Безопасная зона</small>
        <strong>Отдохнуть и подготовиться</strong>
        <p>В городе здоровье восстанавливается автоматически. Проверьте экипировку и таланты перед новым походом.</p>
      </div>
    </section>

    <UIPanel v-if="!selectedEncounter" class="paths">
      <template #title>Куда отправиться</template>
      <div v-if="world.outgoingTransitions.length > 0" class="paths__list">
        <UICard v-for="location in world.outgoingTransitions" :key="location.id" class="path-card">
          <div class="path-card__copy">
            <strong>{{ transitionName(location.id, location.displayName) }}</strong>
            <small>{{ dangerText(location.dangerLevel) }} · рекомендуемый уровень {{ location.recommendedLevel }}</small>
          </div>
          <UIButton
            :data-travel="location.id"
            :aria-label="`Отправиться: ${transitionName(location.id, location.displayName)}`"
            variant="secondary"
            :loading="session.mutationPending"
            :disabled="session.mutationPending"
            @click="session.travel(location.id)"
          >
            Отправиться
          </UIButton>
        </UICard>
      </div>
      <UILoadingState v-else state="empty" title="Пути не найдены" message="Эта область пока не открывает новых направлений." />
    </UIPanel>

    <div v-if="session.errorCode" role="alert">
      <UIToast tone="danger" title="Не удалось выполнить действие">Код ошибки: {{ session.errorCode }}</UIToast>
    </div>
    <div v-if="combat.errorCode" role="alert" data-combat-diagnostic>
      <UIToast tone="danger" title="Ошибка соединения с боем">
        <p>Код: <code>{{ combat.errorCode }}</code></p>
        <div v-if="combat.diagnostic" class="diagnostic">
          <small><b>Этап:</b> {{ combat.diagnostic.stage }}</small>
          <small v-if="combat.diagnostic.operation"><b>Операция:</b> {{ combat.diagnostic.operation }}</small>
          <small v-if="combat.diagnostic.statusCode !== null"><b>HTTP:</b> {{ combat.diagnostic.statusCode }}</small>
          <small><b>Сообщение:</b> {{ combat.diagnostic.message }}</small>
        </div>
      </UIToast>
    </div>
  </section>
</template>

<style scoped>
.world { display: grid; width: min(100%, var(--ui-content-width)); margin-inline: auto; gap: var(--ui-space-4); padding: var(--ui-space-4) var(--ui-space-4) var(--ui-space-7); }
.scene { position: relative; min-height: clamp(17rem, 62vw, 25rem); overflow: hidden; border: 1px solid var(--ui-color-border-strong); border-radius: var(--ui-radius-lg); background-color: var(--ui-color-surface-1); background-position: center; background-size: cover; box-shadow: var(--ui-shadow-panel); }
.scene__shade { position: absolute; inset: 0; background: linear-gradient(180deg, rgb(5 8 16 / 18%) 10%, rgb(5 8 16 / 20%) 45%, rgb(5 8 16 / 92%) 100%); }
.scene__header { position: absolute; z-index: 1; top: var(--ui-space-3); right: var(--ui-space-3); left: var(--ui-space-3); display: flex; align-items: center; justify-content: space-between; gap: var(--ui-space-2); }
.scene__header small { padding: 4px 8px; border-radius: var(--ui-radius-round); background: rgb(5 8 16 / 62%); color: var(--ui-color-text-secondary); backdrop-filter: blur(6px); }
.danger-badge { padding: 4px 9px; border: 1px solid var(--ui-color-border-strong); border-radius: var(--ui-radius-round); background: rgb(5 8 16 / 68%); color: var(--ui-color-warning); font-size: var(--ui-font-size-xs); font-weight: 700; backdrop-filter: blur(6px); }
.danger-badge[data-danger='SAFE'] { color: var(--ui-color-success); }
.danger-badge[data-danger='DANGEROUS'] { color: var(--ui-color-danger); }
.scene__copy { position: absolute; z-index: 1; right: var(--ui-space-4); bottom: var(--ui-space-4); left: var(--ui-space-4); display: grid; max-width: 32rem; gap: var(--ui-space-1); }
.scene__copy p, .scene__copy h1, .scene__copy span { margin: 0; }
.scene__copy p { color: var(--ui-color-primary); font-size: var(--ui-font-size-xs); font-weight: 700; letter-spacing: .1em; text-transform: uppercase; }
.scene__copy h1 { font-family: var(--ui-font-display); font-size: clamp(var(--ui-font-size-xl), 9vw, 2.4rem); }
.scene__copy span { color: var(--ui-color-text-secondary); line-height: var(--ui-line-height-normal); text-shadow: 0 1px 2px rgb(0 0 0 / 70%); }
.hero-hud { display: grid; grid-template-columns: minmax(0, .72fr) minmax(0, 1.28fr); align-items: center; gap: var(--ui-space-4); padding: var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-lg); background: var(--ui-color-surface-2); }
.hero-hud__identity { display: grid; gap: 2px; }
.hero-hud__identity strong { color: var(--ui-color-text-primary); }
.hero-hud__identity small { color: var(--ui-color-text-muted); }
.hero-hud__bars { display: grid; gap: var(--ui-space-2); }
.primary-action { display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: center; gap: var(--ui-space-4); padding: var(--ui-space-4); border: 1px solid rgb(255 178 74 / 35%); border-radius: var(--ui-radius-lg); background: linear-gradient(120deg, rgb(255 165 61 / 10%), var(--ui-color-surface-2)); }
.primary-action--safe { border-color: rgb(81 211 152 / 30%); background: linear-gradient(120deg, rgb(81 211 152 / 8%), var(--ui-color-surface-2)); }
.primary-action > div { display: grid; gap: var(--ui-space-1); }
.primary-action small { color: var(--ui-color-warning); text-transform: uppercase; }
.primary-action--safe small { color: var(--ui-color-success); }
.primary-action strong { color: var(--ui-color-text-primary); font-size: var(--ui-font-size-md); }
.primary-action p { margin: 0; color: var(--ui-color-text-muted); line-height: var(--ui-line-height-normal); }
.encounter { display: grid; grid-template-columns: 8rem minmax(0, 1fr); gap: var(--ui-space-4); border-color: var(--ui-color-warning); background: radial-gradient(circle at 10% 20%, rgb(255 162 67 / 12%), transparent 35%), var(--ui-color-surface-2); }
.encounter__art { display: grid; min-height: 8rem; place-items: center; overflow: hidden; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: rgb(6 9 18 / 58%); }
.encounter__art img { width: 100%; max-height: 8rem; object-fit: contain; filter: drop-shadow(0 .5rem .7rem rgb(0 0 0 / 60%)); }
.encounter__copy { display: grid; align-content: center; gap: var(--ui-space-1); }
.encounter__copy h2, .encounter__copy p { margin: 0; }
.encounter__copy h2 { font-family: var(--ui-font-display); }
.encounter__copy p:not(.encounter__eyebrow) { color: var(--ui-color-text-muted); line-height: var(--ui-line-height-normal); }
.encounter__eyebrow { color: var(--ui-color-warning); font-size: var(--ui-font-size-xs); font-weight: 700; letter-spacing: .08em; text-transform: uppercase; }
.encounter__level { font-weight: 700; }
.encounter__actions { grid-column: 1 / -1; display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--ui-space-2); }
.paths { box-shadow: none; }
.paths__list { display: grid; gap: var(--ui-space-3); }
.path-card { display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: center; gap: var(--ui-space-3); }
.path-card__copy { display: grid; min-width: 0; gap: var(--ui-space-1); }
.path-card__copy strong { color: var(--ui-color-text-primary); }
.path-card__copy small { color: var(--ui-color-text-muted); }
.reward-card { display: grid; gap: var(--ui-space-3); border-color: var(--ui-color-success); }
.reward-card__heading { display: flex; align-items: center; justify-content: space-between; gap: var(--ui-space-3); }
.reward-card__heading small { color: var(--ui-color-text-muted); text-transform: uppercase; }
.reward-card__heading h2 { margin: 0; color: var(--ui-color-success); font-family: var(--ui-font-display); }
.reward-card__heading > b { color: var(--ui-color-warning); }
.reward-card ul { display: grid; gap: var(--ui-space-2); margin: 0; padding: 0; list-style: none; }
.reward-card li { display: grid; grid-template-columns: 1fr auto; gap: 0 var(--ui-space-2); padding-top: var(--ui-space-2); border-top: 1px solid var(--ui-color-border); }
.reward-card li small { grid-column: 1 / -1; color: var(--ui-color-text-muted); }
.reward-card p { margin: 0; color: var(--ui-color-text-muted); }
.diagnostic { display: grid; gap: var(--ui-space-1); margin-top: var(--ui-space-2); overflow-wrap: anywhere; }
.diagnostic small { color: var(--ui-color-text-muted); }
.diagnostic b { color: var(--ui-color-text-secondary); }
code { overflow-wrap: anywhere; color: var(--ui-color-danger); }
@media (max-width: 430px) { .world { padding-inline: var(--ui-space-3); } .scene { min-height: 18rem; } .scene__header { align-items: flex-start; } .scene__header small { max-width: 9rem; text-align: right; } .hero-hud { grid-template-columns: 1fr; gap: var(--ui-space-3); } .primary-action { grid-template-columns: 1fr; } .encounter { grid-template-columns: 6rem minmax(0, 1fr); } .encounter__art { min-height: 6rem; } .encounter__art img { max-height: 6rem; } }
@media (max-width: 360px) { .encounter__actions, .path-card { grid-template-columns: 1fr; } }
</style>
