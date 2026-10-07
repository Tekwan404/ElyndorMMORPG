interface TelegramViewportInsets {
  readonly top: number
  readonly right: number
  readonly bottom: number
  readonly left: number
}

type TelegramViewportEvent =
  | 'viewportChanged'
  | 'safeAreaChanged'
  | 'contentSafeAreaChanged'
  | 'fullscreenChanged'
  | 'fullscreenFailed'

type TelegramEventHandler = (...args: unknown[]) => void

interface TelegramWebApp {
  readonly initData: string
  readonly isFullscreen?: boolean
  readonly viewportStableHeight?: number
  readonly safeAreaInset?: Partial<TelegramViewportInsets>
  readonly contentSafeAreaInset?: Partial<TelegramViewportInsets>
  ready?: () => void
  expand?: () => void
  onEvent?: (eventType: TelegramViewportEvent, eventHandler: TelegramEventHandler) => void
  offEvent?: (eventType: TelegramViewportEvent, eventHandler: TelegramEventHandler) => void
}

declare global {
  interface Window {
    Telegram?: {
      WebApp?: TelegramWebApp
    }
  }
}

const viewportEvents: readonly TelegramViewportEvent[] = [
  'viewportChanged',
  'safeAreaChanged',
  'contentSafeAreaChanged',
  'fullscreenChanged',
  'fullscreenFailed',
]

interface StoredWebAuthenticationData {
  value: string
  expiresAtUtc: string
  telegramUserId?: string | null
}

interface RuntimeAuthenticationData {
  value: string
  expiresAtUtc: string | null
  telegramUserId: string | null
}

const webAuthenticationStorageKey = 'elyndor.telegram-web-auth.session'

let webAuthenticationData: RuntimeAuthenticationData | null = null
let subscribedWebApp: TelegramWebApp | null = null

const viewportEventHandler: TelegramEventHandler = () => {
  if (subscribedWebApp) syncTelegramGeometry(subscribedWebApp)
}

export function getTelegramInitData(): string | null {
  const miniAppInitData = getTelegramMiniAppInitData()

  if (!webAuthenticationData) {
    webAuthenticationData = readStoredWebAuthenticationData()
  }

  if (webAuthenticationData?.value.startsWith('session:')) {
    const miniAppUserId = getTelegramMiniAppUserId()
    if (
      !miniAppInitData
      || (
        webAuthenticationData.telegramUserId
        && miniAppUserId === webAuthenticationData.telegramUserId
      )
    ) {
      return webAuthenticationData.value
    }
  }

  if (miniAppInitData) return miniAppInitData
  return webAuthenticationData?.value ?? null
}

export function getTelegramMiniAppInitData(): string | null {
  const initData = window.Telegram?.WebApp?.initData
  return initData && initData.length > 0 ? initData : null
}

export function getTelegramMiniAppUserId(): string | null {
  const initData = getTelegramMiniAppInitData()
  if (!initData) return null

  try {
    const user = new URLSearchParams(initData).get('user')
    if (!user) return null
    const parsed = JSON.parse(user) as { id?: number | string }
    return typeof parsed.id === 'number' || typeof parsed.id === 'string'
      ? String(parsed.id)
      : null
  } catch {
    return null
  }
}

export function setWebAuthenticationData(
  value: string | null,
  expiresAtUtc: string | null = null,
  telegramUserId: string | null = null,
): void {
  webAuthenticationData = value && value.length > 0
    ? { value, expiresAtUtc, telegramUserId }
    : null
  if (!webAuthenticationData) {
    removeStoredWebAuthenticationData()
    return
  }

  if (!expiresAtUtc) return

  const expiresAtMs = Date.parse(expiresAtUtc)
  if (!Number.isFinite(expiresAtMs) || expiresAtMs <= Date.now()) {
    return
  }

  try {
    const stored: StoredWebAuthenticationData = {
      value: webAuthenticationData.value,
      expiresAtUtc,
      telegramUserId,
    }
    window.sessionStorage.setItem(webAuthenticationStorageKey, JSON.stringify(stored))
  } catch {
    // Keep the current in-memory session even when browser storage is unavailable.
  }
}

