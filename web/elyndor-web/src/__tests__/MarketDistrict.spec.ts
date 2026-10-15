import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, expect, it, vi } from 'vitest'
import MarketDistrict from '@/game/world/components/MarketDistrict.vue'
import MerchantShop from '@/game/world/components/MerchantShop.vue'
import { useGameSessionStore } from '@/stores/gameSession'

beforeEach(() => setActivePinia(createPinia()))

it('offers distinct merchants and honors their identity when opening a shop', async () => {
  const wrapper = mount(MarketDistrict)
  expect(wrapper.findAll('[data-market-merchant]')).toHaveLength(4)
  await wrapper.get('[data-market-merchant="BORIN_ARMORY"]').trigger('click')
  expect(wrapper.emitted('open-merchant')?.[0]).toEqual(['BORIN_ARMORY'])
  await wrapper.get('[data-market-honor]').trigger('click')
  expect(wrapper.emitted('open-honor')).toHaveLength(1)
  await wrapper.get('[data-market-crystals]').trigger('click')
  expect(wrapper.emitted('open-store')).toHaveLength(1)
})

it('loads and buys from the chosen NPC rather than the legacy merchant', async () => {
  const session = useGameSessionStore()
  const merchant = {
    id: 'BORIN_ARMORY',
    name: 'Борин',
    description: 'Оружейная лавка',
    gold: 100,
    locationId: 'STARTER_TOWN',
    buybackItems: [],
    items: [],
  }
  const load = vi.spyOn(session, 'getMerchant').mockResolvedValue(merchant)
  const wrapper = mount(MerchantShop, {
    props: { open: true, merchantId: 'BORIN_ARMORY' },
    global: { stubs: { Teleport: true } },
  })
  await flushPromises()
  expect(load).toHaveBeenCalledWith('BORIN_ARMORY')
  expect(wrapper.get('[role="dialog"]').text()).toContain('Борин')
})

it('ignores an old catalog response after selecting another merchant', async () => {
  const session = useGameSessionStore()
  const catalog = {
    id: 'LIORA_HUNTING',
    name: 'Следопыт Лиора',
    description: 'Луки',
    gold: 100,
    items: [],
  }
  let finishOld!: (result: typeof catalog) => void
  vi.spyOn(session, 'getMerchant')
    .mockReturnValueOnce(
      new Promise((resolve) => {
        finishOld = resolve
      }),
    )
    .mockResolvedValueOnce(catalog)
  const wrapper = mount(MerchantShop, {
    props: { open: true, merchantId: 'BORIN_ARMORY' },
    global: { stubs: { Teleport: true } },
  })
  await wrapper.setProps({ merchantId: 'LIORA_HUNTING' })
  await flushPromises()
  finishOld({ ...catalog, id: 'BORIN_ARMORY', name: 'Оружейник Борин' })
  await flushPromises()
  expect(wrapper.get('.merchant__identity h2').text()).toBe('Следопыт Лиора')
  wrapper.unmount()
})
