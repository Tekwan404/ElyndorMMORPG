import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import CharacterCreationView from '@/game/character/views/CharacterCreationView.vue'
import { useGameSessionStore } from '@/stores/gameSession'

describe('CharacterCreationView', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('offers only the approved prototype identity choices', () => {
    const wrapper = mount(CharacterCreationView)

    expect(wrapper.findAll('input[name="raceId"]')).toHaveLength(2)
    expect(wrapper.findAll('input[name="genderId"]')).toHaveLength(2)
    expect(wrapper.findAll('input[name="classId"]')).toHaveLength(3)
    expect(wrapper.text()).toContain('Воин')
    expect(wrapper.text()).toContain('Лучник')
    expect(wrapper.text()).toContain('Маг')
  })

  it('keeps submission disabled until the formal name hint is satisfied', async () => {
    const wrapper = mount(CharacterCreationView)
    const name = wrapper.get('input[autocomplete="off"]')
    const submit = wrapper.get('button[type="submit"]')

    expect(submit.attributes('disabled')).toBeDefined()
    await name.setValue('Ab')
    expect(submit.attributes('disabled')).toBeDefined()
    await name.setValue('Ar-тас')
    expect(submit.attributes('disabled')).toBeDefined()
    await name.setValue('Arthas')
    expect(submit.attributes('disabled')).toBeUndefined()
  })

  it('reuses the same request id for an explicit retry and preserves the form', async () => {
    const store = useGameSessionStore()
    const createCharacter = vi.spyOn(store, 'createCharacter').mockResolvedValue()
    const wrapper = mount(CharacterCreationView)
    await wrapper.get('input[autocomplete="off"]').setValue('Arthas')
    await wrapper.get('input[value="ARCHER"]').setValue(true)

    await wrapper.get('form').trigger('submit')
    store.errorCode = 'character_name_conflict'
    await wrapper.get('form').trigger('submit')

    expect(createCharacter).toHaveBeenCalledTimes(2)
    expect(createCharacter.mock.calls[0]?.[0].requestId).toBe(
      createCharacter.mock.calls[1]?.[0].requestId,
    )
    expect(wrapper.get<HTMLInputElement>('input[autocomplete="off"]').element.value).toBe('Arthas')
    expect(wrapper.text()).toContain('character_name_conflict')
  })

  it('disables submission while a mutation is pending', async () => {
    const store = useGameSessionStore()
    const wrapper = mount(CharacterCreationView)
    await wrapper.get('input[autocomplete="off"]').setValue('Arthas')
    store.mutationPending = true
    await wrapper.vm.$nextTick()

    expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeDefined()
  })
})
