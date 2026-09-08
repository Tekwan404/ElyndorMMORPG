<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { usePartyStore } from '@/game/party/partyStore'
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

async function search(): Promise<void> {
  searching.value = true
  await social.search(query.value)
  searching.value = false
}

async function addFriend(characterId: string): Promise<void> {
  await social.sendRequest(characterId)
  await search()
}

async function inviteToParty(characterId: string): Promise<void> {
  await party.invite(characterId, friendIds.value.has(characterId) ? 'Friend' : 'Direct')
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

    <p v-if="social.errorCode" class="error-state" role="alert">Ошибка: {{ social.errorCode }}</p>

    <UIPanel title="Найти игрока">
      <form class="search-form" @submit.prevent="search">
        <input v-model="query" placeholder="Имя, @username или ELY-код" aria-label="Поиск игрока" />
        <UIButton type="submit" :disabled="searching || query.trim().length < 2">Найти</UIButton>
      </form>
      <div v-if="social.searchResults.length" class="result-list">
        <article v-for="player in social.searchResults" :key="player.characterId" class="player-row">
          <div>
            <strong>{{ player.name }}</strong>
            <small>ур. {{ player.level }} · {{ player.classId }} · {{ player.publicCode }}</small>
          </div>
          <UIButton variant="secondary" @click="addFriend(player.characterId)">Добавить</UIButton>
          <UIButton
            v-if="isPartyLeader"
            data-party-invite
            @click="inviteToParty(player.characterId)"
          >В группу</UIButton>
        </article>
      </div>
    </UIPanel>

    <UIPanel v-if="social.incomingRequests.length" title="Входящие заявки">
      <article v-for="request in social.incomingRequests" :key="request.id" class="player-row">
        <small>Заявка от игрока {{ request.requesterCharacterId }}</small>
        <div class="actions">
          <UIButton @click="social.decideRequest(request, true)">Принять</UIButton>
          <UIButton variant="secondary" @click="social.decideRequest(request, false)">Отклонить</UIButton>
        </div>
      </article>
    </UIPanel>

    <UIPanel v-if="social.outgoingRequests.length" title="Исходящие заявки">
      <article v-for="request in social.outgoingRequests" :key="request.id" class="player-row">
        <small>Заявка игроку {{ request.targetCharacterId }}</small>
      </article>
    </UIPanel>

    <UIPanel title="Мои друзья">
      <p v-if="!social.friends.length" class="empty-state">Пока здесь тихо. Найди первого товарища.</p>
      <article v-for="friend in social.friends" :key="friend.characterId" class="player-row">
        <div>
          <strong>{{ friend.name }}</strong>
          <small>ур. {{ friend.level }} · {{ friend.classId }}</small>
        </div>
        <span class="online-state">готов</span>
        <UIButton
          variant="secondary"
          data-remove-friend
          @click="social.removeFriend(friend.characterId)"
        >Удалить</UIButton>
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
.actions { display: flex; gap: 6px; }
</style>
