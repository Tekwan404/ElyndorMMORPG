import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

vi.mock('@/assets/itemArt', () => ({
  resolveItemArtUrl: (iconId: string | null | undefined) => iconId === 'broken' ? '/broken.webp' : undefined,
}))

import ItemIcon from './ItemIcon.vue'

describe('ItemIcon', () => {
  it('renders a generated fallback when no authored asset resolves', () => {
    const wrapper = mount(ItemIcon, {
      props: {
        itemId: 'IRON_ORE',
        name: 'Железная руда',
        type: 'Material',
        iconId: 'missing',
      },
    })

    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.attributes('data-item-icon-fallback')).toBe('true')
    expect(wrapper.find('[data-icon-id="item-IRON_ORE"]').exists()).toBe(true)
  })

  it('falls back when a resolved image fails at runtime', async () => {
    const wrapper = mount(ItemIcon, {
      props: {
        itemId: 'BROKEN_ITEM',
        name: 'Broken item',
        type: 'Equipment',
        iconId: 'broken',
      },
    })

    expect(wrapper.find('img').exists()).toBe(true)
    await wrapper.find('img').trigger('error')
    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.attributes('data-item-icon-fallback')).toBe('true')
    expect(wrapper.find('[data-icon-id="item-BROKEN_ITEM"]').exists()).toBe(true)
  })
})
