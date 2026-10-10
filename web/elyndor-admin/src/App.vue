<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'

import AdminView from '@/admin/AdminView.vue'
import GmForgeView from '@/admin/GmForgeView.vue'
import AdminPlayersView from '@/admin/AdminPlayersView.vue'
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

type ViewState = 'login' | 'code' | 'password' | 'dashboard' | 'content' | 'gmforge' | 'players' | 'server'

const view = ref<ViewState>('login')
const telegramId = ref('')
const code = ref('')
const password = ref('')
const challenge = ref<AdminChallenge | null>(null)
const busy = ref(false)
const errorMessage = ref('')
const serviceStatus = ref<ApiStatus | null>(null)
const content = ref<ContentAdminCurrent | null>(null)
const history = ref<ContentAdminHistory | null>(null)
const tokenExpiresAtUtc = ref<string | null>(null)
const now = ref(Date.now())
const contentSection = ref('monsters')
const contentDirty = ref(false)
const forgeRecipientId = ref('')
const forgeCloneItemId = ref('')
const contentFocus = ref('')

const contentNavigation = [
  { key: 'monsters', label: 'Монстры' },
  { key: 'items', label: 'Предметы' },
  { key: 'abilities', label: 'Способности' },
  { key: 'talentTrees', label: 'Таланты' },
  { key: 'classProfiles', label: 'Классы' },
  { key: 'locations', label: 'Локации' },
  { key: 'lootTables', label: 'Таблицы добычи' },
  { key: 'merchants', label: 'Торговцы' },
  { key: 'equipmentSets', label: 'Сеты экипировки' },
] as const

const sections = [
  { group: 'BALANCE', items: ['Combat Simulator'] },
  { group: 'RELEASES', items: ['Drafts', 'Revisions', 'Releases'] },
  { group: 'OPERATIONS', items: ['Players', 'Server', 'GM Forge'] },
] as const

function operationLabel(value: string): string {
  const labels: Record<string, string> = {
    'Combat Simulator': 'Симулятор боя', Drafts: 'Черновики',
    Revisions: 'Версии', Releases: 'Публикации', Players: 'Игроки',
    Server: 'Сервер', 'GM Forge': 'Выдача предметов',
  }
  return labels[value] ?? value
}
function operationGroupLabel(value: string): string {
  return ({ BALANCE: 'БАЛАНС', RELEASES: 'ВЕРСИИ', OPERATIONS: 'УПРАВЛЕНИЕ' } as Record<string, string>)[value] ?? value
}

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

function handleInvalidSession(): void {
  clearSession('Сессия администратора недействительна. Войди повторно.')
}

onMounted(async () => {
  window.addEventListener('elyndor-admin-session-expired', handleInvalidSession)
  clock = setInterval(() => {
    now.value = Date.now()
    if (tokenExpiresAtUtc.value && now.value >= Date.parse(tokenExpiresAtUtc.value)) {
      clearSession('Срок действия сессии истёк. Войди в админку повторно.')
    }
  }, 1000)
  try {
    serviceStatus.value = await adminRequest<ApiStatus>('/api/v1/status')
  } catch {
    // Login remains available; the explicit request will show a useful error.
  }
})

