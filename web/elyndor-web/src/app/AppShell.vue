<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'

import { gameArt } from '@/assets/gameArt'
import { classLabel, resourceLabel } from '@/game/character/characterPresentation'
import CharacterCreationView from '@/game/character/views/CharacterCreationView.vue'
import HeroView from '@/game/character/views/HeroView.vue'
import CombatView from '@/game/combat/views/CombatView.vue'
import MenuView, { type MenuSection } from '@/game/menu/views/MenuView.vue'
import QuestView from '@/game/quests/views/QuestView.vue'
import WorldMapView from '@/game/world/views/WorldMapView.vue'
import WorldView from '@/game/world/views/WorldView.vue'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'
import { initializeTelegramWebApp } from '@/telegram/telegramWebApp'
import { UIButton, UIHealthBar, UILoadingState } from '@/ui/components'

type ShellView = 'world' | 'location' | 'hero' | 'quests' | 'menu'

const session = useGameSessionStore()
const combat = useCombatSessionStore()
const activeView = ref<ShellView>('location')
const menuSection = ref<MenuSection>('profile')
const character = computed(() => session.snapshot?.character)
const currentLocation = computed(() => session.snapshot?.world?.currentLocation ?? null)
const activeTravel = computed(() => session.snapshot?.world?.travel ?? null)
const portraitArt = computed(() =>
  character.value?.classId === 'WARRIOR' ? gameArt.characters.warrior : null,
)
function worldLocationName(locationId: string): string {
  if (locationId === 'STARTER_TOWN') return 'Стартовый город'
  if (locationId === 'WHISPERING_FOREST') return 'Шепчущий лес'
  if (locationId === 'DEEP_FOREST') return 'Глубокий лес'
  if (locationId === 'ANCIENT_MINE') return 'Древняя шахта'
  if (locationId === 'BROODMOTHER_LAIR') return 'Логово Прародительницы'
  if (locationId === 'BLIGHTED_GROVE') return 'Осквернённая чаща'
  return locationId
}

const locationName = computed(() => {
  if (activeTravel.value) {
    return `В пути → ${worldLocationName(activeTravel.value.targetLocationId)}`
  }
  const location = currentLocation.value
  if (!location) return 'Неизвестная область'
  return worldLocationName(location.id) === location.id
    ? location.displayName
    : worldLocationName(location.id)
})
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
  if (code === 'internal_server_error' || code === 'http_500') {
    const trace = session.errorCorrelationId
      ? ` ID запроса: ${session.errorCorrelationId}`
      : ''
    return `Сервер не смог восстановить состояние игры.${trace}`
  }
  const trace = session.errorCorrelationId
    ? ` · ID: ${session.errorCorrelationId}`
    : ''
  return `Не удалось продолжить игру. Код ошибки: ${code}${trace}`
})

const navigation: readonly {
  id: ShellView
  label: string
  icon: string
  enabled: boolean
  primary?: boolean
}[] = [
  { id: 'world', label: 'Мир', icon: gameArt.navigation.world, enabled: true },
  { id: 'hero', label: 'Герой', icon: gameArt.navigation.hero, enabled: true },
  { id: 'location', label: 'Локация', icon: gameArt.navigation.location, enabled: true, primary: true },
  { id: 'quests', label: 'Квесты', icon: gameArt.navigation.quests, enabled: true },
  { id: 'menu', label: 'Меню', icon: gameArt.navigation.menu, enabled: true },
]

function selectView(item: (typeof navigation)[number]) {
  if (item.enabled) {
    activeView.value = item.id
    if (item.id === 'menu') menuSection.value = 'profile'
  }
}

function openMenu(section: MenuSection): void {
  menuSection.value = section
  activeView.value = 'menu'
}

watch(() => session.state, async state => {
  if (state !== 'world') return
  try {
    await combat.connect()
    await combat.resume()
  } catch {
    // The combat store retains the connection error and supports retry/reconnect.
  }
}, { immediate: true })

watch(() => combat.isActive, (active, wasActive) => {
  if (!active && wasActive) void session.refreshSnapshot()
})

