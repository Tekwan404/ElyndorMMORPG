<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'

import AdminEntityForm from '@/admin/AdminEntityForm.vue'
import { apiClient, ApiRequestError } from '@/api/apiClient'
import type {
  ContentAdminCurrent,
  ContentAdminHistory,
  ContentAdminRelease,
  ContentAdminRevision,
  ContentAdminValidation,
} from '@/api/contracts'
import { useGameSessionStore } from '@/stores/gameSession'
import { initializeTelegramWebApp } from '@/telegram/telegramWebApp'

type AccessState = 'loading' | 'ready' | 'denied' | 'error'
type JsonRecord = Record<string, unknown>

const sections = [
  { key: 'monsters', label: 'Monsters' },
  { key: 'items', label: 'Items' },
  { key: 'abilities', label: 'Abilities' },
  { key: 'talentTrees', label: 'Talents' },
  { key: 'locations', label: 'Locations' },
  { key: 'classProfiles', label: 'Classes' },
  { key: 'lootTables', label: 'Loot' },
  { key: 'merchants', label: 'Merchants' },
  { key: 'equipmentSets', label: 'Sets' },
] as const

const session = useGameSessionStore()
const accessState = ref<AccessState>('loading')
const current = ref<ContentAdminCurrent | null>(null)
const history = ref<ContentAdminHistory>({ revisions: [], releases: [] })
const draftJson = ref('')
const selectedSection = ref<(typeof sections)[number]['key']>('monsters')
const selectedEntityId = ref<string | null>(null)
const entityJson = ref('')
const editorMode = ref<'form' | 'json'>('form')
const note = ref('')
const validation = ref<ContentAdminValidation | null>(null)
const busyAction = ref<string | null>(null)
const errorMessage = ref('')
const statusMessage = ref('')
const rollbackCandidate = ref<string | null>(null)

const draftPackage = computed<JsonRecord | null>(() => parseRecord(draftJson.value))
const entityList = computed<JsonRecord[]>(() => {
  const value = draftPackage.value?.[selectedSection.value]
  return Array.isArray(value) ? value.filter(isRecord) : []
})
const selectedEntity = computed<JsonRecord | null>(() => parseRecord(entityJson.value))
const hasStructuredEditor = computed(() =>
  ['monsters', 'abilities', 'items'].includes(selectedSection.value),
)
const isDirty = computed(() => {
  if (!current.value) return false
  return draftJson.value !== prettyJson(current.value.payloadJson)
})
const validationLabel = computed(() => {
  if (!validation.value) return 'Не проверено'
  return validation.value.isValid ? 'VALID' : 'INVALID'
})

function entityId(entity: JsonRecord): string {
  const id = entity.id
  return typeof id === 'string' ? id : '(without id)'
}

function selectEntity(entity: JsonRecord): void {
  selectedEntityId.value = entityId(entity)
  entityJson.value = JSON.stringify(entity, null, 2)
  editorMode.value = ['monsters', 'abilities', 'items'].includes(selectedSection.value)
    ? 'form'
    : 'json'
  validation.value = null
}

function changeSection(): void {
  selectedEntityId.value = null
  entityJson.value = ''
  editorMode.value = ['monsters', 'abilities', 'items'].includes(selectedSection.value)
    ? 'form'
    : 'json'
}

function updateEntityFromForm(entity: JsonRecord): void {
  entityJson.value = JSON.stringify(entity, null, 2)
  validation.value = null
  statusMessage.value = ''
}

function applyEntity(): void {
  errorMessage.value = ''
  statusMessage.value = ''
  const packageObject = parseRecord(draftJson.value)
  const entity = parseRecord(entityJson.value)
  if (!packageObject || !entity) {
    errorMessage.value = 'JSON сущности или пакета некорректен.'
    return
  }

  const id = entity.id
  if (typeof id !== 'string' || !id) {
    errorMessage.value = 'У сущности должен быть строковый id.'
    return
  }

  const source = packageObject[selectedSection.value]
  if (!Array.isArray(source)) {
    errorMessage.value = 'В выбранной категории нет массива сущностей.'
    return
  }

  const index = source.findIndex(
    (item) => isRecord(item) && item.id === selectedEntityId.value,
  )
  if (index < 0) {
    errorMessage.value = 'Сущность больше не найдена в текущем draft.'
    return
  }

  const next = [...source]
  next[index] = entity
  packageObject[selectedSection.value] = next
  draftJson.value = JSON.stringify(packageObject, null, 2)
  selectedEntityId.value = id
  validation.value = null
  statusMessage.value = 'Изменение применено к локальному draft.'
}

