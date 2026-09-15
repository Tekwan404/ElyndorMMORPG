<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import type { ReleaseNoteEntry } from '@/api/contracts'
import { useGameSessionStore } from '@/stores/gameSession'
import { UILoadingState } from '@/ui/components'
import IconGenerator from '@/ui/icons/IconGenerator.vue'

const session = useGameSessionStore()
const loading = ref(true)
const failed = ref(false)
const releases = computed(() => session.releaseHistory)

onMounted(async () => {
  try {
    await session.loadReleaseHistory()
  } catch {
    failed.value = true
  } finally {
    loading.value = false
  }
})

function sectionLabel(kind: ReleaseNoteEntry['kind']): string {
  if (kind === 'Added') return 'Добавлено'
  if (kind === 'Changed') return 'Изменено'
  return 'Исправлено'
}
</script>

<template>
  <section class="release-notes" data-release-notes-history>
    <header class="release-notes__header">
      <IconGenerator :config="{ id: 'release-notes-scroll', glyph: 'scroll', category: 'utility' }" />
      <div>
        <small>ЖУРНАЛ ELYNDOR</small>
        <h2>Новости игры</h2>
      </div>
    </header>

    <UILoadingState v-if="loading" state="loading" title="Загружаем обновления" />
    <UILoadingState v-else-if="failed" state="error" title="Не удалось загрузить обновления" />
    <p v-else-if="releases.length === 0" class="release-notes__empty">Пока нет опубликованных обновлений.</p>
    <ol v-else class="release-notes__list">
      <li v-for="release in releases" :key="release.id" class="release-note">
        <header>
          <div>
            <small>ВЕРСИЯ {{ release.id }}</small>
            <h3>{{ release.title }}</h3>
          </div>
          <time :datetime="release.publishedAtUtc">{{ new Date(release.publishedAtUtc).toLocaleDateString('ru-RU') }}</time>
        </header>
        <ul>
          <li v-for="entry in release.entries" :key="`${entry.kind}-${entry.text}`">
            <b :data-tone="entry.kind">{{ sectionLabel(entry.kind) }}</b>
            <span>{{ entry.text }}</span>
          </li>
        </ul>
      </li>
    </ol>
  </section>
</template>

<style scoped>
.release-notes { display: grid; gap: 12px; }
.release-notes__header { display: flex; align-items: center; gap: 10px; padding-bottom: 9px; border-bottom: 1px solid rgb(205 177 113 / 25%); }
.release-notes__header :deep(.icon-generator) { width: 34px; height: 34px; color: var(--ui-color-gold); }
.release-notes__header small, .release-note header small { color: var(--ui-color-gold); font-size: .58rem; font-weight: 800; letter-spacing: .11em; }
.release-notes__header h2, .release-note h3 { margin: 2px 0 0; font-family: var(--ui-font-display); }
.release-notes__header h2 { font-size: 1.25rem; }
.release-notes__empty { margin: 0; color: var(--ui-color-text-muted); }
.release-notes__list { display: grid; gap: 9px; margin: 0; padding: 0; list-style: none; }
.release-note { padding: 11px; border: 1px solid rgb(205 177 113 / 26%); background: linear-gradient(120deg, rgb(205 177 113 / 8%), transparent 54%), var(--ui-gradient-panel); }
.release-note header { display: flex; justify-content: space-between; gap: 10px; }
.release-note h3 { font-size: 1rem; }
.release-note time { color: var(--ui-color-text-muted); font-size: .7rem; white-space: nowrap; }
.release-note ul { display: grid; gap: 7px; margin: 10px 0 0; padding: 0; list-style: none; }
.release-note li { display: grid; grid-template-columns: 5.6rem minmax(0, 1fr); gap: 7px; color: var(--ui-color-text-secondary); font-size: .78rem; line-height: 1.36; }
.release-note li b { color: var(--ui-color-gold); font-size: .62rem; letter-spacing: .04em; }
.release-note li b[data-tone='Fixed'] { color: var(--ui-color-success); }
.release-note li b[data-tone='Changed'] { color: #b9a5f6; }
</style>
