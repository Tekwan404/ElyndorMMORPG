<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'

import AdminView from '@/admin/AdminView.vue'
import {
  adminRequest,
  AdminApiError,
  setAdminAccessToken,
  type AdminChallenge,
  type ApiStatus,
  type AuthenticationResponse,
  type ContentAdminCurrent,
  type ContentAdminHistory,
} from './api'

type ViewState = 'login' | 'code' | 'dashboard' | 'content'

const view = ref<ViewState>('login')
const telegramId = ref('')
const code = ref('')
const challenge = ref<AdminChallenge | null>(null)
const busy = ref(false)
const errorMessage = ref('')
const serviceStatus = ref<ApiStatus | null>(null)
const content = ref<ContentAdminCurrent | null>(null)
const history = ref<ContentAdminHistory | null>(null)
const tokenExpiresAtUtc = ref<string | null>(null)
const now = ref(Date.now())

const sections = [
  { group: 'CONTENT', items: ['Monsters', 'Items', 'Abilities', 'Talents', 'Classes', 'Locations', 'Loot Tables', 'Merchants', 'Equipment Sets'] },
  { group: 'BALANCE', items: ['Combat Simulator'] },
  { group: 'RELEASES', items: ['Drafts', 'Revisions', 'Releases'] },
  { group: 'OPERATIONS', items: ['Players', 'Server'] },
] as const

const countdown = computed(() => {
  if (!challenge.value) return ''
  const seconds = Math.max(
    0,
    Math.ceil((new Date(challenge.value.expiresAtUtc).getTime() - now.value) / 1000),
  )
  const minutes = Math.floor(seconds / 60)
  const remainder = String(seconds % 60).padStart(2, '0')
  return `${minutes}:${remainder}`
})

const releaseLabel = computed(() => shortId(content.value?.releaseId ?? null))
const revisionLabel = computed(() => shortId(content.value?.revisionId ?? null))
let clock: ReturnType<typeof setInterval> | null = null

onMounted(async () => {
  clock = setInterval(() => { now.value = Date.now() }, 1000)
  try {
    serviceStatus.value = await adminRequest<ApiStatus>('/api/v1/status')
  } catch {
    // Login remains available; the explicit request will show a useful error.
  }
})

onBeforeUnmount(() => {
  if (clock) clearInterval(clock)
})

async function requestCode(): Promise<void> {
  const normalized = telegramId.value.trim()
  if (!/^\d+$/.test(normalized)) {
    errorMessage.value = 'Укажи числовой Telegram ID.'
    return
  }

  await run(async () => {
    challenge.value = await adminRequest<AdminChallenge>(
      '/api/v1/admin/auth/request-code',
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ telegramUserId: Number(normalized) }),
      },
    )
    code.value = ''
    view.value = 'code'
  })
}

async function verifyCode(): Promise<void> {
  if (!challenge.value || !/^\d{6}$/.test(code.value.trim())) {
    errorMessage.value = 'Введи шестизначный код из Telegram.'
    return
  }

  await run(async () => {
    const authentication = await adminRequest<AuthenticationResponse>(
      '/api/v1/admin/auth/verify-code',
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          challengeId: challenge.value?.challengeId,
          telegramUserId: Number(telegramId.value.trim()),
          code: code.value.trim(),
        }),
      },
    )

    if (!authentication.roles.includes('SUPER_ADMIN')) {
      throw new Error('admin_role_missing')
    }

    setAdminAccessToken(authentication.accessToken)
    tokenExpiresAtUtc.value = authentication.expiresAtUtc
    await loadDashboard()
    view.value = 'dashboard'
  })
}

async function loadDashboard(): Promise<void> {
  const [status, current, adminHistory] = await Promise.all([
    adminRequest<ApiStatus>('/api/v1/status'),
    adminRequest<ContentAdminCurrent>('/api/v1/admin/content/current'),
    adminRequest<ContentAdminHistory>('/api/v1/admin/content/history?limit=6'),
  ])
  serviceStatus.value = status
  content.value = current
  history.value = adminHistory
}

function logout(): void {
  setAdminAccessToken(null)
  tokenExpiresAtUtc.value = null
  challenge.value = null
  code.value = ''
  view.value = 'login'
}

function backToId(): void {
  challenge.value = null
  code.value = ''
  errorMessage.value = ''
  view.value = 'login'
}

