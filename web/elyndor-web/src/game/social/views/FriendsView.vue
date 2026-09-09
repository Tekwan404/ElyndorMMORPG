<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { usePartyStore } from '@/game/party/partyStore'
import { classLabel } from '@/game/character/characterPresentation'
import { socialErrorMessage } from '@/game/social/socialPresentation'
import { useSocialStore } from '@/game/social/socialStore'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UIPanel } from '@/ui/components'

const social = useSocialStore()
const party = usePartyStore()
const session = useGameSessionStore()
const query = ref('')
const searching = ref(false)
const currentCharacterId = computed(() => session.snapshot?.character?.id ?? '')
const isPartyLeader = computed(() => party.snapshot?.leaderCharacterId === currentCharacterId.value)
const friendIds = computed(() => new Set(social.friends.map(friend => friend.characterId)))
const inviting = ref<string | null>(null)
function canInvite(characterId: string): boolean {
  return isPartyLeader.value && (party.snapshot?.members.length ?? 5) < 5
    && !party.snapshot?.members.some(member => member.characterId === characterId)
}

async function search(): Promise<void> {
  searching.value = true
  await social.search(query.value)
  searching.value = false
}

async function addFriend(characterId: string): Promise<void> {
  await social.sendRequest(characterId)
  await search()
}

async function cancelRequest(requestId: string): Promise<void> {
  await social.cancelRequest(requestId)
  await search()
}

async function decideSearchRequest(requestId: string, accept: boolean): Promise<void> {
  await social.decideRequestById(requestId, accept)
  await search()
}

async function inviteToParty(characterId: string): Promise<void> {
  if (inviting.value || !canInvite(characterId)) return
  inviting.value = characterId
  try {
    await party.invite(characterId, friendIds.value.has(characterId) ? 'Friend' : 'Direct')
  } finally {
    inviting.value = null
  }
}

onMounted(() => {
  void Promise.all([social.refresh(), party.refresh()])
})
</script>

