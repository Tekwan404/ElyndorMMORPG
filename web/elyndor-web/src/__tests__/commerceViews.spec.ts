import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import AuctionView from '@/game/economy/views/AuctionView.vue'
import MailboxView from '@/game/economy/views/MailboxView.vue'
import TradePanel from '@/game/economy/views/TradePanel.vue'
import { useTradeStore } from '@/game/economy/tradeStore'
import { loadAuction, loadAuctionSellableItems, loadMailbox, claimMail, previewAuctionBatch, createAuctionBatch } from '@/game/economy/commerce'

vi.mock('@/game/economy/commerce', async importOriginal => {
  const actual = await importOriginal<typeof import('@/game/economy/commerce')>()
  return { ...actual, loadAuction: vi.fn<typeof actual.loadAuction>(),
    loadAuctionSellableItems: vi.fn<typeof actual.loadAuctionSellableItems>(),
    loadMailbox: vi.fn<typeof actual.loadMailbox>(), claimMail: vi.fn<typeof actual.claimMail>(),
    previewAuctionBatch: vi.fn<typeof actual.previewAuctionBatch>(),
    createAuctionBatch: vi.fn<typeof actual.createAuctionBatch>() }
})

beforeEach(() => {
  setActivePinia(createPinia())
  vi.clearAllMocks()
  vi.mocked(loadAuctionSellableItems).mockResolvedValue([])
})

