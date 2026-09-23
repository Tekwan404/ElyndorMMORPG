import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { CharacterSkinMutationResponse, CharacterSkinStoreSnapshot } from '@/api/contracts'
import CharacterSkinStoreView from '@/game/economy/views/CharacterSkinStoreView.vue'

const store = vi.hoisted(() => ({
  getCharacterSkins: vi.fn<() => Promise<CharacterSkinStoreSnapshot>>(),
  buyCharacterSkin: vi.fn<(skinId: string) => Promise<CharacterSkinMutationResponse>>(),
  equipCharacterSkin: vi.fn<(skinId: string | null) => Promise<CharacterSkinMutationResponse>>(),
  mutationPending: false,
  snapshot: { character: { classId: 'MAGE', genderId: 'FEMALE' } },
}))

vi.mock('@/stores/gameSession', () => ({ useGameSessionStore: () => store }))

function catalog(owned = false): CharacterSkinStoreSnapshot {
  return {
    crystalBalance: 1000,
    activeSkinId: null,
    skins: [
      { id: 'MAGE_FEMALE_FIRE', name: 'Пламенная чародейка', classId: 'MAGE', genderId: 'FEMALE', imageId: 'mage-female-fire', crystalPrice: 650, owned, eligible: true, purchasable: true },
      { id: 'ARCHER_FEMALE_DEFAULT', name: 'Лучница', classId: 'ARCHER', genderId: 'FEMALE', imageId: 'archer-female-default', crystalPrice: 0, owned: true, eligible: false, purchasable: false },
    ],
  }
}

describe('character skin shop', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    store.getCharacterSkins.mockResolvedValue(catalog())
    store.buyCharacterSkin.mockResolvedValue({ crystalBalance: 350, activeSkinId: null })
    store.equipCharacterSkin.mockResolvedValue({ crystalBalance: 1000, activeSkinId: 'MAGE_FEMALE_FIRE' })
  })

  it('shows a real preview and locks other-class skins', async () => {
    const wrapper = mount(CharacterSkinStoreView)
    await flushPromises()

    expect(wrapper.find('img[alt="Пламенная чародейка"]').attributes('src')).toContain('mage-female-fire.webp')
    expect(wrapper.text()).toContain('Только для класса')
    const locked = wrapper.findAll('.skin-card').find(card => card.text().includes('Лучница'))
    expect(locked?.find('button').attributes('disabled')).toBeDefined()
  })

  it('buys a compatible skin through the game session', async () => {
    const wrapper = mount(CharacterSkinStoreView)
    await flushPromises()
    await wrapper.findAll('.skin-card')[0]!.find('button').trigger('click')
    await flushPromises()

    expect(store.buyCharacterSkin).toHaveBeenCalledWith('MAGE_FEMALE_FIRE')
  })

  it('equips an owned skin without purchasing again', async () => {
    store.getCharacterSkins.mockResolvedValue(catalog(true))
    const wrapper = mount(CharacterSkinStoreView)
    await flushPromises()
    await wrapper.findAll('.skin-card')[0]!.find('button').trigger('click')
    await flushPromises()

    expect(store.equipCharacterSkin).toHaveBeenCalledWith('MAGE_FEMALE_FIRE')
    expect(store.buyCharacterSkin).not.toHaveBeenCalled()
  })
})
