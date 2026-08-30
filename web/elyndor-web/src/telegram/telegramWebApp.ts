interface TelegramWebApp {
  readonly initData: string
  ready?: () => void
  expand?: () => void
}

declare global {
  interface Window {
    Telegram?: {
      WebApp?: TelegramWebApp
    }
  }
}

export function getTelegramInitData(): string | null {
  const initData = window.Telegram?.WebApp?.initData
  return initData && initData.length > 0 ? initData : null
}

export function initializeTelegramWebApp(): void {
  window.Telegram?.WebApp?.ready?.()
  window.Telegram?.WebApp?.expand?.()
}
