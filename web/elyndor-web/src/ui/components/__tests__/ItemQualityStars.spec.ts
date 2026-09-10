import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import ItemQualityStars from '../ItemQualityStars.vue'

describe('ItemQualityStars', () => {
  it('renders one filled SVG star for a one-star item', () => {
    const wrapper = mount(ItemQualityStars, { props: { stars: 1 } })

    expect(wrapper.get('[data-item-quality-stars]').attributes('aria-label')).toBe(
      '\u041a\u0430\u0447\u0435\u0441\u0442\u0432\u043e \u043f\u0440\u0435\u0434\u043c\u0435\u0442\u0430: 1 \u0438\u0437 5',
    )
    expect(wrapper.findAll('[data-quality-star="filled"]')).toHaveLength(1)
    expect(wrapper.findAll('[data-quality-star="empty"]')).toHaveLength(4)
  })

  it('renders five filled SVG stars for a perfect five-star item', () => {
    const wrapper = mount(ItemQualityStars, { props: { stars: 5 } })

    expect(wrapper.findAll('[data-quality-star="filled"]')).toHaveLength(5)
    expect(wrapper.findAll('[data-quality-star="empty"]')).toHaveLength(0)
    expect(wrapper.findAll('svg')).toHaveLength(5)
  })

  it('does not render a quality marker when the item has no generated stars', () => {
    const wrapper = mount(ItemQualityStars, { props: { stars: null } })

    expect(wrapper.find('[data-item-quality-stars]').exists()).toBe(false)
  })
})
