<script setup lang="ts">
import { computed, shallowRef } from 'vue'
import { useOpenWorldPlayers, type OpenWorldPlayer } from '@/game/world/useOpenWorldPlayers'
import { useGameSessionStore } from '@/stores/gameSession'
import { useCombatSessionStore } from '@/stores/combatSession'

const session = useGameSessionStore()
const combat = useCombatSessionStore()
const { players, loading, error, refresh } = useOpenWorldPlayers()
const pending = shallowRef(false)
const actionError = shallowRef<string | null>(null)
const raceId = computed(() => session.snapshot?.character?.raceId)

async function enter(player: OpenWorldPlayer, attackPlayer: boolean): Promise<void> {
  if (pending.value) return
  pending.value = true
  actionError.value = null
  try {
    if (!await combat.enterOpenWorldCombat(player.characterId, attackPlayer)) {
      actionError.value = combat.errorCode === 'world_pvp_combat_full'
        ? 'В этой схватке уже 20 участников.'
        : 'Ситуация изменилась: игрок уже ушёл или бой завершён. Осмотрись ещё раз.'
      await refresh()
    }
  } catch {
    actionError.value = 'Не удалось вступить в бой. Повтори попытку.'
  } finally {
    pending.value = false
  }
}
</script>

<template>
  <section class="world-players" aria-label="Игроки в Мёртвых Пределах" data-world-pvp>
    <header class="world-players__header">
      <div><small>Открытое PvPvE · ур. 60</small><h2>Игроки рядом</h2></div>
      <button type="button" :disabled="loading || pending" @click="refresh">Осмотреться</button>
    </header>
    <p class="world-players__hint">Люди и нежить — враги. Можно атаковать противника или вмешаться в его схватку.</p>
    <p v-if="error || actionError" role="alert">{{ actionError ?? error }}</p>
    <p v-else-if="!players.length" role="status">{{ loading ? 'Осматриваешь окрестности…' : 'Поблизости пока никого.' }}</p>
    <ul class="world-players__list">
      <li v-for="player in players" :key="player.characterId" class="world-players__player" :data-enemy="player.raceId !== raceId">
        <div><strong>{{ player.name }}</strong><small>{{ player.raceId === 'UNDEAD' ? 'Нежить' : 'Человек' }} · ур. {{ player.level }} · {{ player.inCombat ? 'В бою' : 'В локации' }}</small></div>
        <div class="world-players__actions">
          <button v-if="player.raceId !== raceId" type="button" :disabled="pending || combat.isActive" @click="enter(player, true)">Атаковать</button>
          <button v-if="player.inCombat" type="button" :disabled="pending || combat.isActive" @click="enter(player, false)">{{ player.raceId === raceId ? 'Помочь союзнику' : 'Вступить в схватку' }}</button>
        </div>
      </li>
    </ul>
  </section>
</template>

<style scoped>
.world-players { padding: 1rem; border-block: 1px solid #66513a; background: #111419; color: #e6dfcf; }
.world-players__header, .world-players__player { display: flex; justify-content: space-between; align-items: center; gap: .6rem; }
.world-players h2 { margin: .2rem 0; font-size: 1.1rem; }
.world-players small { display: block; color: #b9ab92; font-size: .75rem; }
.world-players__hint { font-size: .8rem; line-height: 1.5; color: #c3b599; }
.world-players__list { list-style: none; padding: 0; margin: 0; }
.world-players__player { flex-wrap: wrap; padding-block: .7rem; border-top: 1px solid #34312b; }
.world-players__player[data-enemy='true'] strong { color: #ef9d91; }
.world-players__actions { display: flex; flex-wrap: wrap; gap: .4rem; }
.world-players button { min-height: 44px; padding: .4rem .6rem; border: 1px solid #76603d; background: #272117; color: #e8d2a7; font: inherit; font-size: .75rem; cursor: pointer; }
.world-players button:disabled { opacity: .5; cursor: default; }
.world-players button:focus-visible { outline: 2px solid #ecd09a; outline-offset: 2px; }
</style>
