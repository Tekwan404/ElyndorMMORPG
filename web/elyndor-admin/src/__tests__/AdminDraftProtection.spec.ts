import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import AdminView from '../admin/AdminView.vue'

describe('content workspace draft protection', () => {
  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
    localStorage.clear()
  })

  it('does not discard un-applied JSON when selecting another item', async () => {
    localStorage.clear()
    vi.stubGlobal('fetch', vi.fn(async (path: string) => {
      const result = String(path).includes('/content/history')
        ? { revisions: [], releases: [] }
        : {
            contentVersion: '1', balanceVersion: '1', payloadSha256: 'workspace-test-sha',
            sourcePublishedAtUtc: '2026-10-09T00:00:00Z',
            revisionId: null, releaseId: null,
            payloadJson: JSON.stringify({ items: [
              { id: 'TEST_SWORD', name: 'Sword', type: 'Equipment' },
              { id: 'TEST_SHIELD', name: 'Shield', type: 'Equipment' },
            ] }),
          }
      return new Response(JSON.stringify(result), { status: 200 })
    }))
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    const wrapper = mount(AdminView, { props: { initialSection: 'items' } })
    await flushPromises()

    const items = wrapper.findAll('.entity-list button')
    expect(items.length).toBe(2)
    await items[0]!.trigger('click')
    const jsonButton = wrapper.findAll('.editor-mode button').find(x => x.text() === 'JSON')
    await jsonButton!.trigger('click')
    await wrapper.find('.code-editor--entity').setValue(
      '{"id":"TEST_SWORD","name":"Modified sword","type":"Equipment"}',
    )
    await wrapper.findAll('.entity-list button')[1]!.trigger('click')
    await flushPromises()

    expect(confirm).toHaveBeenCalled()
    expect(wrapper.find('.editor__header h2').text()).toBe('TEST_SWORD')
    expect(wrapper.text()).toContain('неприменённые изменения')
    wrapper.unmount()
  })
})
