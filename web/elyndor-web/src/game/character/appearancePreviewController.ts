export interface AppearancePreviewGateway<TSnapshot> {
  capture(): TSnapshot
  applyPreview(cosmeticId: string): void
  restore(snapshot: TSnapshot): void
}

export interface AppearancePreviewController {
  readonly activeCosmeticId: string | null
  preview(cosmeticId: string): void
  close(): void
}

export function createAppearancePreviewController<TSnapshot>(gateway: AppearancePreviewGateway<TSnapshot>): AppearancePreviewController {
  let snapshot: TSnapshot | undefined
  let activeCosmeticId: string | null = null

  return {
    get activeCosmeticId() {
      return activeCosmeticId
    },
    preview(cosmeticId: string) {
      if (!cosmeticId) return
      if (snapshot === undefined) snapshot = gateway.capture()
      activeCosmeticId = cosmeticId
      gateway.applyPreview(cosmeticId)
    },
    close() {
      if (snapshot !== undefined) gateway.restore(snapshot)
      snapshot = undefined
      activeCosmeticId = null
    },
  }
}
