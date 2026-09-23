import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import UIHealthBar from '@/ui/components/UIHealthBar.vue'

describe('UIHealthBar', () => {
  it('classifies hp into healthy, warning and critical states', async () => {
    const wrapper = mount(UIHealthBar, {
      props: { value: 100, max: 100, label: 'Здоровье' },
    })

    expect(wrapper.classes()).toContain('ui-bar--healthy')

    await wrapper.setProps({ value: 50 })
    expect(wrapper.classes()).toContain('ui-bar--warning')

    await wrapper.setProps({ value: 25 })
    expect(wrapper.classes()).toContain('ui-bar--critical')
  })

  it('does not apply hp danger states to class resources', () => {
    const wrapper = mount(UIHealthBar, {
      props: { value: 10, max: 100, tone: 'rage', label: 'Ярость' },
    })

    expect(wrapper.classes()).toContain('ui-bar--rage')
    expect(wrapper.classes()).not.toContain('ui-bar--critical')
    expect(wrapper.classes()).not.toContain('ui-bar--warning')
  })
})
