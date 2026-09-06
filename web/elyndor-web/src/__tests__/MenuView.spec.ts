import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import MenuView from '@/app/MenuView.vue'
import { useGameSessionStore } from '@/stores/gameSession'

describe('MenuView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    delete document.documentElement.dataset.elyndorAtmosphere
    delete document.documentElement.dataset.elyndorMotion
  })

  it('persists presentation preferences and applies them to the document', async () => {
    const session = useGameSessionStore()
    session.state = 'world'

    const wrapper = mount(MenuView)

    expect(wrapper.get('[data-setting-atmosphere]').attributes('aria-checked')).toBe('true')
    expect(document.documentElement.dataset.elyndorAtmosphere).toBe('on')
    expect(document.documentElement.dataset.elyndorMotion).toBe('system')

    await wrapper.get('[data-setting-atmosphere]').trigger('click')
    expect(wrapper.get('[data-setting-atmosphere]').attributes('aria-checked')).toBe('false')
    expect(document.documentElement.dataset.elyndorAtmosphere).toBe('off')
    expect(localStorage.getItem('elyndor.ui.atmosphere')).toBe('off')

    await wrapper.get('[data-motion-option="reduced"]').trigger('click')
    expect(document.documentElement.dataset.elyndorMotion).toBe('reduced')
    expect(localStorage.getItem('elyndor.ui.motion')).toBe('reduced')
  })

  it('requests a fresh authoritative snapshot from the system card', async () => {
    const session = useGameSessionStore()
    session.state = 'world'
    const refresh = vi.spyOn(session, 'refreshSnapshot').mockResolvedValue(undefined)

    const wrapper = mount(MenuView)

    await wrapper.get('[data-sync-world]').trigger('click')
    await flushPromises()

    expect(refresh).toHaveBeenCalledTimes(1)
    expect(wrapper.get('[data-sync-result="success"]').text()).toContain('Состояние обновлено')
  })
})