onMounted(() => {
  initializeTelegramWebApp()
  void session.start()
})
</script>

<template>
  <div class="game-shell">
    <section v-if="session.state === 'world' && character && !combat.isActive" class="hud" aria-label="Состояние героя">
      <div class="hud__main">
        <button class="hud__portrait" type="button" aria-label="Открыть героя" @click="activeView = 'hero'">
          <img v-if="portraitArt" :src="portraitArt" alt="" aria-hidden="true" />
          <span v-else>{{ character.name.slice(0, 1).toUpperCase() }}</span>
        </button>

        <button class="hud__identity" type="button" @click="activeView = 'hero'">
          <small class="hud__brand">ELYNDOR</small>
          <b>{{ character.name }}</b>
          <span>ур. {{ character.level }} · {{ classLabel(character.classId) }}</span>
          <small class="hud__code">{{ character.publicCode ?? 'ELY ID недоступен' }}</small>
        </button>

        <div class="hud__meta">
          <div class="hud__wallet" aria-label="Золото">
            <span aria-hidden="true">●</span>
            <strong>{{ character.gold }}</strong>
          </div>
          <div class="server-state" :data-state="session.state" aria-live="polite">
            <i aria-hidden="true" /><span>{{ connectionLabel }}</span>
          </div>
          <RouterLink v-if="session.isAdmin" class="admin-link" to="/admin">Админка</RouterLink>
        </div>
      </div>

      <div class="hud__bars">
        <UIHealthBar label="Здоровье" :value="character.vitals.currentHp" :max="character.vitals.maxHp" />
        <UIHealthBar :label="resourceName" :tone="resourceTone" :value="character.vitals.currentResource" :max="character.vitals.maxResource" />
      </div>

      <div class="hud__context">
        <button type="button" data-hud-location @click="activeView = 'location'">
          <img :src="gameArt.navigation.location" alt="" aria-hidden="true" />
          <span>{{ locationName }}</span>
        </button>
        <div
          class="xp"
          role="progressbar"
          aria-label="Опыт"
          :aria-valuenow="character.experience"
          :aria-valuemax="character.xpToNextLevel || 1"
        >
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
      <CombatView v-else-if="session.state === 'world' && combat.isActive" @leave="activeView = 'location'" />
      <WorldMapView
        v-else-if="session.state === 'world' && activeView === 'world'"
        @open-location="activeView = 'location'"
      />
      <WorldView
        v-else-if="session.state === 'world' && activeView === 'location'"
        @open-party="openMenu('party')"
      />
      <HeroView v-else-if="session.state === 'world' && activeView === 'hero'" />
      <QuestView v-else-if="session.state === 'world' && activeView === 'quests'" />
      <MenuView
        v-else-if="session.state === 'world' && activeView === 'menu'"
        :initial-section="menuSection"
      />
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
  grid-template-rows: auto minmax(0, 1fr) auto;
  overflow: hidden;
  border-inline: 1px solid var(--ui-color-border);
  background:
    radial-gradient(circle at 50% -4rem, rgb(146 136 255 / 10%), transparent 22rem),
    rgb(5 7 13 / 98%);
  color: var(--ui-color-text-primary);
}

.hud {
  grid-row: 1;
  position: relative;
  z-index: 3;
  display: grid;
  gap: 6px;
  padding:
    calc(7px + var(--ui-safe-area-top))
    calc(var(--ui-space-3) + var(--ui-safe-area-right))
    7px
    calc(var(--ui-space-3) + var(--ui-safe-area-left));
  border-bottom: 1px solid rgb(255 255 255 / 7%);
  background:
    radial-gradient(circle at 18% 22%, rgb(146 136 255 / 11%), transparent 11rem),
    linear-gradient(180deg, rgb(15 21 34 / 99%), rgb(8 12 20 / 97%));
  box-shadow: 0 10px 26px rgb(0 0 0 / 20%);
}

.hud::after {
  position: absolute;
  right: 13%;
  bottom: -1px;
  left: 13%;
  height: 1px;
  background: linear-gradient(90deg, transparent, rgb(146 136 255 / 34%), transparent);
  content: '';
  pointer-events: none;
}

