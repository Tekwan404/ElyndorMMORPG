import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import GmForgeView from '../admin/GmForgeView.vue'

describe('GM Forge admin form', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('prepares a 1500 damage weapon and reuses its request ID after a network retry', async () => {
    const mockFetch = vi.fn<typeof fetch>()
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
    const firstCall = mockFetch.mock.calls[0]
    const secondCall = mockFetch.mock.calls[1]
    if (!firstCall || !secondCall) throw new Error('Expected two mint requests')
    const first = JSON.parse(String(firstCall[1]?.body))
    const second = JSON.parse(String(secondCall[1]?.body))
    expect(first.requestId).toBe(second.requestId)
    expect(first.specification).toContain('WEAPON_DAMAGE=1500')
    expect(first.specification).toContain('CRITICAL_DAMAGE=150')
    wrapper.unmount()
  })

  it('grants an ordinary item without any affix configuration', async () => {
    const mockFetch = vi.fn<typeof fetch>().mockResolvedValue(
      new Response(JSON.stringify({ items: [
        { index: 0, isSuccess: true, isDuplicate: false, code: 'admin_item_granted', message: 'Выдано' },
      ] }), { status: 200 }),
    )
    vi.stubGlobal('fetch', mockFetch)
    vi.stubGlobal('crypto', { randomUUID: () => '04e72af5-4b11-495f-8e92-b6f04412ec4c' })
    const wrapper = mount(GmForgeView, {
      props: {
        defaultTelegramId: '123',
        packageJson: JSON.stringify({ items: [
          { id: 'TEST_ORE', name: 'Тестовая руда', type: 'Material' },
        ] }),
      },
    })
    await wrapper.find('#forge-item').setValue('TEST_ORE')
    await wrapper.find('#forge-quantity').setValue('25')
    const action = wrapper.findAll('button').find(x => x.text().includes('Создать и выдать предмет'))
    await action!.trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Выдано позиций: 1 из 1')
    const call = mockFetch.mock.calls[0]
    if (!call) throw new Error('Expected batch request')
    expect(String(call[0])).toContain('/api/v1/admin/gm-forge/batch')
    const sent = JSON.parse(String(call[1]?.body))
    expect(sent.items[0]).toEqual({ mode: 'regular', specification: 'TEST_ORE 25 NORMAL' })
    wrapper.unmount()
  })

  it('queues two different equipment types for one batch delivery', async () => {
    const mockFetch = vi.fn<typeof fetch>().mockResolvedValue(
      new Response(JSON.stringify({ items: [
        { index: 0, isSuccess: true, isDuplicate: false, code: 'admin_item_granted', message: 'Первый' },
        { index: 1, isSuccess: true, isDuplicate: false, code: 'admin_item_granted', message: 'Второй' },
      ] }), { status: 200 }),
    )
    vi.stubGlobal('fetch', mockFetch)
    vi.stubGlobal('crypto', { randomUUID: () => '3be6b1b3-e98d-4014-b08a-a3700452d812' })
    const wrapper = mount(GmForgeView, {
      props: { defaultTelegramId: '123', packageJson: JSON.stringify({ items: [
        { id: 'SWORD', name: 'Меч', type: 'Equipment' },
        { id: 'SHIELD', name: 'Щит', type: 'Equipment' },
      ] }) },
    })
    await wrapper.find('#forge-item').setValue('SWORD')
    await wrapper.findAll('button').find(x => x.text().includes('Добавить в список'))!.trigger('click')
    await wrapper.find('#forge-item').setValue('SHIELD')
    await wrapper.findAll('button').find(x => x.text().includes('Добавить в список'))!.trigger('click')
    await wrapper.findAll('button').find(x => x.text().includes('Выдать всё (2)'))!.trigger('click')
    await flushPromises()
    const call = mockFetch.mock.calls[0]
    if (!call) throw new Error('Expected batch request')
    const sent = JSON.parse(String(call[1]?.body))
    expect(sent.items).toHaveLength(2)
    expect(sent.items[0].specification).toContain('SWORD')
    expect(sent.items[1].specification).toContain('SHIELD')
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
