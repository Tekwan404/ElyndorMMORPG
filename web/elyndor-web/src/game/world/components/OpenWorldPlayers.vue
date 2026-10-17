<script setup lang="ts">
import { computed, shallowRef } from 'vue'
import { useOpenWorldPlayers, type OpenWorldPlayer } from '@/game/world/useOpenWorldPlayers'
import { useGameSessionStore } from '@/stores/gameSession'
import { useCombatSessionStore } from '@/stores/combatSession'

type PlayerFilter = 'enemies' | 'allies' | 'all'
const session = useGameSessionStore()
const combat = useCombatSessionStore()
const { players, available, loading, error, refresh } = useOpenWorldPlayers()
const filter = shallowRef<PlayerFilter>('enemies')
const pendingTargetId = shallowRef<string | null>(null)
const actionError = shallowRef<string | null>(null)
const raceId = computed(() => session.snapshot?.character?.raceId)
const enemyCount = computed(() => players.value.filter(player => player.raceId !== raceId.value).length)
const allyCount = computed(() => players.value.length - enemyCount.value)
const visiblePlayers = computed(() => players.value.filter(player =>
  filter.value === 'all'
    || (filter.value === 'enemies' && player.raceId !== raceId.value)
    || (filter.value === 'allies' && player.raceId === raceId.value),
))
const blocked = computed(() =>
  pendingTargetId.value !== null || combat.isActive || combat.lifecyclePending
  || session.mutationPending || session.snapshot?.afkFarm?.status === 'Active',
)

function errorMessage(): string {
  switch (combat.errorCode) {
    case 'world_pvp_combat_full': return 'Схватка заполнена: максимум 20 участников.'
    case 'world_pvp_friendly_target': return 'Нельзя атаковать союзника своей фракции.'
    case 'combat_already_active': return 'Ты уже участвуешь в другом бою.'
    default: return 'Игрок ушёл или схватка завершилась. Обнови список и попробуй снова.'
  }
}

async function enter(player: OpenWorldPlayer, attackPlayer: boolean): Promise<void> {
  if (blocked.value) return
  pendingTargetId.value = player.characterId
  actionError.value = null
  try {
    if (!(await combat.enterOpenWorldCombat(player.characterId, attackPlayer))) {
      actionError.value = errorMessage()
      await refresh()
    }
  } catch {
    actionError.value = 'Не удалось вступить в бой. Проверь соединение и повтори попытку.'
  } finally {
    pendingTargetId.value = null
  }
}

async function refreshList(): Promise<void> {
  actionError.value = null
  await refresh()
}
</script>

<template>
  <section class="world-players" aria-label="Игроки в Мёртвых Пределах" data-world-pvp>
    <header class="world-players__header">
      <div>
        <small class="world-players__eyebrow">МЁРТВЫЕ ПРЕДЕЛЫ · ОТКРЫТОЕ PVP · УР. 60</small>
        <h2>Игроки поблизости</h2>
        <p>Нападай на противников или вступай в текущие сражения.</p>
      </div>
      <button type="button" class="world-players__refresh" :disabled="loading || pendingTargetId !== null" @click="refreshList">
        {{ loading ? 'Поиск…' : 'Обновить' }}
      </button>
    </header>

    <p v-if="actionError || error" class="world-players__feedback world-players__feedback--error" role="alert">
      {{ actionError ?? error }}
    </p>
    <p v-if="!available && !error" class="world-players__feedback" role="status">
      Участие недоступно. Нужны 60 уровень, здоровье выше нуля, завершённый переход и отключённая автоохота.
    </p>
    <template v-else-if="available">
      <nav class="world-players__filters" aria-label="Фильтр игроков">
        <button type="button" :aria-pressed="filter === 'enemies'" @click="filter = 'enemies'">
          Противники <span>{{ enemyCount }}</span>
        </button>
        <button type="button" :aria-pressed="filter === 'allies'" @click="filter = 'allies'">
          Союзники <span>{{ allyCount }}</span>
        </button>
        <button type="button" :aria-pressed="filter === 'all'" @click="filter = 'all'">
          Все <span>{{ players.length }}</span>
        </button>
      </nav>
      <p v-if="loading && !players.length" class="world-players__feedback" role="status">
        Ищем игроков…
      </p>
      <p v-else-if="!visiblePlayers.length && !error" class="world-players__feedback" role="status">
        {{ filter === 'enemies' ? 'Противников пока нет. Попробуй обновить список.'
          : filter === 'allies' ? 'Союзников пока нет.'
          : 'Поблизости пока никого.' }}
      </p>
      <ul v-if="visiblePlayers.length" class="world-players__list">
        <li v-for="player in visiblePlayers" :key="player.characterId" class="world-players__player"
          :data-enemy="player.raceId !== raceId">
          <div class="world-players__identity">
            <strong>{{ player.name }}</strong>
            <small>{{ player.raceId === 'UNDEAD' ? 'Нежить' : 'Человек' }} · Ур. {{ player.level }}</small>
            <span class="world-players__status" :data-in-combat="player.inCombat">
              {{ player.inCombat ? 'В сражении' : 'Свободен' }}
            </span>
          </div>
          <div class="world-players__actions">
            <button v-if="player.raceId !== raceId" type="button" class="world-players__attack"
              :disabled="blocked" :aria-label="'Атаковать игрока ' + player.name"
              @click="enter(player, true)">
              {{ pendingTargetId === player.characterId ? 'Входим…' : 'Атаковать' }}
            </button>
            <button v-if="player.inCombat" type="button"
              :disabled="blocked"
              :aria-label="(player.raceId === raceId ? 'Помочь игроку ' : 'Вступить в бой игрока ') + player.name"
              @click="enter(player, false)">
              {{ player.raceId === raceId ? 'Помочь' : 'Вмешаться' }}
            </button>
          </div>
        </li>
      </ul>
      <p v-if="players.length" class="world-players__footnote">
        В бою можно помогать союзникам своей фракции. Список обновляется каждые 10 секунд;
        сервер проверяет доступность цели перед атакой.
      </p>
    </template>
  </section>
