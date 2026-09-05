import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'

import App from '../App.vue'

describe('Admin V2 foundation', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('starts with the dedicated Telegram admin login', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
      new Response(JSON.stringify({
        service: 'Elyndor.Server',
        status: 'ready',
        utcNow: '2026-09-05T18:00:00Z',
      }), { status: 200, headers: { 'Content-Type': 'application/json' } }),
    ))

    const wrapper = mount(App)
    expect(wrapper.text()).toContain('Elyndor Admin')
    expect(wrapper.text()).toContain('Войти через Telegram')
    expect(wrapper.find('input[autocomplete="username"]').exists()).toBe(true)
    expect(wrapper.text()).not.toContain('Content Workspace migration')
  })
})
