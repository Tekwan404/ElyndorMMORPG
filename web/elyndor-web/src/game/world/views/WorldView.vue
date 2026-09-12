<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'

import { gameArt } from '@/assets/gameArt'
import AdventurerGuildBoard from '@/game/world/components/AdventurerGuildBoard.vue'
import DungeonLocationCard from '@/game/world/components/DungeonLocationCard.vue'
import MerchantShop from '@/game/world/components/MerchantShop.vue'
import { locationKind, locationLabel, locationPresentation } from '@/game/world/locationPresentation'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'
import { usePartyStore } from '@/game/party/partyStore'
import { UIButton, UICard, UIModal, UIToast } from '@/ui/components'
import IconGenerator from '@/ui/icons/IconGenerator.vue'

const props = withDefaults(defineProps<{ openGuild?: boolean }>(), { openGuild: false })
const emit = defineEmits<{ 'open-party': [] }>()

type CombatResult = 'Victory' | 'Defeat' | 'Cancelled'

const session = useGameSessionStore()
const combat = useCombatSessionStore()
const party = usePartyStore()
const lastCombatResult = ref<CombatResult | null>(null)
const lastEnemyName = ref<string | null>(null)
const lootNow = ref(Date.now())
const merchantOpen = ref(false)
const guildOpen = ref(false)
const afkOpen = ref(false)
const afkDurationMinutes = ref(60)
const afkPreviewLoading = ref(false)
const afkPreview = ref<Awaited<ReturnType<typeof session.previewAfkFarm>>>(null)
let vitalsRefreshTimer: ReturnType<typeof setInterval> | null = null
let combatResultTimer: number | null = null
let vitalsRefreshPending = false
const COMBAT_RESULT_SESSION_KEY = 'elyndor:last-combat-result-announcement'
const lootTimer = window.setInterval(() => (lootNow.value = Date.now()), 1000)

const world = computed(() => session.snapshot?.world)
const activeTravel = computed(() => world.value?.travel ?? null)
const isTravelling = computed(() => activeTravel.value !== null)
const character = computed(() => session.snapshot?.character)
const currentLocationId = computed(() => world.value?.currentLocation.id ?? '')
const isDungeonLocation = computed(() => locationKind(currentLocationId.value) === 'dungeon')
const isCityLocation = computed(() => locationKind(currentLocationId.value) === 'city')
const activeAfkFarm = computed(() => session.snapshot?.afkFarm?.status === 'Active'
  ? session.snapshot.afkFarm
  : null)
const canUseAfkFarm = computed(() =>
  !isTravelling.value
  && !combat.isActive
  && !isDungeonLocation.value
  && world.value?.currentLocation.allowAfk === true
  && activeAfkFarm.value === null,
)
const canExplore = computed(() =>
  !isTravelling.value && world.value?.currentLocation.dangerLevel !== 'SAFE',
)
const canStartWorldCombat = computed(() =>
  party.snapshot === null
    || party.snapshot.leaderCharacterId === character.value?.id,
)
const locationContracts = computed(() => (world.value?.contracts ?? []).filter((contract) =>
  contract.offerLocationId === currentLocationId.value
  || contract.status === 'ACTIVE',
))
const locationQuestLeads = computed(() => (session.questJournal?.quests ?? []).filter(quest =>
  quest.status === 'AVAILABLE'
  && quest.type !== 'CONTRACT'
  && quest.offerLocationId === currentLocationId.value,
))
const hasLocalGuildContracts = computed(() => (session.questJournal?.quests ?? []).some(quest =>
  quest.type === 'CONTRACT'
  && quest.offerLocationId === currentLocationId.value
  && quest.status !== 'COMPLETED',
))
const locationName = computed(() => world.value
  ? locationLabel(world.value.currentLocation)
  : 'Неизвестная область')
const locationDescription = computed(() =>
  world.value?.currentLocation.description
  || 'Исследуйте текущую область. Для путешествия между областями используйте карту мира.',
)

