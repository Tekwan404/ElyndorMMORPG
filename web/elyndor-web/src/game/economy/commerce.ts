import { apiClient } from '@/api/apiClient'
import { moneyUnits, type MoneyValue } from '@/shared/money'

export interface AuctionAffix {
  slotKey: string
  statId: string
  value: number
  affixTier: number
  rollQuality: number
  isGuaranteed: boolean
  isReforgeSlot: boolean
}

export interface AuctionRolledStats {
  strength: number | null
  agility: number | null
  intellect: number | null
  stamina: number | null
}

export interface AuctionSellableItem {
  itemId: string
  itemDefinitionId: string
  name: string
  iconId: string | null
  type: string
  rarity: string
  quantity: number
  itemLevel: number | null
  itemPower: number | null
  rollQuality: number | null
  stars: number | null
  isPerfect: boolean
  enhancementLevel: number
}

export interface AuctionLot {
  id: string
  sellerId: string
  sellerName: string
  itemId: string
  itemDefinitionId: string
  name: string
  iconId: string | null
  type: string
  rarity: string
  quantity: number
  itemLevel: number | null
  itemPower: number | null
  rollQuality: number | null
  stars: number | null
  isPerfect: boolean
  enhancementLevel: number
  rolledStats: AuctionRolledStats | null
  affixes: AuctionAffix[]
  price: MoneyValue
  expiresAt: string
}

export interface AuctionFeePreview {
  itemId: string
  price: MoneyValue
  fee: MoneyValue
  tax: MoneyValue
  sellerProceeds: MoneyValue
}

export interface AuctionMutation {
  id: string
  state: string
  sellerId: string
  itemId: string
  price: MoneyValue
  fee: MoneyValue
  tax: MoneyValue
  buyerId: string | null
  expiresAt: string
}

export interface AuctionBatchFeePreview {
  itemIds: string[]
  pricePerItem: MoneyValue
  feePerItem: MoneyValue
  taxPerItem: MoneyValue
  sellerProceedsPerItem: MoneyValue
  totalFee: MoneyValue
  totalTax: MoneyValue
  totalSellerProceeds: MoneyValue
}

export interface AuctionBatchMutation {
  listings: AuctionMutation[]
  totalFee: MoneyValue
}

export interface CommerceMail {
  id: string
  itemId: string
  itemDefinitionId: string
  name: string
  iconId: string | null
  quantity: number
  createdAt: string
  source: 'PURCHASE' | 'RETURN'
}

export interface TradeSnapshot {
  id: string
  state: 'OPEN' | 'COMPLETED' | 'CANCELLED'
  revision: number
  characterAId: string
  characterBId: string
  itemsA: string[]
  itemsB: string[]
  goldA: MoneyValue
  goldB: MoneyValue
  lockedA: boolean
  lockedB: boolean
  confirmedA: boolean
  confirmedB: boolean
}

export interface TradeResult {
  succeeded: boolean
  errorCode: string | null
  snapshot: TradeSnapshot | null
}

export interface TradeItem {
  id: string
  name: string
  iconId: string | null
  type: string
  rarity: string
  quantity: number
}

const errorText: Record<string, string> = {
  commerce_insufficient_funds: 'Недостаточно золота.',
  commerce_inventory_full: 'В сумке нет свободного места.',
  commerce_item_missing: 'Предмет больше недоступен.',
  commerce_item_not_owned: 'Предмет больше вам не принадлежит.',
  commerce_item_not_tradeable: 'Этот предмет нельзя передать.',
  commerce_item_locked: 'Предмет уже занят другой операцией.',
  commerce_item_equipped: 'Сначала снимите предмет.',
  commerce_character_unavailable: 'Обмен недоступен во время боя или путешествия.',
  trade_location_mismatch: 'Для обмена нужно находиться в одной локации.',
  trade_already_active: 'У одного из игроков уже есть обмен.',
  trade_stale_revision: 'Предложение изменилось. Проверьте его ещё раз.',
  trade_disconnected: 'Связь с обменом прервана.',
  trade_closed: 'Этот обмен уже завершён.',
  auction_unavailable: 'Лот больше недоступен.',
  auction_listing_limit: 'Нельзя создать столько лотов: превышен лимит активных лотов.',
  auction_quote_changed: 'Комиссия аукциона изменилась. Проверьте новый расчёт и подтвердите ещё раз.',
  auction_invalid_listing: 'Проверьте цену и выбранные предметы.',
  mail_item_unavailable: 'Посылка больше недоступна.',
}

export function commerceMessage(code: string): string {
  return errorText[code] ?? 'Не удалось выполнить действие. Обновите раздел и попробуйте снова.'
}

