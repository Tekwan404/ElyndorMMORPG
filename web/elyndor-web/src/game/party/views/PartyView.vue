<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { classLabel } from '@/game/character/characterPresentation'
import { socialErrorMessage } from '@/game/social/socialPresentation'

import { usePartyStore } from '@/game/party/partyStore'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UIModal, UIPanel } from '@/ui/components'

const props = withDefaults(defineProps<{ embedded?: boolean }>(), { embedded: false })
const emit = defineEmits<{ 'open-world': [] }>()

const party = usePartyStore()
const session = useGameSessionStore()
const currentCharacterId = computed(() => session.snapshot?.character?.id ?? '')
const isLeader = computed(() => party.snapshot?.leaderCharacterId === currentCharacterId.value)
const currentMember = computed(() => party.snapshot?.members.find(
  (member) => member.characterId === currentCharacterId.value,
) ?? null)
const leaderMember = computed(() => party.snapshot?.members.find(
  (member) => member.characterId === party.snapshot?.leaderCharacterId,
) ?? null)
const canFollowLeader = computed(() => {
  const leader = leaderMember.value
  if (!leader?.locationId || leader.characterId === currentCharacterId.value) return false
  if (party.snapshot?.activeDungeonRunId) return false
  return leader.locationId !== currentMember.value?.locationId
})

type PendingAction =
  | { type: 'disband' }
  | { type: 'leave' }
  | { type: 'kick'; characterId: string }
const pendingAction = ref<PendingAction | null>(null)
let refreshTimer: ReturnType<typeof setInterval> | null = null

const confirmationTitle = computed(() => {
  if (pendingAction.value?.type === 'disband') return 'Распустить группу?'
  if (pendingAction.value?.type === 'kick') return 'Исключить игрока?'
  return 'Покинуть группу?'
})
const confirmationMessage = computed(() => {
  if (pendingAction.value?.type === 'disband') return 'Группа исчезнет для всех участников. Это действие нельзя отменить.'
  if (pendingAction.value?.type === 'kick') return 'Игрок больше не сможет участвовать в текущем составе группы.'
  return 'Ты выйдешь из текущей группы и потеряешь место в её составе.'
})

onMounted(() => {
  void party.refresh()
  refreshTimer = setInterval(() => {
    void party.refresh(true)
  }, 5_000)
})

onUnmounted(() => {
  if (refreshTimer !== null) clearInterval(refreshTimer)
})

function locationLabel(locationId?: string | null): string {
  if (!locationId) return 'локация неизвестна'
  const world = session.snapshot?.world
  if (world?.currentLocation.id === locationId) return world.currentLocation.displayName
  const known = world?.outgoingTransitions.find((location) => location.id === locationId)
  if (known) return known.displayName
  return locationId
    .toLowerCase()
    .split('_')
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ')
}

async function followLeader(): Promise<void> {
  const targetLocationId = leaderMember.value?.locationId
  if (!targetLocationId || !canFollowLeader.value) return
  party.clearLeaderLocationChange()
  await session.travel(targetLocationId)
  await party.refresh()
}

async function kickMember(characterId: string): Promise<void> {
  if (characterId !== currentCharacterId.value) await party.kick(characterId)
}

async function transferLeadership(characterId: string): Promise<void> {
  if (characterId !== currentCharacterId.value) await party.transferLeadership(characterId)
}

async function disbandParty(): Promise<void> {
  if (isLeader.value) await party.disband()
}

function requestConfirmation(action: PendingAction): void {
  pendingAction.value = action
}

async function confirmPendingAction(): Promise<void> {
  const action = pendingAction.value
  if (!action) return
  pendingAction.value = null
  if (action.type === 'disband' && isLeader.value) await disbandParty()
  if (action.type === 'leave') await party.leave()
  if (action.type === 'kick') await kickMember(action.characterId)
}
</script>