async function acceptQuest(questId: string): Promise<void> {
  if (isTravelling.value || session.mutationPending || combat.isActive) return
  await session.acceptQuest(questId)
}
const sceneBackground = computed(() => {
  return locationPresentation(currentLocationId.value).art
})
function displayLocationName(locationId: string | null | undefined): string {
  return locationPresentation(locationId).label
}
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
  return 'Сервер не подтвердил действие. Проверьте связь и повторите попытку.'
})
const recoveryMessage = computed(() => {
  const vitals = character.value?.vitals
  if (!vitals || combat.isActive || isTravelling.value) return null
  if (vitals.currentHp < vitals.maxHp) {
    return isCityLocation.value
      ? 'Отдых в городе: здоровье восстанавливается со скоростью 15% от максимального здоровья в секунду.'
      : 'Вне города здоровье восстанавливается со скоростью 10% от максимального здоровья в секунду.'
  }
  if (vitals.resourceType === 'RAGE' && vitals.currentResource > 0) return 'После боя ярость постепенно угасает.'
  return null
})
const needsOutOfCombatRefresh = computed(() => {
  const vitals = character.value?.vitals
  if (!vitals || combat.isActive) return false
  return vitals.currentHp < vitals.maxHp
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
  if (!canExplore.value
    || !canStartWorldCombat.value
    || combat.isActive
    || session.mutationPending
    || combat.pending) return

  dismissCombatResult()
  const encounter = await session.explore()
  if (!encounter) return

  if (await combat.startCombat(encounter)) {
    lastEnemyName.value = encounter.name
    lastCombatResult.value = null
  }
}

async function openAfkFarm(): Promise<void> {
  if (!canUseAfkFarm.value) return
  afkOpen.value = true
  await loadAfkPreview()
}

async function loadAfkPreview(): Promise<void> {
  if (!currentLocationId.value || afkPreviewLoading.value) return
  afkPreviewLoading.value = true
  try {
    afkPreview.value = await session.previewAfkFarm(currentLocationId.value, afkDurationMinutes.value)
  } finally {
    afkPreviewLoading.value = false
  }
}

async function selectAfkDuration(durationMinutes: number): Promise<void> {
  afkDurationMinutes.value = durationMinutes
  await loadAfkPreview()
}

async function startAfkFarm(): Promise<void> {
  if (!currentLocationId.value || session.mutationPending) return
  const started = await session.startAfkFarm(currentLocationId.value, afkDurationMinutes.value)
  if (started) afkOpen.value = false
}

async function stopAfkFarm(): Promise<void> {
  if (session.mutationPending) return
  await session.stopAfkFarm()
}

async function startTraining(): Promise<void> {
  if (isTravelling.value || !isCityLocation.value || combat.pending) return
  if (await combat.startTraining()) {
    lastEnemyName.value = 'Тренировочный манекен'
    dismissCombatResult()
  }
}

function lootRemaining(endsAtUtc: string): number {
  return Math.max(0, (Date.parse(endsAtUtc) - lootNow.value) / 1_000)
}

async function chooseLootRoll(lootRollId: string, choice: 'Need' | 'Greed' | 'Pass'): Promise<void> {
  await combat.chooseLootRoll(lootRollId, choice)
}

async function attachToPartyCombat(): Promise<void> {
  if (!combat.isAwaitingAttachment || !combat.snapshot || combat.pending) return
  await combat.attachCombat(combat.snapshot.sessionId)
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

function dismissCombatResult(): void {
  if (combatResultTimer !== null) {
    window.clearTimeout(combatResultTimer)
    combatResultTimer = null
  }
  lastCombatResult.value = null
}

function scheduleCombatResultDismiss(): void {
  if (combatResultTimer !== null) window.clearTimeout(combatResultTimer)
  combatResultTimer = window.setTimeout(() => {
    lastCombatResult.value = null
    combatResultTimer = null
  }, 8_000)
}

watch(isTravelling, travelling => {
  if (travelling) {
    merchantOpen.value = false
    guildOpen.value = false
    dismissCombatResult()
  }
})

watch(currentLocationId, (locationId, previousLocationId) => {
  if (locationId !== previousLocationId) {
    merchantOpen.value = false
    guildOpen.value = false
    if (previousLocationId) dismissCombatResult()
  }
}, { immediate: true })

watch(() => combat.snapshot?.status, (status) => {
  if (status === 'Victory' || status === 'Defeat') {
    const terminalSnapshot = combat.snapshot
    if (!terminalSnapshot) return

    if (window.sessionStorage.getItem(COMBAT_RESULT_SESSION_KEY) !== terminalSnapshot.sessionId) {
      window.sessionStorage.setItem(COMBAT_RESULT_SESSION_KEY, terminalSnapshot.sessionId)
      lastEnemyName.value = terminalSnapshot.enemies?.[0]?.name ?? terminalSnapshot.enemy.name
      lastCombatResult.value = combat.participantStatus === 'Fled' ? 'Cancelled' : status
      scheduleCombatResultDismiss()
    }
    void session.refreshSnapshot()
  }
}, { immediate: true })

watch(needsOutOfCombatRefresh, syncVitalsRefreshTimer, { immediate: true })
watch(() => props.openGuild, open => {
  if (open && isCityLocation.value && !isTravelling.value) guildOpen.value = true
})
onBeforeUnmount(() => {
  syncVitalsRefreshTimer(false)
  if (combatResultTimer !== null) window.clearTimeout(combatResultTimer)
  window.clearInterval(lootTimer)
})
onMounted(() => {
  void party.refresh()
  void session.refreshQuestJournal()
  if (props.openGuild && isCityLocation.value && !isTravelling.value) guildOpen.value = true
})
</script>

<template>
  <section v-if="world && character" class="world">
    <section v-if="!isDungeonLocation" class="scene" :style="{ backgroundImage: `url(${sceneBackground})` }">
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
        <div v-if="canExplore && lastCombatResult !== 'Victory' && canStartWorldCombat" class="scene__actions" aria-label="Действия локации">
          <UIButton
            data-explore
            :loading="session.mutationPending"
            @click="explore"
          >
            Исследовать
          </UIButton>
          <UIButton
            v-if="canUseAfkFarm"
            data-afk-farming
            variant="secondary"
            :disabled="session.mutationPending"
            @click="openAfkFarm"
          >AFK-фарм</UIButton>
        </div>
      </div>
    </section>

    <DungeonLocationCard
      v-if="isDungeonLocation && currentLocationId"
      :dungeon-id="currentLocationId"
      @open-party="emit('open-party')"
    />

    <div v-if="session.errorCode" class="world-error" role="alert">
      <strong>{{ worldErrorMessage }}</strong>
    </div>

    <UICard v-if="activeAfkFarm" class="afk-status" data-afk-active>
      <div>
        <small>AFK-ФАРМ АКТИВЕН</small>
        <strong>{{ activeAfkFarm.kills }} побед · +{{ activeAfkFarm.xpEarned }} опыта · +{{ activeAfkFarm.goldEarned }} золота</strong>
        <p>До {{ new Date(activeAfkFarm.endsAtUtc).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' }) }}</p>
      </div>
      <UIButton variant="secondary" :loading="session.mutationPending" @click="stopAfkFarm">Остановить</UIButton>
    </UICard>

    <UICard v-if="combat.isAwaitingAttachment" class="party-combat-card" data-party-combat-pending>
      <div class="reward-card__heading">
        <small>СОВМЕСТНЫЙ БОЙ</small>
        <strong>Группа уже сражается</strong>
      </div>
      <p>Доберись до локации боя и присоединись к текущему столкновению.</p>
      <UIButton
        :loading="combat.pending"
        :disabled="isTravelling"
        data-attach-party-combat
        @click="attachToPartyCombat"
      >Войти в бой</UIButton>
    </UICard>

    <UICard v-if="lastCombatResult === 'Victory'" class="reward-card" data-combat-result="victory">
      <div class="reward-card__heading">
        <small>ПОБЕДА</small>
        <strong>{{ lastEnemyName ?? 'Противник' }} повержен</strong>
      </div>
      <div v-if="combat.reward" class="reward-card__summary">
        <strong>+{{ combat.reward.xpEarned }} опыта · +{{ combat.reward.goldEarned }} золота</strong>
        <p v-if="combat.reward.completedContractIds?.includes('CONTRACT_BROODMOTHER_GATE')" class="contract-completed">
          <IconGenerator :config="{ id: 'contract-completed', glyph: 'star', category: 'utility' }" />
          Контракт выполнен: Прародительница. Путь в Осквернённую чащу открыт.
        </p>
        <ul v-if="combat.reward.items.length">
          <li v-for="item in combat.reward.items" :key="item.itemId">{{ item.name }} ×{{ item.quantity }}</li>
        </ul>
      </div>
      <UIButton v-if="canExplore && canStartWorldCombat" data-explore-after-victory :loading="session.mutationPending" @click="explore">Исследовать дальше</UIButton>
      <UIButton variant="secondary" data-dismiss-combat-result @click="dismissCombatResult">Закрыть</UIButton>
    </UICard>

    <UICard v-if="combat.lootRolls.length" class="loot-roll-card">
      <div class="reward-card__heading">
        <small>ЦЕННАЯ ДОБЫЧА</small>
        <strong>Выберите участие в розыгрыше</strong>
      </div>
      <article v-for="roll in combat.lootRolls" :key="roll.lootRollId" class="loot-roll-row">
        <div>
          <strong>{{ roll.name }} ×{{ roll.quantity }}</strong>
          <small>{{ roll.rarity }} · {{ Math.ceil(lootRemaining(roll.endsAtUtc)) }}с</small>
        </div>
        <div class="loot-roll-actions">
          <UIButton
            :disabled="combat.pending || !roll.canNeed"
            :title="roll.canNeed ? 'Приоритетный бросок' : 'Персонаж не может использовать этот предмет'"
            @click="chooseLootRoll(roll.lootRollId, 'Need')"
          >Нужно</UIButton>
          <UIButton :disabled="combat.pending" variant="secondary" @click="chooseLootRoll(roll.lootRollId, 'Greed')">Претендовать</UIButton>
          <UIButton :disabled="combat.pending" variant="secondary" @click="chooseLootRoll(roll.lootRollId, 'Pass')">Отказаться</UIButton>
        </div>
      </article>
    </UICard>

    <UIToast
      v-if="activeTravel"
      tone="info"
      title="Герой в пути"
      data-location-travel
    >
      Путешествие к {{ displayLocationName(activeTravel.targetLocationId) }} уже началось. Боевые,
      торговые и контрактные действия станут доступны после прибытия.
    </UIToast>

    <UIToast v-if="lastCombatResult === 'Defeat'" tone="danger" title="Поражение" data-combat-result="defeat">
      {{ isDungeonLocation
        ? 'Вы восстановились у входа в подземелье. Повтор доступен с 50% ресурса.'
        : 'Вы очнулись в Стартовом городе.' }}
    </UIToast>
    <UIToast v-if="recoveryMessage" tone="info" title="Восстановление">{{ recoveryMessage }}</UIToast>

    <section v-if="locationQuestLeads.length" class="world-stories" aria-labelledby="stories-title">
      <header class="section-heading">
        <div>
          <small>ЛЮДИ И ИСТОРИИ</small>
          <strong id="stories-title">Что происходит рядом</strong>
        </div>
        <span>{{ locationQuestLeads.length }}</span>
      </header>
      <div class="story-list">
        <article v-for="quest in locationQuestLeads" :key="quest.id" class="story-card" :data-world-quest-id="quest.id">
          <div class="story-card__icon" aria-hidden="true">
            <IconGenerator :config="{ id: `story-${quest.id}`, glyph: 'scroll', category: 'utility' }" />
          </div>
          <div class="story-card__copy">
            <small>{{ quest.type === 'SIDE' ? 'ПОРУЧЕНИЕ' : 'СЮЖЕТ' }} · ур. {{ quest.requiredLevel }}</small>
            <strong>{{ quest.displayName }}</strong>
            <p>{{ quest.description }}</p>
            <span class="story-card__reward">+{{ quest.rewardXp }} опыта · +{{ quest.rewardGold }} золота</span>
          </div>
          <UIButton
            data-accept-world-quest
            :disabled="isTravelling || session.mutationPending"
            @click="acceptQuest(quest.id)"
          >
            {{ quest.type === 'SIDE' ? 'Принять поручение' : 'Продолжить историю' }}
          </UIButton>
        </article>
      </div>
    </section>

    <section v-if="!isCityLocation && hasLocalGuildContracts" class="field-guild" aria-labelledby="field-guild-title">
        <header class="section-heading">
          <div>
            <small>ГИЛЬДИЯ АВАНТЮРИСТОВ</small>
            <strong id="field-guild-title">Экспедиционный пост</strong>
          </div>
          <span>КОНТРАКТЫ</span>
        </header>
        <article class="activity-card">
          <div class="activity-card__icon" aria-hidden="true">
            <IconGenerator :config="{ id: 'field-guild', glyph: 'sword', category: 'utility' }" />
          </div>
          <div class="activity-card__copy">
            <small>ПОЛЕВОЙ РЕГИСТРАТОР</small>
            <strong>Журнал контрактов экспедиции</strong>
            <p>Здесь регистрируют работу, связанную с угрозами текущего региона.</p>
          </div>
          <UIButton data-open-field-guild @click="guildOpen = true">Открыть журнал</UIButton>
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
          <div class="contract-card__icon" aria-hidden="true">
            <IconGenerator :config="{ id: `contract-${contract.id}`, glyph: 'star', category: 'utility' }" />
          </div>
          <div class="contract-card__copy">
            <small>{{ contractStatusLabel(contract.status) }} · УР. {{ contract.requiredLevel }}</small>
            <strong>{{ contract.displayName }}</strong>
            <p>{{ contract.description }}</p>
            <div class="contract-card__reward">
              <span>Награда</span>
              <b>+{{ contract.rewardXp }} опыта · +{{ contract.rewardGold }} золота</b>
              <em>Открывает: {{ displayLocationName(contract.unlockLocationId) }}</em>
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
    <section v-if="isCityLocation" class="town-services" aria-labelledby="town-services-title">
      <header class="section-heading">
        <div>
          <small>В ГОРОДЕ</small>
          <strong id="town-services-title">Городские сервисы</strong>
        </div>
        <span data-safe>4 МЕСТА</span>
      </header>

      <p class="town-services__hint">Выберите представителя, чтобы открыть его услугу.</p>

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

        <article class="service-card service-card--guild" data-town-service="guild">
          <img class="service-card__portrait" :src="gameArt.npc.registrar" alt="Регистратор гильдии" />
          <div class="service-card__copy">
            <small>ГИЛЬДИЯ АВАНТЮРИСТОВ</small>
            <strong>Представительство Гильдии</strong>
            <p>Селия выдаёт контракты и отмечает новые угрозы.</p>
          </div>
          <UIButton data-open-adventurer-guild :disabled="isTravelling" @click="guildOpen = true">Войти</UIButton>
        </article>

        <article class="service-card service-card--rest" data-town-service="rest">
          <img class="service-card__portrait" :src="gameArt.npc.innkeeper" alt="Хозяйка постоялого двора" />
          <div class="service-card__copy">
            <small>ПОСТОЯЛЫЙ ДВОР</small>
            <strong>Отдых на площади</strong>
            <p>Безопасная зона восстанавливает здоровье героя.</p>
          </div>
          <span class="service-card__status">Активно</span>
        </article>
      </div>
    </section>

    <MerchantShop :open="merchantOpen" @close="merchantOpen = false" />
    <AdventurerGuildBoard
      :open="guildOpen"
      :location-id="currentLocationId ?? ''"
      @close="guildOpen = false"
    />
    <UIModal :open="afkOpen" title="AFK-фарм" @close="afkOpen = false">
      <div class="afk-modal" data-afk-farm-modal>
        <p>Герой останется в этой области и получит безопасный фоновый бонус.</p>
        <div class="afk-duration" aria-label="Длительность AFK-фарма">
          <UIButton
            v-for="duration in [15, 60, 240]"
            :key="duration"
            :variant="afkDurationMinutes === duration ? 'primary' : 'secondary'"
            :disabled="afkPreviewLoading || session.mutationPending"
            @click="selectAfkDuration(duration)"
          >{{ duration < 60 ? `${duration} мин` : `${duration / 60} ч` }}</UIButton>
        </div>
        <div v-if="afkPreview" class="afk-preview">
          <span>Примерно {{ afkPreview.kills }} побед</span>
          <strong>+{{ afkPreview.estimatedXp }} опыта · +{{ afkPreview.estimatedGold }} золота</strong>
          <small>{{ afkPreview.potentialLootRolls }} возможн. лут-роллов · риск: {{ afkPreview.estimatedIncomingDamage }} урона</small>
        </div>
        <p v-else-if="afkPreviewLoading">Рассчитываем маршрут фарма…</p>
        <p v-else-if="session.errorCode">Не удалось получить расчёт. Проверьте условия локации.</p>
      </div>
      <template #actions>
        <UIButton variant="secondary" @click="afkOpen = false">Отмена</UIButton>
        <UIButton :loading="session.mutationPending" :disabled="!afkPreview" @click="startAfkFarm">Начать</UIButton>
      </template>
    </UIModal>
  </section>
</template>

<style scoped>
.world {
  display: grid;
  width: min(100%, var(--ui-content-width));
  min-height: 100%;
  margin-inline: auto;
  gap: var(--ui-space-3);
  padding: var(--ui-space-3) var(--ui-space-4) var(--ui-space-7);
}

.world-stories,
.field-guild,
.location-contracts,
.town-services {
  display: grid;
  gap: var(--ui-space-3);
}

.afk-status,
.afk-modal,
.afk-preview {
  display: grid;
  gap: var(--ui-space-2);
}

.afk-status {
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: center;
}

.afk-status small,
.afk-status p,
.afk-preview small {
  margin: 0;
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
}

.afk-status strong,
.afk-preview strong {
  color: var(--ui-color-text-primary);
}

.afk-duration {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: var(--ui-space-2);
}

.afk-preview {
  padding: var(--ui-space-3);
  border: 1px solid rgb(102 141 225 / 35%);
  border-radius: var(--ui-radius-md);
  background: rgb(61 81 137 / 14%);
}

.world-stories + .field-guild,
.field-guild + .location-contracts,
.world-stories + .town-services,
.location-contracts + .town-services {
  padding-top: var(--ui-space-3);
  border-top: 1px solid var(--ui-color-border);
}

.story-list {
  display: grid;
  gap: var(--ui-space-2);
}

.story-card {
  display: grid;
  grid-template-columns: 2.75rem minmax(0, 1fr);
  gap: var(--ui-space-2);
  padding: var(--ui-space-3);
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: var(--ui-color-surface-1);
}

.story-card__icon {
  display: grid;
  width: 2.75rem;
  height: 2.75rem;
  place-items: center;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: var(--ui-radius-md);
  color: var(--ui-color-primary);
}

.story-card__copy {
  display: grid;
  min-width: 0;
  gap: 3px;
}

.story-card__copy small,
.story-card__reward {
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
}

.story-card__copy strong {
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-sm);
}

.story-card__copy p {
  margin: 0;
  color: var(--ui-color-text-secondary);
  font-size: var(--ui-font-size-sm);
  line-height: 1.45;
}

.story-card :deep(.ui-button),
.activity-card :deep(.ui-button),
.contract-card :deep(.ui-button) {
  grid-column: 1 / -1;
  width: 100%;
}

.scene {
  position: relative;
  order: -2;
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
  font-size: var(--ui-font-size-xs);
}

.reward-card ul {
  margin: var(--ui-space-2) 0 0;
  padding-left: 1.2rem;
}

.loot-roll-card {
  display: grid;
  gap: var(--ui-space-3);
  border-color: rgb(219 164 83 / 42%);
  background: linear-gradient(135deg, rgb(72 48 21 / 72%), rgb(12 13 20 / 96%));
}

.loot-roll-card .reward-card__heading small {
  color: #e4bc76;
}

.loot-roll-row {
  display: grid;
  gap: 8px;
  padding-top: 8px;
  border-top: 1px solid rgb(255 255 255 / 7%);
}

.loot-roll-row > div:first-child {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 8px;
}

.loot-roll-row small {
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
}

.loot-roll-actions {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 5px;
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
  font-size: var(--ui-font-size-xs);
  line-height: 1.4;
}

.contract-card__reward {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 8px;
  margin-top: 4px;
  font-size: var(--ui-font-size-xs);
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
  font-size: var(--ui-font-size-xs);
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
  font-size: var(--ui-font-size-xs);
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
  font-size: var(--ui-font-size-xs);
  font-weight: 700;
}

.town-services__hint {
  margin: 0;
  padding: 8px 4px 2px;
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
}

/* Sections read as one location hub; individual controls carry the hierarchy. */
.location-activities,
.location-contracts,
.town-services,
.location-routes {
  overflow: visible;
  border: 0;
  border-radius: 0;
  background: transparent;
  box-shadow: none;
}

.section-heading {
  padding-inline: 2px;
  background: transparent;
}

.service-grid {
  gap: 6px;
  background: transparent;
}

.service-card {
  min-height: 10.5rem;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: linear-gradient(150deg, rgb(26 32 39 / 96%), rgb(7 11 16 / 99%));
}

.service-card--training {
  border-color: rgb(182 161 236 / 30%);
}

.service-card--merchant,
.service-card--guild {
  border-color: rgb(205 177 113 / 32%);
}

.service-card--rest {
  border-color: rgb(79 185 150 / 28%);
}

.service-card__portrait {
  width: 3.2rem;
  height: 3.2rem;
  border-color: rgb(205 177 113 / 42%);
  border-radius: var(--ui-radius-sm);
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
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .service-card,
  .service-card--rest {
    grid-column: auto;
    grid-template-columns: 1fr;
    min-height: 10.5rem;
    align-items: start;
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

  .service-card__portrait,
  .service-card__icon {
    width: 2.65rem;
    height: 2.65rem;
  }

}
</style>
