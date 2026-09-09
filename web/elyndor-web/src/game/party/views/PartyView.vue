<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { classLabel } from '@/game/character/characterPresentation'
import { socialErrorMessage } from '@/game/social/socialPresentation'

import { usePartyStore } from '@/game/party/partyStore'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UIPanel } from '@/ui/components'

const party = usePartyStore()
const session = useGameSessionStore()
const currentCharacterId = computed(() => session.snapshot?.character?.id ?? '')
const isLeader = computed(() => party.snapshot?.leaderCharacterId === currentCharacterId.value)

onMounted(() => {
  void party.refresh()
})

async function kickMember(characterId: string): Promise<void> {
  if (characterId !== currentCharacterId.value) await party.kick(characterId)
}

async function transferLeadership(characterId: string): Promise<void> {
  if (characterId !== currentCharacterId.value) await party.transferLeadership(characterId)
}

async function disbandParty(): Promise<void> {
  if (isLeader.value) await party.disband()
}

</script>

<template>
  <div class="party-view">
    <header class="party-view__header">
      <div>
        <small>СОВМЕСТНЫЙ ПУТЬ</small>
        <h1>Группа</h1>
      </div>
      <span>{{ party.snapshot?.members.length ?? 0 }} / 5</span>
    </header>

    <p v-if="party.errorCode" class="error-state" role="alert">{{ socialErrorMessage(party.errorCode) }}</p>

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
      <UIButton v-if="isLeader" variant="secondary" data-party-disband @click="disbandParty">Распустить группу</UIButton>
      <article v-for="member in party.snapshot.members" :key="member.characterId" class="member-row">
        <div v-if="isLeader && member.characterId !== currentCharacterId" class="member-actions">
          <UIButton variant="secondary" @click="transferLeadership(member.characterId)">Лидер</UIButton>
          <UIButton variant="secondary" @click="kickMember(member.characterId)">Исключить</UIButton>
        </div>
        <div>
          <strong>{{ member.name }} <span v-if="member.isLeader">★</span></strong>
          <small>ур. {{ member.level }} · {{ classLabel(member.classId) }}</small>
        </div>
        <span v-if="member.characterId === currentCharacterId && member.isLeader" class="leader-label">лидер</span>
      </article>
      <UIButton variant="secondary" @click="party.leave">Покинуть группу</UIButton>
    </UIPanel>

    <UIPanel v-else title="Группа">
      <p class="empty-state">Создай группу, чтобы пригласить друзей в совместные походы.</p>
      <UIButton @click="party.create">Создать группу</UIButton>
    </UIPanel>
  </div>
</template>

<style scoped>
.party-view { display: grid; gap: 12px; padding: 14px; }
.party-view__header { display: flex; align-items: end; justify-content: space-between; }
.party-view__header small { color: var(--ui-color-primary); font-size: .55rem; letter-spacing: .14em; }
h1 { margin: 2px 0 0; font-family: var(--ui-font-display); font-size: 1.35rem; }
.party-view__header > span { color: var(--ui-color-text-muted); font-size: .68rem; }
.member-row, .invite-row { display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 9px 0; border-bottom: 1px solid rgb(255 255 255 / 7%); }
.member-row div, .invite-row div:first-child { display: grid; gap: 3px; }
.member-row small, .invite-row small, .empty-state { color: var(--ui-color-text-muted); font-size: .68rem; }
.error-state { color: var(--ui-color-danger, #ff8d8d); font-size: .72rem; }
.leader-label { color: var(--ui-color-primary); font-size: .62rem; }
.actions, .member-actions { display: flex; gap: 6px; align-items: center; }
</style>
