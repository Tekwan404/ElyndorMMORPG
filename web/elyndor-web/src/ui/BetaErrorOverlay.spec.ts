import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { nextTick } from 'vue'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { useGameSessionStore } from '@/stores/gameSession'
import BetaErrorOverlay from '@/ui/BetaErrorOverlay.vue'

afterEach(() => {
  vi.useRealTimers()
})

describe('BetaErrorOverlay', () => {
  it('auto-hides after the beta timeout and restarts the timer for a repeated error', async () => {
    vi.useFakeTimers()
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

    vi.advanceTimersByTime(5_000)
    session.errorCorrelationId = 'request-2'
    await nextTick()

    vi.advanceTimersByTime(7_999)
    await nextTick()
    expect(wrapper.find('[data-beta-error-diagnostic]').exists()).toBe(true)
    expect(wrapper.text()).toContain('request-2')

    vi.advanceTimersByTime(1)
    await nextTick()
    expect(wrapper.find('[data-beta-error-diagnostic]').exists()).toBe(false)
  })

  it('can be closed manually before the timeout and reopens for a new diagnostic', async () => {
    vi.useFakeTimers()
    const pinia = createPinia()
    setActivePinia(pinia)
    const session = useGameSessionStore(pinia)
    const wrapper = mount(BetaErrorOverlay, { global: { plugins: [pinia] } })

    session.errorCode = 'inventory_conflict'
    session.errorCorrelationId = 'request-1'
    await nextTick()

    await wrapper.get('button[aria-label="Закрыть диагностическую ошибку"]').trigger('click')
    expect(wrapper.find('[data-beta-error-diagnostic]').exists()).toBe(false)

    session.errorCorrelationId = 'request-2'
    await nextTick()

    expect(wrapper.find('[data-beta-error-diagnostic]').exists()).toBe(true)
  })

  it('renders a correlation id even when an error code is unavailable', async () => {
    vi.useFakeTimers()
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
