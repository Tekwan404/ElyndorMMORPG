import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import PersonalizationStoreView from './PersonalizationStoreView.vue'
import type { PersonalizationStore, PersonalizationMutation } from '../personalizationContracts'

const session = vi.hoisted(() => ({
  getPersonalizationStore: vi.fn<() => Promise<PersonalizationStore>>(),
  purchaseCosmetic: vi.fn<(id: string) => Promise<PersonalizationMutation | null>>(),
  equipCosmetic: vi.fn<(kind: string, id: string | null) => Promise<PersonalizationMutation | null>>(),
  renameCharacter: vi.fn<(name: string) => Promise<PersonalizationMutation | null>>(),
  isMutationDomainPending: () => false, crystalBalance: 500, errorCode: null as string | null,
  snapshot: { character: { name: 'Hero', classId: 'WARRIOR', genderId: 'MALE' } },
}))
vi.mock('@/stores/gameSession', () => ({ useGameSessionStore: () => session }))
const catalog = (): PersonalizationStore => ({
  crystalBalance: 500, characterName: 'Hero', activeFrameId: null, activeTitleId: null, renamePrice: 150,
  cosmetics: [{ id: 'FRAME_ASH', name: 'Ash', description: 'Permanent frame', kind: 'Frame', styleId: 'ash', crystalPrice: 120, owned: false, enabled: true }],
})
const modal = { props: ['open'], template: '<div v-if="open"><slot /><slot name="actions" /></div>' }
function mountShop(mode: 'cosmetics' | 'services' = 'cosmetics') {
  return mount(PersonalizationStoreView, { props: { mode }, global: { stubs: { UIModal: modal } } })
}
describe('personalization shop', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    session.crystalBalance = 500
    session.errorCode = null
    session.getPersonalizationStore.mockResolvedValue(catalog())
    session.purchaseCosmetic.mockResolvedValue({ crystalBalance: 380, characterName: 'Hero', activeFrameId: null, activeTitleId: null })
    session.equipCosmetic.mockResolvedValue({ crystalBalance: 380, characterName: 'Hero', activeFrameId: null, activeTitleId: null })
    session.renameCharacter.mockResolvedValue({ crystalBalance: 350, characterName: 'NewHero', activeFrameId: null, activeTitleId: null })
  })
  it('previews without a mutation, confirms purchase, then equips and removes for free', async () => {
    const wrapper = mountShop()
    await flushPromises()
    expect(wrapper.find('[data-profile-frame="ash"]').exists()).toBe(true)
    expect(session.purchaseCosmetic).not.toHaveBeenCalled()
    await wrapper.get('[data-buy-cosmetic="FRAME_ASH"]').trigger('click')
    expect(session.purchaseCosmetic).not.toHaveBeenCalled()
    const owned = catalog()
    owned.cosmetics[0]!.owned = true
    session.getPersonalizationStore.mockResolvedValue(owned)
    await wrapper.get('[data-confirm-cosmetic]').trigger('click')
    await flushPromises()
    expect(session.purchaseCosmetic).toHaveBeenCalledExactlyOnceWith('FRAME_ASH')
    owned.activeFrameId = 'FRAME_ASH'
    await wrapper.get('[data-equip-cosmetic="FRAME_ASH"]').trigger('click')
    await flushPromises()
    expect(session.equipCosmetic).toHaveBeenCalledWith('Frame', 'FRAME_ASH')
    const remove = wrapper.findAll('button').find(button => button.text() === 'Снять')!
    await remove.trigger('click')
    await flushPromises()
    expect(session.equipCosmetic).toHaveBeenLastCalledWith('Frame', null)
    expect(session.purchaseCosmetic).toHaveBeenCalledTimes(1)
  })
  it('disables unaffordable purchases and retries a catalog failure', async () => {
    session.getPersonalizationStore.mockRejectedValueOnce(new Error('offline'))
    const wrapper = mountShop()
    await flushPromises()
    expect(wrapper.find('[role="alert"]').exists()).toBe(true)
    session.crystalBalance = 10
    await wrapper.get('button').trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-buy-cosmetic]').attributes('disabled')).toBeDefined()
    expect(session.purchaseCosmetic).not.toHaveBeenCalled()
  })
  it('requires rename confirmation and preserves input on a rejected name', async () => {
    const wrapper = mountShop('services')
    await flushPromises()
    await wrapper.get('[data-rename-input]').setValue('TakenHero')
    await wrapper.get('form').trigger('submit')
    expect(session.renameCharacter).not.toHaveBeenCalled()
    session.renameCharacter.mockResolvedValueOnce(null)
    session.errorCode = 'rename_name_taken'
    await wrapper.get('[data-rename-confirm]').trigger('click')
    await flushPromises()
    expect((wrapper.get('[data-rename-input]').element as HTMLInputElement).value).toBe('TakenHero')
    expect(wrapper.get('[role="alert"]').text()).toContain('занято')
    await wrapper.get('[data-rename-input]').setValue('NewHero')
    await wrapper.get('[data-rename-confirm]').trigger('click')
    await flushPromises()
    expect(session.renameCharacter).toHaveBeenLastCalledWith('NewHero')
    expect(wrapper.find('[data-rename-confirm]').exists()).toBe(false)
    expect(wrapper.text()).toContain('NewHero')
  })
})
