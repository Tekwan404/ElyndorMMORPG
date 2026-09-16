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

let webAuthenticationData: string | null = null
let subscribedWebApp: TelegramWebApp | null = null

const viewportEventHandler: TelegramEventHandler = () => {
  if (subscribedWebApp) syncTelegramGeometry(subscribedWebApp)
}

export function getTelegramInitData(): string | null {
  const initData = window.Telegram?.WebApp?.initData
  if (initData && initData.length > 0) return initData

  return webAuthenticationData
}

export function setWebAuthenticationData(value: string | null): void {
  webAuthenticationData = value && value.length > 0 ? value : null
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