<template>
  <div class="social-view">
    <header class="social-view__header">
      <div>
        <small>СОЦИАЛЬНЫЙ КРУГ</small>
        <h1>Друзья</h1>
      </div>
      <span>{{ social.friends.length }} друзей</span>
    </header>

    <p v-if="social.errorCode || party.errorCode" class="error-state" role="alert">{{ socialErrorMessage(social.errorCode || party.errorCode!) }}</p>

    <UIPanel title="Найти игрока">
      <form class="search-form" @submit.prevent="search">
        <input v-model="query" placeholder="Имя, @username или ELY-код" aria-label="Поиск игрока" />
        <UIButton type="submit" :disabled="searching || query.trim().length < 2">Найти</UIButton>
      </form>
      <div v-if="social.searchResults.length" class="result-list">
        <article v-for="player in social.searchResults" :key="player.characterId" class="player-row">
          <div>
            <strong>{{ player.name }}</strong>
            <small>ур. {{ player.level }} · {{ classLabel(player.classId) }} · {{ player.publicCode }}</small>
          </div>
          <div class="actions">
            <UIButton
              v-if="player.relationship === 'NONE'"
              variant="secondary"
              :disabled="social.mutationPending"
              @click="addFriend(player.characterId)"
            >Добавить</UIButton>
            <span v-else-if="player.relationship === 'FRIEND'" class="relationship-state">Уже в друзьях</span>
            <UIButton
              v-else-if="player.relationship === 'OUTGOING_REQUEST' && player.pendingRequestId"
              variant="secondary"
              :disabled="social.mutationPending"
              @click="cancelRequest(player.pendingRequestId)"
            >Отменить заявку</UIButton>
            <template v-else-if="player.relationship === 'INCOMING_REQUEST' && player.pendingRequestId">
              <UIButton
                :disabled="social.mutationPending"
                @click="decideSearchRequest(player.pendingRequestId, true)"
              >Принять</UIButton>
              <UIButton
                variant="secondary"
                :disabled="social.mutationPending"
                @click="decideSearchRequest(player.pendingRequestId, false)"
              >Отклонить</UIButton>
            </template>
            <UIButton
              v-if="canInvite(player.characterId)"
              data-party-invite
              :disabled="inviting !== null"
              @click="inviteToParty(player.characterId)"
            >В группу</UIButton>
          </div>
        </article>
      </div>
    </UIPanel>

    <UIPanel v-if="social.incomingRequests.length" title="Входящие заявки">
      <article v-for="request in social.incomingRequests" :key="request.id" class="player-row">
        <small>Заявка от {{ request.requesterName ?? 'героя' }}</small>
        <div class="actions">
          <UIButton :disabled="social.mutationPending" @click="social.decideRequest(request, true)">Принять</UIButton>
          <UIButton variant="secondary" :disabled="social.mutationPending" @click="social.decideRequest(request, false)">Отклонить</UIButton>
        </div>
      </article>
    </UIPanel>

    <UIPanel v-if="social.outgoingRequests.length" title="Исходящие заявки">
      <article v-for="request in social.outgoingRequests" :key="request.id" class="player-row">
        <small>Заявка: {{ request.targetName ?? 'герой' }}</small>
        <UIButton
          variant="secondary"
          :disabled="social.mutationPending"
          @click="social.cancelRequest(request.id)"
        >Отменить</UIButton>
      </article>
    </UIPanel>

    <UIPanel title="Мои друзья">
      <template v-if="!party.snapshot">
        <p class="empty-state">Создайте группу, чтобы приглашать друзей прямо отсюда.</p>
        <UIButton :disabled="party.loading" @click="party.create">Создать группу</UIButton>
      </template>
      <p v-if="!social.friends.length" class="empty-state">Пока здесь тихо. Найди первого товарища.</p>
      <article v-for="friend in social.friends" :key="friend.characterId" class="player-row">
        <div>
          <strong>{{ friend.name }}</strong>
          <small>ур. {{ friend.level }} · {{ classLabel(friend.classId) }}</small>
        </div>
        <div class="actions">
          <UIButton v-if="canInvite(friend.characterId)" data-party-invite :disabled="inviting !== null" @click="inviteToParty(friend.characterId)">В группу</UIButton>
          <UIButton
            variant="secondary"
            data-remove-friend
            :disabled="social.mutationPending"
            @click="social.removeFriend(friend.characterId)"
          >Удалить</UIButton>
        </div>
      </article>
    </UIPanel>
  </div>
</template>

<style scoped>
.social-view { display: grid; gap: 12px; padding: 14px; }
.social-view__header { display: flex; align-items: end; justify-content: space-between; gap: 12px; }
.social-view__header small { color: var(--ui-color-primary); font-size: .55rem; letter-spacing: .14em; }
h1 { margin: 2px 0 0; font-family: var(--ui-font-display); font-size: 1.35rem; }
.social-view__header > span, .online-state { color: var(--ui-color-text-muted); font-size: .65rem; }
.search-form { display: flex; gap: 8px; }
input { min-width: 0; flex: 1; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: rgb(0 0 0 / 20%); color: inherit; padding: 9px; font: inherit; }
.result-list { display: grid; gap: 6px; margin-top: 10px; }
.player-row { display: flex; align-items: center; justify-content: space-between; gap: 10px; padding: 9px 0; border-bottom: 1px solid rgb(255 255 255 / 7%); }
.player-row:last-child { border-bottom: 0; }
.player-row div:first-child { display: grid; min-width: 0; gap: 3px; }
.player-row small, .empty-state { color: var(--ui-color-text-muted); font-size: .68rem; }
.error-state { color: var(--ui-color-danger, #ff8d8d); font-size: .72rem; }
.actions { display: flex; align-items: center; justify-content: flex-end; flex-wrap: wrap; gap: 6px; }
.relationship-state { color: var(--ui-color-success, #86d7a8); font-size: .66rem; font-weight: 700; }
</style>
