import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it } from 'vitest'

import type { ArenaHonorShop } from '../arenaContracts'
import ArenaHonorShop from './ArenaHonorShop.vue'

function shop(): ArenaHonorShop {
  return {
    honor: 140,
    items: [
      {
        itemId: 'L60_PVP_T1_WARRIOR_GUARDIAN_HEAD',
        name: 'Шлем «Оплот Железного Круга»',
        rarity: 'Legendary',
        slot: 'Head',
        iconId: 'sets/set_heart_of_blighted_grove_warrior_guardian_head',
        setId: 'SET_L60_PVP_T1_WARRIOR_GUARDIAN',
        requiredLevel: 1,
        honorPrice: 80,
      },
      {
        itemId: 'L60_PVP_T1_WARRIOR_BERSERKER_HANDS',
        name: 'Рукавицы «Клятва Багрового Претендента»',
        rarity: 'Legendary',
        slot: 'Hands',
        iconId: 'sets/set_heart_of_blighted_grove_warrior_berserker_hands',
        setId: 'SET_L60_PVP_T1_WARRIOR_BERSERKER',
        requiredLevel: 60,
        honorPrice: 60,
      },
    ],
  }
}

describe('ArenaHonorShop', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('renders Honor balance, groups offers by set and buys the selected piece', async () => {
    const wrapper = mount(ArenaHonorShop, {
      props: {
        shop: shop(),
        loading: false,
        pendingItemId: null,
        errorMessage: null,
      },
      global: { stubs: { ItemIcon: true } },
    })

    expect(wrapper.text()).toContain('Магазин чести')
    expect(wrapper.text()).toContain('140')
    expect(wrapper.text()).toContain('Оплот Железного Круга')
    expect(wrapper.text()).not.toContain('Клятва Багрового Претендента')

    const buy = wrapper.findAll('button').find(button => button.text() === 'Купить')
    expect(buy).toBeDefined()
    await buy!.trigger('click')

    expect(wrapper.emitted('buy')).toEqual([
      ['L60_PVP_T1_WARRIOR_GUARDIAN_HEAD'],
    ])
  })

  it('switches between class sets and explains level gating', async () => {
    const wrapper = mount(ArenaHonorShop, {
      props: {
        shop: shop(),
        loading: false,
        pendingItemId: null,
        errorMessage: null,
      },
      global: { stubs: { ItemIcon: true } },
    })

    const berserkerTab = wrapper.findAll('button')
      .find(button => button.text().includes('Клятва Багрового Претендента'))
    expect(berserkerTab).toBeDefined()
    await berserkerTab!.trigger('click')

    expect(wrapper.text()).toContain('Рукавицы')
    expect(wrapper.text()).toContain('Нужен ур. 60')
    const gated = wrapper.findAll('button').find(button => button.text() === 'Нужен ур. 60')
    expect(gated?.attributes('disabled')).toBeDefined()
  })

  it('shows the server error without hiding the available offers', () => {
    const wrapper = mount(ArenaHonorShop, {
      props: {
        shop: shop(),
        loading: false,
        pendingItemId: null,
        errorMessage: 'Недостаточно чести для этой покупки.',
      },
      global: { stubs: { ItemIcon: true } },
    })

    expect(wrapper.get('[role="alert"]').text()).toContain('Недостаточно чести')
    expect(wrapper.text()).toContain('Оплот Железного Круга')
  })
})