export function commerceRarityLabel(rarity: string): string {
  return ({ Common: 'Обычный', Uncommon: 'Необычный', Rare: 'Редкий', Epic: 'Эпический',
    Legendary: 'Легендарный', Unique: 'Уникальный' } as Record<string, string>)[rarity] ?? rarity
}

export function parseMoneyInput(gold: string, silver: string, bronze: string): number {
  const parsePart = (value: string, label: string, max?: bigint): bigint => {
    const normalized = value.trim() || '0'
    if (!/^\d+$/.test(normalized)) throw new RangeError(`${label}: используйте только целое число`)
    const parsed = BigInt(normalized)
    if (max !== undefined && parsed > max) throw new RangeError(`${label}: допустимо от 0 до ${max}`)
    return parsed
  }
  const total = parsePart(gold, 'Золото') * 10_000n
    + parsePart(silver, 'Серебро', 99n) * 100n
    + parsePart(bronze, 'Бронза', 99n)
  if (total > BigInt(Number.MAX_SAFE_INTEGER)) throw new RangeError('Сумма слишком велика')
  return Number(total)
}

export function parseGoldInput(value: string): number {
  if (!/^(0|[1-9]\d*)(?:\.\d{1,4})?$/.test(value.trim())) throw new RangeError('Введите количество золота, до 4 знаков после точки')
  const [whole, fraction = ''] = value.trim().split('.')
  const bronze = BigInt(whole!) * 10_000n + BigInt(fraction.padEnd(4, '0'))
  if (bronze > BigInt(Number.MAX_SAFE_INTEGER)) throw new RangeError('Сумма слишком велика')
  return Number(bronze)
}

export function formatGoldInput(value: MoneyValue): string {
  const units = moneyUnits(value)
  const fraction = (units % 10_000n).toString().padStart(4, '0').replace(/0+$/, '')
  return `${units / 10_000n}${fraction ? `.${fraction}` : ''}`
}

export async function loadAuctionSellableItems(): Promise<AuctionSellableItem[]> {
  return apiClient.request<AuctionSellableItem[]>('/api/v1/auction/sellable-items')
}

export async function loadAuction(mine: boolean, search = '', type = '', page = 0): Promise<AuctionLot[]> {
  const query = new URLSearchParams({ mine: String(mine), search, type, page: String(page) })
  return apiClient.request<AuctionLot[]>(`/api/v1/auction?${query}`)
}

export async function previewAuction(itemId: string, price: number): Promise<AuctionFeePreview> {
  return apiClient.request<AuctionFeePreview>('/api/v1/auction/preview', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ itemId, price }),
  })
}

export async function createAuction(itemId: string, price: number, preview: AuctionFeePreview): Promise<AuctionMutation> {
  const fee = moneyUnits(preview.fee)
  const tax = moneyUnits(preview.tax)
  if (fee > BigInt(Number.MAX_SAFE_INTEGER) || tax > BigInt(Number.MAX_SAFE_INTEGER)) throw new RangeError('Комиссия слишком велика')
  return apiClient.request<AuctionMutation>('/api/v1/auction', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ requestId: crypto.randomUUID(), itemId, price, expectedFee: Number(fee), expectedTax: Number(tax) }),
  })
}

export async function previewAuctionBatch(itemIds: string[], price: number): Promise<AuctionBatchFeePreview> {
  return apiClient.request<AuctionBatchFeePreview>('/api/v1/auction/batch/preview', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ itemIds, price }),
  })
}

export async function createAuctionBatch(
  itemIds: string[],
  price: number,
  preview: AuctionBatchFeePreview,
): Promise<AuctionBatchMutation> {
  const feePerItem = moneyUnits(preview.feePerItem)
  const taxPerItem = moneyUnits(preview.taxPerItem)
  if (feePerItem > BigInt(Number.MAX_SAFE_INTEGER) || taxPerItem > BigInt(Number.MAX_SAFE_INTEGER)) {
    throw new RangeError('Комиссия слишком велика')
  }
  return apiClient.request<AuctionBatchMutation>('/api/v1/auction/batch', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      requestId: crypto.randomUUID(),
      itemIds,
      price,
      expectedFeePerItem: Number(feePerItem),
      expectedTaxPerItem: Number(taxPerItem),
    }),
  })
}

export async function settleAuction(id: string, action: 'buy' | 'cancel'): Promise<void> {
  await apiClient.request(`/api/v1/auction/${id}/${action}`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ requestId: crypto.randomUUID() }),
  })
}

export async function loadMailbox(): Promise<CommerceMail[]> {
  return apiClient.request<CommerceMail[]>('/api/v1/mailbox')
}

export async function claimMail(id: string): Promise<void> {
  await apiClient.request(`/api/v1/mailbox/${id}/claim`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ requestId: crypto.randomUUID() }),
  })
}
