import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import AdminCombatSimulator from '@/admin/AdminCombatSimulator.vue'
import { apiClient } from '@/api/apiClient'

describe('AdminCombatSimulator', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('runs the current local draft and renders aggregate metrics', async () => {
    const request = vi.spyOn(apiClient, 'request').mockResolvedValue({
      contentVersion: '1.0.0',
      balanceVersion: '1.0.1',
      classId: 'WARRIOR',
      playerLevel: 5,
      monsterId: 'WOLF',
      iterations: 100,
      victories: 72,
      defeats: 28,
      timeouts: 0,
      winRatePercent: 72,
      averageDurationSeconds: 14.2,
      p50DurationSeconds: 13.8,
      p95DurationSeconds: 20.4,
      averagePlayerDps: 19.4,
      averageEnemyDps: 8.2,
      averagePlayerRemainingHp: 54,
      damageSources: [
        { definitionId: 'STRIKE', averageDamage: 160, damageSharePercent: 62 },
      ],
    })

    const wrapper = mount(AdminCombatSimulator, {
      props: {
        payloadJson: '{"balanceVersion":"1.0.1"}',
        classes: [{ id: 'WARRIOR' }],
        monsters: [{ id: 'WOLF', name: 'Волк', level: 3 }],
      },
    })

    await wrapper.get('[data-testid="simulation-run"]').trigger('click')
    await flushPromises()

    expect(request).toHaveBeenCalledWith(
      '/api/v1/admin/content/simulate',
      expect.objectContaining({
        method: 'POST',
        body: expect.stringContaining('"payloadJson":"{\\\"balanceVersion\\\":\\\"1.0.1\\\"}"'),
      }),
    )
    expect(wrapper.get('[data-testid="simulation-win-rate"]').text()).toBe('72.0%')
    expect(wrapper.text()).toContain('STRIKE')
  })
})