describe('commerce views', () => {
  it('shows buy, sell and own-auction paths without mixing seller and buyer lists', async () => {
    vi.mocked(loadAuction).mockResolvedValue([])
    const wrapper = mount(AuctionView, { global: { stubs: { ItemIcon: true } } })
    await flushPromises()
    expect(loadAuction).toHaveBeenCalledWith(false, '', '', 0)
    await wrapper.get('[aria-label="Разделы аукциона"] button:nth-child(3)').trigger('click')
    await flushPromises()
    expect(loadAuction).toHaveBeenCalledWith(true, '', '', 0)
    vi.mocked(loadAuctionSellableItems).mockResolvedValueOnce([{
      itemId: 'sellable-1', itemDefinitionId: 'ITEM', name: 'Клинок дозорного', iconId: null,
      type: 'Equipment', rarity: 'Rare', quantity: 1, itemLevel: 20, itemPower: 50,
      rollQuality: .7, stars: 2, isPerfect: false, enhancementLevel: 1,
    }])
    await wrapper.get('[aria-label="Разделы аукциона"] button:nth-child(2)').trigger('click')
    await flushPromises()
    expect(loadAuctionSellableItems).toHaveBeenCalled()
    expect(wrapper.find('[data-auction-item-picker]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Клинок дозорного')
    wrapper.unmount()
  })

  it('selects several different items and previews one shared price for separate lots', async () => {
    vi.mocked(loadAuction).mockResolvedValue([])
    vi.mocked(loadAuctionSellableItems).mockResolvedValue([
      {
        itemId: 'sellable-1', itemDefinitionId: 'ITEM_A', name: 'Шлем', iconId: null,
        type: 'Equipment', rarity: 'Epic', quantity: 1, itemLevel: 60, itemPower: 500,
        rollQuality: 82, stars: 4, isPerfect: false, enhancementLevel: 0,
      },
      {
        itemId: 'sellable-2', itemDefinitionId: 'ITEM_B', name: 'Посох', iconId: null,
        type: 'Equipment', rarity: 'Epic', quantity: 1, itemLevel: 60, itemPower: 700,
        rollQuality: 91, stars: 5, isPerfect: false, enhancementLevel: 0,
      },
    ])
    vi.mocked(previewAuctionBatch).mockResolvedValue({
      itemIds: ['sellable-1', 'sellable-2'],
      pricePerItem: '10000',
      feePerItem: '100',
      taxPerItem: '500',
      sellerProceedsPerItem: '9500',
      totalFee: '200',
      totalTax: '1000',
      totalSellerProceeds: '19000',
    })

    const wrapper = mount(AuctionView, { global: { stubs: { ItemIcon: true } } })
    await wrapper.get('[aria-label="Разделы аукциона"] button:nth-child(2)').trigger('click')
    await flushPromises()

    const items = wrapper.findAll('[data-auction-item-picker] button')
    await items[0]!.trigger('click')
    await items[1]!.trigger('click')
    expect(wrapper.get('[data-auction-selected-items]').text()).toContain('Выбрано: 2')

    await wrapper.get('input[aria-label="Золото"]').setValue('1')
    await wrapper.get('.auction-form').trigger('submit')
    await flushPromises()

    expect(previewAuctionBatch).toHaveBeenCalledWith(['sellable-1', 'sellable-2'], 10000)
    expect(wrapper.text()).toContain('Цена каждого')
    expect(wrapper.text()).toContain('Общая плата сейчас')
    expect(wrapper.text()).toContain('Выставить 2 лота')
    expect(createAuctionBatch).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('shows derived affix tier together with its roll percentage', async () => {
    vi.mocked(loadAuction).mockResolvedValue([{
      id: 'lot-1', sellerId: 'other', sellerName: 'Other', itemId: 'item-1',
      itemDefinitionId: 'ITEM', name: 'Око Погасшей Звезды', iconId: null,
      type: 'Equipment', rarity: 'Epic', quantity: 1, itemLevel: 60, itemPower: 5607,
      rollQuality: 95.93, stars: 5, isPerfect: false, enhancementLevel: 0,
      rolledStats: null,
      affixes: [{
        slotKey: 'AFFIX_4', statId: 'MAGIC_PENETRATION', value: 36.9,
        affixTier: 4, rollQuality: 83.6, isGuaranteed: false, isReforgeSlot: false,
      }],
      price: '10000', expiresAt: '2027-01-01T00:00:00Z',
    }])

    const wrapper = mount(AuctionView, { global: { stubs: { ItemIcon: true } } })
    await flushPromises()
    expect(wrapper.text()).toContain('T4 · 83.6%')
    wrapper.unmount()
  })

  it('does not replace the current auction tab with a late response from another tab', async () => {
    let finishBuy!: (lots: Awaited<ReturnType<typeof loadAuction>>) => void
    vi.mocked(loadAuction).mockImplementationOnce(() => new Promise(resolve => { finishBuy = resolve }))
      .mockResolvedValueOnce([])
    const wrapper = mount(AuctionView, { global: { stubs: { ItemIcon: true } } })
    await wrapper.get('[aria-label="Разделы аукциона"] button:nth-child(3)').trigger('click')
    await flushPromises()
    finishBuy([{ id: 'stale', sellerId: 'other', sellerName: 'Other', itemId: 'item',
      itemDefinitionId: 'ITEM', name: 'Stale lot', iconId: null, type: 'Equipment', rarity: 'Rare',
      quantity: 1, itemLevel: 25, itemPower: 72.5, rollQuality: .8, stars: 3, isPerfect: false,
      enhancementLevel: 2, rolledStats: null, affixes: [], price: '100', expiresAt: '2027-01-01T00:00:00Z' }])
    await flushPromises()
    expect(wrapper.text()).not.toContain('Stale lot')
    wrapper.unmount()
  })

  it('claims mailbox items through the authoritative endpoint', async () => {
    vi.mocked(loadMailbox).mockResolvedValue([{ id: 'mail-1', itemId: 'item-1', itemDefinitionId: 'ITEM', name: 'Sword', iconId: null, quantity: 1, createdAt: '2026-01-01T00:00:00Z', source: 'PURCHASE' }])
    vi.mocked(claimMail).mockResolvedValue()
    const wrapper = mount(MailboxView, { global: { stubs: { ItemIcon: true } } })
    await flushPromises()
    expect(wrapper.text()).toContain('Sword')
    await wrapper.get('button').trigger('click')
    await flushPromises()
    expect(claimMail).toHaveBeenCalledWith('mail-1')
    wrapper.unmount()
  })

  it('keeps confirm disabled until both trade offers are locked', async () => {
    const trade = useTradeStore()
    trade.connected = true
    trade.joined = true
    trade.current = { id: 'trade', state: 'OPEN', revision: 2, characterAId: 'a', characterBId: 'b',
      itemsA: [], itemsB: [], goldA: '0', goldB: '0', lockedA: true, lockedB: false, confirmedA: false, confirmedB: false }
    const wrapper = mount(TradePanel, { global: { stubs: { ItemIcon: true } } })
    await flushPromises()
    const confirm = wrapper.findAll('button').find(button => button.text() === 'Подтвердить')!
    expect(confirm.attributes('disabled')).toBeDefined()
    trade.current = { ...trade.current, lockedB: true }
    await flushPromises()
    expect(confirm.attributes('disabled')).toBeUndefined()
    wrapper.unmount()
  })
})
