<script setup lang="ts">
import { computed, ref } from 'vue'

import { useGameSessionStore } from '@/stores/gameSession'
import {
  type MotionPreference,
  useUiPreferencesStore,
} from '@/stores/uiPreferences'
import { UIButton } from '@/ui/components'

const session = useGameSessionStore()
const preferences = useUiPreferencesStore()
const syncing = ref(false)
const syncResult = ref<'idle' | 'success' | 'error'>('idle')

const connectionLabel = computed(() => {
  if (session.state === 'world') return 'Онлайн'
  if (session.state === 'offline' || session.state === 'error') return 'Нет связи'
  return 'Синхронизация'
})

const contentVersion = computed(() => session.snapshot?.contentVersion ?? '—')
const balanceVersion = computed(() => session.snapshot?.balanceVersion ?? '—')

const motionOptions: readonly {
  value: MotionPreference
  label: string
  description: string
}[] = [
  {
    value: 'system',
    label: 'Система',
    description: 'Следовать настройке Reduced Motion устройства.',
  },
  {
    value: 'reduced',
    label: 'Меньше',
    description: 'Отключить декоративные анимации Elyndor.',
  },
  {
    value: 'full',
    label: 'Полно',
    description: 'Разрешить все доступные декоративные анимации.',
  },
]

async function syncWorld(): Promise<void> {
  if (syncing.value) return
  syncing.value = true
  syncResult.value = 'idle'
  try {
    await session.refreshSnapshot()
    syncResult.value = 'success'
  } catch {
    syncResult.value = 'error'
  } finally {
    syncing.value = false
  }
}
</script>

<template>
  <section class="menu-view">
    <header class="menu-header">
      <div>
        <small>ELYNDOR · СИСТЕМА</small>
        <h1>Меню</h1>
        <p>Настройки интерфейса и состояние текущей игровой сессии.</p>
      </div>
      <span class="menu-header__state" :data-state="session.state">{{ connectionLabel }}</span>
    </header>

    <section class="menu-card" aria-labelledby="graphics-settings-title">
      <header class="menu-card__heading">
        <div>
          <small>ГРАФИКА</small>
          <strong id="graphics-settings-title">Представление мира</strong>
        </div>
        <span>Локально</span>
      </header>

      <div class="setting-row">
        <div class="setting-row__copy">
          <strong>Атмосферные эффекты</strong>
          <p>Туман, декоративное свечение и фоновые эффекты карты.</p>
        </div>
        <button
          class="setting-switch"
          data-setting-atmosphere
          type="button"
          role="switch"
          :aria-checked="preferences.atmosphereEnabled"
          @click="preferences.setAtmosphereEnabled(!preferences.atmosphereEnabled)"
        >
          <span />
          <b>{{ preferences.atmosphereEnabled ? 'Вкл.' : 'Выкл.' }}</b>
        </button>
      </div>

      <div class="setting-block">
        <div class="setting-row__copy">
          <strong>Движение интерфейса</strong>
          <p>
            {{ motionOptions.find(option => option.value === preferences.motionPreference)?.description }}
          </p>
        </div>
        <div class="motion-options" role="group" aria-label="Движение интерфейса">
          <button
            v-for="option in motionOptions"
            :key="option.value"
            type="button"
            :data-motion-option="option.value"
            :class="{ active: preferences.motionPreference === option.value }"
            :aria-pressed="preferences.motionPreference === option.value"
            @click="preferences.setMotionPreference(option.value)"
          >
            {{ option.label }}
          </button>
        </div>
      </div>
    </section>

    <section class="menu-card" aria-labelledby="system-state-title">
      <header class="menu-card__heading">
        <div>
          <small>СИСТЕМА</small>
          <strong id="system-state-title">Игровая сессия</strong>
        </div>
        <span :data-online="session.state === 'world'">{{ connectionLabel }}</span>
      </header>

      <dl class="system-grid">
        <div>
          <dt>Контент</dt>
          <dd>{{ contentVersion }}</dd>
        </div>
        <div>
          <dt>Баланс</dt>
          <dd>{{ balanceVersion }}</dd>
        </div>
        <div>
          <dt>Персонаж</dt>
          <dd>{{ session.snapshot?.character?.name ?? '—' }}</dd>
        </div>
        <div>
          <dt>Состояние</dt>
          <dd>{{ connectionLabel }}</dd>
        </div>
      </dl>

      <div class="sync-row">
        <div>
          <strong>Синхронизировать состояние</strong>
          <p>Повторно получить authoritative snapshot героя, инвентаря и мира.</p>
          <small v-if="syncResult === 'success'" data-sync-result="success">Состояние обновлено.</small>
          <small v-else-if="syncResult === 'error'" data-sync-result="error">Не удалось обновить состояние.</small>
        </div>
        <UIButton
          data-sync-world
          variant="secondary"
          :loading="syncing"
          @click="syncWorld"
        >
          Обновить
        </UIButton>
      </div>
    </section>

    <section class="menu-card menu-card--help" aria-labelledby="help-title">
      <header class="menu-card__heading">
        <div>
          <small>ПОМОЩЬ</small>
          <strong id="help-title">Если что-то пошло не так</strong>
        </div>
      </header>
      <p>
        При потере связи Elyndor не считает локальный экран источником истины.
        После восстановления соединения игра заново получает состояние с сервера.
      </p>
    </section>
  </section>
</template>

<style scoped>
.menu-view {
  display: grid;
  width: min(100%, var(--ui-content-width));
  margin-inline: auto;
  gap: var(--ui-space-3);
  padding: var(--ui-space-3) var(--ui-space-4) var(--ui-space-7);
}

.menu-header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: var(--ui-space-3);
  padding: var(--ui-space-2) var(--ui-space-1);
}

