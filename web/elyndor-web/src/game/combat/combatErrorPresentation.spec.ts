import { describe, expect, it } from 'vitest'
import { COMBAT_ERRORS, combatErrorPresentation } from './combatErrorPresentation'

describe('combat wire error presentation', () => {
  it.each([
    COMBAT_ERRORS.abilityOnCooldown,
    COMBAT_ERRORS.insufficientResource,
    COMBAT_ERRORS.invalidTarget,
    COMBAT_ERRORS.actorDead,
    COMBAT_ERRORS.commandRejected,
    COMBAT_ERRORS.duplicateCommand,
    COMBAT_ERRORS.rateLimited,
  ])('does not show a network failure for gameplay rejection %s', code => {
    expect(combatErrorPresentation(code).showToast).toBe(false)
  })

  it('only describes a realtime failure as a connection problem', () => {
    expect(combatErrorPresentation('combat_hub_use_ability_failed')).toEqual({
      message: 'Не удалось связаться с боем. Повторите подключение.',
      showToast: true,
    })
    expect(combatErrorPresentation('combat_unknown_action').message).not.toContain('связ')
  })
})
