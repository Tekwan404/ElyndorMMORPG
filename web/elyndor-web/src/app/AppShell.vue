<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import CharacterCreationView from '@/game/character/views/CharacterCreationView.vue'
import CharacterStatsView from '@/game/character/views/CharacterStatsView.vue'
import WorldView from '@/game/world/views/WorldView.vue'
import { useGameSessionStore } from '@/stores/gameSession'
import { initializeTelegramWebApp } from '@/telegram/telegramWebApp'

const session = useGameSessionStore()
const activeView = ref<'world' | 'hero'>('world')
const character = computed(() => session.snapshot?.character)
const hpPercent = computed(() =>
  character.value ? (character.value.vitals.currentHp / character.value.vitals.maxHp) * 100 : 0,
)
const resourcePercent = computed(() =>
  character.value
    ? (character.value.vitals.currentResource / character.value.vitals.maxResource) * 100
    : 0,
)

onMounted(() => {
  initializeTelegramWebApp()
  void session.start()
})
</script>

<template>
  <div class="game-shell">
    <header class="game-shell__header">
      <div>
        <p class="brand">ELYNDOR</p>
        <p class="subtitle">Telegram MMORPG</p>
      </div>
      <div class="server-state" :data-state="session.state" aria-live="polite">
        <i></i><span>{{ session.state === 'world' ? 'Мир доступен' : 'Синхронизация' }}</span>
      </div>
    </header>
    <section v-if="session.state === 'world' && character" class="hud" aria-label="Состояние героя">
      <div class="hud__identity">
        <b>{{ character.name }}</b
        ><small>ур. {{ character.level }} · {{ character.classId }}</small>
      </div>
      <div class="hud__bars">
        <label class="bar bar--hp">
          <span :style="{ width: `${hpPercent}%` }"></span>
          <b>HP {{ character.vitals.currentHp }} / {{ character.vitals.maxHp }}</b>
        </label>
        <label class="bar" :class="`bar--${character.vitals.resourceType.toLowerCase()}`">
          <span :style="{ width: `${resourcePercent}%` }"></span>
          <b
            >{{ character.vitals.resourceType }} {{ character.vitals.currentResource }} /
            {{ character.vitals.maxResource }}</b
          >
        </label>
      </div>
    </section>
    <main class="content">
      <section
        v-if="['idle', 'authenticating', 'reauthenticating', 'loading'].includes(session.state)"
        class="system-state"
      >
        <b>✦</b>
        <h1>
          {{ session.state === 'reauthenticating' ? 'Возвращаем связь' : 'Входим в Elyndor' }}
        </h1>
        <p>Восстанавливаем героя и его положение в мире.</p>
      </section>
      <section
        v-else-if="session.state === 'offline' || session.state === 'error'"
        class="system-state"
      >
        <b class="danger">!</b>
        <h1>Связь с миром потеряна</h1>
        <p data-testid="session-error">{{ session.errorCode }}</p>
        <button class="primary" type="button" @click="session.start">Повторить вход</button>
      </section>
      <CharacterCreationView v-else-if="session.state === 'needs-character'" />
      <WorldView v-else-if="session.state === 'world' && activeView === 'world'" />
      <CharacterStatsView v-else-if="session.state === 'world'" />
    </main>
    <nav v-if="session.state === 'world'" class="nav" aria-label="Основная навигация">
      <button
        :class="{ active: activeView === 'world' }"
        type="button"
        @click="activeView = 'world'"
      >
        ◈<small>Мир</small>
      </button>
      <button :class="{ active: activeView === 'hero' }" type="button" @click="activeView = 'hero'">
        ♙<small>Герой</small>
      </button>
      <button type="button" disabled>⌖<small>Локация</small></button>
      <button type="button" disabled>◇<small>Квесты</small></button>
      <button type="button" disabled>☰<small>Меню</small></button>
    </nav>
  </div>
</template>