onBeforeUnmount(() => {
  if (clock) clearInterval(clock)
  window.removeEventListener('elyndor-admin-session-expired', handleInvalidSession)
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

function openPasswordLogin(): void {
  const normalized = telegramId.value.trim()
  if (!/^\d+$/.test(normalized)) {
    errorMessage.value = 'Укажи числовой Telegram ID.'
    return
  }

  password.value = ''
  errorMessage.value = ''
  view.value = 'password'
}

async function verifyPassword(): Promise<void> {
  const normalized = telegramId.value.trim()
  if (!/^\d+$/.test(normalized) || password.value.length === 0) {
    errorMessage.value = 'Укажи Telegram ID и резервный пароль.'
    return
  }

  await run(async () => {
    const authentication = await adminRequest<AuthenticationResponse>(
      '/api/v1/admin/auth/password',
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          telegramUserId: Number(normalized),
          password: password.value,
        }),
      },
    )

    if (!authentication.roles.includes('SUPER_ADMIN')) {
      throw new Error('admin_role_missing')
    }

    password.value = ''
    setAdminAccessToken(authentication.accessToken)
    tokenExpiresAtUtc.value = authentication.expiresAtUtc
    try {
      await loadDashboard()
      view.value = 'dashboard'
    } catch (error) {
      setAdminAccessToken(null)
      tokenExpiresAtUtc.value = null
      throw error
    }
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
    try {
      await loadDashboard()
      view.value = 'dashboard'
    } catch (error) {
      setAdminAccessToken(null)
      tokenExpiresAtUtc.value = null
      throw error
    }
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

function clearSession(message = ''): void {
  setAdminAccessToken(null)
  tokenExpiresAtUtc.value = null
  challenge.value = null
  code.value = ''
  password.value = ''
  contentDirty.value = false
  view.value = 'login'
  errorMessage.value = message
  content.value = null
  history.value = null
}

function logout(): void {
  if (!confirmWorkspaceNavigation()) return
  clearSession()
}

function backToId(): void {
  challenge.value = null
  code.value = ''
  password.value = ''
  errorMessage.value = ''
  view.value = 'login'
}

function openDashboard(): void {
  if (!confirmWorkspaceNavigation()) return
  contentDirty.value = false
  view.value = 'dashboard'
}

function openGmForge(): void {
  if (!confirmWorkspaceNavigation()) return
  contentDirty.value = false
  forgeRecipientId.value = telegramId.value
  forgeCloneItemId.value = ''
  view.value = 'gmforge'
}

function openGmForgeForPlayer(target: string): void {
  if (!confirmWorkspaceNavigation()) return
  contentDirty.value = false
  forgeRecipientId.value = target
  forgeCloneItemId.value = ''
  view.value = 'gmforge'
}

function openGmForgeClone(target: string, itemId: string): void {
  if (!confirmWorkspaceNavigation()) return
  contentDirty.value = false
  forgeRecipientId.value = target
  forgeCloneItemId.value = itemId
  view.value = 'gmforge'
}

function openOperations(section: string): void {
  if (section === 'GM Forge') { openGmForge(); return }
  if (section === 'Players' || section === 'Server') {
    if (!confirmWorkspaceNavigation()) return
    contentDirty.value = false
    view.value = section === 'Players' ? 'players' : 'server'
    return
  }
  if (!confirmWorkspaceNavigation()) return
  contentDirty.value = false
  contentSection.value = 'monsters'
  contentFocus.value = section === 'Combat Simulator' ? 'simulator'
    : section === 'Drafts' ? 'drafts'
    : section === 'Revisions' ? 'revisions' : 'releases'
  view.value = 'content'
}

function openContent(section: string): void {
  if (!confirmWorkspaceNavigation()) return
  contentSection.value = section
  contentFocus.value = ''
  view.value = 'content'
}

function confirmWorkspaceNavigation(): boolean {
  if (view.value !== 'content' || !contentDirty.value) return true
  return window.confirm(
    'Есть изменения, которые ещё не сохранены как revision. '
    + 'Локальный autosave сохранится. Перейти?',
  )
}

async function run(action: () => Promise<void>): Promise<void> {
  if (busy.value) return
  busy.value = true
  errorMessage.value = ''
  try {
    await action()
  } catch (error) {
    const message = friendlyError(error)
    if (error instanceof AdminApiError && error.status === 401 && tokenExpiresAtUtc.value) {
      clearSession('Сессия администратора недействительна. Войди повторно.')
    } else {
      errorMessage.value = message
    }
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
    admin_login_password_invalid: 'Неверный резервный пароль.',
    admin_login_password_rate_limited: 'Слишком много неверных попыток. Попробуй через 5 минут.',
    admin_login_password_disabled: 'Резервный вход по паролю отключён на сервере.',
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
  <main v-if="view === 'login' || view === 'code' || view === 'password'" class="auth-page">
    <section class="auth-brand">
      <span class="brand-mark">E</span>
      <div>
        <p class="eyebrow">Панель управления Elyndor</p>
        <h1>Админка Elyndor</h1>
      </div>
    </section>

    <section class="auth-card">
      <div class="status-chip" :data-ok="serviceStatus?.status === 'ready'">
        <span></span>
        {{ serviceStatus?.status === 'ready' ? 'Сервер доступен' : 'Проверяем сервер' }}
      </div>

      <template v-if="view === 'login'">
        <p class="eyebrow">Защищённый вход</p>
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

        <button class="text-button" type="button" :disabled="busy" @click="openPasswordLogin">
          Telegram недоступен? Войти по резервному паролю
        </button>
      </template>

      <template v-else-if="view === 'code'">
        <button class="text-button" type="button" @click="backToId">← Другой Telegram ID</button>
        <p class="eyebrow">Одноразовый код</p>
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
          {{ busy ? 'Проверяем…' : 'Войти в админку' }}
        </button>
      </template>

      <template v-else>
        <button class="text-button" type="button" @click="backToId">← Назад</button>
        <p class="eyebrow">Резервный вход</p>
        <h2>Резервный вход</h2>
        <p class="muted">
          Используй временный пароль только пока Telegram недоступен.
          Telegram ID всё равно должен находиться в server-side allowlist.
        </p>

        <label>
          <span>Резервный пароль</span>
          <input
            v-model="password"
            type="password"
            autocomplete="current-password"
            placeholder="Введите пароль"
            @keyup.enter="verifyPassword"
          />
        </label>

        <button class="primary" type="button" :disabled="busy" @click="verifyPassword">
          {{ busy ? 'Проверяем…' : 'Войти по паролю' }}
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
          <small>Админка</small>
        </div>
      </div>

      <nav>
        <button
          class="nav-item"
          :class="{ active: view === 'dashboard' }"
          type="button"
          @click="openDashboard"
        >
          <span>Обзор</span>
        </button>
        <div class="nav-group">
          <p>Контент</p>
          <button
            v-for="item in contentNavigation"
            :key="item.key"
            type="button"
            class="nav-item"
            :class="{ active: view === 'content' && contentSection === item.key }"
            @click="openContent(item.key)"
          >
            <span>{{ item.label }}</span>
          </button>
        </div>

        <div v-for="section in sections" :key="section.group" class="nav-group">
          <p>{{ operationGroupLabel(section.group) }}</p>
          <button v-for="item in section.items" :key="item" type="button" class="nav-item"
            :class="{ active: (item === 'GM Forge' && view === 'gmforge') || (item === 'Players' && view === 'players') || (item === 'Server' && view === 'server') || (view === 'content' && contentFocus && (item === 'Combat Simulator' && contentFocus === 'simulator' || item === 'Drafts' && contentFocus === 'drafts' || item === 'Revisions' && contentFocus === 'revisions' || item === 'Releases' && contentFocus === 'releases')) }"
            @click="openOperations(item)">
            <span>{{ operationLabel(item) }}</span>

          </button>
        </div>
      </nav>

      <div class="sidebar-footer">
        <span class="status-dot"></span>
        <div>
          <strong>Игровой сервер</strong>
          <small>game.elyndor.su</small>
        </div>
      </div>
    </aside>

    <section class="admin-main">
      <template v-if="view === 'dashboard'">
      <header class="topbar">
        <div>
          <p class="eyebrow">Управление игрой</p>
          <h1>Обзор игры</h1>
        </div>
        <div class="topbar-actions">
          <span class="session-pill">SUPER_ADMIN · до {{ formatDate(tokenExpiresAtUtc) }}</span>
          <button type="button" @click="logout">Выйти</button>
        </div>
      </header>

      <section class="hero-panel">
        <div>
          <span class="live-label">● LIVE</span>
          <h2>Панель управления Elyndor</h2>
          <p>
            Контент, публикации и тестирование GM-предметов доступны из одного интерфейса.
            Для изменений live-баланса используй проверку и предварительный просмотр публикации.
          </p>
        </div>
        <button type="button" :disabled="busy" @click="run(loadDashboard)">
          {{ busy ? 'Обновляем…' : 'Refresh' }}
        </button>
      </section>

      <section class="metric-grid">
        <article>
          <span>СЕРВЕР</span>
          <b>{{ serviceStatus?.status?.toUpperCase() ?? 'UNKNOWN' }}</b>
          <small>{{ serviceStatus?.service ?? 'Elyndor.Server' }}</small>
        </article>
        <article>
          <span>КОНТЕНТ</span>
          <b>{{ content?.contentVersion ?? '—' }}</b>
          <small>Текущая версия</small>
        </article>
        <article>
          <span>БАЛАНС</span>
          <b>{{ content?.balanceVersion ?? '—' }}</b>
          <small>Текущий баланс</small>
        </article>
        <article>
          <span>ПУБЛИКАЦИЯ</span>
          <b>{{ releaseLabel }}</b>
          <small>версия {{ revisionLabel }}</small>
        </article>
      </section>

      <section class="dashboard-grid">
        <article class="panel">
          <div class="panel-heading">
            <div>
              <p class="eyebrow">Управление контентом</p>
              <h2>Состояние игры</h2>
            </div>
            <span class="status-chip" data-ok="true"><span></span>Защищено</span>
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
              <p class="eyebrow">Последние изменения</p>
              <h2>Публикации</h2>
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
        <p class="eyebrow">Быстрые действия</p>
        <h2>С чего начать?</h2>
        <p>Выбирай нужный инструмент. Изменения контента сохраняются в draft и публикуются отдельно; Мастерская выдаёт испытательные предметы.</p>
        <div class="quick-actions">
          <button class="primary-link" type="button" @click="openContent('monsters')">Редактор контента</button>
          <button class="primary-link" type="button" @click="openContent('items')">Предметы и экипировка</button>
          <button class="primary-link" type="button" @click="openGmForge">Выдача предметов</button>
        </div>
      </section>

      <p v-if="errorMessage" class="error-message error-message--dashboard">{{ errorMessage }}</p>
      </template>

      <GmForgeView
        v-else-if="view === 'gmforge'"
        :default-telegram-id="forgeRecipientId"
        :clone-from-id="forgeCloneItemId"
        :package-json="content?.payloadJson ?? ''"
      />

      <AdminPlayersView
        v-else-if="view === 'players'"
        @forge-target="openGmForgeForPlayer"
        @clone-item="openGmForgeClone"
      />

      <section v-else-if="view === 'server'" class="server-panel">
        <p class="eyebrow">Управление · Сервер</p>
        <h1>Состояние сервера</h1>
        <p>Данные базового статуса API. Подробные показатели состояния доступны в защищённой Telegram-админке.</p>
        <dl>
          <div><dt>Сервис</dt><dd>{{ serviceStatus?.service ?? '—' }}</dd></div>
          <div><dt>Статус</dt><dd>{{ serviceStatus?.status ?? '—' }}</dd></div>
          <div><dt>Время сервера (UTC)</dt><dd>{{ serviceStatus?.utcNow ?? '—' }}</dd></div>
          <div><dt>Версия контента</dt><dd>{{ content?.contentVersion ?? '—' }}</dd></div>
          <div><dt>Последняя публикация</dt><dd>{{ content?.sourcePublishedAtUtc ?? '—' }}</dd></div>
        </dl>
        <button type="button" :disabled="busy" @click="run(loadDashboard)">{{ busy ? 'Обновление…' : 'Обновить статус' }}</button>
        <p v-if="errorMessage" class="error-message" role="alert">{{ errorMessage }}</p>
      </section>

      <AdminView
        v-else-if="view === 'content'"
        :initial-section="contentSection"
        :initial-focus="contentFocus"
        @dirty-change="contentDirty = $event"
      />
    </section>
  </div>
</template>
