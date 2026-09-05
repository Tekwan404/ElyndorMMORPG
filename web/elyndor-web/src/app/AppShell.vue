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

type ShellView = 'location' | 'hero'

const session = useGameSessionStore()
const combat = useCombatSessionStore()
const activeView = ref<ShellView>('location')
const character = computed(() => session.snapshot?.character)
const resourceTone = computed<'rage' | 'focus' | 'mana'>(() => {
  const value = character.value?.vitals.resourceType.toLowerCase()
  return value === 'rage' || value === 'mana' ? value : 'focus'
})
const resourceName = computed(() => resourceLabel(character.value?.vitals.resourceType ?? ''))
const connectionLabel = computed(() => {
  if (session.state === 'world') return 'Онлайн'
  if (session.state === 'offline' || session.state === 'error') return 'Нет связи'
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
  id: ShellView | 'world' | 'quests' | 'menu'
  label: string
  icon: string
  enabled: boolean
  primary?: boolean
}[] = [
  { id: 'world', label: 'Мир', icon: gameArt.navigation.world, enabled: false },
  { id: 'hero', label: 'Герой', icon: gameArt.navigation.hero, enabled: true },
  { id: 'location', label: 'Локация', icon: gameArt.navigation.location, enabled: true, primary: true },
  { id: 'quests', label: 'Квесты', icon: gameArt.navigation.quests, enabled: false },
  { id: 'menu', label: 'Меню', icon: gameArt.navigation.menu, enabled: false },
]

function selectView(item: (typeof navigation)[number]) {
  if (item.enabled && (item.id === 'location' || item.id === 'hero')) {
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
      <div class="brand-lockup">
        <p class="brand">ELYNDOR</p>
        <small>Telegram MMORPG</small>
      </div>
      <div class="game-shell__actions">
        <RouterLink v-if="session.isAdmin" class="admin-link" to="/admin">Admin</RouterLink>
        <div class="server-state" :data-state="session.state" aria-live="polite">
          <i aria-hidden="true" /><span>{{ connectionLabel }}</span>
        </div>
      </div>
    </header>

    <section v-if="session.state === 'world' && character && !combat.isActive" class="hud" aria-label="Состояние героя">
      <div class="hud__topline">
        <button class="hud__portrait" type="button" aria-label="Открыть героя" @click="activeView = 'hero'">
          {{ character.name.slice(0, 1).toUpperCase() }}
        </button>
        <button class="hud__identity" type="button" @click="activeView = 'hero'">
          <b>{{ character.name }}</b>
          <small>ур. {{ character.level }} · {{ classLabel(character.classId) }}</small>
        </button>
        <div class="hud__wallet" aria-label="Валюта">
          <span aria-hidden="true">●</span>
          <strong>{{ character.gold }}</strong>
        </div>
      </div>

      <div class="hud__bars">
        <UIHealthBar label="Здоровье" :value="character.vitals.currentHp" :max="character.vitals.maxHp" />
        <UIHealthBar :label="resourceName" :tone="resourceTone" :value="character.vitals.currentResource" :max="character.vitals.maxResource" />
        <div class="xp" role="progressbar" aria-label="Опыт" :aria-valuenow="character.experience" :aria-valuemax="character.xpToNextLevel || 1">
          <span :style="{ width: `${character.xpToNextLevel ? Math.min(100, character.experience / character.xpToNextLevel * 100) : 100}%` }" />
          <small>{{ character.experience }} / {{ character.xpToNextLevel || 'МАКС.' }} XP</small>
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
      <WorldView v-else-if="session.state === 'world' && activeView === 'location'" />
      <HeroView v-else-if="session.state === 'world' && activeView === 'hero'" />
    </main>

    <nav v-if="session.state === 'world' && !combat.isActive" class="navigation" aria-label="Основная навигация">
      <button
        v-for="item in navigation"
        :key="item.id"
        class="navigation__item"
        :class="{
          'navigation__item--active': item.id === activeView,
          'navigation__item--primary': item.primary,
        }"
        :data-nav="item.id"
        type="button"
        :disabled="!item.enabled"
        :aria-current="item.id === activeView ? 'page' : undefined"
        @click="selectView(item)"
      >
        <span class="navigation__icon-wrap">
          <img class="navigation__icon" :src="item.icon" alt="" aria-hidden="true" />
        </span>
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
  min-height: 42px;
  align-items: center;
  justify-content: space-between;
  gap: var(--ui-space-3);
  padding:
    calc(var(--ui-space-2) + var(--ui-safe-area-top))
    calc(var(--ui-space-3) + var(--ui-safe-area-right))
    var(--ui-space-2)
    calc(var(--ui-space-3) + var(--ui-safe-area-left));
  border-bottom: 1px solid rgb(255 255 255 / 5%);
  background: linear-gradient(180deg, rgb(13 19 31 / 98%), rgb(8 12 21 / 96%));
}

.brand-lockup {
  display: flex;
  align-items: baseline;
  gap: var(--ui-space-2);
}

.brand,
.brand-lockup small {
  margin: 0;
}

.brand {
  background: linear-gradient(90deg, #d8d4ff, var(--ui-color-primary), #a8dcea);
  background-clip: text;
  color: transparent;
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-md);
  font-weight: var(--ui-font-weight-bold);
  letter-spacing: .16em;
  line-height: 1;
}

.brand-lockup small {
  color: var(--ui-color-text-muted);
  font-size: .54rem;
  letter-spacing: .08em;
  text-transform: uppercase;
}

.game-shell__actions {
  display: flex;
  align-items: center;
  gap: var(--ui-space-2);
}

.admin-link {
  padding: 4px 7px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
  color: var(--ui-color-primary);
  font-size: .65rem;
  text-decoration: none;
}

.server-state {
  display: flex;
  align-items: center;
  gap: 5px;
  color: var(--ui-color-text-muted);
  font-size: .62rem;
  white-space: nowrap;
}

.server-state i {
  width: 6px;
  height: 6px;
  border-radius: var(--ui-radius-round);
  background: var(--ui-color-warning);
}

.server-state[data-state='world'] i {
  background: var(--ui-color-success);
  box-shadow: 0 0 9px rgb(79 185 150 / 40%);
}

.server-state[data-state='offline'] i,
.server-state[data-state='error'] i {
  background: var(--ui-color-danger);
  box-shadow: var(--ui-glow-danger);
}

.hud {
  position: relative;
  z-index: 1;
  display: grid;
  gap: var(--ui-space-2);
  padding: var(--ui-space-2) var(--ui-space-3) var(--ui-space-3);
  border-bottom: 1px solid var(--ui-color-border);
  background:
    radial-gradient(circle at 12% 50%, rgb(146 136 255 / 9%), transparent 10rem),
    linear-gradient(180deg, rgb(15 22 36 / 96%), rgb(9 14 24 / 96%));
  box-shadow: 0 8px 22px rgb(0 0 0 / 14%);
}

.hud__topline {
  display: grid;
  grid-template-columns: 2.3rem minmax(0, 1fr) auto;
  align-items: center;
  gap: var(--ui-space-2);
}

.hud__portrait {
  display: grid;
  width: 2.3rem;
  height: 2.3rem;
  place-items: center;
  padding: 0;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: 50%;
  background:
    radial-gradient(circle at 35% 25%, rgb(255 255 255 / 10%), transparent 36%),
    var(--ui-color-surface-2);
  box-shadow: 0 0 0 2px rgb(146 136 255 / 8%);
  color: #d8d4ff;
  font: inherit;
  font-family: var(--ui-font-display);
  font-weight: 700;
}

.hud__identity {
  display: grid;
  min-width: 0;
  gap: 1px;
  padding: 0;
  border: 0;
  background: transparent;
  color: inherit;
  font: inherit;
  text-align: left;
}

.hud__identity b {
  overflow: hidden;
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-sm);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.hud__identity small {
  color: var(--ui-color-text-muted);
  font-size: .61rem;
}

.hud__wallet {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 5px 8px;
  border: 1px solid rgb(232 200 102 / 16%);
  border-radius: var(--ui-radius-round);
  background: rgb(232 200 102 / 5%);
  color: var(--ui-color-gold);
  font-size: .69rem;
  font-variant-numeric: tabular-nums;
}

.hud__bars {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 5px var(--ui-space-2);
}

.xp {
  position: relative;
  grid-column: 1 / -1;
  min-height: 11px;
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
}

.xp small {
  position: relative;
  z-index: 1;
  display: block;
  color: rgb(242 244 255 / 88%);
  font-size: .53rem;
  font-weight: 600;
  line-height: 9px;
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
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 1px;
  padding:
    4px
    calc(var(--ui-space-1) + var(--ui-safe-area-right))
    calc(4px + var(--ui-safe-area-bottom))
    calc(var(--ui-space-1) + var(--ui-safe-area-left));
  border-top: 1px solid var(--ui-color-border);
  background: linear-gradient(180deg, rgb(13 19 31 / 97%), rgb(7 10 18 / 99%));
  box-shadow: 0 -12px 28px rgb(0 0 0 / 24%);
}

.navigation__item {
  position: relative;
  display: grid;
  min-width: 0;
  min-height: 54px;
  place-items: center;
  align-content: center;
  gap: 1px;
  padding: 2px;
  border: 0;
  border-radius: var(--ui-radius-md);
  background: transparent;
  color: var(--ui-color-text-muted);
  font: inherit;
  cursor: pointer;
  transition: color var(--ui-transition-fast), background var(--ui-transition-fast), transform var(--ui-transition-fast);
}

.navigation__item::after {
  position: absolute;
  right: 31%;
  bottom: 0;
  left: 31%;
  height: 2px;
  border-radius: var(--ui-radius-round);
  background: transparent;
  content: '';
}

.navigation__item:disabled {
  color: var(--ui-color-disabled);
  cursor: not-allowed;
  opacity: .36;
}

.navigation__item--active {
  background: linear-gradient(180deg, rgb(146 136 255 / 11%), transparent);
  color: #d0ccff;
}

.navigation__item--active::after {
  background: var(--ui-color-primary);
  box-shadow: 0 0 9px rgb(146 136 255 / 55%);
}

.navigation__item--primary .navigation__icon-wrap {
  width: 34px;
  height: 34px;
  margin-top: -9px;
  border: 1px solid color-mix(in srgb, var(--ui-color-primary) 45%, var(--ui-color-border));
  border-radius: 50%;
  background: linear-gradient(180deg, rgb(28 31 55 / 98%), rgb(10 14 25 / 98%));
  box-shadow: 0 -6px 18px rgb(0 0 0 / 26%), 0 0 12px rgb(146 136 255 / 10%);
}

.navigation__item:active:not(:disabled) {
  transform: scale(.97);
}

.navigation__icon-wrap {
  display: grid;
  width: 29px;
  height: 29px;
  place-items: center;
}

.navigation__icon {
  width: 25px;
  height: 25px;
  object-fit: contain;
  filter: grayscale(.18) saturate(.72) brightness(.88);
  transition: filter var(--ui-transition-fast), transform var(--ui-transition-fast);
}

.navigation__item--active .navigation__icon {
  filter: saturate(1.12) brightness(1.08) drop-shadow(0 0 .4rem rgb(112 100 245 / 48%));
  transform: translateY(-1px);
}

.navigation small {
  overflow: hidden;
  max-width: 100%;
  font-size: .56rem;
  font-weight: var(--ui-font-weight-medium);
  text-overflow: ellipsis;
  white-space: nowrap;
}

@media (max-width: 390px) {
  .brand-lockup small,
  .server-state span {
    display: none;
  }

  .hud {
    padding-inline: var(--ui-space-2);
  }
}

@media (min-width: 582px) {
  .game-shell {
    border-inline: 1px solid var(--ui-color-border-strong);
    box-shadow: 0 0 0 1px rgb(255 255 255 / 2%), 0 24px 70px rgb(0 0 0 / 42%);
  }
}
</style>
