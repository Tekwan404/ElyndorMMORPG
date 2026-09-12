import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { nextTick } from 'vue'
import { describe, expect, it } from 'vitest'

import { useGameSessionStore } from '@/stores/gameSession'
import BetaErrorOverlay from '@/ui/BetaErrorOverlay.vue'

describe('BetaErrorOverlay', () => {
  it('stays visible until manual dismissal and reopens for a new diagnostic', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    const session = useGameSessionStore(pinia)
    const wrapper = mount(BetaErrorOverlay, { global: { plugins: [pinia] } })

    session.errorCode = 'star_upgrade_profile_missing'
    session.errorCorrelationId = 'request-1'
    await nextTick()

    expect(wrapper.find('[data-beta-error-diagnostic]').exists()).toBe(true)
    expect(wrapper.text()).toContain('star_upgrade_profile_missing')
    expect(wrapper.text()).toContain('request-1')

    await wrapper.get('button[aria-label="Закрыть диагностическую ошибку"]').trigger('click')
    expect(wrapper.find('[data-beta-error-diagnostic]').exists()).toBe(false)

    session.errorCorrelationId = 'request-2'
    await nextTick()

    expect(wrapper.find('[data-beta-error-diagnostic]').exists()).toBe(true)
    expect(wrapper.text()).toContain('request-2')
  })

  it('renders a correlation id even when an error code is unavailable', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    const session = useGameSessionStore(pinia)
    session.errorCorrelationId = 'correlation-only'

    const wrapper = mount(BetaErrorOverlay, { global: { plugins: [pinia] } })
    await nextTick()

    expect(wrapper.find('[data-beta-error-diagnostic]').exists()).toBe(true)
    expect(wrapper.text()).toContain('correlation-only')
  })
})