export function clearWebAuthenticationData(): void {
  webAuthenticationData = null
  removeStoredWebAuthenticationData()
}

function readStoredWebAuthenticationData(): RuntimeAuthenticationData | null {
  try {
    const raw = window.sessionStorage.getItem(webAuthenticationStorageKey)
    if (!raw) return null

    const stored = JSON.parse(raw) as Partial<StoredWebAuthenticationData>
    const expiresAtMs = typeof stored.expiresAtUtc === 'string'
      ? Date.parse(stored.expiresAtUtc)
      : Number.NaN
    if (
      typeof stored.value !== 'string'
      || stored.value.length === 0
      || !Number.isFinite(expiresAtMs)
      || expiresAtMs <= Date.now()
    ) {
      window.sessionStorage.removeItem(webAuthenticationStorageKey)
      return null
    }

    return {
      value: stored.value,
      expiresAtUtc: stored.expiresAtUtc,
      telegramUserId: typeof stored.telegramUserId === 'string'
        ? stored.telegramUserId
        : null,
    }
  } catch {
    removeStoredWebAuthenticationData()
    return null
  }
}

function removeStoredWebAuthenticationData(): void {
  try {
    window.sessionStorage.removeItem(webAuthenticationStorageKey)
  } catch {
    // Storage can be unavailable in hardened/private browser contexts.
  }
}

export function initializeTelegramWebApp(): void {
  const webApp = window.Telegram?.WebApp
  if (!webApp) return

  webApp.ready?.()

  // Keep Telegram in its normal full-height Mini App mode. BotFather Fullsize and
  // expand() preserve the native header/back controls on iOS and avoid turning the
  // desktop client into an immersive fullscreen window. If Telegram is already in
  // true fullscreen (for example via an explicit launch mode), geometry handling
  // below still respects its safe/content-safe areas.
  webApp.expand?.()

  subscribeToTelegramGeometry(webApp)
  syncTelegramGeometry(webApp)
}

function subscribeToTelegramGeometry(webApp: TelegramWebApp): void {
  if (subscribedWebApp === webApp) return

  if (subscribedWebApp?.offEvent) {
    for (const event of viewportEvents) {
      subscribedWebApp.offEvent(event, viewportEventHandler)
    }
  }

  subscribedWebApp = webApp
  if (!webApp.onEvent) return

  for (const event of viewportEvents) {
    webApp.onEvent(event, viewportEventHandler)
  }
}

function syncTelegramGeometry(webApp: TelegramWebApp): void {
  const root = document.documentElement
  setPixelVariable(root, '--elyndor-tg-viewport-stable-height', webApp.viewportStableHeight)
  syncInsets(root, '--elyndor-tg-safe-area', webApp.safeAreaInset)
  syncInsets(root, '--elyndor-tg-content-safe-area', webApp.contentSafeAreaInset)
  root.dataset.telegramFullscreen = webApp.isFullscreen ? 'true' : 'false'
}

function syncInsets(
  root: HTMLElement,
  prefix: string,
  insets: Partial<TelegramViewportInsets> | undefined,
): void {
  setPixelVariable(root, `${prefix}-top`, insets?.top)
  setPixelVariable(root, `${prefix}-right`, insets?.right)
  setPixelVariable(root, `${prefix}-bottom`, insets?.bottom)
  setPixelVariable(root, `${prefix}-left`, insets?.left)
}

function setPixelVariable(root: HTMLElement, name: string, value: number | undefined): void {
  if (typeof value === 'number' && Number.isFinite(value) && value >= 0) {
    root.style.setProperty(name, `${value}px`)
    return
  }

  root.style.removeProperty(name)
}
