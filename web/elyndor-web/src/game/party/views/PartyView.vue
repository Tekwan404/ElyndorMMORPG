<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { classLabel } from '@/game/character/characterPresentation'
import { useDungeonStore } from '@/game/party/dungeonStore'
import { usePartyStore } from '@/game/party/partyStore'
import { socialErrorMessage } from '@/game/social/socialPresentation'
import { locationPresentation } from '@/game/world/locationPresentation'
import { connectLiveState, subscribeLiveState } from '@/realtime/liveState'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UILoadingState, UIModal, UIPanel, UIToast } from '@/ui/components'

const props = withDefaults(defineProps<{ embedded?: boolean }>(), { embedded: false })
const emit = defineEmits<{ 'open-world': [] }>()

const party = usePartyStore()
const dungeon = useDungeonStore()
const session = useGameSessionStore()
const currentCharacterId = computed(() => session.snapshot?.character?.id ?? '')
const currentLocationId = computed(() => session.snapshot?.world?.currentLocation.id ?? '')
const isLeader = computed(() => party.snapshot?.leaderCharacterId === currentCharacterId.value)
const currentMember = computed(
  () =>
    party.snapshot?.members.find((member) => member.characterId === currentCharacterId.value) ??
    null,
)
const leaderMember = computed(
  () =>
    party.snapshot?.members.find(
      (member) => member.characterId === party.snapshot?.leaderCharacterId,
    ) ?? null,
)
const currentDungeonMember = computed(
  () =>
    dungeon.current?.members.find((member) => member.characterId === currentCharacterId.value) ??
    null,
)
const currentDungeonRun = computed(() =>
  currentDungeonMember.value?.state === 'Active' ? dungeon.current : null,
)
const currentDungeonPreview = computed(
  () =>
    dungeon.previews.find((preview) => preview.id === currentDungeonRun.value?.dungeonId) ?? null,
)
const currentDungeonEncounter = computed(
  () =>
    currentDungeonRun.value?.encounters.find(
      (encounter) => encounter.encounterIndex === currentDungeonRun.value?.currentEncounterIndex,
    ) ?? null,
)
const currentDungeonEntryLocationId = computed(
  () => currentDungeonPreview.value?.entryLocationId ?? currentDungeonRun.value?.dungeonId ?? '',
)
const dungeonStageNumber = computed(() => {
  const run = currentDungeonRun.value
  if (!run) return 0
  if (run.state === 'Completed') return run.encounterCount
  return Math.min(run.currentEncounterIndex + 1, run.encounterCount)
})
const dungeonStateLabel = computed(() => {
  const run = currentDungeonRun.value
  if (!run) return ''
  if (run.state === 'Completed') return 'Завершено'
  if (run.state === 'Abandoned') return 'Забег прекращён'
  if (currentDungeonEncounter.value?.state === 'Active') return 'В бою'
  return 'Между боями'
})
const canReturnToDungeonRun = computed(() =>
  Boolean(
    currentDungeonRun.value?.state === 'Active' &&
    currentDungeonEntryLocationId.value &&
    currentLocationId.value !== currentDungeonEntryLocationId.value &&
    currentDungeonEncounter.value?.state !== 'Active',
  ),
)
const canFollowLeader = computed(() => {
  const leader = leaderMember.value
  if (!leader?.locationId || leader.characterId === currentCharacterId.value) return false
  if (party.snapshot?.activeDungeonRunId) return false
  return leader.locationId !== currentMember.value?.locationId
})

type PendingAction = { type: 'disband' } | { type: 'leave' } | { type: 'kick'; characterId: string }
const pendingAction = ref<PendingAction | null>(null)
const pending = ref<string | null>(null)
const feedback = ref<string | null>(null)
const actionError = ref<string | null>(null)
const hasLoaded = ref(false)
let refreshTimer: ReturnType<typeof setInterval> | null = null
let unsubscribeParty: (() => void) | null = null

const confirmationTitle = computed(() => {
  if (pendingAction.value?.type === 'disband') return 'Распустить группу?'
  if (pendingAction.value?.type === 'kick') return 'Исключить игрока?'
  return 'Покинуть группу?'
})
const confirmationMessage = computed(() => {
  if (pendingAction.value?.type === 'disband') return 'Группа будет распущена для всех участников.'
  if (pendingAction.value?.type === 'kick') return 'Игрок будет исключён из группы.'
  return 'Ты выйдешь из текущей группы.'
})

