import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import WorldPvpView from './WorldPvpView.vue'
import { worldPvpPreviewDashboard } from './worldPvpPreviewDashboard'

describe('WorldPvpView', () => {
  it('shows truthful empty states without making up arena statistics or currency', async () => {
    const wrapper = mount(WorldPvpView)

    expect(wrapper.get('[data-world-pvp-season-status]').text()).toBe('Скоро')
    expect(wrapper.get('[data-world-pvp-overview-empty]').text()).toContain('ещё не началась')
    expect(wrapper.find('[data-world-pvp-preview]').exists()).toBe(false)

    await wrapper.get('[data-world-pvp-tab="rating"]').trigger('click')
    expect(wrapper.get('[data-world-pvp-rating-empty]').text()).toContain('Арены не используются')

    await wrapper.get('[data-world-pvp-tab="rewards"]').trigger('click')
    expect(wrapper.get('[data-world-pvp-rewards-empty]').text()).toContain('не выполняет')

    await wrapper.get('[data-world-pvp-tab="bounties"]').trigger('click')
    expect(wrapper.get('[data-world-pvp-bounties-empty]').text()).toContain('Никто не объявлен')

    expect(wrapper.find('button[data-buy]').exists()).toBe(false)
  })

  it('keeps independent World PvP rating and filters human or undead results', async () => {
    const wrapper = mount(WorldPvpView, { props: { dashboard: worldPvpPreviewDashboard, preview: true } })

    expect(wrapper.get('[data-world-pvp-preview]').text()).toContain('вымышлены')
    expect(wrapper.get('[data-world-pvp-panel="overview"]').text()).toContain('3 450')
    await wrapper.get('[data-world-pvp-tab="rating"]').trigger('click')

    expect(wrapper.findAll('[data-world-pvp-standing]')).toHaveLength(4)

    await wrapper.get('[data-world-pvp-rating-scope="HUMAN"]').trigger('click')
    expect(wrapper.findAll('[data-world-pvp-standing]')).toHaveLength(2)
    expect(wrapper.get('[data-world-pvp-standing="preview-human-a"]').text()).toContain('Арториус')

    await wrapper.get('[data-world-pvp-rating-scope="UNDEAD"]').trigger('click')
    expect(wrapper.findAll('[data-world-pvp-standing]')).toHaveLength(2)
    expect(wrapper.get('[data-world-pvp-standing="preview-undead-hero"]').classes()).toContain('is-self')
  })

  it('filters World PvP rewards and never offers client-side purchase', async () => {
    const wrapper = mount(WorldPvpView, { props: { dashboard: worldPvpPreviewDashboard } })
    await wrapper.get('[data-world-pvp-tab="rewards"]').trigger('click')
    expect(wrapper.findAll('[data-world-pvp-reward]')).toHaveLength(4)
    expect(wrapper.get('[data-world-pvp-panel="rewards"]').text()).toContain('Кровавые жетоны')

    await wrapper.get('[data-world-pvp-reward-filter="Cosmetic"]').trigger('click')
    expect(wrapper.findAll('[data-world-pvp-reward]')).toHaveLength(2)
    expect(wrapper.get('[data-world-pvp-panel="rewards"]').text()).toContain('Покупка пока недоступна')
    expect(wrapper.find('button[data-buy]').exists()).toBe(false)
  })

  it('hides allied bounty targets when personal faction is known', async () => {
    const wrapper = mount(WorldPvpView, { props: { dashboard: worldPvpPreviewDashboard } })
    await wrapper.get('[data-world-pvp-tab="bounties"]').trigger('click')

    expect(wrapper.findAll('[data-world-pvp-bounty]')).toHaveLength(2)
    expect(wrapper.get('[data-world-pvp-bounty="preview-bounty-a"]').text()).toContain('Арториус')
    expect(wrapper.find('[data-world-pvp-bounty="preview-bounty-c"]').exists()).toBe(false)
  })

  it('offers an explicit retry without calling Arena or performing purchases', async () => {
    const retry = vi.fn()
    const wrapper = mount(WorldPvpView, { props: { error: 'Не удалось загрузить данные', onRefresh: retry } })
    await wrapper.get('[data-world-pvp-error] button').trigger('click')
    expect(retry).toHaveBeenCalledTimes(1)
  })
})
