import type { ItemSalvageReward } from '@/api/contracts'

export interface ItemEnhancementResponse {
  itemInstanceId: string
  enhancementLevel: number
  enhancementBonusPercent: number
  finalItemPower: number | null
}

export interface ItemEnhancementSalvageRefund {
  enhancementMaterialItemId: string | null
  enhancementMaterialQuantity: number
  catalystItemId: string | null
  catalystQuantity: number
}

export interface ItemSalvagePreviewV2 {
  characterItemId: string
  reward: ItemSalvageReward
  requiresConfirmation: boolean
  enhancementRefund: ItemEnhancementSalvageRefund
}

export interface ItemSalvageResponseV2 {
  reward: ItemSalvageReward
  enhancementRefund: ItemEnhancementSalvageRefund
}
