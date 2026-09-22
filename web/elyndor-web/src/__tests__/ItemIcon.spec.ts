import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import ItemIcon from '@/game/items/components/ItemIcon.vue'

describe('ItemIcon', () => {
  const props = {
    itemId: 'RECRUIT_IRON_SWORD',
    name: 'Recruit iron sword',
    type: 'Equipment',
    equipmentSlot: 'MainHand',
    rarity: 'Common',
  }

  it('renders a canonical item asset', () => {
    const wrapper = mount(ItemIcon, {
      props: { ...props, iconId: 'sets/ancient-mine/mine_tracker/mine_tracker_helmet' },
    })

    expect(wrapper.get('img').attributes('src')).toMatch(/mine_tracker_helmet\.webp$/)
  })

  it('renders a generated fallback when IconId is absent', () => {
    const wrapper = mount(ItemIcon, { props })

    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.get('.icon-generator').attributes('data-icon-id')).toBe('item-RECRUIT_IRON_SWORD')
  })

  it('replaces a browser-failed asset with a generated fallback', async () => {
    const wrapper = mount(ItemIcon, {
      props: { ...props, iconId: 'sets/ancient-mine/mine_tracker/mine_tracker_helmet' },
    })

    await wrapper.get('img').trigger('error')

    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.find('.icon-generator').exists()).toBe(true)
  })
})