<template>
  <div class="party-view" :class="{ 'party-view--embedded': props.embedded }">
    <header class="party-view__header">
      <div>
        <small>СОВМЕСТНЫЙ ПУТЬ</small>
        <h1>Группа</h1>
      </div>
      <div class="party-view__header-actions">
        <UIButton data-party-open-dungeons variant="secondary" @click="emit('open-world')">Подземелья</UIButton>
        <span>{{ party.snapshot?.members.length ?? 0 }} / 5</span>
      </div>
    </header>

    <p v-if="party.errorCode" class="error-state" role="alert">{{ socialErrorMessage(party.errorCode) }}</p>

    <UIPanel
      v-if="party.leaderLocationChange && party.leaderLocationChange.leaderCharacterId !== currentCharacterId"
      title="Лидер сменил локацию"
    >
      <div class="leader-move">
        <p>
          <strong>{{ party.leaderLocationChange.leaderName }}</strong>
          перешёл в {{ locationLabel(party.leaderLocationChange.locationId) }}.
        </p>
        <div class="actions">
          <UIButton v-if="canFollowLeader" @click="followLeader">Следовать</UIButton>
          <UIButton variant="ghost" @click="party.clearLeaderLocationChange">Скрыть</UIButton>
        </div>
      </div>
    </UIPanel>

    <UIPanel v-if="party.invites.length" title="Приглашения">
      <article v-for="invite in party.invites" :key="invite.id" class="invite-row">
        <div><strong>Приглашение в группу</strong><small>от {{ invite.inviterName ?? 'героя' }}</small></div>
        <div class="actions">
          <UIButton @click="party.acceptInvite(invite.id)">Войти</UIButton>
          <UIButton variant="secondary" @click="party.declineInvite(invite.id)">Нет</UIButton>
        </div>
      </article>
    </UIPanel>

    <UIPanel v-if="party.snapshot" :title="`Группа · ${party.snapshot.members.length}/5`">
      <UIButton v-if="isLeader" variant="danger" data-party-disband @click="requestConfirmation({ type: 'disband' })">Распустить группу</UIButton>
      <article v-for="member in party.snapshot.members" :key="member.characterId" class="member-row">
        <div v-if="isLeader && member.characterId !== currentCharacterId" class="member-actions">
          <UIButton variant="secondary" @click="transferLeadership(member.characterId)">Лидер</UIButton>
          <UIButton variant="danger" :data-party-kick="member.characterId" @click="requestConfirmation({ type: 'kick', characterId: member.characterId })">Исключить</UIButton>
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
          <span v-if="member.characterId === currentCharacterId && member.isLeader" class="leader-label">лидер</span>
          <UIButton
            v-if="member.isLeader && member.characterId !== currentCharacterId && canFollowLeader"
            variant="secondary"
            @click="followLeader"
          >Следовать</UIButton>
        </div>
      </article>
      <UIButton variant="danger" data-party-leave @click="requestConfirmation({ type: 'leave' })">Покинуть группу</UIButton>
    </UIPanel>

    <UIPanel v-else title="Группа">
      <p class="empty-state">Создай группу, чтобы пригласить друзей в совместные походы.</p>
      <UIButton @click="party.create">Создать группу</UIButton>
    </UIPanel>
  </div>
  <UIModal :open="pendingAction !== null" :title="confirmationTitle" @close="pendingAction = null">
    <p class="party-confirmation">{{ confirmationMessage }}</p>
    <template #actions>
      <UIButton variant="ghost" @click="pendingAction = null">Отмена</UIButton>
      <UIButton variant="danger" data-party-confirm @click="confirmPendingAction">Подтвердить</UIButton>
    </template>
  </UIModal>
</template>

<style scoped>
.party-view { display: grid; gap: 12px; padding: 14px; }
.party-view--embedded { padding: 0; }
.party-view__header { display: flex; align-items: end; justify-content: space-between; gap: 10px; }
.party-view__header small { color: var(--ui-color-primary); font-size: .55rem; letter-spacing: .14em; }
h1 { margin: 2px 0 0; font-family: var(--ui-font-display); font-size: 1.35rem; }
.party-view__header-actions { display: flex; align-items: center; gap: 8px; }
.party-view__header-actions > span { color: var(--ui-color-text-muted); font-size: .68rem; white-space: nowrap; }
.party-view__header-actions :deep(.ui-button) { min-height: 2.15rem; padding-inline: .62rem; font-size: .65rem; }
.member-row, .invite-row { display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 9px 0; border-bottom: 1px solid rgb(255 255 255 / 7%); }
.member-row > .member-copy, .invite-row div:first-child { display: grid; gap: 3px; }
.member-row small, .invite-row small, .empty-state { color: var(--ui-color-text-muted); font-size: .68rem; }
.member-location { color: var(--ui-color-text-secondary) !important; }
.member-state { display: grid; justify-items: end; gap: 5px; }
.member-state :deep(.ui-button) { min-height: 1.85rem; padding-inline: .5rem; font-size: .6rem; }
.error-state { color: var(--ui-color-danger, #ff8d8d); font-size: .72rem; }
.leader-label { color: var(--ui-color-primary); font-size: .62rem; }
.actions, .member-actions { display: flex; gap: 6px; align-items: center; }
.leader-move { display: grid; gap: 10px; }
.leader-move p { margin: 0; color: var(--ui-color-text-secondary); font-size: .72rem; line-height: 1.45; }
.party-confirmation { margin: 0; color: var(--ui-color-text-secondary); line-height: 1.5; }
</style>
