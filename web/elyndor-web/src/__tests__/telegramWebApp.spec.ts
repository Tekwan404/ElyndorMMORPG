import { afterEach, describe, expect, it } from 'vitest'

import {
  clearWebAuthenticationData,
  getTelegramInitData,
  initializeTelegramWebApp,
  setWebAuthenticationData,
} from '@/telegram/telegramWebApp'

const webAuthenticationStorageKey = 'elyndor.telegram-web-auth.session'

const geometryVariables = [
  '--elyndor-tg-viewport-stable-height',
  '--elyndor-tg-safe-area-top',
  '--elyndor-tg-safe-area-right',
  '--elyndor-tg-safe-area-bottom',
  '--elyndor-tg-safe-area-left',
  '--elyndor-tg-content-safe-area-top',
  '--elyndor-tg-content-safe-area-right',
  '--elyndor-tg-content-safe-area-bottom',
  '--elyndor-tg-content-safe-area-left',
]

describe('telegramWebApp authentication data', () => {
  afterEach(() => {
    setWebAuthenticationData(null)
    window.sessionStorage.removeItem(webAuthenticationStorageKey)
    delete window.Telegram
    delete document.documentElement.dataset.telegramFullscreen
    for (const variable of geometryVariables) {
      document.documentElement.style.removeProperty(variable)
    }
  })

  it('uses a runtime browser credential when Mini App initData is absent', () => {
    setWebAuthenticationData('web:signed-browser-credential')

    expect(getTelegramInitData()).toBe('web:signed-browser-credential')
  })

  it('restores a valid browser credential from session storage after a page reload', () => {
    const expiresAtUtc = new Date(Date.now() + 60_000).toISOString()
    window.sessionStorage.setItem(webAuthenticationStorageKey, JSON.stringify({
      value: 'web:persisted-browser-credential',
      expiresAtUtc,
    }))

    expect(getTelegramInitData()).toBe('web:persisted-browser-credential')
  })

  it('does not restore an expired browser credential', () => {
    const expiresAtUtc = new Date(Date.now() - 60_000).toISOString()
    window.sessionStorage.setItem(webAuthenticationStorageKey, JSON.stringify({
      value: 'web:expired-browser-credential',
      expiresAtUtc,
    }))

    expect(getTelegramInitData()).toBeNull()
    expect(window.sessionStorage.getItem(webAuthenticationStorageKey)).toBeNull()
  })

  it('persists the browser credential when the server supplies its expiry', () => {
    const expiresAtUtc = new Date(Date.now() + 60_000).toISOString()

    setWebAuthenticationData('web:signed-browser-credential', expiresAtUtc)

    expect(window.sessionStorage.getItem(webAuthenticationStorageKey)).toBe(JSON.stringify({
      value: 'web:signed-browser-credential',
      expiresAtUtc,
    }))
  })

  it('clears both the in-memory and persisted browser credential', () => {
    const expiresAtUtc = new Date(Date.now() + 60_000).toISOString()
    setWebAuthenticationData('web:stale-browser-credential', expiresAtUtc)

    clearWebAuthenticationData()

    expect(getTelegramInitData()).toBeNull()
    expect(window.sessionStorage.getItem(webAuthenticationStorageKey)).toBeNull()
  })

  it('always prefers real Telegram Mini App initData', () => {
    setWebAuthenticationData('web:signed-browser-credential')
    window.Telegram = {
      WebApp: {
        initData: 'signed-mini-app-init-data',
      },
    }

    expect(getTelegramInitData()).toBe('signed-mini-app-init-data')
  })

  it('keeps the Mini App expanded and mirrors Telegram viewport geometry into CSS variables', () => {
    const handlers = new Map<string, (...args: unknown[]) => void>()
    let readyCalls = 0
    let expandCalls = 0
    const webApp = {
      initData: 'signed-mini-app-init-data',
      isFullscreen: false,
      viewportStableHeight: 844,
      safeAreaInset: { top: 10, right: 2, bottom: 18, left: 2 },
      contentSafeAreaInset: { top: 54, right: 4, bottom: 24, left: 4 },
      ready: () => { readyCalls += 1 },
      expand: () => { expandCalls += 1 },
      onEvent: (eventType: string, handler: (...args: unknown[]) => void) => {
        handlers.set(eventType, handler)
      },
      offEvent: () => {},
    }
    window.Telegram = { WebApp: webApp }

    initializeTelegramWebApp()

    expect(readyCalls).toBe(1)
    expect(expandCalls).toBe(1)
    expect(document.documentElement.style.getPropertyValue('--elyndor-tg-viewport-stable-height')).toBe('844px')
    expect(document.documentElement.style.getPropertyValue('--elyndor-tg-safe-area-top')).toBe('10px')
    expect(document.documentElement.style.getPropertyValue('--elyndor-tg-content-safe-area-top')).toBe('54px')
    expect(document.documentElement.dataset.telegramFullscreen).toBe('false')

    webApp.viewportStableHeight = 1024
    webApp.isFullscreen = true
    webApp.contentSafeAreaInset.top = 62
    handlers.get('fullscreenChanged')?.()

    expect(document.documentElement.style.getPropertyValue('--elyndor-tg-viewport-stable-height')).toBe('1024px')
    expect(document.documentElement.style.getPropertyValue('--elyndor-tg-content-safe-area-top')).toBe('62px')
    expect(document.documentElement.dataset.telegramFullscreen).toBe('true')
  })

  it('works on clients that only expose the normal expanded Mini App API', () => {
    let expandCalls = 0
    window.Telegram = {
      WebApp: {
        initData: 'signed-mini-app-init-data',
        ready: () => {},
        expand: () => { expandCalls += 1 },
      },
    }

    expect(() => initializeTelegramWebApp()).not.toThrow()
    expect(expandCalls).toBe(1)
  })
})