async function loadRuntime(resetDraft: boolean): Promise<void> {
  const next = await apiClient.request<ContentAdminCurrent>(
    '/api/v1/admin/content/current',
  )
  current.value = next
  if (resetDraft) {
    draftJson.value = prettyJson(next.payloadJson)
    selectedEntityId.value = null
    entityJson.value = ''
    validation.value = null
  }
}

async function loadHistory(): Promise<void> {
  history.value = await apiClient.request<ContentAdminHistory>(
    '/api/v1/admin/content/history?limit=40',
  )
}

async function refreshAll(resetDraft = true): Promise<void> {
  await Promise.all([loadRuntime(resetDraft), loadHistory()])
}

async function validateDraft(): Promise<void> {
  await runAction('validate', async () => {
    validation.value = await apiClient.request<ContentAdminValidation>(
      '/api/v1/admin/content/validate',
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ payloadJson: draftJson.value }),
      },
    )
    statusMessage.value = validation.value.isValid
      ? 'Draft прошёл серверную валидацию.'
      : 'Draft содержит ошибки.'
  })
}

async function saveDraft(): Promise<void> {
  if (!current.value) return

  await runAction('save', async () => {
    const revision = await apiClient.request<ContentAdminRevision>(
      '/api/v1/admin/content/revisions',
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          payloadJson: draftJson.value,
          basePayloadSha256: current.value?.payloadSha256,
          note: note.value.trim() || null,
        }),
      },
    )
    statusMessage.value = `Revision ${shortId(revision.id)} сохранена как immutable draft.`
    validation.value = {
      isValid: true,
      canonicalPayloadJson: null,
      payloadSha256: revision.payloadSha256,
      errors: [],
    }
    await loadHistory()
  })
}

async function publishRevision(revisionId: string): Promise<void> {
  if (!current.value) return

  await runAction(`publish:${revisionId}`, async () => {
    await apiClient.request<ContentAdminRelease>(
      `/api/v1/admin/content/revisions/${revisionId}/publish`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          expectedLivePayloadSha256: current.value?.payloadSha256,
          note: note.value.trim() || null,
        }),
      },
    )
    statusMessage.value = `Revision ${shortId(revisionId)} опубликована.`
    note.value = ''
    await refreshAll(true)
  })
}

async function rollbackRelease(releaseId: string): Promise<void> {
  if (!current.value) return
  if (rollbackCandidate.value !== releaseId) {
    rollbackCandidate.value = releaseId
    statusMessage.value = 'Нажми «Подтвердить rollback», чтобы создать новый release на выбранной revision.'
    return
  }

  await runAction(`rollback:${releaseId}`, async () => {
    await apiClient.request<ContentAdminRelease>(
      `/api/v1/admin/content/releases/${releaseId}/rollback`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          expectedLivePayloadSha256: current.value?.payloadSha256,
          note: note.value.trim() || null,
        }),
      },
    )
    rollbackCandidate.value = null
    note.value = ''
    statusMessage.value = `Rollback к release ${shortId(releaseId)} опубликован новым release.`
    await refreshAll(true)
  })
}

async function runAction(name: string, action: () => Promise<void>): Promise<void> {
  if (busyAction.value) return
  busyAction.value = name
  errorMessage.value = ''
  statusMessage.value = ''
  try {
    await action()
  } catch (error) {
    if (
      error instanceof ApiRequestError
      && (error.code === 'content_live_changed' || error.code === 'content_draft_stale')
    ) {
      errorMessage.value =
        'Live content уже изменился. Текущий live hash обновлён; проверь draft и повтори действие.'
      await loadRuntime(false)
    } else if (error instanceof ApiRequestError) {
      errorMessage.value = `Ошибка API: ${error.code}`
    } else {
      errorMessage.value = 'Не удалось выполнить операцию.'
    }
  } finally {
    busyAction.value = null
  }
}

function resetDraft(): void {
  if (!current.value) return
  draftJson.value = prettyJson(current.value.payloadJson)
  selectedEntityId.value = null
  entityJson.value = ''
  validation.value = null
  errorMessage.value = ''
  statusMessage.value = 'Локальные изменения сброшены до live.'
}

function prettyJson(value: string): string {
  try {
    return JSON.stringify(JSON.parse(value), null, 2)
  } catch {
    return value
  }
}

function parseRecord(value: string): JsonRecord | null {
  try {
    const parsed: unknown = JSON.parse(value)
    return isRecord(parsed) ? parsed : null
  } catch {
    return null
  }
}

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function shortId(value: string | null): string {
  return value ? value.slice(0, 8) : 'file'
}

