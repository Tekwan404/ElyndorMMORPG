import { onScopeDispose, shallowRef } from 'vue'

export interface ConfirmationRequest {
  title: string
  message: string
  confirmLabel?: string
}

export function useConfirmation() {
  const request = shallowRef<ConfirmationRequest | null>(null)
  let resolve: ((accepted: boolean) => void) | null = null
  function settle(accepted: boolean) {
    const pending = resolve
    request.value = null
    resolve = null
    pending?.(accepted)
  }
  function ask(options: ConfirmationRequest): Promise<boolean> {
    if (request.value) return Promise.resolve(false)
    request.value = options
    return new Promise<boolean>((complete) => {
      resolve = complete
    })
  }
  onScopeDispose(() => settle(false))
  return { request, ask, settle }
}