onMounted(() => {
  unsubscribeParty = subscribeLiveState('party', () => {
    if (!pending.value && !pendingAction.value) {
      void Promise.all([party.refresh(true), dungeon.refresh()])
    }
  })
  void connectLiveState().catch(() => {})
  void refresh()
  refreshTimer = setInterval(() => {
    if (!pending.value && !pendingAction.value) void party.refresh(true)
  }, 30_000)
})

async function refresh(): Promise<void> {
  await Promise.all([party.refresh(), dungeon.refresh()])
  hasLoaded.value = true
}

async function act(key: string, action: () => Promise<void>, message: string): Promise<boolean> {
  if (pending.value) return false
  pending.value = key
  feedback.value = null
  actionError.value = null
  try {
    await action()
    const code =
      key === 'return' ? dungeon.errorCode : key === 'follow' ? session.errorCode : party.errorCode
    if (code) {
      actionError.value = socialErrorMessage(code)
      return false
    }
    feedback.value = message
    return true
  } catch {
    actionError.value = 'Не удалось выполнить действие. Попробуйте ещё раз.'
    return false
  } finally {
    pending.value = null
  }
}

onUnmounted(() => {
  unsubscribeParty?.()
  if (refreshTimer !== null) clearInterval(refreshTimer)
})

function locationLabel(locationId?: string | null): string {
  if (!locationId) return 'Локация неизвестна'
  const world = session.snapshot?.world
  if (world?.currentLocation.id === locationId) {
    return locationPresentation(locationId, world.currentLocation.displayName).label
  }
  const known = world?.outgoingTransitions.find((location) => location.id === locationId)
  if (known) return locationPresentation(locationId, known.displayName).label
  return locationPresentation(locationId).label
}

async function returnToDungeonRun(): Promise<void> {
  const run = currentDungeonRun.value
  if (!run || !canReturnToDungeonRun.value) return
  await act('return', () => dungeon.returnToRun(run.runId), 'Вы вернулись в забег.')
}

async function followLeader(): Promise<void> {
  const targetLocationId = leaderMember.value?.locationId
  if (!targetLocationId || !canFollowLeader.value) return
  const followed = await act('follow', () => session.travel(targetLocationId), 'Переход выполнен.')
  if (followed) {
    party.clearLeaderLocationChange()
    await party.refresh()
  }
}

async function kickMember(characterId: string): Promise<void> {
  if (characterId !== currentCharacterId.value) await party.kick(characterId)
}

async function transferLeadership(characterId: string): Promise<void> {
  if (characterId !== currentCharacterId.value)
    await act(
      `transfer:${characterId}`,
      () => party.transferLeadership(characterId),
      'Лидерство передано.',
    )
}

async function disbandParty(): Promise<void> {
  if (isLeader.value) await party.disband()
}

function requestConfirmation(action: PendingAction): void {
  if (pending.value) return
  actionError.value = null
  pendingAction.value = action
}

async function confirmPendingAction(): Promise<void> {
  const action = pendingAction.value
  if (!action || pending.value) return
  const success = await act(
    'confirm',
    async () => {
      if (action.type === 'disband') {
        if (!isLeader.value) throw new Error('party_not_leader')
        await disbandParty()
      }
      if (action.type === 'leave') await party.leave()
      if (action.type === 'kick') await kickMember(action.characterId)
    },
    'Действие с группой выполнено.',
  )
  if (success) pendingAction.value = null
}
</script>

