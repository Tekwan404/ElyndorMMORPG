<script setup lang="ts">
import type { ContentAdminRevisionDetail } from '@/api/contracts'
import type { ContentDiffEntry } from '@/admin/contentDiff'

defineProps<{
  revision: ContentAdminRevisionDetail
  entries: ContentDiffEntry[]
  busy: boolean
}>()

defineEmits<{
  confirm: []
  cancel: []
}>()
</script>

<template>
  <div class="review-backdrop" role="presentation" @click.self="$emit('cancel')">
    <section class="review-card" role="dialog" aria-modal="true" aria-labelledby="publish-review-title">
      <header>
        <div>
          <small>REVIEW BEFORE PUBLISH</small>
          <h2 id="publish-review-title">Revision {{ revision.id.slice(0, 8) }}</h2>
          <p>{{ revision.contentVersion }} / {{ revision.balanceVersion }}</p>
        </div>
        <button type="button" :disabled="busy" @click="$emit('cancel')">✕</button>
      </header>

      <div class="summary">
        <b>{{ entries.length }} change(s)</b>
        <span>Сравнение этой revision с текущим LIVE.</span>
      </div>

      <div v-if="entries.length" class="diff-list">
        <article v-for="entry in entries" :key="`${entry.kind}:${entry.path}`" class="diff-row">
          <div class="diff-row__title">
            <span class="badge" :data-kind="entry.kind">{{ entry.kind }}</span>
            <code>{{ entry.path }}</code>
          </div>
          <div class="values">
            <span class="before">{{ entry.before ?? '—' }}</span>
            <span>→</span>
            <span class="after">{{ entry.after ?? '—' }}</span>
          </div>
        </article>
      </div>
      <p v-else class="empty">Revision совпадает с текущим LIVE. Публикация не требуется.</p>

      <footer>
        <button type="button" :disabled="busy" @click="$emit('cancel')">Отмена</button>
        <button
          class="primary"
          type="button"
          :disabled="busy || entries.length === 0"
          @click="$emit('confirm')"
        >
          {{ busy ? 'Publishing…' : 'Confirm publish' }}
        </button>
      </footer>
    </section>
  </div>
</template>

<style scoped>
.review-backdrop { position: fixed; z-index: 50; inset: 0; display: grid; place-items: center; padding: var(--ui-space-3); background: rgb(0 0 0 / .72); }
.review-card { width: min(58rem, 100%); max-height: min(82dvh, 54rem); display: grid; gap: var(--ui-space-3); padding: var(--ui-space-4); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: var(--ui-color-surface-1); box-shadow: 0 1.5rem 5rem rgb(0 0 0 / .45); }
header, footer, .diff-row__title, .values, .summary { display: flex; align-items: center; gap: var(--ui-space-2); }
header, footer { justify-content: space-between; }
header h2, header p { margin: 0; }
header small, .summary, .empty { color: var(--ui-color-text-muted); }
.diff-list { overflow: auto; border-block: 1px solid var(--ui-color-border); }
.diff-row { display: grid; gap: var(--ui-space-2); padding: var(--ui-space-3) 0; border-bottom: 1px solid var(--ui-color-border); }
.diff-row:last-child { border-bottom: 0; }
.diff-row code { overflow-wrap: anywhere; color: var(--ui-color-text-primary); }
.badge { padding: .15rem .45rem; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-round); font-size: var(--ui-font-size-xs); text-transform: uppercase; }
.badge[data-kind='added'] { color: var(--ui-color-success); border-color: var(--ui-color-success); }
.badge[data-kind='removed'] { color: var(--ui-color-danger); border-color: var(--ui-color-danger); }
.badge[data-kind='changed'] { color: var(--ui-color-warning); border-color: var(--ui-color-warning); }
.values { align-items: baseline; padding-left: var(--ui-space-1); color: var(--ui-color-text-muted); }
.before, .after { flex: 1; min-width: 0; overflow-wrap: anywhere; font-family: ui-monospace, SFMono-Regular, Consolas, monospace; }
.before { text-decoration: line-through; opacity: .72; }
.after { color: var(--ui-color-text-primary); }
button { min-height: var(--ui-touch-target); padding: var(--ui-space-2) var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); background: var(--ui-color-surface-2); color: var(--ui-color-text-primary); }
button.primary { border-color: var(--ui-color-primary); color: var(--ui-color-primary); }
button:disabled { opacity: .45; }
@media (max-width: 600px) {
  .review-card { max-height: 90dvh; padding: var(--ui-space-3); }
  .values { display: grid; grid-template-columns: 1fr; }
  .values > span:nth-child(2) { display: none; }
}
</style>
