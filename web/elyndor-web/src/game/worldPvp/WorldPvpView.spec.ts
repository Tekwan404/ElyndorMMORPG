import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import WorldPvpView from './WorldPvpView.vue'
import { worldPvpPreviewDashboard } from './worldPvpPreviewDashboard'

describe('WorldPvpView', () => {
  it('shows truthful empty states without making up arena statistics or currency', async () => {
    const wrapper = mount(WorldPvpView)

    expect(wrapper.get('[data-world-pvp-season-status]').text()).toBe('Скоро')
    expect(wrapper.get('[data-world-pvp-overview-empty]').text()).toContain('ещё не началась')
    expect(wrapper.find('[data-world-pvp-preview]').exists()).toBe(false)

    await wrapper.get('[data-world-pvp-tab="rating"]').trigger('click')
    expect(wrapper.get('[data-world-pvp-rating-empty]').text()).toContain('арены не используются')

    await wrapper.get('[data-world-pvp-tab="rewards"]').trigger('click')
    expect(wrapper.get('[data-world-pvp-rewards-empty]').text()).toContain('не выполняет')

    await wrapper.get('[data-world-pvp-tab="bounties"]').trigger('click')
    expect(wrapper.get('[data-world-pvp-bounties-empty]').text()).toContain('Никто не объявлен')

    await wrapper.get('[data-world-pvp-tab="titles"]').trigger('click')
    expect(wrapper.get('[data-world-pvp-titles-empty]').text()).toContain('Летопись титулов')
    expect(wrapper.find('[data-world-pvp-title]').exists()).toBe(false)
    expect(wrapper.find('button[data-buy]').exists()).toBe(false)
  })

  it('keeps independent World PvP rating and filters human or undead results', async () => {
    const wrapper = mount(WorldPvpView, { props: { dashboard: worldPvpPreviewDashboard, preview: true } })

    expect(wrapper.get('[data-world-pvp-preview]').text()).toContain('вымышлены')
    expect(wrapper.get('[data-world-pvp-panel="overview"]').text()).toMatch(/3[\s\u00a0\u202f]450/)
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

  it('shows only server-provided title unlocks and progress without granting titles', async () => {
    const wrapper = mount(WorldPvpView, {
      props: { dashboard: worldPvpPreviewDashboard, preview: true },
    })
    await wrapper.get('[data-world-pvp-tab="titles"]').trigger('click')

    expect(wrapper.findAll('[data-world-pvp-title]')).toHaveLength(3)
    expect(wrapper.get('[data-world-pvp-title="preview-hunter"]').text()).toContain('Выбран')
    expect(wrapper.get('[data-world-pvp-title="preview-executioner"]').text()).toContain('6 / 10')
    expect(wrapper.get('[data-world-pvp-title="preview-executioner"]').text()).toContain('Не открыт')
    const progress = wrapper.get('[data-world-pvp-title="preview-executioner"] [role="progressbar"]')
    expect(progress.attributes('aria-valuenow')).toBe('6')
    expect(progress.attributes('aria-valuemax')).toBe('10')
    expect(wrapper.find('[data-world-pvp-title] button').exists()).toBe(false)
    wrapper.unmount()
  })

  it('offers an explicit retry without calling Arena or performing purchases', async () => {
    const wrapper = mount(WorldPvpView, { props: { error: 'Не удалось загрузить данные' } })
    await wrapper.get('[data-world-pvp-error] button').trigger('click')
    expect(wrapper.emitted('refresh')).toHaveLength(1)
  })
})
