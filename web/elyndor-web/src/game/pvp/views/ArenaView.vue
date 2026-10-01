<script setup lang="ts">
import { computed, onMounted, onUnmounted } from 'vue'

import ArenaBattlefield from '@/game/pvp/components/ArenaBattlefield.vue'
import ArenaInvitations from '@/game/pvp/components/ArenaInvitations.vue'
import { useArenaStore } from '@/game/pvp/arenaStore'
import { arenaErrorMessage, arenaResultLabel } from '@/game/pvp/arenaPresentation'
import { UIButton } from '@/ui/components'

const arena = useArenaStore()
const status = computed(() => arena.status)
const match = computed(() => arena.match)
const battle = computed(() => match.value?.battle ?? null)
const resultLabel = computed(() => arenaResultLabel(match.value?.result, match.value?.outcome))
const activeFight = computed(() => match.value?.status === 'Active' && battle.value?.status === 'Active')

onMounted(async () => {
  await arena.refresh()
  await arena.loadLeaderboard()
  if (arena.enabled) {
    try { await arena.connect(); await arena.loadInvites() }
    catch { arena.errorCode = 'arena_load_failed' }
  }
})

onUnmounted(() => {
  if (!arena.status?.isQueued && !arena.inMatch && arena.invitations.length === 0) void arena.disconnect()
})
</script>

<template>
  <ArenaBattlefield
    v-if="match && match.status !== 'Searching' && battle"
    :battle="battle"
    :events="arena.log"
    :active="activeFight"
    :result-label="resultLabel"
    :pending="arena.pending"
    :auto-attack-pending="arena.autoAttackPending"
    :rating="status?.rating ?? null"
    :rating-delta="arena.ratingDelta"
    :honor-delta="arena.honorDelta"
    :error-message="arena.errorCode ? arenaErrorMessage(arena.errorCode) : null"
    @use-ability="arena.useAbility"
    @surrender="arena.surrender"
    @toggle-auto-attack="arena.toggleAutoAttack"
    @dismiss="arena.dismissMatch"
    @next-opponent="arena.findNextOpponent('Ranked')"
  />

  <section v-else class="arena" aria-label="Арена">
    <header class="arena__head">
      <div>
        <small>Испытание против другого героя</small>
        <h2>Арена 1×1</h2>
      </div>
      <p v-if="status">
        <b>{{ status.rating }}</b>
        <span>рейтинг</span>
      </p>
    </header>

    <p v-if="arena.errorCode" class="arena__error" role="alert">{{ arenaErrorMessage(arena.errorCode) }}</p>

    <div v-if="!arena.enabled" class="arena__note">
      <strong>Арена пока недоступна</strong>
      <span>Режим выключен на сервере.</span>
    </div>

    <template v-else>
      <ArenaInvitations :invitations="arena.invitations" :pending="arena.invitePending"
        :unavailable="Boolean(status?.isQueued || status?.activeMatchId)" :notice="arena.inviteNotice"
        @invite="arena.invitePlayer" @respond="arena.respondToInvite" @refresh="arena.loadInvites" />
      <section class="arena__record" aria-label="Статистика арены">
        <div><small>Победы</small><strong>{{ status?.wins ?? 0 }}</strong></div>
        <div><small>Поражения</small><strong>{{ status?.losses ?? 0 }}</strong></div>
        <div><small>Ничьи</small><strong>{{ status?.draws ?? 0 }}</strong></div>
        <div><small>Честь</small><strong>{{ status?.honor ?? 0 }}</strong></div>
      </section>

      <section class="arena__queue">
        <template v-if="status?.isQueued">
          <span class="arena__queue-pulse" aria-hidden="true" />
          <div>
            <strong>Ищем соперника…</strong>
            <small>Не закрывайте игру, пока идёт поиск.</small>
          </div>
          <UIButton :disabled="arena.pending" variant="ghost" @click="arena.leaveQueue()">Отменить</UIButton>
        </template>
        <template v-else>
          <div>
            <strong>Рейтинговый бой 1×1</strong>
            <small>Ваш герой, снаряжение и таланты. Побеждайте, чтобы заработать честь и подняться в рейтинге.</small>
          </div>
          <UIButton :disabled="arena.pending" @click="arena.joinQueue('Ranked')">Найти бой</UIButton>
        </template>
      </section>

      <section v-if="match && !battle" class="arena__loading">
        <strong>{{ resultLabel ?? 'Матч найден' }}</strong>
        <span>Подготавливаем поле боя…</span>
        <UIButton v-if="match.status === 'Completed'" @click="arena.dismissMatch()">К арене</UIButton>
      </section>

      <section class="arena__leaderboard">
        <header>
          <div>
            <small>Сильнейшие бойцы</small>
            <h3>Таблица лидеров</h3>
          </div>
        </header>
        <ol class="arena__board" aria-label="Таблица лидеров">
          <li v-for="entry in arena.leaderboard" :key="entry.characterId">
            <span>#{{ entry.rank }}</span>
            <strong>{{ entry.name }}</strong>
            <em>{{ entry.rating }}</em>
            <small>{{ entry.wins }}–{{ entry.losses }}–{{ entry.draws }}</small>
          </li>
          <li v-if="arena.leaderboard.length === 0" class="arena__empty">Пока нет сыгранных боёв.</li>
        </ol>
      </section>
    </template>
  </section>
