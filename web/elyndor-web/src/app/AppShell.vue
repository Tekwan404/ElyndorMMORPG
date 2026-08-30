<script setup lang="ts">
import CharacterCreationView from '@/game/character/views/CharacterCreationView.vue'
import WorldView from '@/game/world/views/WorldView.vue'
import { useGameSessionStore } from '@/stores/gameSession'
const session = useGameSessionStore()
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
      <WorldView v-else-if="session.state === 'world'" />
    </main>
    <nav v-if="session.state === 'world'" class="nav" aria-label="Основная навигация">
      <span class="active">◈<small>Мир</small></span
      ><span>♙<small>Герой</small></span
      ><span>⌖<small>Локация</small></span
      ><span>◇<small>Квесты</small></span
      ><span>☰<small>Меню</small></span>
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
.nav span {
  display: flex;
  gap: 3px;
  align-items: center;
  min-height: 49px;
  color: var(--color-text-muted);
  flex-direction: column;
  justify-content: center;
  opacity: 0.5;
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