.menu-header > div {
  display: grid;
  gap: var(--ui-space-1);
}

.menu-header small,
.menu-card__heading small {
  color: #aaa3ff;
  font-size: .56rem;
  font-weight: 800;
  letter-spacing: .09em;
}

.menu-header h1,
.menu-header p,
.setting-row__copy p,
.sync-row p,
.menu-card--help > p {
  margin: 0;
}

.menu-header h1 {
  font-family: var(--ui-font-display);
  font-size: clamp(1.55rem, 7vw, 2rem);
}

.menu-header p,
.setting-row__copy p,
.sync-row p,
.menu-card--help > p {
  color: var(--ui-color-text-muted);
  font-size: .68rem;
  line-height: 1.45;
}

.menu-header__state {
  padding: 5px 8px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-round);
  color: var(--ui-color-text-muted);
  font-size: .56rem;
  white-space: nowrap;
}

.menu-header__state[data-state='world'] {
  border-color: rgb(79 185 150 / 30%);
  color: #84d5bb;
}

.menu-card {
  overflow: hidden;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-lg);
  background:
    linear-gradient(180deg, rgb(13 18 30 / 82%), rgb(6 9 16 / 88%));
  box-shadow: var(--ui-shadow-inset);
}

.menu-card__heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ui-space-3);
  padding: var(--ui-space-3) var(--ui-space-4);
  border-bottom: 1px solid rgb(255 255 255 / 6%);
  background: rgb(255 255 255 / 1.5%);
}

.menu-card__heading > div {
  display: grid;
  gap: 2px;
}

.menu-card__heading strong {
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-md);
}

.menu-card__heading > span {
  color: var(--ui-color-text-muted);
  font-size: .54rem;
}

.menu-card__heading > span[data-online='true'] {
  color: #84d5bb;
}

.setting-row,
.setting-block,
.sync-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ui-space-4);
  padding: var(--ui-space-4);
  border-bottom: 1px solid rgb(255 255 255 / 6%);
}

.setting-block {
  align-items: flex-start;
  border-bottom: 0;
}

.setting-row__copy,
.sync-row > div {
  display: grid;
  min-width: 0;
  gap: 2px;
}

.setting-row__copy strong,
.sync-row strong {
  font-size: var(--ui-font-size-sm);
}

.setting-switch {
  display: grid;
  grid-template-columns: 2.2rem auto;
  align-items: center;
  gap: 6px;
  min-height: var(--ui-touch-target);
  padding: 5px 8px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-round);
  background: rgb(4 7 12 / 72%);
  color: var(--ui-color-text-muted);
  font: inherit;
  font-size: .58rem;
}

.setting-switch > span {
  position: relative;
  width: 2.2rem;
  height: 1.2rem;
  border-radius: var(--ui-radius-round);
  background: #303747;
}

.setting-switch > span::after {
  position: absolute;
  top: 2px;
  left: 2px;
  width: calc(1.2rem - 4px);
  height: calc(1.2rem - 4px);
  border-radius: 50%;
  background: #8b92a3;
  content: '';
  transition: transform var(--ui-transition-fast), background var(--ui-transition-fast);
}

.setting-switch[aria-checked='true'] {
  border-color: rgb(146 136 255 / 34%);
  color: #d5d1ff;
}

.setting-switch[aria-checked='true'] > span {
  background: rgb(146 136 255 / 30%);
}

.setting-switch[aria-checked='true'] > span::after {
  background: #b8b1ff;
  transform: translateX(1rem);
}

.motion-options {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  min-width: min(15rem, 48%);
  overflow: hidden;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
}

.motion-options button {
  min-height: var(--ui-touch-target);
  padding: 5px 8px;
  border: 0;
  border-right: 1px solid rgb(255 255 255 / 5%);
  background: transparent;
  color: var(--ui-color-text-muted);
  font: inherit;
  font-size: .58rem;
}

.motion-options button:last-child {
  border-right: 0;
}

.motion-options button.active {
  background: rgb(146 136 255 / 10%);
  color: #d5d1ff;
}

.system-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  margin: 0;
  border-bottom: 1px solid rgb(255 255 255 / 6%);
}

.system-grid > div {
  display: grid;
  gap: 2px;
  padding: var(--ui-space-3) var(--ui-space-4);
  border-right: 1px solid rgb(255 255 255 / 6%);
  border-bottom: 1px solid rgb(255 255 255 / 6%);
}

.system-grid > div:nth-child(2n) {
  border-right: 0;
}

.system-grid > div:nth-last-child(-n + 2) {
  border-bottom: 0;
}

.system-grid dt {
  color: var(--ui-color-text-muted);
  font-size: .52rem;
  text-transform: uppercase;
}

.system-grid dd {
  margin: 0;
  font-size: .68rem;
  font-weight: 700;
}

.sync-row {
  border-bottom: 0;
}

.sync-row small[data-sync-result='success'] {
  color: #84d5bb;
}

.sync-row small[data-sync-result='error'] {
  color: #ef9bab;
}

.menu-card--help > p {
  padding: var(--ui-space-4);
}

:global(html[data-elyndor-motion='reduced']) .setting-switch > span::after {
  transition: none;
}

@media (max-width: 480px) {
  .menu-view {
    padding-inline: var(--ui-space-3);
  }

  .menu-header {
    align-items: start;
    flex-direction: column;
  }

  .setting-row,
  .setting-block,
  .sync-row {
    display: grid;
    grid-template-columns: 1fr;
    gap: var(--ui-space-3);
    padding: var(--ui-space-3);
  }

  .setting-switch,
  .motion-options,
  .sync-row :deep(.ui-button) {
    width: 100%;
  }

  .motion-options {
    min-width: 0;
  }
}
</style>
