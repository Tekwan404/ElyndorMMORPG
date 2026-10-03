import { enableAutoUnmount, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
enableAutoUnmount(afterEach)

import UIButton from '../UIButton.vue'
import UIItemSlot from '../UIItemSlot.vue'
import UIModal from '../UIModal.vue'
import UITabs from '../UITabs.vue'
import UIToast from '../UIToast.vue'

describe('Arcane Minimal UI primitives', () => {
  it('stacks overlays by opening order rather than component DOM order', async () => {
    const confirmation = mount(UIModal, { props: { open: false, title: 'Подтверждение' }, attachTo: document.body })
    const item = mount(UIModal, { props: { open: true, title: 'Предмет' }, attachTo: document.body })
    await item.vm.$nextTick()
    await confirmation.setProps({ open: true })
    const dialogs = [...document.querySelectorAll<HTMLElement>('.ui-modal')]
    const top = dialogs.find(dialog => dialog.textContent?.includes('Подтверждение'))!
    const bottom = dialogs.find(dialog => dialog.textContent?.includes('Предмет'))!
    expect(top.style.zIndex).not.toBe(bottom.style.zIndex)
  })
  it('only dismisses the top modal and keeps scroll locked until the last closes', async () => {
    const first = mount(UIModal, { props: { open: true, title: 'Предмет' }, attachTo: document.body })
    await first.vm.$nextTick()
    const second = mount(UIModal, { props: { open: true, title: 'Подтверждение' }, attachTo: document.body })
    await second.vm.$nextTick()
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    expect(second.emitted('close')).toHaveLength(1)
    expect(first.emitted('close')).toBeUndefined()
    await second.setProps({ open: false })
    expect(document.body.style.overflow).toBe('hidden')
    await first.setProps({ open: false })
    expect(document.body.style.overflow).not.toBe('hidden')
  })
  it('announces the action being processed without hiding its button', () => {
    const wrapper = mount(UIButton, { props: { loading: true, loadingLabel: 'Надеваем…' }, slots: { default: 'Надеть' } })
    expect(wrapper.text()).toContain('Надеваем…')
  })

  it('keeps error notifications out of page flow and announces them assertively', () => {
    const wrapper = mount(UIToast, { props: { tone: 'danger', placement: 'overlay' }, slots: { default: 'Ошибка' } })
    expect(wrapper.get('[role="alert"]').attributes('aria-live')).toBe('assertive')
    expect(wrapper.classes()).toContain('ui-toast--overlay')
  })

  it('moves tab focus with arrows while skipping disabled tabs', async () => {
    const wrapper = mount(UITabs, { props: { modelValue: 'items', tabs: [
      { value: 'items', label: 'Предметы' }, { value: 'locked', label: 'Закрыто', disabled: true }, { value: 'stats', label: 'Характеристики' },
    ] }, attachTo: document.body })
    await wrapper.get('[data-tab="items"]').trigger('keydown', { key: 'ArrowRight' })
    expect(wrapper.emitted('update:modelValue')).toEqual([['stats']])
    expect(document.activeElement?.getAttribute('data-tab')).toBe('stats')
    wrapper.unmount()
  })

  it('focuses the modal, locks scroll, traps tab and restores the trigger on close', async () => {
    const trigger = document.createElement('button')
    document.body.append(trigger)
    trigger.focus()
    const wrapper = mount(UIModal, { props: { open: false, title: 'Предмет' }, attachTo: document.body,
      slots: { default: '<button data-last-action>Надеть</button>' } })
    await wrapper.setProps({ open: true })
    await wrapper.vm.$nextTick()
    expect(document.body.style.overflow).toBe('hidden')
    expect(document.activeElement?.hasAttribute('data-modal-close')).toBe(true)
    const last = document.querySelector<HTMLButtonElement>('[data-last-action]')!
    last.focus()
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true, cancelable: true }))
    expect(document.activeElement?.hasAttribute('data-modal-close')).toBe(true)
    await wrapper.setProps({ open: false })
    expect(document.activeElement).toBe(trigger)
    expect(document.body.style.overflow).not.toBe('hidden')
    wrapper.unmount()
    trigger.remove()
  })

  it('does not dismiss a busy confirmation with Escape or backdrop', async () => {
    const wrapper = mount(UIModal, { props: { open: true, title: 'Уничтожить?', busy: true }, attachTo: document.body })
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    document.querySelector<HTMLElement>('.ui-modal')!.click()
    await wrapper.vm.$nextTick()
    expect(wrapper.emitted('close')).toBeUndefined()
    wrapper.unmount()
  })
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
    expect(wrapper.get('[data-item-slot]').attributes('disabled')).toBeDefined()
  })
})
