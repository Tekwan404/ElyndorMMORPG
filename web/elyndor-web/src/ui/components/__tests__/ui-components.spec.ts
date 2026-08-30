import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import UIButton from '../UIButton.vue'
import UIItemSlot from '../UIItemSlot.vue'
import UIModal from '../UIModal.vue'
import UITabs from '../UITabs.vue'

describe('Arcane Minimal UI primitives', () => {
  it('disables UIButton while loading and exposes busy state', () => {
    const wrapper = mount(UIButton, {
      props: { loading: true },
      slots: { default: 'Travel' },
    })

    expect(wrapper.get('button').attributes('disabled')).toBeDefined()
    expect(wrapper.get('button').attributes('aria-busy')).toBe('true')
    expect(wrapper.text()).toContain('Travel')
  })

  it('emits an enabled tab and ignores a disabled tab', async () => {
    const wrapper = mount(UITabs, {
      props: {
        modelValue: 'items',
        tabs: [
          { value: 'items', label: 'Items' },
          { value: 'stats', label: 'Stats' },
          { value: 'locked', label: 'Locked', disabled: true },
        ],
      },
    })

    await wrapper.get('[data-tab="stats"]').trigger('click')
    await wrapper.get('[data-tab="locked"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')).toEqual([['stats']])
  })

  it('renders modal dialog semantics and emits close', async () => {
    const wrapper = mount(UIModal, {
      props: { open: true, title: 'Details' },
      attachTo: document.body,
    })

    expect(document.body.querySelector('[role="dialog"]')).not.toBeNull()
    const closeButton = document.body.querySelector<HTMLButtonElement>('[data-modal-close]')
    expect(closeButton).not.toBeNull()
    closeButton?.click()
    await wrapper.vm.$nextTick()
    expect(wrapper.emitted('close')).toHaveLength(1)
    wrapper.unmount()
  })

  it('describes a locked item slot accessibly', () => {
    const wrapper = mount(UIItemSlot, {
      props: {
        label: 'Ancient chest',
        icon: { id: 'chest', glyph: 'chest', category: 'utility', state: 'locked' },
      },
    })

    expect(wrapper.get('[data-item-slot]').attributes('aria-label')).toBe('Ancient chest, locked')
  })
})
