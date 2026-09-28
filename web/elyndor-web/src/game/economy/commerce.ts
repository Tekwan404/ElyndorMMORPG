import { apiClient } from '@/api/apiClient'
import { moneyUnits, type MoneyValue } from '@/shared/money'

export interface AuctionAffix {
  slotKey: string
  statId: string
  value: number
  affixTier: number
  isGuaranteed: boolean
  isReforgeSlot: boolean
}

export interface AuctionRolledStats {
  strength: number | null
  agility: number | null
  intellect: number | null
  stamina: number | null
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
  auction_listing_limit: 'Достигнут лимит активных лотов.',
  auction_quote_changed: 'Комиссия аукциона изменилась. Проверьте новый расчёт и подтвердите ещё раз.',
  auction_invalid_listing: 'Проверьте цену лота.',
  mail_item_unavailable: 'Посылка больше недоступна.',
}

export function commerceMessage(code: string): string {
  return errorText[code] ?? 'Не удалось выполнить действие. Обновите раздел и попробуйте снова.'
}

export function commerceRarityLabel(rarity: string): string {
  return ({ Common: 'Обычный', Uncommon: 'Необычный', Rare: 'Редкий', Epic: 'Эпический',
    Legendary: 'Легендарный', Unique: 'Уникальный' } as Record<string, string>)[rarity] ?? rarity
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
