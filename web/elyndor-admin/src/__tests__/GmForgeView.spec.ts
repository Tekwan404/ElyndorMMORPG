import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import GmForgeView from '../admin/GmForgeView.vue'

describe('GM Forge admin form', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('prepares a 1500 damage weapon and reuses its request ID after a network retry', async () => {
    const mockFetch = vi.fn()
      .mockRejectedValueOnce(new Error('network'))
      .mockResolvedValueOnce(new Response(
        JSON.stringify({ code: 'admin_gmforge_created', message: 'DEV создан', isDuplicate: false }),
        { status: 200 },
      ))
    vi.stubGlobal('fetch', mockFetch)
    vi.stubGlobal('crypto', { randomUUID: () => 'ad0302c9-a519-48d1-bb20-898e00a39c88' })

    const wrapper = mount(GmForgeView, {
      props: {
        defaultTelegramId: '123',
        packageJson: JSON.stringify({ items: [
          { id: 'TEST_SWORD', name: 'Test Sword', type: 'Equipment', generationMode: 'Rolled' },
        ] }),
      },
    })
    await wrapper.find('#forge-item').setValue('TEST_SWORD')
    const preset = wrapper.findAll('button').find(x => x.text().includes('Пресет: 1500'))
    await preset!.trigger('click')
    expect(wrapper.text()).toContain('1500')
    const mint = wrapper.findAll('button').find(x => x.text().includes('Создать и выдать'))
    await mint!.trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('тот же ID предотвратит двойную выдачу')

    await mint!.trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('DEV создан')
    expect(mockFetch).toHaveBeenCalledTimes(2)
    const first = JSON.parse(String((mockFetch.mock.calls[0]?.[1] as RequestInit).body))
    const second = JSON.parse(String((mockFetch.mock.calls[1]?.[1] as RequestInit).body))
    expect(first.requestId).toBe(second.requestId)
    expect(first.specification).toContain('WEAPON_DAMAGE=1500')
    expect(first.specification).toContain('CRITICAL_DAMAGE=150')
    wrapper.unmount()
  })

  it('preserves clone enhancement unless explicitly overridden', async () => {
    const wrapper = mount(GmForgeView, {
      props: { defaultTelegramId: '123', packageJson: '{"items":[]}' },
    })
    const clone = wrapper.findAll('button').find(x => x.text().includes('Копировать существующий'))
    await clone!.trigger('click')
    await wrapper.find('#forge-clone').setValue('319484de-7c4f-4493-acb7-8c23701e522f')
    expect(wrapper.text()).toContain('Как в оригинале')
    expect(wrapper.find('.forge-command').text()).not.toContain('enhance=')
    wrapper.unmount()
  })
})
