<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink, RouterView } from 'vue-router'

import { isApiHealthy } from '@/api/health'

type ApiState = 'checking' | 'online' | 'offline'

const apiState = ref<ApiState>('checking')

onMounted(async () => {
  apiState.value = (await isApiHealthy()) ? 'online' : 'offline'
})
</script>

<template>
  <div class="game-shell">
    <header class="game-shell__header">
      <div>
        <p class="game-shell__eyebrow">ELYNDOR</p>
        <p class="game-shell__subtitle">Telegram MMORPG</p>
      </div>

      <div class="server-state" :data-state="apiState" aria-live="polite">
        <span class="server-state__dot" aria-hidden="true"></span>
        <span>{{
          apiState === 'online'
            ? 'Сервер доступен'
            : apiState === 'offline'
              ? 'Сервер недоступен'
              : 'Проверка сервера'
        }}</span>
      </div>
    </header>

    <main class="game-shell__content">
      <RouterView />
    </main>

    <nav class="bottom-navigation" aria-label="Основная навигация">
      <RouterLink class="bottom-navigation__item" to="/world">
        <span aria-hidden="true">◈</span>
        <span>Мир</span>
      </RouterLink>
      <button class="bottom-navigation__item" type="button" disabled>
        <span aria-hidden="true">♙</span>
        <span>Герой</span>
      </button>
      <button class="bottom-navigation__item" type="button" disabled>
        <span aria-hidden="true">⌖</span>
        <span>Локация</span>
      </button>
      <button class="bottom-navigation__item" type="button" disabled>
        <span aria-hidden="true">◇</span>
        <span>Квесты</span>
      </button>
      <button class="bottom-navigation__item" type="button" disabled>
        <span aria-hidden="true">☰</span>
        <span>Меню</span>
      </button>
    </nav>
  </div>
</template>

<style scoped lang="scss">
.game-shell {
  position: relative;
  display: grid;
  grid-template-rows: auto 1fr auto;
  min-height: 100dvh;
  overflow: hidden;
  background:
    radial-gradient(circle at 75% 10%, rgb(79 68 173 / 24%), transparent 36%),
    linear-gradient(180deg, #0c1220 0%, #070a11 100%);
  color: var(--color-text-primary);
}

.game-shell::before {
  position: absolute;
  inset: 0;
  pointer-events: none;
  content: '';
  background-image: linear-gradient(rgb(255 255 255 / 2%) 1px, transparent 1px);
  background-size: 100% 4px;
  opacity: 0.25;
}

.game-shell__header {
  z-index: 1;
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 68px;
  padding: calc(12px + var(--safe-area-top)) 16px 12px;
  border-bottom: 1px solid var(--color-border);
  background: rgb(7 11 19 / 88%);
  backdrop-filter: blur(12px);
}

.game-shell__eyebrow,
.game-shell__subtitle {
  margin: 0;
}

.game-shell__eyebrow {
  color: var(--color-gold);
  font-family: Georgia, 'Times New Roman', serif;
  font-size: 1.1rem;
  font-weight: 700;
  letter-spacing: 0.18em;
}

.game-shell__subtitle {
  margin-top: 2px;
  color: var(--color-text-muted);
  font-size: 0.7rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.server-state {
  display: flex;
  gap: 7px;
  align-items: center;
  color: var(--color-text-muted);
  font-size: 0.72rem;
}

.server-state__dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: #c89b4a;
  box-shadow: 0 0 8px currentcolor;
}

.server-state[data-state='online'] .server-state__dot {
  background: #6fc58f;
}

.server-state[data-state='offline'] .server-state__dot {
  background: #d35f67;
}

.game-shell__content {
  z-index: 1;
  min-height: 0;
  overflow-y: auto;
}

.bottom-navigation {
  z-index: 1;
  display: grid;
  grid-template-columns: repeat(5, 1fr);
  padding: 7px 6px calc(7px + var(--safe-area-bottom));
  border-top: 1px solid var(--color-border);
  background: rgb(7 11 19 / 94%);
  backdrop-filter: blur(14px);
}

.bottom-navigation__item {
  display: flex;
  gap: 3px;
  align-items: center;
  justify-content: center;
  min-width: 0;
  min-height: 49px;
  padding: 4px 2px;
  border: 0;
  background: transparent;
  color: var(--color-text-muted);
  font: inherit;
  font-size: 0.67rem;
  text-decoration: none;
  text-transform: uppercase;
  flex-direction: column;
}

.bottom-navigation__item > span:first-child {
  font-size: 1.05rem;
}

.bottom-navigation__item.router-link-active {
  color: var(--color-gold-bright);
  text-shadow: 0 0 12px rgb(211 177 101 / 50%);
}

.bottom-navigation__item:disabled {
  cursor: not-allowed;
  opacity: 0.45;
}

@media (min-width: 720px) {
  .game-shell {
    width: min(100%, 540px);
    margin-inline: auto;
    border-inline: 1px solid var(--color-border);
  }
}
</style>