async function run(action: () => Promise<void>): Promise<void> {
  if (busy.value) return
  busy.value = true
  errorMessage.value = ''
  try {
    await action()
  } catch (error) {
    errorMessage.value = friendlyError(error)
  } finally {
    busy.value = false
  }
}

function friendlyError(error: unknown): string {
  if (!(error instanceof AdminApiError)) {
    return error instanceof Error && error.message === 'admin_role_missing'
      ? 'Токен не содержит SUPER_ADMIN.'
      : 'Не удалось связаться с Elyndor Server.'
  }

  const messages: Record<string, string> = {
    admin_login_not_allowed: 'Этот Telegram ID не входит в allowlist администраторов.',
    admin_login_rate_limited: 'Код уже отправлен. Подожди немного перед повторным запросом.',
    admin_login_delivery_failed: 'Telegram не подтвердил отправку кода. Проверь, что бот запущен.',
    admin_login_code_invalid: 'Код неверный или уже использован.',
    admin_login_code_expired: 'Срок действия кода истёк. Запроси новый.',
  }
  return messages[error.code] ?? `Ошибка: ${error.code}`
}

function shortId(value: string | null): string {
  return value ? value.slice(0, 8) : 'file'
}

function formatDate(value: string | null | undefined): string {
  return value ? new Date(value).toLocaleString('ru-RU') : '—'
}
</script>

