import { computed, nextTick, onBeforeUnmount, shallowReactive, watch, type Ref } from 'vue'

// Overlay-only ownership: the top layer handles input and the last layer restores scroll.
const layers = shallowReactive<symbol[]>([])
let previousOverflow = ''
const focusSelector =
  'button:not(:disabled), [href], input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex="-1"])'

export function useModalLayer(options: {
  open: () => boolean
  root: Ref<HTMLElement | null>
  close: () => void
}) {
  const id = Symbol('modal')
  let trigger: HTMLElement | null = null
  let active = false

  function release() {
    if (!active) return
    active = false
    const wasTop = layers[layers.length - 1] === id
    layers.splice(layers.indexOf(id), 1)
    if (!layers.length) document.body.style.overflow = previousOverflow
    document.removeEventListener('keydown', keydown)
    if (wasTop && trigger?.isConnected) trigger.focus()
  }

  function keydown(event: KeyboardEvent) {
    if (layers[layers.length - 1] !== id) return
    if (event.key === 'Escape') {
      event.preventDefault()
      event.stopImmediatePropagation()
      options.close()
    } else if (event.key === 'Tab') {
      const root = options.root.value
      if (!root) return
      const controls = [...root.querySelectorAll<HTMLElement>(focusSelector)].filter(
        (element) => !element.hidden && !element.closest('[hidden], [inert]'),
      )
      const first = controls[0] ?? root
      const last = controls[controls.length - 1] ?? root
      if (
        !root.contains(document.activeElement) ||
        (!event.shiftKey && document.activeElement === last)
      ) {
        event.preventDefault()
        first.focus()
      } else if (
        event.shiftKey &&
        (document.activeElement === first || document.activeElement === root)
      ) {
        event.preventDefault()
        last.focus()
      }
    }
  }

  watch(
    options.open,
    async (open) => {
      if (!open) {
        release()
        return
      }
      trigger = document.activeElement instanceof HTMLElement ? document.activeElement : null
      if (!layers.length) {
        previousOverflow = document.body.style.overflow
        document.body.style.overflow = 'hidden'
      }
      active = true
      layers.push(id)
      document.addEventListener('keydown', keydown)
      await nextTick()
      if (active && layers[layers.length - 1] === id) {
        const root = options.root.value
        ;(root?.querySelector<HTMLElement>(focusSelector) ?? root)?.focus()
      }
    },
    { immediate: true, flush: 'post' },
  )
  onBeforeUnmount(release)
  return { layerIndex: computed(() => Math.max(0, layers.indexOf(id))) }
}
