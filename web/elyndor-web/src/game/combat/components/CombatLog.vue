<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, shallowRef, useTemplateRef } from 'vue'

import type { BattleLogEntry } from '@/game/combat/battleEventPresentation'

const props = defineProps<{ entries: readonly BattleLogEntry[] }>()
const open = shallowRef(false)
const toggleRef = useTemplateRef<HTMLButtonElement>('toggle')
const closeRef = useTemplateRef<HTMLButtonElement>('close')
const latestEntry = computed(() => props.entries.at(-1) ?? null)
const recentEntries = computed(() => props.entries.slice(-40).reverse())

async function openDrawer(): Promise<void> {
  open.value = true
  await nextTick()
  closeRef.value?.focus()
}

async function closeDrawer(): Promise<void> {
  open.value = false
  await nextTick()
  toggleRef.value?.focus()
}

function toggleDrawer(): void {
  if (open.value) void closeDrawer()
  else void openDrawer()
}

function onKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape' && open.value) void closeDrawer()
}

function icon(entry: BattleLogEntry): string {
  if (entry.eventType === 'HealingApplied') return '✦'
  if (entry.eventType === 'ActorDied' || entry.eventType === 'EnemyKilled') return '☠'
  if (entry.eventType === 'AbilityUsed') return '◆'
  if (entry.side === 'enemy') return '♠'
  if (entry.side === 'system') return '•'
  return '⚔'
}

function time(utc: string): string {
  const date = new Date(utc)
  return Number.isNaN(date.getTime())
    ? '--:--'
    : date.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' })
}

onMounted(() => window.addEventListener('keydown', onKeydown))
onUnmounted(() => window.removeEventListener('keydown', onKeydown))
</script>

<template>
  <section class="combat-log" data-combat-log>
    <button
      ref="toggle"
      type="button"
      class="combat-log__toggle"
      data-combat-log-toggle
      :aria-expanded="open"
      aria-controls="battle-log-drawer"
      @click="toggleDrawer"
    >
      <strong>Журнал</strong>
      <span v-if="latestEntry"><i aria-hidden="true">{{ icon(latestEntry) }}</i>{{ latestEntry.text }}</span>
      <span v-else>События появятся во время боя</span>
      <b aria-hidden="true">{{ open ? '⌄' : '⌃' }}</b>
    </button>

    <Transition name="log-drawer">
      <div v-if="open" class="combat-log__overlay" @click.self="closeDrawer">
        <section
          id="battle-log-drawer"
          class="combat-log__drawer"
          data-combat-log-drawer
          role="dialog"
          aria-modal="true"
          aria-labelledby="battle-log-title"
        >
          <header>
            <h2 id="battle-log-title">Журнал боя</h2>
            <button ref="close" type="button" aria-label="Закрыть журнал боя" @click="closeDrawer">×</button>
          </header>
          <ol>
            <li v-for="entry in recentEntries" :key="entry.key" :data-side="entry.side" data-combat-log-entry>
              <time :datetime="entry.occurredAtUtc">{{ time(entry.occurredAtUtc) }}</time>
              <i aria-hidden="true">{{ icon(entry) }}</i>
              <span>{{ entry.text }}</span>
            </li>
          </ol>
        </section>
      </div>
    </Transition>
  </section>
</template>

<style scoped>
.combat-log { position: relative; z-index: 100; }
.combat-log__toggle { display: grid; width: 100%; min-height: 36px; grid-template-columns: auto minmax(0, 1fr) auto; align-items: center; gap: .45rem; padding: .35rem .5rem; border: 1px solid rgb(177 151 91 / 28%); border-radius: 8px; background: rgb(5 8 13 / 94%); color: #dfd7ca; font: inherit; text-align: left; }
.combat-log__toggle strong { color: #cfc2a4; font-family: var(--ui-font-display); font-size: .6rem; }
.combat-log__toggle > span { overflow: hidden; color: #9e9992; font-size: .52rem; text-overflow: ellipsis; white-space: nowrap; }
.combat-log__toggle > span i { margin-right: .3rem; color: #c9a7e8; font-style: normal; }
.combat-log__toggle > b { color: #d8c077; }
.combat-log__toggle:focus-visible { outline: 2px solid #e7cc7b; outline-offset: 2px; }
.combat-log__overlay { position: fixed; z-index: 500; inset: 0; display: flex; align-items: flex-end; justify-content: center; padding: 0 max(.5rem, env(safe-area-inset-right)) max(.5rem, env(safe-area-inset-bottom)) max(.5rem, env(safe-area-inset-left)); background: rgb(0 0 0 / 55%); }
.combat-log__drawer { display: grid; width: min(100%, 44rem); max-height: 48svh; grid-template-rows: auto minmax(0, 1fr); overflow: hidden; border: 1px solid rgb(190 158 87 / 52%); border-radius: 12px 12px 8px 8px; background: linear-gradient(180deg, #11151e, #07090f); box-shadow: 0 -12px 38px rgb(0 0 0 / 58%); }
.combat-log__drawer header { display: flex; align-items: center; justify-content: space-between; padding: .65rem .75rem; border-bottom: 1px solid rgb(177 151 91 / 22%); }
.combat-log__drawer h2 { margin: 0; color: #e7d8b4; font-family: var(--ui-font-display); font-size: .82rem; }
.combat-log__drawer header button { width: 36px; height: 36px; border: 1px solid rgb(255 255 255 / 17%); border-radius: 7px; background: transparent; color: #e8dfd2; font-size: 1.15rem; }
.combat-log__drawer ol { display: grid; gap: .12rem; margin: 0; padding: .5rem .65rem .8rem; overflow-y: auto; list-style: none; }
.combat-log__drawer li { display: grid; grid-template-columns: 2.5rem 1rem 1fr; align-items: baseline; gap: .25rem; padding: .28rem .2rem; border-bottom: 1px solid rgb(255 255 255 / 5%); font-size: .58rem; }
.combat-log__drawer li time { color: #777a80; font-variant-numeric: tabular-nums; }
.combat-log__drawer li i { color: #b99adc; font-style: normal; }
.combat-log__drawer li[data-side='enemy'] i, .combat-log__drawer li[data-side='enemy'] span { color: #e98a98; }
.combat-log__drawer li[data-side='player'] span { color: #e8d8b0; }
.log-drawer-enter-active, .log-drawer-leave-active { transition: opacity 180ms ease; }
.log-drawer-enter-from, .log-drawer-leave-to { opacity: 0; }
@media (prefers-reduced-motion: reduce) { .log-drawer-enter-active, .log-drawer-leave-active { transition-duration: 1ms; } }
</style>