</template>

<style scoped>
.arena {
  display: grid;
  gap: 0.75rem;
  width: 100%;
  padding: 0.2rem 0 0.75rem;
  color: #eee6da;
}
.arena__head {
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: 0.75rem;
  padding: 0.7rem;
  border: 1px solid rgb(184 71 91 / 30%);
  border-radius: 10px;
  background: linear-gradient(135deg, rgb(78 25 39 / 22%), rgb(8 10 16 / 94%) 58%);
}
.arena__head > div { display: grid; gap: 0.1rem; }
.arena__head small,
.arena__leaderboard header small {
  color: #c59c58;
  font-size: 0.48rem;
  font-weight: 800;
  letter-spacing: 0.13em;
}
.arena__head h2,
.arena__leaderboard h3 { margin: 0; font-family: var(--ui-font-display); }
.arena__head h2 { font-size: 1.2rem; }
.arena__head p { display: grid; margin: 0; justify-items: end; }
.arena__head p b { color: #efd18a; font-size: 1.15rem; }
.arena__head p span { color: #938c82; font-size: 0.48rem; }
.arena__error {
  margin: 0;
  padding: 0.55rem;
  border: 1px solid rgb(213 75 94 / 35%);
  border-radius: 8px;
  background: rgb(74 17 28 / 28%);
  color: #ff9aa7;
}
.arena__note,
.arena__loading {
  display: grid;
  gap: 0.2rem;
  padding: 0.8rem;
  border: 1px solid rgb(173 147 87 / 28%);
  border-radius: 9px;
  background: #0b0e15;
}
.arena__note span,
.arena__loading span { color: #8f8a82; font-size: 0.56rem; }
.arena__record {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 0.35rem;
}
.arena__record div {
  display: grid;
  justify-items: center;
  gap: 0.12rem;
  padding: 0.55rem 0.3rem;
  border: 1px solid rgb(177 151 91 / 24%);
  border-radius: 8px;
  background: #0a0d14;
}
.arena__record small { color: #8e887f; font-size: 0.46rem; }
.arena__record strong { color: #e6d7b3; font-size: 0.86rem; }
.arena__queue {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 0.65rem;
  padding: 0.7rem;
  border: 1px solid rgb(184 71 91 / 34%);
  border-radius: 10px;
  background: linear-gradient(90deg, rgb(71 21 34 / 24%), #0a0d14 60%);
}
.arena__queue > div { display: grid; gap: 0.15rem; }
.arena__queue strong { font-size: 0.72rem; }
.arena__queue small { color: #908981; font-size: 0.5rem; line-height: 1.35; }
.arena__queue-pulse {
  width: 0.7rem;
  height: 0.7rem;
  border-radius: 50%;
  background: #d55d72;
  box-shadow: 0 0 0 0 rgb(213 93 114 / 45%);
  animation: queue-pulse 1.6s infinite;
}
.arena__leaderboard {
  display: grid;
  gap: 0.45rem;
  padding: 0.65rem;
  border: 1px solid rgb(177 151 91 / 24%);
  border-radius: 10px;
  background: #090c12;
}
.arena__leaderboard header > div { display: grid; gap: 0.1rem; }
.arena__leaderboard h3 { font-size: 0.86rem; }
.arena__board { display: grid; gap: 0.25rem; margin: 0; padding: 0; list-style: none; }
.arena__board li {
  display: grid;
  grid-template-columns: 2.4rem minmax(0, 1fr) auto auto;
  gap: 0.5rem;
  align-items: baseline;
  min-height: 2.4rem;
  padding: 0.45rem 0.5rem;
  border-radius: 7px;
  background: rgb(255 255 255 / 2.5%);
}
.arena__board li > span { color: #b39257; font-size: 0.58rem; }
.arena__board strong { overflow: hidden; font-size: 0.65rem; text-overflow: ellipsis; white-space: nowrap; }
.arena__board em { color: #ead089; font-size: 0.62rem; font-style: normal; font-weight: 700; }
.arena__board small { color: #817c75; font-size: 0.48rem; }
.arena__board .arena__empty { display: block; color: #8f8980; font-size: 0.58rem; }

@keyframes queue-pulse {
  70% { box-shadow: 0 0 0 0.45rem rgb(213 93 114 / 0%); }
  100% { box-shadow: 0 0 0 0 rgb(213 93 114 / 0%); }
}

.arena__head { min-height: 120px; border-color: #9a7746; background: radial-gradient(ellipse at 80% 0, rgb(130 47 60 / 30%), transparent 70%), linear-gradient(120deg, #211c14, #090c12); }
.arena__head h2 { font-size: 1.8rem; color: #efd9a7; }
.arena__head small, .arena__leaderboard header small { font-size: 0.75rem; letter-spacing: 0; }
.arena__head p span, .arena__record small, .arena__queue small,
.arena__board small, .arena__note span, .arena__loading span { font-size: 0.75rem; }
.arena__record { gap: 0; border-block: 1px solid rgb(177 151 91 / 28%); }
.arena__record div { border: 0; border-radius: 0; background: transparent; padding-block: 0.8rem; }
.arena__record strong { font-size: 1.2rem; }
.arena__record div:last-child strong { color: #efd18a; }
.arena__queue { grid-template-columns: minmax(0, 1fr) auto; padding: 1rem; }
.arena__queue:has(.arena__queue-pulse) { grid-template-columns: auto minmax(0, 1fr) auto; }
.arena__queue strong, .arena__board strong { font-size: 0.9rem; }
.arena__queue :deep(button) { min-height: 48px; }
.arena__board li > span, .arena__board em, .arena__board .arena__empty { font-size: 0.8rem; }
.arena__leaderboard h3 { font-size: 1.1rem; }
.arena__board li:first-child:not(.arena__empty) { background: rgb(174 137 58 / 12%); }
.arena__error { position: fixed; z-index: 1100; bottom: max(1rem, env(safe-area-inset-bottom)); left: 50%; width: min(90vw, 28rem); transform: translateX(-50%); background: #35141c; }
@media (prefers-reduced-motion: reduce) { .arena__queue-pulse { animation: none; } }

@media (max-width: 430px) {
  .arena__queue { grid-template-columns: auto minmax(0, 1fr); }
  .arena__queue:has(.arena__queue-pulse) { grid-template-columns: auto minmax(0, 1fr); }
  .arena__queue :deep(.ui-button) { grid-column: 1 / -1; width: 100%; }
  .arena__board li { grid-template-columns: 2rem minmax(0, 1fr) auto; }
  .arena__board li small { display: none; }
}
</style>