.hud__main {
  display: grid;
  grid-template-columns: 46px minmax(0, 1fr) auto;
  align-items: center;
  gap: 9px;
}

.hud__portrait {
  position: relative;
  display: grid;
  width: 46px;
  height: 46px;
  place-items: center;
  overflow: hidden;
  padding: 0;
  border: 1px solid color-mix(in srgb, var(--ui-color-primary) 45%, var(--ui-color-border));
  border-radius: 50%;
  background:
    radial-gradient(circle at 50% 25%, rgb(146 136 255 / 17%), transparent 58%),
    rgb(7 10 17 / 95%);
  box-shadow:
    inset 0 0 0 2px rgb(255 255 255 / 3%),
    0 0 14px rgb(99 87 211 / 12%);
  color: #dedbff;
  font: 700 1rem var(--ui-font-display);
}

.hud__portrait img {
  width: 125%;
  height: 125%;
  object-fit: cover;
  object-position: 50% 18%;
  transform: translateY(8%);
}

.hud__identity {
  display: grid;
  min-width: 0;
  gap: 0;
  padding: 0;
  border: 0;
  background: transparent;
  color: inherit;
  font: inherit;
  text-align: left;
}

.hud__brand {
  margin-bottom: 1px;
  color: #9991ec;
  font-size: .46rem;
  font-weight: 800;
  letter-spacing: .17em;
}

.hud__identity b {
  overflow: hidden;
  font-family: var(--ui-font-display);
  font-size: .9rem;
  line-height: 1.08;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.hud__identity > span {
  margin-top: 2px;
  color: var(--ui-color-text-muted);
  font-size: .57rem;
}

.hud__code {
  margin-top: 2px;
  color: #aaa5e8;
  font-size: .5rem;
  letter-spacing: .08em;
}

.hud__meta {
  display: grid;
  justify-items: end;
  gap: 3px;
}

.hud__wallet {
  display: flex;
  align-items: center;
  gap: 4px;
  min-height: 24px;
  padding: 3px 7px;
  border: 1px solid rgb(232 200 102 / 18%);
  border-radius: var(--ui-radius-round);
  background: rgb(232 200 102 / 5%);
  color: var(--ui-color-gold);
  font-size: .64rem;
  font-variant-numeric: tabular-nums;
}

.server-state {
  display: flex;
  align-items: center;
  gap: 4px;
  color: var(--ui-color-text-muted);
  font-size: .49rem;
  white-space: nowrap;
}

.server-state i {
  width: 5px;
  height: 5px;
  border-radius: 50%;
  background: var(--ui-color-warning);
}

.server-state[data-state='world'] i {
  background: var(--ui-color-success);
  box-shadow: 0 0 8px rgb(79 185 150 / 45%);
}

.server-state[data-state='offline'] i,
.server-state[data-state='error'] i {
  background: var(--ui-color-danger);
}

.admin-link {
  color: #a9a2f4;
  font-size: .46rem;
  text-decoration: none;
}

.hud__bars {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 6px;
}

.hud__context {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr);
  align-items: center;
  gap: 8px;
}

.hud__context > button {
  display: flex;
  min-width: 0;
  max-width: 9.5rem;
  align-items: center;
  gap: 4px;
  padding: 0;
  border: 0;
  background: transparent;
  color: var(--ui-color-text-muted);
  font: inherit;
  font-size: .52rem;
}

.hud__context > button img {
  width: 15px;
  height: 15px;
  object-fit: contain;
  opacity: .78;
}

.hud__context > button span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.xp {
  position: relative;
  min-height: 8px;
  overflow: hidden;
  border: 1px solid rgb(255 255 255 / 5%);
  border-radius: var(--ui-radius-round);
  background: rgb(2 4 8 / 76%);
  box-shadow: inset 0 1px 3px rgb(0 0 0 / 45%);
}

