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
        <span class="hud__gold">{{ character.gold }} золота</span>
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
.game-shell {
  position: relative;
  display: grid;
  width: min(100%, var(--ui-content-width));
  height: var(--ui-viewport-height);
  margin-inline: auto;
  grid-template-rows: auto auto minmax(0, 1fr) auto;
  overflow: hidden;
  border-inline: 1px solid var(--ui-color-border);
  background:
    radial-gradient(circle at 50% 0, rgb(146 136 255 / 7%), transparent 17rem),
    rgb(5 7 13 / 96%);
  color: var(--ui-color-text-primary);
}

.game-shell__header {
  position: relative;
  z-index: 2;
  display: flex;
  min-height: var(--ui-control-height-lg);
  align-items: center;
  justify-content: space-between;
  gap: var(--ui-space-3);
  padding:
    calc(var(--ui-space-3) + var(--ui-safe-area-top))
    calc(var(--ui-space-4) + var(--ui-safe-area-right))
    var(--ui-space-3)
    calc(var(--ui-space-4) + var(--ui-safe-area-left));
  border-bottom: 1px solid var(--ui-color-border);
  background: linear-gradient(180deg, rgb(14 20 33 / 97%), rgb(8 12 21 / 94%));
  box-shadow: 0 8px 24px rgb(0 0 0 / 14%);
}

.game-shell__header::after {
  position: absolute;
  right: 18%;
  bottom: -1px;
  left: 18%;
  height: 1px;
  background: linear-gradient(90deg, transparent, rgb(146 136 255 / 55%), transparent);
  content: '';
}

.brand,
.subtitle {
  margin: 0;
}

.brand {
  background: linear-gradient(90deg, #d8d4ff, var(--ui-color-primary), #a8dcea);
  background-clip: text;
  color: transparent;
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-lg);
  font-weight: var(--ui-font-weight-bold);
  letter-spacing: .18em;
  line-height: 1;
}

.subtitle {
  margin-top: 4px;
  color: var(--ui-color-text-muted);
  font-size: .59rem;
  font-weight: var(--ui-font-weight-medium);
  letter-spacing: .12em;
  text-transform: uppercase;
}

.game-shell__actions {
  display: flex;
  align-items: center;
  gap: var(--ui-space-3);
}

.admin-link {
  padding: 5px 8px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
  background: rgb(255 255 255 / 2%);
  color: var(--ui-color-primary);
  font-size: var(--ui-font-size-xs);
  text-decoration: none;
}

.admin-link:hover {
  border-color: var(--ui-color-primary);
  background: rgb(146 136 255 / 7%);
}

.server-state {
  display: flex;
  align-items: center;
  gap: var(--ui-space-2);
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
  white-space: nowrap;
}

.server-state i {
  width: 7px;
  height: 7px;
  border-radius: var(--ui-radius-round);
  background: var(--ui-color-warning);
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--ui-color-warning) 10%, transparent);
}

.server-state[data-state='world'] i {
  background: var(--ui-color-success);
  box-shadow: 0 0 0 3px rgb(79 185 150 / 10%), 0 0 10px rgb(79 185 150 / 30%);
}

.server-state[data-state='offline'] i,
.server-state[data-state='error'] i {
  background: var(--ui-color-danger);
  box-shadow: 0 0 0 3px rgb(216 95 114 / 10%), var(--ui-glow-danger);
}

.hud {
  position: relative;
  z-index: 1;
  display: grid;
  grid-template-columns: minmax(6.5rem, auto) minmax(0, 1fr);
  gap: var(--ui-space-3);
  padding: var(--ui-space-2) var(--ui-space-4) var(--ui-space-3);
  border-bottom: 1px solid var(--ui-color-border);
  background: linear-gradient(180deg, rgb(15 22 36 / 92%), rgb(9 14 24 / 94%));
  box-shadow: 0 8px 22px rgb(0 0 0 / 12%);
}

.hud__identity {
  display: flex;
  min-width: 0;
  flex-direction: column;
  justify-content: center;
}

.hud__identity b {
  overflow: hidden;
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-md);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.hud__identity small {
  color: var(--ui-color-text-muted);
  font-size: .64rem;
}

.hud__gold {
  margin-top: 2px;
  color: var(--ui-color-gold);
  font-size: .65rem;
  font-weight: var(--ui-font-weight-semibold);
  font-variant-numeric: tabular-nums;
}

