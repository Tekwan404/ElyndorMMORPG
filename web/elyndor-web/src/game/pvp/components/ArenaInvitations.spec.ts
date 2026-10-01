import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import ArenaInvitations from './ArenaInvitations.vue'
import type { ArenaInvitation } from '../arenaContracts'

const incoming: ArenaInvitation = {
  id: 'invite', inviterCharacterId: 'a', inviterName: 'Friend', targetCharacterId: 'b',
  targetName: 'Me', incoming: true, status: 'Pending', matchId: null,
  expiresAtUtc: '2026-10-02T12:00:00Z',
}

describe('ArenaInvitations', () => {
  it('submits a named challenge and supports keyboard form submission', async () => {
    const wrapper = mount(ArenaInvitations, { props: { invitations: [], pending: false, unavailable: false, notice: null } })
    await wrapper.get('input').setValue('Friend')
    await wrapper.get('form').trigger('submit')
    expect(wrapper.emitted('invite')).toEqual([['Friend']])
    expect(wrapper.text()).toContain('без рейтинга и чести')
  })

  it('shows accept/decline only for incoming invites, cancel for outgoing', async () => {
    const wrapper = mount(ArenaInvitations, { props: { invitations: [incoming], pending: false, unavailable: false, notice: null } })
    const accept = wrapper.findAll('button').find(button => button.text() === 'Принять бой')!
    await accept.trigger('click')
    expect(wrapper.emitted('respond')).toEqual([['invite', 'accept']])
    await wrapper.setProps({ invitations: [{ ...incoming, incoming: false }] })
    expect(wrapper.text()).not.toContain('Принять бой')
    const cancel = wrapper.findAll('button').find(button => button.text() === 'Отменить вызов')!
    await cancel.trigger('click')
    expect(wrapper.emitted('respond')?.[1]).toEqual(['invite', 'cancel'])
  })

  it('blocks acceptance while queued without blocking decline', () => {
    const wrapper = mount(ArenaInvitations, { props: { invitations: [incoming], pending: false, unavailable: true, notice: null } })
    const buttons = wrapper.findAll('button')
    expect(buttons.find(button => button.text() === 'Принять бой')!.attributes('disabled')).toBeDefined()
    expect(buttons.find(button => button.text() === 'Отклонить')!.attributes('disabled')).toBeUndefined()
  })
})
