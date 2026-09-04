<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'

import { gameArt } from '@/assets/gameArt'
import { classLabel, resourceLabel } from '@/game/character/characterPresentation'
import CharacterCreationView from '@/game/character/views/CharacterCreationView.vue'
import HeroView from '@/game/character/views/HeroView.vue'
import WorldView from '@/game/world/views/WorldView.vue'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'
import { initializeTelegramWebApp } from '@/telegram/telegramWebApp'
import { UIButton, UIHealthBar, UILoadingState } from '@/ui/components'

type ShellView = 'world' | 'hero'

const session = useGameSessionStore()
const combat = useCombatSessionStore()
const activeView = ref<ShellView>('world')
const character = computed(() => session.snapshot?.character)
const resourceTone = computed<'rage' | 'focus' | 'mana'>(() => {
  const value = character.value?.vitals.resourceType.toLowerCase()
  return value === 'rage' || value === 'mana' ? value : 'focus'
})
const resourceName = computed(() => resourceLabel(character.value?.vitals.resourceType ?? ''))
const connectionLabel = computed(() => {
  if (session.state === 'world') return 'Мир доступен'
  if (session.state === 'offline' || session.state === 'error') return 'Связь потеряна'
  return 'Синхронизация'
})
const sessionErrorMessage = computed(() => {
  const code = session.errorCode
  if (!code) return 'Не удалось восстановить состояние мира.'
  if (code === 'network_unavailable') return 'Не удалось связаться с сервером. Проверьте подключение и попробуйте снова.'
  if (code === 'authentication_failed') return 'Не удалось подтвердить вход через Telegram. Попробуйте войти ещё раз.'
  if (code === 'bootstrap_failed') return 'Не удалось загрузить состояние персонажа и мира.'
  return `Не удалось продолжить игру. Код ошибки: ${code}`
})

const navigation: readonly {
  id: ShellView | 'quests' | 'menu'
  label: string
  icon: string
  enabled: boolean
}[] = [
  { id: 'world', label: 'Мир', icon: gameArt.navigation.world, enabled: true },
  { id: 'hero', label: 'Герой', icon: gameArt.navigation.hero, enabled: true },
  { id: 'quests', label: 'Квесты', icon: gameArt.navigation.quests, enabled: false },
  { id: 'menu', label: 'Меню', icon: gameArt.navigation.menu, enabled: false },
]

function selectView(item: (typeof navigation)[number]) {
  if (item.enabled && (item.id === 'world' || item.id === 'hero')) {
    activeView.value = item.id
  }
}

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
        <p class="subtitle">MMORPG в Telegram</p>
      </div>
      <div class="game-shell__actions">
        <RouterLink v-if="session.isAdmin" class="admin-link" to="/admin">Admin</RouterLink>
        <div class="server-state" :data-state="session.state" aria-live="polite">
          <i aria-hidden="true" /><span>{{ connectionLabel }}</span>
        </div>
      </div>
    </header>

    <section v-if="session.state === 'world' && character && !combat.isActive" class="hud" aria-label="Состояние героя">
      <div class="hud__identity">
        <b>{{ character.name }}</b>
        <small>ур. {{ character.level }} · {{ classLabel(character.classId) }}</small>
      </div>
      <div class="hud__bars">
        <UIHealthBar label="Здоровье" :value="character.vitals.currentHp" :max="character.vitals.maxHp" />
        <UIHealthBar :label="resourceName" :tone="resourceTone" :value="character.vitals.currentResource" :max="character.vitals.maxResource" />
        <div class="xp" role="progressbar" aria-label="Опыт" :aria-valuenow="character.experience" :aria-valuemax="character.xpToNextLevel || 1">
          <span :style="{ width: `${character.xpToNextLevel ? Math.min(100, character.experience / character.xpToNextLevel * 100) : 100}%` }" />
          <small>Опыт {{ character.experience }} / {{ character.xpToNextLevel || 'МАКС.' }}</small>
        </div>
      </div>
    </section>

    <main class="content">
      <UILoadingState
        v-if="['idle', 'authenticating', 'reauthenticating', 'loading'].includes(session.state)"
        state="loading"
        :title="session.state === 'reauthenticating' ? 'Возвращаем связь' : 'Входим в Elyndor'"
        message="Восстанавливаем героя и его положение в мире."
      />
      <UILoadingState
        v-else-if="session.state === 'offline' || session.state === 'error'"
        state="error"
        title="Связь с миром потеряна"
        :message="sessionErrorMessage"
      >
        <UIButton data-retry-session variant="secondary" @click="session.start">Повторить вход</UIButton>
      </UILoadingState>
      <CharacterCreationView v-else-if="session.state === 'needs-character'" />
      <WorldView v-else-if="session.state === 'world' && activeView === 'world'" />
      <HeroView v-else-if="session.state === 'world'" />
    </main>

    <nav v-if="session.state === 'world' && !combat.isActive" class="navigation" aria-label="Основная навигация">
      <button
        v-for="item in navigation"
        :key="item.id"
        class="navigation__item"
        :class="{ 'navigation__item--active': item.id === activeView }"
        :data-nav="item.id"
        type="button"
        :disabled="!item.enabled"
        :aria-current="item.id === activeView ? 'page' : undefined"
        @click="selectView(item)"
      >
        <img class="navigation__icon" :src="item.icon" alt="" aria-hidden="true" />
        <small>{{ item.label }}</small>
      </button>
    </nav>
  </div>
