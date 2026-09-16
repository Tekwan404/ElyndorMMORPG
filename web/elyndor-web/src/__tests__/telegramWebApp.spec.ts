import { afterEach, describe, expect, it } from 'vitest'

import {
  getTelegramInitData,
  setWebAuthenticationData,
} from '@/telegram/telegramWebApp'

describe('telegramWebApp authentication data', () => {
  afterEach(() => {
    setWebAuthenticationData(null)
    delete window.Telegram
  })

  it('uses a runtime browser credential when Mini App initData is absent', () => {
    setWebAuthenticationData('web:signed-browser-credential')

    expect(getTelegramInitData()).toBe('web:signed-browser-credential')
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
})