<style scoped lang="scss">
.game-shell {
  display: grid;
  grid-template-rows: auto 1fr auto;
  min-height: 100dvh;
  overflow: hidden;
  background:
    radial-gradient(circle at 75% 10%, rgb(79 68 173 / 24%), transparent 36%),
    linear-gradient(180deg, #0c1220, #070a11);
  color: var(--color-text-primary);
}
.game-shell__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 68px;
  padding: calc(12px + var(--safe-area-top)) 16px 12px;
  border-bottom: 1px solid var(--color-border);
  background: rgb(7 11 19 / 90%);
}
.brand,
.subtitle {
  margin: 0;
}
.brand {
  color: var(--color-gold);
  font-family: Georgia, serif;
  font-weight: 700;
  letter-spacing: 0.18em;
}
.subtitle {
  color: var(--color-text-muted);
  font-size: 0.7rem;
  text-transform: uppercase;
}
.server-state {
  display: flex;
  gap: 7px;
  align-items: center;
  color: var(--color-text-muted);
  font-size: 0.72rem;
}
.server-state i {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: #c89b4a;
  box-shadow: 0 0 8px currentcolor;
}
.server-state[data-state='world'] i {
  background: #6fc58f;
}
.content {
  min-height: 0;
  overflow-y: auto;
}
.hud {
  display: grid;
  grid-template-columns: auto 1fr;
  gap: 12px;
  padding: 9px 14px;
  border-bottom: 1px solid var(--color-border);
  background: #0b111d;
}
.hud__identity {
  display: flex;
  min-width: 88px;
  flex-direction: column;
}
.hud__identity b {
  color: #f0e7d2;
  font:
    0.88rem Georgia,
    serif;
}
.hud__identity small {
  color: var(--color-text-muted);
  font-size: 0.58rem;
}
.hud__bars {
  display: grid;
  gap: 5px;
}
.bar {
  position: relative;
  min-height: 17px;
  overflow: hidden;
  border: 1px solid rgb(133 148 177 / 34%);
  background: #080b12;
}
.bar span {
  position: absolute;
  inset-block: 0;
  left: 0;
  background: linear-gradient(90deg, #735418, #d2a83f);
}
.bar--hp span {
  background: linear-gradient(90deg, #6e1723, #c7424f);
}
.bar--focus span {
  background: linear-gradient(90deg, #1b6e66, #42c4aa);
}
.bar--mana span {
  background: linear-gradient(90deg, #30469a, #6f87f0);
}
.bar b {
  position: relative;
  z-index: 1;
  display: block;
  color: #fff7e4;
  font-size: 0.58rem;
  line-height: 15px;
  text-align: center;
  text-shadow: 0 1px 2px #000;
}
.system-state {
  display: grid;
  min-height: 100%;
  padding: 32px;
  text-align: center;
  place-content: center;
}
.system-state b {
  color: var(--color-gold);
  font-size: 2.5rem;
}
.system-state .danger {
  color: #d35f67;
}
.system-state h1 {
  margin: 12px 0 6px;
  font-family: Georgia, serif;
  color: #f0e7d2;
}
.system-state p {
  color: var(--color-text-secondary);
}
.primary {
  min-height: 46px;
  border: 1px solid #c8a963;
  border-radius: 4px;
  background: linear-gradient(#6c5224, #39280f);
  color: #fff4d1;
  font: inherit;
  font-weight: 700;
}
.nav {
  display: grid;
  grid-template-columns: repeat(5, 1fr);
  padding: 7px 6px calc(7px + var(--safe-area-bottom));
  border-top: 1px solid var(--color-border);
  background: rgb(7 11 19 / 95%);
}
.nav button {
  display: flex;
  gap: 3px;
  align-items: center;
  min-height: 49px;
  padding: 0;
  border: 0;
  background: transparent;
  color: var(--color-text-muted);
  flex-direction: column;
  justify-content: center;
  opacity: 0.5;
}
.nav button:disabled {
  opacity: 0.32;
}
.nav small {
  font-size: 0.62rem;
  text-transform: uppercase;
}
.nav .active {
  color: var(--color-gold-bright);
  opacity: 1;
}
@media (min-width: 720px) {
  .game-shell {
    width: min(100%, 540px);
    margin-inline: auto;
    border-inline: 1px solid var(--color-border);
  }
}
</style>
