import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { nextTick } from 'vue'
import { beforeEach, describe, expect, it } from 'vitest'

import type { CombatEvent, CombatSnapshot } from '@/api/contracts'
import CombatLogPreview from '@/game/combat/CombatLogPreview.vue'
import { useCombatSessionStore } from '@/stores/combatSession'

function event(
  sequence: number,
  type: string,
  sourceActorId: string | null,
  targetActorId: string | null,
  amount = 0,
): CombatEvent {
  return {
    sequence,
    type,
    actorId: sourceActorId,
    sourceActorId,
    targetActorId,
    definitionId: null,
    amount,
    amountBeforeShields: amount,
    serverTimeUtc: '2026-09-23T15:00:00Z',
  }
}

function activeSnapshot(status = 'Active'): CombatSnapshot {
  return {
    sessionId: 'session-1',
    status,
    player: {
      actorId: 'player-1',
      name: 'tekwan',
      abilities: [],
    },
    enemy: {
      actorId: 'enemy-1',
      name: 'Враг',
      abilities: [],
    },
  } as CombatSnapshot
}

function createLogTarget(): HTMLElement {
  const section = document.createElement('section')
  const button = document.createElement('button')
  button.className = 'combat-log__toggle'
  const content = document.createElement('span')
  const summary = document.createElement('small')
  summary.textContent = 'Событий пока нет'
  content.appendChild(summary)
  button.appendChild(content)
  section.appendChild(button)
  document.body.appendChild(section)
  return content
}

async function settleTeleport(): Promise<void> {
  await nextTick()
  await nextTick()
}

describe('CombatLogPreview', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
    setActivePinia(createPinia())
  })

  it('shows the latest meaningful events and mirrors critical damage', async () => {
    const combat = useCombatSessionStore()
    combat.snapshot = activeSnapshot()
    combat.events = [
      event(1, 'CombatStarted', null, null),
      event(2, 'CriticalHit', 'player-1', 'enemy-1', 120),
      event(3, 'DamageDealt', 'player-1', 'enemy-1', 120),
      event(4, 'HealingApplied', 'player-1', 'player-1', 25),
    ]
    const target = createLogTarget()

    const wrapper = mount(CombatLogPreview)
    await settleTeleport()

    expect(target.textContent).toContain('+25 здоровья')
    expect(target.textContent).toContain('120 урона · КРИТ')
    expect(target.textContent).toContain('Бой начался')
    wrapper.unmount()
  })

  it('keeps an empty-state summary and removes the preview when combat ends', async () => {
    const combat = useCombatSessionStore()
    combat.snapshot = activeSnapshot()
    combat.events = []
    const target = createLogTarget()

    const wrapper = mount(CombatLogPreview)
    await settleTeleport()

    expect(target.textContent).toContain('Событий пока нет')

    combat.snapshot = activeSnapshot('Completed')
    await settleTeleport()

    expect(target.querySelector('[data-combat-log-preview]')).toBeNull()
    wrapper.unmount()
  })
})
