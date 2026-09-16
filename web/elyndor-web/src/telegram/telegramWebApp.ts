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

let webAuthenticationData: string | null = null

export function getTelegramInitData(): string | null {
  const initData = window.Telegram?.WebApp?.initData
  if (initData && initData.length > 0) return initData

  return webAuthenticationData
}

export function setWebAuthenticationData(value: string | null): void {
  webAuthenticationData = value && value.length > 0 ? value : null
}

export function initializeTelegramWebApp(): void {
  window.Telegram?.WebApp?.ready?.()
  window.Telegram?.WebApp?.expand?.()
}