.hud__bars {
  display: grid;
  gap: 4px;
}

.xp {
  position: relative;
  min-height: 13px;
  overflow: hidden;
  border: 1px solid rgb(255 255 255 / 6%);
  border-radius: var(--ui-radius-round);
  background: rgb(2 4 8 / 76%);
  box-shadow: inset 0 1px 3px rgb(0 0 0 / 45%);
}

.xp > span {
  position: absolute;
  inset-block: 0;
  left: 0;
  background: linear-gradient(90deg, #645dc7, var(--ui-color-primary), var(--ui-color-secondary));
  box-shadow: 0 0 12px rgb(146 136 255 / 24%);
}

.xp small {
  position: relative;
  z-index: 1;
  display: block;
  color: rgb(242 244 255 / 92%);
  font-size: .57rem;
  font-weight: 600;
  line-height: 11px;
  text-align: center;
  text-shadow: 0 1px 2px black;
}

.content {
  min-height: 0;
  overflow-y: auto;
  overscroll-behavior: contain;
  scroll-behavior: smooth;
}

.content > :deep(.ui-system-state) {
  min-height: 100%;
  border: 0;
}

.navigation {
  position: relative;
  z-index: 3;
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 2px;
  padding:
    var(--ui-space-1)
    calc(var(--ui-space-2) + var(--ui-safe-area-right))
    calc(var(--ui-space-1) + var(--ui-safe-area-bottom))
    calc(var(--ui-space-2) + var(--ui-safe-area-left));
  border-top: 1px solid var(--ui-color-border);
  background: linear-gradient(180deg, rgb(13 19 31 / 96%), rgb(7 10 18 / 99%));
  box-shadow: 0 -12px 28px rgb(0 0 0 / 24%);
}

.navigation::before {
  position: absolute;
  top: -1px;
  right: 24%;
  left: 24%;
  height: 1px;
  background: linear-gradient(90deg, transparent, rgb(146 136 255 / 45%), transparent);
  content: '';
}

.navigation__item {
  position: relative;
  display: grid;
  min-width: var(--ui-touch-target);
  min-height: 56px;
  place-items: center;
  align-content: center;
  gap: 2px;
  padding: var(--ui-space-1);
  border: 1px solid transparent;
  border-radius: var(--ui-radius-md);
  background: transparent;
  color: var(--ui-color-text-muted);
  font: inherit;
  cursor: pointer;
  transition: color var(--ui-transition-fast), background var(--ui-transition-fast), transform var(--ui-transition-fast);
}

.navigation__item::after {
  position: absolute;
  right: 30%;
  bottom: 1px;
  left: 30%;
  height: 2px;
  border-radius: var(--ui-radius-round);
  background: transparent;
  content: '';
}

.navigation__item:disabled {
  color: var(--ui-color-disabled);
  cursor: not-allowed;
  opacity: .34;
}

.navigation__item--active {
  background: linear-gradient(180deg, rgb(146 136 255 / 11%), transparent);
  color: #c9c5ff;
}

.navigation__item--active::after {
  background: var(--ui-color-primary);
  box-shadow: 0 0 9px rgb(146 136 255 / 55%);
}

.navigation__item:active:not(:disabled) {
  transform: scale(.97);
}

.navigation__icon {
  width: 27px;
  height: 27px;
  object-fit: contain;
  filter: grayscale(.15) saturate(.72) brightness(.88);
  transition: filter var(--ui-transition-fast), transform var(--ui-transition-fast);
}

.navigation__item--active .navigation__icon {
  filter: saturate(1.12) brightness(1.08) drop-shadow(0 0 .4rem rgb(112 100 245 / 48%));
  transform: translateY(-1px);
}

.navigation small {
  font-size: .63rem;
  font-weight: var(--ui-font-weight-medium);
}

@media (max-width: 390px) {
  .game-shell__header {
    padding-inline: calc(var(--ui-space-3) + var(--ui-safe-area-left));
  }

  .server-state span {
    display: none;
  }

  .hud {
    grid-template-columns: 5.5rem minmax(0, 1fr);
    padding-inline: var(--ui-space-3);
  }
}

@media (min-width: 582px) {
  .game-shell {
    border-inline: 1px solid var(--ui-color-border-strong);
    box-shadow: 0 0 0 1px rgb(255 255 255 / 2%), 0 24px 70px rgb(0 0 0 / 42%);
  }
}
</style>
