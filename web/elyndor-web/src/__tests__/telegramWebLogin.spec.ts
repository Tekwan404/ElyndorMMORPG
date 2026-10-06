import { afterEach, describe, expect, it } from 'vitest'

import { resetPendingTelegramWebLogin } from '@/telegram/telegramWebLogin'

const pendingLoginKey = 'elyndor.telegram-web-login.pending'

describe('telegramWebLogin state recovery', () => {
  afterEach(() => {
    window.sessionStorage.removeItem(pendingLoginKey)
  })

  it('drops stale PKCE state before a fresh browser login attempt', () => {
    window.sessionStorage.setItem(pendingLoginKey, JSON.stringify({
      state: 'stale-state',
      codeVerifier: 'stale-verifier',
      startedAtMs: Date.now() - 60_000,
    }))

    resetPendingTelegramWebLogin()

    expect(window.sessionStorage.getItem(pendingLoginKey)).toBeNull()
  })
})
