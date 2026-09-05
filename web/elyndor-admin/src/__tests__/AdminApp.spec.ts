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
    expect(wrapper.text()).toContain('Telegram недоступен? Войти по резервному паролю')
    expect(wrapper.text()).not.toContain('Content Workspace migration')
  })

  it('opens the break-glass password form for an allowed Telegram id candidate', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
      new Response(JSON.stringify({
        service: 'Elyndor.Server',
        status: 'ready',
        utcNow: '2026-09-05T18:00:00Z',
      }), { status: 200, headers: { 'Content-Type': 'application/json' } }),
    ))

    const wrapper = mount(App)
    await wrapper.find('input[autocomplete="username"]').setValue('42')
    const button = wrapper.findAll('button').find(
      item => item.text().includes('резервному паролю'),
    )
    expect(button).toBeDefined()
    await button!.trigger('click')

    expect(wrapper.text()).toContain('Резервный вход')
    expect(wrapper.find('input[autocomplete="current-password"]').exists()).toBe(true)
  })
})
