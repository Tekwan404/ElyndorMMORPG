<script setup lang="ts">
import { computed, onMounted } from 'vue'

import { usePartyStore } from '@/game/party/partyStore'
import { useDungeonStore } from '@/game/party/dungeonStore'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UIPanel } from '@/ui/components'

const party = usePartyStore()
const dungeon = useDungeonStore()
const combat = useCombatSessionStore()
const session = useGameSessionStore()
const currentCharacterId = computed(() => session.snapshot?.character?.id ?? '')
const isLeader = computed(() => party.snapshot?.leaderCharacterId === currentCharacterId.value)
const currentLocationId = computed(() => session.snapshot?.world?.currentLocation.id ?? '')
const characterLevel = computed(() => session.snapshot?.character?.level ?? 0)
const isAtDungeon = computed(() => currentLocationId.value === 'ANCIENT_MINE')
const currentRunMember = computed(() => dungeon.current?.members.find(
  member => member.characterId === currentCharacterId.value,
))
const needsDungeonEntry = computed(() => currentRunMember.value?.state !== 'Active')

onMounted(() => {
  void party.refresh()
  void dungeon.refresh()
})

async function createDungeon(dungeonId: string): Promise<void> {
  await dungeon.create(dungeonId)
}

async function teleportToDungeon(): Promise<void> {
  if (isAtDungeon.value || characterLevel.value < 15) return
  if (await dungeon.teleport('ANCIENT_MINE')) await session.refreshSnapshot()
}

async function enterDungeon(): Promise<void> {
  if (dungeon.current?.runId) await dungeon.enter(dungeon.current.runId)
}

async function restartDungeon(): Promise<void> {
  if (dungeon.current?.runId && isLeader.value) {
    await dungeon.restart(dungeon.current.runId)
  }
}

async function exitDungeon(): Promise<void> {
  if (dungeon.current?.runId) await dungeon.exit(dungeon.current.runId)
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

async function startDungeon(): Promise<void> {
  if (!dungeon.current || !isLeader.value || dungeon.current.state !== 'Active') return
  const encounter = dungeon.current.encounters.find(
    candidate => candidate.encounterIndex === dungeon.current!.currentEncounterIndex,
  )
  if (!encounter || encounter.state === 'Active' || currentLocationId.value !== 'ANCIENT_MINE') return
  await combat.connect()
  await combat.startDungeonEncounter(dungeon.current.runId)
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

    <p v-if="party.errorCode" class="error-state" role="alert">Ошибка: {{ party.errorCode }}</p>

    <UIPanel v-if="party.invites.length" title="Приглашения">
      <article v-for="invite in party.invites" :key="invite.id" class="invite-row">
        <div><strong>Приглашение в группу</strong><small>от {{ invite.inviterCharacterId }}</small></div>
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
          <small>ур. {{ member.level }} · {{ member.classId }}</small>
        </div>
        <span v-if="member.characterId === currentCharacterId && member.isLeader" class="leader-label">лидер</span>
      </article>
      <UIButton variant="secondary" @click="party.leave">Покинуть группу</UIButton>
    </UIPanel>

    <UIPanel v-else title="Группа">
      <p class="empty-state">Создай группу, чтобы пригласить друзей в совместные походы.</p>
      <UIButton @click="party.create">Создать группу</UIButton>
    </UIPanel>
    <UIPanel v-if="dungeon.previews.length" title="Древняя шахта">
      <p v-if="dungeon.errorCode" class="error-state" role="alert">Ошибка: {{ dungeon.errorCode }}</p>
      <p class="empty-state">Подземелье — отдельный инстанс. Вход выполняется мгновенной телепортацией.</p>
      <UIButton
        v-if="!isAtDungeon"
        data-dungeon-teleport
        :disabled="characterLevel < 15"
        :loading="dungeon.teleporting"
        @click="teleportToDungeon"
      >Телепортироваться в подземелье</UIButton>
      <small v-if="characterLevel < 15" class="empty-state">Доступно с 15 уровня.</small>
      <small v-else-if="isAtDungeon" class="dungeon-location-state">Вы уже на входе в подземелье.</small>
      <template v-if="dungeon.current">
        <p class="empty-state">
          {{ dungeon.current.displayName }} · {{ dungeon.current.currentEncounterIndex + 1 }} / {{ dungeon.current.encounterCount }}
          · checkpoint: {{ dungeon.current.currentCheckpointId }}
        </p>
        <div class="dungeon-progress">
          <span
            v-for="encounter in dungeon.current.encounters"
            :key="encounter.encounterId"
            :data-state="encounter.state"
          >{{ encounter.encounterIndex + 1 }}</span>
        </div>
        <UIButton
          v-if="needsDungeonEntry && currentLocationId === 'ANCIENT_MINE'"
          variant="secondary"
          @click="enterDungeon"
        >Войти в текущий забег</UIButton>
        <UIButton
          v-else-if="isLeader && currentLocationId === 'ANCIENT_MINE' && dungeon.current.state === 'Active'"
          :loading="combat.pending"
          @click="startDungeon"
        >Начать encounter</UIButton>
        <UIButton
          v-if="isLeader && dungeon.current.encounters.some(encounter => encounter.state === 'Wiped')"
          data-dungeon-restart
          variant="secondary"
          @click="restartDungeon"
        >Перезапустить encounter</UIButton>
        <UIButton
          v-if="currentRunMember?.state === 'Active' && !dungeon.current.encounters.some(encounter => encounter.state === 'Active')"
          data-dungeon-exit
          variant="secondary"
          @click="exitDungeon"
        >Выйти из забега</UIButton>
        <small v-if="!needsDungeonEntry && currentLocationId !== 'ANCIENT_MINE'" class="empty-state">
          Сначала переместите группу в ANCIENT_MINE.
        </small>
      </template>
      <template v-else>
        <p class="empty-state">Лидер создаёт забег после сбора группы. В забеге пять encounter’ов: четыре обычных и босс.</p>
        <UIButton
          v-for="preview in dungeon.previews"
          :key="preview.id"
          :disabled="!isLeader"
          @click="createDungeon(preview.id)"
        >Создать забег</UIButton>
      </template>
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
.dungeon-progress { display: flex; gap: 6px; margin: 8px 0; }
.dungeon-progress span { display: grid; width: 26px; height: 26px; place-items: center; border: 1px solid var(--ui-color-border); border-radius: 50%; color: var(--ui-color-text-muted); font-size: .65rem; }
.dungeon-progress span[data-state='Completed'] { border-color: var(--ui-color-success); color: var(--ui-color-success); }
.dungeon-progress span[data-state='Active'] { border-color: var(--ui-color-primary); color: var(--ui-color-primary); }
.dungeon-location-state { color: var(--ui-color-success); font-size: .68rem; }
</style>