function formatDate(value: string): string {
  return new Date(value).toLocaleString()
}

onMounted(async () => {
  initializeTelegramWebApp()
  try {
    await session.authenticate()
    if (!session.isAdmin) {
      accessState.value = 'denied'
      return
    }
    await refreshAll(true)
    accessState.value = 'ready'
  } catch (error) {
    accessState.value = error instanceof ApiRequestError && error.status === 403
      ? 'denied'
      : 'error'
    errorMessage.value =
      error instanceof ApiRequestError ? error.code : 'network_unavailable'
  }
})
</script>

<template>
  <main class="admin-shell">
    <header class="admin-header">
      <div>
        <p class="eyebrow">ELYNDOR CONTROL</p>
        <h1>Content Admin</h1>
      </div>
      <RouterLink class="back-link" to="/world">← В игру</RouterLink>
    </header>

    <section v-if="accessState === 'loading'" class="system-state">
      Проверяем права администратора…
    </section>

    <section v-else-if="accessState === 'denied'" class="system-state system-state--danger">
      <h2>Доступ запрещён</h2>
      <p>Этот Telegram-пользователь не входит в server-side admin allowlist.</p>
    </section>

    <section v-else-if="accessState === 'error'" class="system-state system-state--danger">
      <h2>Admin недоступен</h2>
      <p>{{ errorMessage }}</p>
    </section>

    <template v-else>
      <section v-if="current" class="live-bar">
        <div>
          <small>CONTENT</small>
          <b>{{ current.contentVersion }}</b>
        </div>
        <div>
          <small>BALANCE</small>
          <b>{{ current.balanceVersion }}</b>
        </div>
        <div>
          <small>REVISION</small>
          <b>{{ shortId(current.revisionId) }}</b>
        </div>
        <div>
          <small>RELEASE</small>
          <b>{{ shortId(current.releaseId) }}</b>
        </div>
        <div class="live-bar__hash">
          <small>LIVE SHA</small>
          <code>{{ current.payloadSha256.slice(0, 12) }}</code>
        </div>
      </section>

      <section class="toolbar">
        <label class="note-field">
          <span>Причина изменения</span>
          <input v-model="note" maxlength="240" placeholder="Например: nerf wolf XP after test" />
        </label>
        <div class="toolbar__actions">
          <button type="button" :disabled="Boolean(busyAction)" @click="validateDraft">
            {{ busyAction === 'validate' ? 'Проверка…' : 'Validate' }}
          </button>
          <button class="primary" type="button" :disabled="Boolean(busyAction) || !isDirty" @click="saveDraft">
            {{ busyAction === 'save' ? 'Сохранение…' : 'Save draft' }}
          </button>
          <button type="button" :disabled="Boolean(busyAction) || !isDirty" @click="resetDraft">
            Reset
          </button>
        </div>
      </section>

      <p v-if="statusMessage" class="message message--success">{{ statusMessage }}</p>
      <p v-if="errorMessage" class="message message--danger">{{ errorMessage }}</p>

      <section class="validation-strip" :data-valid="validation?.isValid">
        <b>{{ validationLabel }}</b>
        <span v-if="validation?.errors.length">
          {{ validation.errors.length }} error(s)
        </span>
        <span v-else-if="validation?.isValid">серверный pipeline пройден</span>
        <span v-else>validate перед публикацией</span>
      </section>

      <div class="workspace">
        <aside class="catalog">
          <label>
            <span>Категория</span>
            <select v-model="selectedSection" @change="changeSection">
              <option v-for="section in sections" :key="section.key" :value="section.key">
                {{ section.label }}
              </option>
            </select>
          </label>

          <div class="entity-list">
            <button
              v-for="entity in entityList"
              :key="entityId(entity)"
              type="button"
              :class="{ active: selectedEntityId === entityId(entity) }"
              @click="selectEntity(entity)"
            >
              {{ entityId(entity) }}
            </button>
            <p v-if="entityList.length === 0" class="muted">Нет сущностей.</p>
          </div>
        </aside>

        <section class="editor">
          <div class="editor__header">
            <div>
              <small>ENTITY EDITOR</small>
              <h2>{{ selectedEntityId ?? 'Выбери сущность' }}</h2>
            </div>
            <div class="editor__actions">
              <div v-if="selectedEntityId && hasStructuredEditor" class="editor-mode" aria-label="Режим редактора">
                <button
                  type="button"
                  :class="{ active: editorMode === 'form' }"
                  @click="editorMode = 'form'"
                >
                  Form
                </button>
                <button
                  type="button"
                  :class="{ active: editorMode === 'json' }"
                  @click="editorMode = 'json'"
                >
                  JSON
                </button>
              </div>
              <button
                type="button"
                :disabled="!selectedEntityId"
                @click="applyEntity"
              >
                Apply to draft
              </button>
            </div>
          </div>
          <AdminEntityForm
            v-if="selectedEntityId && selectedEntity && hasStructuredEditor && editorMode === 'form'"
            :section-key="selectedSection"
            :entity="selectedEntity"
            @update:entity="updateEntityFromForm"
          />
          <textarea
            v-else
            v-model="entityJson"
            class="code-editor code-editor--entity"
            spellcheck="false"
            :disabled="!selectedEntityId"
            placeholder="JSON выбранной сущности"
          />

          <details class="package-editor">
            <summary>Full package JSON · {{ draftJson.length.toLocaleString() }} chars</summary>
            <textarea
              v-model="draftJson"
              class="code-editor code-editor--package"
              spellcheck="false"
              @input="validation = null"
            />
          </details>
        </section>
      </div>

      <section v-if="validation?.errors.length" class="errors">
        <h2>Validation errors</h2>
        <article v-for="error in validation.errors" :key="`${error.code}-${error.path}`">
          <b>{{ error.code }}</b>
          <code>{{ error.path }}</code>
          <p>{{ error.message }}</p>
        </article>
      </section>

      <section class="history">
        <div class="history__column">
          <h2>Revisions</h2>
          <article v-for="revision in history.revisions" :key="revision.id" class="history-card">
            <div>
              <b>{{ shortId(revision.id) }}</b>
              <small>{{ revision.contentVersion }} / {{ revision.balanceVersion }}</small>
              <small>{{ formatDate(revision.createdAtUtc) }} · {{ revision.createdBy }}</small>
              <p>{{ revision.note || 'Без комментария' }}</p>
            </div>
            <button
              class="primary"
              type="button"
              :disabled="Boolean(busyAction)"
              @click="publishRevision(revision.id)"
            >
              {{ busyAction === `publish:${revision.id}` ? 'Publishing…' : 'Publish' }}
            </button>
          </article>
        </div>

        <div class="history__column">
          <h2>Releases</h2>
          <article v-for="release in history.releases" :key="release.id" class="history-card">
            <div>
              <b>{{ shortId(release.id) }}</b>
              <small>revision {{ shortId(release.revisionId) }}</small>
              <small>{{ formatDate(release.publishedAtUtc) }} · {{ release.publishedBy }}</small>
              <p>{{ release.note || 'Без комментария' }}</p>
            </div>
            <button
              v-if="release.id !== current?.releaseId"
              class="danger"
              type="button"
              :disabled="Boolean(busyAction)"
              @click="rollbackRelease(release.id)"
            >
              {{
                busyAction === `rollback:${release.id}`
                  ? 'Rollback…'
                  : rollbackCandidate === release.id
                    ? 'Подтвердить rollback'
                    : 'Rollback'
              }}
            </button>
            <span v-else class="live-badge">LIVE</span>
          </article>
        </div>
      </section>
    </template>
  </main>