<template>
  <div class="party-view" :class="{ 'party-view--embedded': props.embedded }">
    <header class="party-view__header">
      <div>
        <small>СОВМЕСТНАЯ ИГРА</small>
        <h1>Группа</h1>
      </div>
      <div class="party-view__header-actions">
        <UIButton data-party-open-dungeons variant="secondary" @click="emit('open-world')"
          >Подземелья</UIButton
        >
        <span>{{ party.snapshot?.members.length ?? 0 }} / 5</span>
      </div>
    </header>

    <UIToast v-if="actionError && !pendingAction" tone="danger">{{ actionError }}</UIToast>
    <UIToast v-else-if="party.errorCode || dungeon.errorCode" tone="danger">
      {{ socialErrorMessage(party.errorCode || dungeon.errorCode || '') }}
      <UIButton variant="secondary" :loading="party.loading || dungeon.loading" @click="refresh"
        >Повторить загрузку</UIButton
      >
    </UIToast>
    <UIToast v-if="feedback" tone="success">{{ feedback }}</UIToast>
    <UILoadingState v-if="!hasLoaded && !party.snapshot" state="loading" title="Загружаем группу" />

    <UIPanel v-if="currentDungeonRun" data-party-dungeon-run>
      <template #title>Текущий забег</template>
      <div class="dungeon-run-summary">
        <div>
          <strong>{{ currentDungeonRun.displayName }}</strong>
          <small>
            Этап {{ dungeonStageNumber }} / {{ currentDungeonRun.encounterCount }} ·
            {{ dungeonStateLabel }}
          </small>
        </div>
        <p v-if="canReturnToDungeonRun">Ты вне подземелья. Прогресс забега сохранён.</p>
        <UIButton
          v-if="canReturnToDungeonRun"
          data-party-dungeon-return
          :loading="pending === 'return'"
          :disabled="pending !== null"
          @click="returnToDungeonRun"
          >Вернуться в забег</UIButton
        >
      </div>
    </UIPanel>

    <UIPanel
      v-if="
        party.leaderLocationChange &&
        party.leaderLocationChange.leaderCharacterId !== currentCharacterId
      "
    >
      <template #title>Лидер сменил локацию</template>
      <div class="leader-move">
        <p>
          <strong>{{ party.leaderLocationChange.leaderName }}</strong>
          перешёл в {{ locationLabel(party.leaderLocationChange.locationId) }}.
        </p>
        <div class="actions">
          <UIButton
            v-if="canFollowLeader"
            :loading="pending === 'follow'"
            :disabled="pending !== null"
            @click="followLeader"
            >Следовать</UIButton
          >
          <UIButton variant="ghost" @click="party.clearLeaderLocationChange">Скрыть</UIButton>
        </div>
      </div>
    </UIPanel>

    <UIPanel v-if="party.invites.length">
      <template #title>Приглашения</template>
      <article v-for="invite in party.invites" :key="invite.id" class="invite-row">
        <div>
          <strong>Приглашение в группу</strong><small>от {{ invite.inviterName ?? 'героя' }}</small>
        </div>
        <div class="actions">
          <UIButton
            :loading="pending === `accept:${invite.id}`"
            :disabled="pending !== null"
            @click="
              act(
                `accept:${invite.id}`,
                () => party.acceptInvite(invite.id),
                'Приглашение принято.',
              )
            "
            >Принять</UIButton
          >
          <UIButton
            variant="secondary"
            :loading="pending === `decline:${invite.id}`"
            :disabled="pending !== null"
            @click="
              act(
                `decline:${invite.id}`,
                () => party.declineInvite(invite.id),
                'Приглашение отклонено.',
              )
            "
            >Отклонить</UIButton
          >
        </div>
      </article>
    </UIPanel>

    <UIPanel v-if="party.snapshot">
      <template #title>Группа · {{ party.snapshot.members.length }}/5</template>
      <UIButton
        v-if="isLeader"
        variant="danger"
        :disabled="pending !== null"
        data-party-disband
        @click="requestConfirmation({ type: 'disband' })"
        >Распустить группу</UIButton
      >
      <article
        v-for="member in party.snapshot.members"
        :key="member.characterId"
        class="member-row"
      >
        <div v-if="isLeader && member.characterId !== currentCharacterId" class="member-actions">
          <UIButton
            variant="secondary"
            :loading="pending === `transfer:${member.characterId}`"
            :disabled="pending !== null"
            @click="transferLeadership(member.characterId)"
            >Передать лидерство</UIButton
          >
          <UIButton
            variant="danger"
            :disabled="pending !== null"
            :data-party-kick="member.characterId"
            @click="requestConfirmation({ type: 'kick', characterId: member.characterId })"
            >Исключить</UIButton
          >
        </div>
        <div class="member-copy">
          <strong>{{ member.name }} <span v-if="member.isLeader">★</span></strong>
          <small>ур. {{ member.level }} · {{ classLabel(member.classId) }}</small>
          <small class="member-location">
            {{ locationLabel(member.locationId) }}
            <span v-if="member.activeDungeonRunId"> · в забеге</span>
          </small>
        </div>
        <div class="member-state">
          <span
            v-if="member.characterId === currentCharacterId && member.isLeader"
            class="leader-label"
            >лидер</span
          >
          <UIButton
            v-if="member.isLeader && member.characterId !== currentCharacterId && canFollowLeader"
            variant="secondary"
            :loading="pending === 'follow'"
            :disabled="pending !== null"
            @click="followLeader"
            >Следовать</UIButton
          >
        </div>
      </article>
      <UIButton
        variant="danger"
        :disabled="pending !== null"
        data-party-leave
        @click="requestConfirmation({ type: 'leave' })"
        >Покинуть группу</UIButton
      >
    </UIPanel>

    <UIPanel v-else-if="hasLoaded && !party.errorCode">
      <template #title>Группа</template>
      <UILoadingState
        state="empty"
        title="Вы пока без группы"
        message="Создайте группу для совместных походов."
      />
      <UIButton
        :loading="pending === 'create'"
        :disabled="pending !== null"
        @click="act('create', party.create, 'Группа создана.')"
        >Создать группу</UIButton
      >
    </UIPanel>
  </div>
  <UIModal
    :open="pendingAction !== null"
    :title="confirmationTitle"
    :busy="pending !== null"
    @close="pendingAction = null"
  >
    <p class="party-confirmation">{{ confirmationMessage }}</p>
    <UIToast v-if="actionError" tone="danger">{{ actionError }}</UIToast>
    <template #actions>
      <UIButton variant="ghost" :disabled="pending !== null" @click="pendingAction = null"
        >Отмена</UIButton
      >
      <UIButton
        variant="danger"
        :loading="pending === 'confirm'"
        :disabled="pending !== null"
        data-party-confirm
        @click="confirmPendingAction"
        >Подтвердить</UIButton
      >
    </template>
  </UIModal>