</template>

<style scoped>
.world-players {
  display: grid;
  min-width: 0;
  gap: 12px;
  padding: clamp(12px, 3.5vw, 20px);
  border: 1px solid rgb(173 136 78 / 40%);
  border-radius: 14px;
  background: linear-gradient(160deg, #201c1b, #0d121a 70%);
  color: #eee1cb;
}
.world-players__header { display: flex; flex-wrap: wrap; align-items: flex-start; justify-content: space-between; gap: 12px; }
.world-players__header > div { flex: 1 1 210px; min-width: 0; }
.world-players__eyebrow { color: #d4aa6a; font-size: .66rem; font-weight: 800; letter-spacing: .08em; }
.world-players h2 { margin: 5px 0; font-size: clamp(1.1rem, 4vw, 1.35rem); }
.world-players p { margin: 0; line-height: 1.45; }
.world-players__header p, .world-players__footnote { color: #b8ac99; font-size: .8rem; }
.world-players button {
  min-height: 44px;
  padding: 9px 12px;
  border: 1px solid #705a3a;
  border-radius: 9px;
  background: #29231c;
  color: #f0dfc0;
  font: inherit;
  font-size: .82rem;
  cursor: pointer;
}
.world-players button:disabled { opacity: .48; cursor: not-allowed; }
.world-players button:focus-visible { outline: 2px solid #eccb8f; outline-offset: 2px; }
.world-players__refresh { flex: 0 0 auto; }
.world-players__filters { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 6px; }
.world-players__filters button { padding-inline: 4px; white-space: nowrap; font-size: .77rem; }
.world-players__filters button[aria-pressed='true'] {
  border-color: #d3a463;
  background: rgb(147 97 47 / 29%);
  color: #ffe6b0;
}
.world-players__filters span { color: #d8b983; font-variant-numeric: tabular-nums; }
.world-players__list { display: grid; gap: 7px; padding: 0; margin: 0; list-style: none; }
.world-players__player {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  min-width: 0;
  padding: 11px;
  border: 1px solid rgb(208 174 109 / 16%);
  border-radius: 10px;
  background: rgb(8 13 20 / 55%);
}
.world-players__identity { display: flex; flex: 1 1 160px; flex-wrap: wrap; align-items: baseline; gap: 4px 9px; min-width: 0; }
.world-players__identity strong { width: 100%; overflow-wrap: anywhere; }
.world-players__player[data-enemy='true'] strong { color: #ecab9e; }
.world-players__identity small { color: #bcb2a4; font-size: .74rem; }
.world-players__status {
  padding: 2px 6px;
  border: 1px solid rgb(176 174 154 / 20%);
  border-radius: 20px;
  color: #c1b8a9;
  font-size: .7rem;
}
.world-players__status[data-in-combat='true'] { border-color: rgb(212 144 83 / 40%); color: #f2b87b; }
.world-players__actions { display: flex; flex-wrap: wrap; gap: 6px; }
.world-players__actions .world-players__attack { border-color: #a75f54; background: #542b2a; color: #ffe4dd; }
.world-players__feedback { padding: 10px; border-radius: 8px; background: rgb(226 191 121 / 8%); color: #dfcfb5; font-size: .81rem; }
.world-players__feedback--error { border: 1px solid rgb(228 105 96 / 45%); color: #ffc8c2; }
@media (max-width: 390px) {
  .world-players__actions, .world-players__actions button { flex: 1 1 auto; }
}
</style>