</template>

<style scoped>
.admin-shell { min-height: 100dvh; padding: var(--ui-space-4); background: var(--ui-color-background); color: var(--ui-color-text-primary); }
.admin-header, .toolbar, .live-bar, .editor__header, .history-card { display: flex; align-items: center; justify-content: space-between; gap: var(--ui-space-3); }
.admin-header { margin: 0 auto var(--ui-space-4); max-width: 86rem; }
.admin-header h1, .editor h2, .history h2 { margin: 0; font-family: var(--ui-font-display); }
.eyebrow { margin: 0; color: var(--ui-color-primary); font-size: var(--ui-font-size-xs); letter-spacing: .12em; }
.back-link { color: var(--ui-color-text-muted); text-decoration: none; }
.system-state, .message, .validation-strip { max-width: 86rem; margin: var(--ui-space-3) auto; padding: var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: var(--ui-color-surface-1); }
.system-state--danger, .message--danger { border-color: var(--ui-color-danger); }
.message--success { border-color: var(--ui-color-success); }
.live-bar { max-width: 86rem; margin: 0 auto var(--ui-space-3); padding: var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: var(--ui-color-surface-1); }
.live-bar > div { display: grid; gap: var(--ui-space-1); }
.live-bar small, .editor small, .history-card small, .note-field span, .catalog label span { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.live-bar__hash { margin-left: auto; }
.toolbar { max-width: 86rem; margin: 0 auto var(--ui-space-3); align-items: end; }
.note-field { display: grid; flex: 1; gap: var(--ui-space-1); }
.note-field input, .catalog select { min-height: var(--ui-touch-target); padding: var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); background: var(--ui-color-surface-1); color: inherit; }
.toolbar__actions { display: flex; flex-wrap: wrap; gap: var(--ui-space-2); }
button { min-height: var(--ui-touch-target); padding: var(--ui-space-2) var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); background: var(--ui-color-surface-2); color: var(--ui-color-text-primary); font: inherit; cursor: pointer; }
button:hover:not(:disabled) { border-color: var(--ui-color-primary); }
button:disabled { cursor: not-allowed; opacity: .45; }
button.primary { border-color: var(--ui-color-primary); color: var(--ui-color-primary); }
button.danger { border-color: var(--ui-color-danger); color: var(--ui-color-danger); }
.validation-strip { display: flex; align-items: center; gap: var(--ui-space-3); color: var(--ui-color-text-muted); }
.validation-strip[data-valid='true'] b { color: var(--ui-color-success); }
.validation-strip[data-valid='false'] b { color: var(--ui-color-danger); }
.workspace { display: grid; max-width: 86rem; margin: 0 auto; grid-template-columns: minmax(12rem, 18rem) minmax(0, 1fr); gap: var(--ui-space-3); }
.catalog, .editor, .history__column, .errors { border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: var(--ui-color-surface-1); }
.catalog { padding: var(--ui-space-3); }
.catalog label { display: grid; gap: var(--ui-space-1); }
.entity-list { display: grid; max-height: 38rem; margin-top: var(--ui-space-3); gap: var(--ui-space-1); overflow: auto; }
.entity-list button { min-height: 2.5rem; text-align: left; }
.entity-list button.active { border-color: var(--ui-color-primary); color: var(--ui-color-primary); }
.editor { min-width: 0; padding: var(--ui-space-3); }
.editor__header { margin-bottom: var(--ui-space-2); }
.editor__actions { display: flex; align-items: center; gap: var(--ui-space-2); }
.editor-mode { display: flex; gap: var(--ui-space-1); }
.editor-mode button { min-height: 2.25rem; padding: var(--ui-space-1) var(--ui-space-2); }
.editor-mode button.active { border-color: var(--ui-color-primary); color: var(--ui-color-primary); background: var(--ui-color-surface-2); }
.code-editor { width: 100%; resize: vertical; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); background: #090b12; color: #d9e0ff; font: .78rem/1.5 ui-monospace, SFMono-Regular, Consolas, monospace; tab-size: 2; }
.code-editor--entity { min-height: 24rem; }
.code-editor--package { min-height: 36rem; margin-top: var(--ui-space-2); }
.package-editor { margin-top: var(--ui-space-3); color: var(--ui-color-text-muted); }
.package-editor summary { cursor: pointer; }
.errors { max-width: 86rem; margin: var(--ui-space-3) auto; padding: var(--ui-space-3); }
.errors article { padding: var(--ui-space-2) 0; border-top: 1px solid var(--ui-color-border); }
.errors code { margin-left: var(--ui-space-2); color: var(--ui-color-warning); }
.errors p { margin-bottom: 0; color: var(--ui-color-text-muted); }
.history { display: grid; max-width: 86rem; margin: var(--ui-space-4) auto 0; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--ui-space-3); }
.history__column { padding: var(--ui-space-3); }
.history-card { padding: var(--ui-space-3) 0; border-top: 1px solid var(--ui-color-border); align-items: start; }
.history-card > div { display: grid; gap: var(--ui-space-1); min-width: 0; }
.history-card p { margin: var(--ui-space-1) 0 0; color: var(--ui-color-text-muted); overflow-wrap: anywhere; }
.live-badge { padding: var(--ui-space-1) var(--ui-space-2); border: 1px solid var(--ui-color-success); border-radius: var(--ui-radius-round); color: var(--ui-color-success); font-size: var(--ui-font-size-xs); }
.muted { color: var(--ui-color-text-muted); }
@media (max-width: 800px) {
  .admin-shell { padding: var(--ui-space-2); }
  .live-bar { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .live-bar__hash { margin-left: 0; }
  .toolbar { align-items: stretch; flex-direction: column; }
  .workspace, .history { grid-template-columns: 1fr; }
  .entity-list { max-height: 14rem; }
  .code-editor--entity { min-height: 18rem; }
}
</style>