.xp > span {
  position: absolute;
  inset-block: 0;
  left: 0;
  background: linear-gradient(90deg, #5b55bd, var(--ui-color-primary), #72b7c9);
}

.xp small {
  position: relative;
  z-index: 1;
  display: block;
  color: rgb(242 244 255 / 80%);
  font-size: .44rem;
  font-weight: 700;
  line-height: 6px;
  text-align: center;
  text-shadow: 0 1px 2px black;
}

.content {
  grid-row: 2;
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
  grid-row: 3;
  position: relative;
  z-index: 4;
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 1px;
  padding:
    3px
    calc(4px + var(--ui-safe-area-right))
    calc(4px + var(--ui-safe-area-bottom))
    calc(4px + var(--ui-safe-area-left));
  border-top: 1px solid rgb(255 255 255 / 8%);
  background:
    radial-gradient(circle at 50% 0, rgb(146 136 255 / 9%), transparent 7rem),
    linear-gradient(180deg, rgb(12 17 28 / 98%), rgb(5 8 14 / 100%));
  box-shadow: 0 -12px 30px rgb(0 0 0 / 28%);
}

.navigation__item {
  position: relative;
  display: grid;
  min-width: 0;
  min-height: 54px;
  place-items: center;
  align-content: center;
  gap: 1px;
  padding: 1px 2px;
  border: 0;
  border-radius: var(--ui-radius-md);
  background: transparent;
  color: var(--ui-color-text-muted);
  font: inherit;
  cursor: pointer;
  transition:
    color var(--ui-transition-fast),
    background var(--ui-transition-fast),
    transform var(--ui-transition-fast);
}

.navigation__item::after {
  position: absolute;
  right: 31%;
  bottom: 1px;
  left: 31%;
  height: 2px;
  border-radius: var(--ui-radius-round);
  background: transparent;
  content: '';
}

.navigation__item:disabled {
  color: var(--ui-color-disabled);
  cursor: not-allowed;
  opacity: .28;
}

.navigation__item--active {
  background: linear-gradient(180deg, rgb(146 136 255 / 10%), transparent 76%);
  color: #d5d1ff;
}

.navigation__item--active::after {
  background: var(--ui-color-primary);
  box-shadow: 0 0 9px rgb(146 136 255 / 55%);
}

.navigation__item--primary .navigation__icon-wrap {
  width: 38px;
  height: 38px;
  margin-top: -13px;
  border: 1px solid color-mix(in srgb, var(--ui-color-primary) 55%, var(--ui-color-border));
  border-radius: 50%;
  background:
    radial-gradient(circle at 45% 25%, rgb(146 136 255 / 18%), transparent 55%),
    linear-gradient(180deg, rgb(28 31 55 / 100%), rgb(8 12 21 / 100%));
  box-shadow:
    0 -6px 16px rgb(0 0 0 / 28%),
    0 0 14px rgb(146 136 255 / 15%);
}

.navigation__item:active:not(:disabled) {
  transform: scale(.96);
}

.navigation__icon-wrap {
  display: grid;
  width: 30px;
  height: 30px;
  place-items: center;
}

.navigation__icon {
  width: 25px;
  height: 25px;
  object-fit: contain;
  filter: grayscale(.2) saturate(.76) brightness(.86);
  transition: filter var(--ui-transition-fast), transform var(--ui-transition-fast);
}

.navigation__item--primary .navigation__icon {
  width: 28px;
  height: 28px;
}

.navigation__item--active .navigation__icon {
  filter: saturate(1.15) brightness(1.1) drop-shadow(0 0 .38rem rgb(112 100 245 / 52%));
  transform: translateY(-1px);
}

.navigation small {
  overflow: hidden;
  max-width: 100%;
  font-size: .54rem;
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

@media (max-width: 360px) {
  .hud {
    padding-inline: var(--ui-space-2);
  }

  .server-state span {
    display: none;
  }

  .hud__context > button {
    max-width: 7.5rem;
  }
}

@media (min-width: 582px) {
  .game-shell {
    border-inline: 1px solid var(--ui-color-border-strong);
    box-shadow: 0 0 0 1px rgb(255 255 255 / 2%), 0 24px 70px rgb(0 0 0 / 42%);
  }
}
</style>