<template>
  <main v-if="view === 'login' || view === 'code'" class="auth-page">
    <section class="auth-brand">
      <span class="brand-mark">E</span>
      <div>
        <p class="eyebrow">ELYNDOR CONTROL</p>
        <h1>Elyndor Admin</h1>
      </div>
    </section>

    <section class="auth-card">
      <div class="status-chip" :data-ok="serviceStatus?.status === 'ready'">
        <span></span>
        {{ serviceStatus?.status === 'ready' ? 'Production API online' : 'Checking production API' }}
      </div>

      <template v-if="view === 'login'">
        <p class="eyebrow">SECURE SIGN IN</p>
        <h2>Войти через Telegram</h2>
        <p class="muted">
          Укажи Telegram ID из server-side allowlist. Elyndor Bot пришлёт одноразовый код.
        </p>

        <label>
          <span>Telegram ID</span>
          <input
            v-model="telegramId"
            inputmode="numeric"
            autocomplete="username"
            placeholder="123456789"
            @keyup.enter="requestCode"
          />
        </label>

        <button class="primary" type="button" :disabled="busy" @click="requestCode">
          {{ busy ? 'Отправляем…' : 'Получить код' }}
        </button>
      </template>

      <template v-else>
        <button class="text-button" type="button" @click="backToId">← Другой Telegram ID</button>
        <p class="eyebrow">ONE-TIME CODE</p>
        <h2>Проверь Telegram</h2>
        <p class="muted">
          Код отправлен на аккаунт <b>{{ telegramId }}</b>. Он одноразовый и действует 5 минут.
        </p>

        <label>
          <span>Код · {{ countdown }}</span>
          <input
            v-model="code"
            inputmode="numeric"
            autocomplete="one-time-code"
            maxlength="6"
            class="code-input"
            placeholder="000000"
            @keyup.enter="verifyCode"
          />
        </label>

        <button class="primary" type="button" :disabled="busy" @click="verifyCode">
          {{ busy ? 'Проверяем…' : 'Войти в Admin' }}
        </button>
      </template>

      <p v-if="errorMessage" class="error-message">{{ errorMessage }}</p>

      <footer>
        JWT хранится только в памяти вкладки. Закрытие/обновление страницы завершит web-сессию.
      </footer>
    </section>
  </main>

  <div v-else class="admin-layout">
    <aside class="sidebar">
      <div class="sidebar-brand">
        <span class="brand-mark brand-mark--small">E</span>
        <div>
          <strong>ELYNDOR</strong>
          <small>ADMIN V2</small>
        </div>
      </div>

      <nav>
        <button
          class="nav-item"
          :class="{ active: view === 'dashboard' }"
          type="button"
          @click="view = 'dashboard'"
        >
          <span>Dashboard</span>
        </button>
        <button
          class="nav-item"
          :class="{ active: view === 'content' }"
          type="button"
          @click="view = 'content'"
        >
          <span>Content Workspace</span>
        </button>

        <div v-for="section in sections" :key="section.group" class="nav-group">
          <p>{{ section.group }}</p>
          <button v-for="item in section.items" :key="item" type="button" class="nav-item" disabled>
            <span>{{ item }}</span>
            <small>soon</small>
          </button>
        </div>
      </nav>

      <div class="sidebar-footer">
        <span class="status-dot"></span>
        <div>
          <strong>Production</strong>
          <small>game.elyndor.su</small>
        </div>
      </div>
    </aside>

    <section class="admin-main">
      <template v-if="view === 'dashboard'">
      <header class="topbar">
        <div>
          <p class="eyebrow">ADMIN V2 / FOUNDATION</p>
          <h1>Dashboard</h1>
        </div>
        <div class="topbar-actions">
          <span class="session-pill">SUPER_ADMIN · до {{ formatDate(tokenExpiresAtUtc) }}</span>
          <button type="button" @click="logout">Выйти</button>
        </div>
      </header>

      <section class="hero-panel">
        <div>
          <span class="live-label">● LIVE</span>
          <h2>Production control plane</h2>
          <p>
            Новый отдельный Admin SPA подключён к server-side SUPER_ADMIN API.
            Следующий блок — перенос Content Workspace.
          </p>
        </div>
        <button type="button" :disabled="busy" @click="run(loadDashboard)">
          {{ busy ? 'Обновляем…' : 'Refresh' }}
        </button>
      </section>

      <section class="metric-grid">
        <article>
          <span>SERVER</span>
          <b>{{ serviceStatus?.status?.toUpperCase() ?? 'UNKNOWN' }}</b>
          <small>{{ serviceStatus?.service ?? 'Elyndor.Server' }}</small>
        </article>
        <article>
          <span>CONTENT</span>
          <b>{{ content?.contentVersion ?? '—' }}</b>
          <small>LIVE package</small>
        </article>
        <article>
          <span>BALANCE</span>
          <b>{{ content?.balanceVersion ?? '—' }}</b>
          <small>LIVE profile</small>
        </article>
        <article>
          <span>RELEASE</span>
          <b>{{ releaseLabel }}</b>
          <small>revision {{ revisionLabel }}</small>
        </article>
      </section>

      <section class="dashboard-grid">
        <article class="panel">
          <div class="panel-heading">
            <div>
              <p class="eyebrow">CONTENT PLATFORM</p>
              <h2>Live state</h2>
            </div>
            <span class="status-chip" data-ok="true"><span></span>Protected</span>
          </div>
          <dl>
            <div><dt>Payload SHA</dt><dd><code>{{ content?.payloadSha256?.slice(0, 16) ?? '—' }}</code></dd></div>
            <div><dt>Revision</dt><dd>{{ revisionLabel }}</dd></div>
            <div><dt>Release</dt><dd>{{ releaseLabel }}</dd></div>
          </dl>
        </article>

        <article class="panel">
          <div class="panel-heading">
            <div>
              <p class="eyebrow">RECENT ACTIVITY</p>
              <h2>Releases</h2>
            </div>
          </div>
          <div v-if="history?.releases.length" class="activity-list">
            <div v-for="release in history.releases.slice(0, 5)" :key="release.id">
              <span class="activity-dot"></span>
              <div>
                <strong>{{ shortId(release.id) }}</strong>
                <small>{{ release.publishedBy }} · {{ formatDate(release.publishedAtUtc) }}</small>
                <p>{{ release.note || 'Без комментария' }}</p>
              </div>
            </div>
          </div>
          <p v-else class="muted">Release history пока пуст.</p>
        </article>
      </section>

      <section class="next-panel">
        <p class="eyebrow">NEXT ADMIN V2 BLOCK</p>
        <h2>Content Workspace migration</h2>
        <p>
          Monsters, Items, Abilities, Talents, Locations, Loot, Merchants, Simulator,
          Validation, Revisions, Publish и Rollback уже доступны в отдельном Content Workspace.
          Следующий блок — global search, filters, relations и улучшение editor UX.
        </p>
        <button class="primary-link" type="button" @click="view = 'content'">
          Открыть Content Workspace
        </button>
      </section>

      <p v-if="errorMessage" class="error-message error-message--dashboard">{{ errorMessage }}</p>
      </template>

      <AdminView v-else-if="view === 'content'" />
    </section>
  </div>
</template>
