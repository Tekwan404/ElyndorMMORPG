<script setup lang="ts">
import { computed, onMounted, onUnmounted } from 'vue'

import { useArenaStore } from '@/game/pvp/arenaStore'
import { arenaErrorMessage, arenaResultLabel } from '@/game/pvp/arenaPresentation'
import { UIButton } from '@/ui/components'

const arena = useArenaStore()
const status = computed(() => arena.status)
const match = computed(() => arena.match)
const battle = computed(() => match.value?.battle ?? null)
const abilities = computed(() => battle.value?.player.abilities ?? [])
const opponentId = computed(() => battle.value?.enemy.actorId ?? '')
const resultLabel = computed(() => arenaResultLabel(match.value?.result, match.value?.outcome))

function hpPercent(current: number, max: number): number {
  return max > 0 ? Math.max(0, Math.min(100, (current / max) * 100)) : 0
}

function abilityReady(abilityId: string): boolean {
  const readyAt = battle.value?.player.cooldowns[abilityId]
  return !readyAt || new Date(readyAt).getTime() <= Date.now()
}

onMounted(async () => {
  await arena.refresh()
  await arena.loadLeaderboard()
})
onUnmounted(() => {
  // Keep the connection while queued or fighting: it is what keeps the player present.
  if (!arena.status?.isQueued && !arena.inMatch) void arena.disconnect()
})
</script>

<template>
  <section class="arena" aria-label="Арена">
    <header class="arena__head">
      <h2>Арена 1×1</h2>
      <p v-if="status">
        Рейтинг {{ status.rating }} · Честь {{ status.honor }} ·
        {{ status.wins }}П / {{ status.losses }}Пор / {{ status.draws }}Н
      </p>
    </header>

    <p v-if="arena.errorCode" class="arena__error" role="alert">{{ arenaErrorMessage(arena.errorCode) }}</p>

    <div v-if="!arena.enabled" class="arena__note">Арена пока недоступна.</div>

    <template v-else-if="match && match.status !== 'Searching'">
      <div v-if="battle" class="arena__fight">
        <div class="arena__fighter">
          <strong>{{ battle.player.name }}</strong>
          <div class="arena__bar"><i :style="{ width: hpPercent(battle.player.hp, battle.player.maxHp) + '%' }" /></div>
          <small>{{ Math.ceil(battle.player.hp) }} / {{ Math.ceil(battle.player.maxHp) }}</small>
        </div>
        <div class="arena__fighter arena__fighter--enemy">
          <strong>{{ match.opponentName ?? battle.enemy.name }}</strong>
          <div class="arena__bar"><i :style="{ width: hpPercent(battle.enemy.hp, battle.enemy.maxHp) + '%' }" /></div>
          <small>{{ Math.ceil(battle.enemy.hp) }} / {{ Math.ceil(battle.enemy.maxHp) }}</small>
        </div>
      </div>

      <p v-if="resultLabel" class="arena__result">{{ resultLabel }}</p>

      <div v-if="match.status === 'Active'" class="arena__skills">
        <UIButton
          v-for="ability in abilities"
          :key="ability.id"
          :disabled="!abilityReady(ability.id)"
          @click="arena.useAbility(ability.id, ability.targetType === 'Self' ? battle!.player.actorId : opponentId)"
        >{{ ability.displayName }}</UIButton>
        <UIButton variant="ghost" @click="arena.surrender()">Сдаться</UIButton>
      </div>
      <UIButton v-else @click="arena.dismissMatch()">К арене</UIButton>
    </template>

    <template v-else>
      <div class="arena__queue">
        <p v-if="status?.isQueued">Поиск соперника…</p>
        <UIButton v-if="status?.isQueued" :disabled="arena.pending" @click="arena.leaveQueue()">Отменить поиск</UIButton>
        <UIButton v-else :disabled="arena.pending" @click="arena.joinQueue('Ranked')">Найти бой</UIButton>
      </div>

      <ol class="arena__board" aria-label="Таблица лидеров">
        <li v-for="entry in arena.leaderboard" :key="entry.characterId">
          <span>{{ entry.rank }}</span>
          <strong>{{ entry.name }}</strong>
          <em>{{ entry.rating }}</em>
          <small>{{ entry.wins }}/{{ entry.losses }}/{{ entry.draws }}</small>
        </li>
        <li v-if="arena.leaderboard.length === 0" class="arena__empty">Пока нет сыгранных боёв.</li>
      </ol>
    </template>
  </section>
</template>

<style scoped>
.arena { display: grid; gap: 0.75rem; padding: 0.75rem; padding-bottom: calc(0.75rem + env(safe-area-inset-bottom, 0px)); }
.arena__head h2 { margin: 0; }
.arena__head p, .arena__note, .arena__empty { margin: 0; opacity: 0.8; }
.arena__error { margin: 0; color: #ff8a8a; }
.arena__fight { display: grid; grid-template-columns: 1fr 1fr; gap: 0.5rem; }
.arena__fighter { display: grid; gap: 0.25rem; min-width: 0; }
.arena__fighter strong { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.arena__bar { height: 0.6rem; border-radius: 0.3rem; background: rgb(255 255 255 / 12%); overflow: hidden; }
.arena__bar i { display: block; height: 100%; background: #4cc26a; transition: width 0.2s; }
.arena__fighter--enemy .arena__bar i { background: #d4564e; }
.arena__result { margin: 0; font-weight: 700; text-align: center; }
.arena__skills { display: flex; flex-wrap: wrap; gap: 0.5rem; }
.arena__skills > * { min-height: 2.75rem; min-width: 2.75rem; }
.arena__queue { display: grid; gap: 0.5rem; justify-items: start; }
.arena__board { display: grid; gap: 0.25rem; margin: 0; padding: 0; list-style: none; }
.arena__board li { display: grid; grid-template-columns: 2rem 1fr auto auto; gap: 0.5rem; align-items: baseline; }
.arena__board strong { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
</style>