</template>

<style scoped>
.party-view {
  display: grid;
  gap: 12px;
  padding: 14px;
}
.party-view--embedded {
  padding: 0;
}
.party-view__header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: 10px;
}
.party-view__header small {
  color: var(--ui-color-primary);
  font-size: 0.55rem;
  letter-spacing: 0.14em;
}
h1 {
  margin: 2px 0 0;
  font-family: var(--ui-font-display);
  font-size: 1.35rem;
}
.party-view__header-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}
.party-view__header-actions > span {
  color: var(--ui-color-text-muted);
  font-size: 0.68rem;
  white-space: nowrap;
}
.party-view__header-actions :deep(.ui-button) {
  padding-inline: 0.62rem;
}
.dungeon-run-summary {
  display: grid;
  gap: 9px;
}
.dungeon-run-summary > div {
  display: grid;
  gap: 3px;
}
.dungeon-run-summary small {
  color: var(--ui-color-text-muted);
  font-size: 0.68rem;
}
.dungeon-run-summary p {
  margin: 0;
  color: var(--ui-color-text-secondary);
  font-size: 0.72rem;
  line-height: 1.45;
}
.dungeon-run-summary :deep(.ui-button) {
  justify-self: start;
}
.member-row,
.invite-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 9px 0;
  border-bottom: 1px solid rgb(255 255 255 / 7%);
}
.member-row > .member-copy,
.invite-row div:first-child {
  display: grid;
  gap: 3px;
}
.member-row small,
.invite-row small {
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-sm);
}
.member-location {
  color: var(--ui-color-text-secondary) !important;
}
.member-state {
  display: grid;
  justify-items: end;
  gap: 5px;
}
.member-state :deep(.ui-button) {
  padding-inline: 0.5rem;
}
.leader-label {
  color: var(--ui-color-primary);
  font-size: 0.62rem;
}
.actions,
.member-actions {
  display: flex;
  gap: 6px;
  align-items: center;
}
.leader-move {
  display: grid;
  gap: 10px;
}
.leader-move p {
  margin: 0;
  color: var(--ui-color-text-secondary);
  font-size: 0.72rem;
  line-height: 1.45;
}
.party-confirmation {
  margin: 0;
  color: var(--ui-color-text-secondary);
  line-height: 1.5;
}
@media (max-width: 480px) {
  .party-view__header {
    flex-wrap: wrap;
  }
  .member-row,
  .invite-row {
    flex-wrap: wrap;
  }
  .member-copy {
    min-width: 0;
    overflow-wrap: anywhere;
  }
  .member-actions {
    order: 3;
    width: 100%;
    flex-wrap: wrap;
  }
  .actions {
    flex-wrap: wrap;
  }
}
</style>