</template>

<style scoped>
.game-shell { display: grid; width: min(100%, var(--ui-content-width)); height: var(--ui-viewport-height); margin-inline: auto; grid-template-rows: auto auto minmax(0, 1fr) auto; overflow: hidden; border-inline: 1px solid var(--ui-color-border); background: var(--ui-color-background); color: var(--ui-color-text-primary); }
.game-shell__header { display: flex; min-height: var(--ui-control-height-lg); align-items: center; justify-content: space-between; gap: var(--ui-space-3); padding: calc(var(--ui-space-3) + var(--ui-safe-area-top)) calc(var(--ui-space-4) + var(--ui-safe-area-right)) var(--ui-space-3) calc(var(--ui-space-4) + var(--ui-safe-area-left)); border-bottom: 1px solid var(--ui-color-border); background: var(--ui-color-surface-1); }
.brand, .subtitle { margin: 0; }
.game-shell__actions { display: flex; align-items: center; gap: var(--ui-space-3); }
.admin-link { padding: var(--ui-space-1) var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); color: var(--ui-color-primary); font-size: var(--ui-font-size-xs); text-decoration: none; }
.admin-link:hover { border-color: var(--ui-color-primary); }
.brand { color: var(--ui-color-primary); font-family: var(--ui-font-display); font-weight: var(--ui-font-weight-bold); letter-spacing: var(--ui-space-1); }
.subtitle { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); text-transform: uppercase; }
.server-state { display: flex; align-items: center; gap: var(--ui-space-2); color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.server-state i { width: var(--ui-space-2); height: var(--ui-space-2); border-radius: var(--ui-radius-round); background: var(--ui-color-warning); }
.server-state[data-state='world'] i { background: var(--ui-color-success); }
.server-state[data-state='offline'] i, .server-state[data-state='error'] i { background: var(--ui-color-danger); box-shadow: var(--ui-glow-danger); }
.hud { display: grid; grid-template-columns: minmax(5rem, auto) minmax(0, 1fr); gap: var(--ui-space-3); padding: var(--ui-space-2) var(--ui-space-4); border-bottom: 1px solid var(--ui-color-border); background: var(--ui-color-surface-1); }
.hud__identity { display: flex; min-width: 0; flex-direction: column; justify-content: center; }
.hud__identity b { overflow: hidden; font-family: var(--ui-font-display); text-overflow: ellipsis; white-space: nowrap; }
.hud__identity small { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.hud__bars { display: grid; gap: var(--ui-space-1); }
.xp { position: relative; min-height: 1rem; overflow: hidden; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-round); background: var(--ui-color-surface-2); }
.xp > span { position: absolute; inset-block: 0; left: 0; background: linear-gradient(90deg, var(--ui-color-primary), var(--ui-color-secondary)); }
.xp small { position: relative; z-index: 1; display: block; color: white; font-size: .62rem; line-height: .9rem; text-align: center; text-shadow: 0 1px 2px black; }
.content { min-height: 0; overflow-y: auto; overscroll-behavior: contain; }
.content > :deep(.ui-system-state) { min-height: 100%; border: 0; }
.navigation { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); padding: var(--ui-space-1) calc(var(--ui-space-1) + var(--ui-safe-area-right)) calc(var(--ui-space-1) + var(--ui-safe-area-bottom)) calc(var(--ui-space-1) + var(--ui-safe-area-left)); border-top: 1px solid var(--ui-color-border); background: var(--ui-color-surface-1); }
.navigation__item { display: grid; min-width: var(--ui-touch-target); min-height: var(--ui-control-height-lg); place-items: center; align-content: center; gap: var(--ui-space-1); padding: var(--ui-space-1); border: 0; background: transparent; color: var(--ui-color-text-muted); font: inherit; cursor: pointer; }
.navigation__item:disabled { color: var(--ui-color-disabled); cursor: not-allowed; opacity: .5; }
.navigation__item--active { color: var(--ui-color-primary); }
.navigation__icon { width: calc(var(--ui-space-6) + var(--ui-space-1)); height: calc(var(--ui-space-6) + var(--ui-space-1)); object-fit: contain; filter: saturate(.8); }
.navigation__item--active .navigation__icon { filter: saturate(1.15) drop-shadow(0 0 .35rem rgb(92 110 255 / 55%)); }
.navigation small { font-size: var(--ui-font-size-xs); }
@media (max-width: 350px) { .navigation small { font-size: var(--ui-font-size-xs); transform: scale(.9); } }
@media (min-width: 542px) { .game-shell { box-shadow: var(--ui-shadow-panel); } }
</style>
