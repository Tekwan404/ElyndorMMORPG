import { flushPromises, mount } from '@vue/test-utils'
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

  it('opens live Players, Server and GM Forge after a SUPER_ADMIN login', async () => {
    vi.stubGlobal('fetch', vi.fn(async (path: string) => {
      const url = String(path)
      const body = url.includes('/admin/auth/password')
        ? { accessToken: 'test-jwt', expiresAtUtc: '2099-01-01T00:00:00Z', roles: ['SUPER_ADMIN'] }
        : url.includes('/content/current')
          ? {
              contentVersion: '1', balanceVersion: '1',
              payloadSha256: 'test-sha', payloadJson: '{"items":[]}',
              revisionId: null, releaseId: null, sourcePublishedAtUtc: '2026-10-09T00:00:00Z',
            }
          : url.includes('/content/history')
            ? { revisions: [], releases: [] }
            : { service: 'Elyndor.Server', status: 'ready', utcNow: '2026-10-09T00:00:00Z' }
      return new Response(JSON.stringify(body), { status: 200 })
    }))

    const wrapper = mount(App)
    await wrapper.find('input[autocomplete="username"]').setValue('777')
    const login = wrapper.findAll('button').find(x => x.text().includes('резервному паролю'))
    await login!.trigger('click')
    await wrapper.find('input[autocomplete="current-password"]').setValue('password')
    const submit = wrapper.findAll('button').find(x => x.text() === 'Войти по паролю')
    await submit!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Панель управления Elyndor')
    expect(wrapper.text()).toContain('GM Forge · тестовый шмот')

    const players = wrapper.find('.sidebar').findAll('button').find(x => x.text() === 'Players')
    await players!.trigger('click')
    expect(wrapper.text()).toContain('Просмотр состояния персонажа')

    const server = wrapper.find('.sidebar').findAll('button').find(x => x.text() === 'Server')
    await server!.trigger('click')
    expect(wrapper.text()).toContain('Состояние сервера')

    const forge = wrapper.find('.sidebar').findAll('button').find(x => x.text() === 'GM Forge')
    await forge!.trigger('click')
    expect(wrapper.text()).toContain('Лаборатория экипировки')
    wrapper.unmount()
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
