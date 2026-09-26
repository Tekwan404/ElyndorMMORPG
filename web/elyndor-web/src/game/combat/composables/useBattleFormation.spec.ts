import { describe, expect, it } from 'vitest'

import {
  buildBattleFormation,
  protectedBoundsOverlap,
  type BattleFormationInput,
} from './useBattleFormation'

const actorIds = ['local', 'ally-1', 'ally-2', 'ally-3', 'ally-4']

function input(overrides: Partial<BattleFormationInput> = {}): BattleFormationInput {
  return {
    actorIds,
    localActorId: 'local',
    aggroActorId: 'ally-2',
    selectedActorId: 'ally-4',
    viewportWidthPx: 390,
    arenaWidthPx: 390,
    arenaHeightPx: 500,
    ...overrides,
  }
}

describe('buildBattleFormation', () => {
  it('gives a solo player a full-size battlefield slot', () => {
    const result = buildBattleFormation(
      input({ actorIds: ['local'], aggroActorId: 'local', selectedActorId: 'local' }),
    )

    expect(result.slots).toHaveLength(1)
    expect(result.slots[0]).toMatchObject({
      actorId: 'local',
      slotId: 'solo-frontline',
      scale: 1,
      bounds: { x: 0.03, y: 0.12, width: 0.44, height: 0.84 },
    })
  })

  it.each([
    { count: 1, width: 360 },
    { count: 2, width: 390 },
    { count: 3, width: 390 },
    { count: 4, width: 390 },
    { count: 5, width: 390 },
    { count: 5, width: 768 },
  ])('keeps protected upper bodies clear for $count actors at $width px', ({ count, width }) => {
    const visibleActorIds = actorIds.slice(0, count)

    for (const aggroActorId of visibleActorIds) {
      const result = buildBattleFormation(
        input({
          actorIds: visibleActorIds,
          aggroActorId,
          selectedActorId: actorIds[count - 1],
          viewportWidthPx: width,
          arenaWidthPx: width,
        }),
      )

      const visible = result.slots.filter((slot) => slot.visible)

      expect(result.mode).toBe('all-visible')
      expect(visible).toHaveLength(count)
      expect(new Set(visible.map((slot) => slot.slotId)).size).toBe(count)
      expect(visible.filter((slot) => slot.isFrontline)).toHaveLength(1)
      expect(visible.every((slot) => slot.touchTargetPx >= 44)).toBe(true)

      for (let left = 0; left < visible.length; left += 1) {
        for (let right = left + 1; right < visible.length; right += 1) {
          expect(
            protectedBoundsOverlap(
              visible[left]!.protectedUpperBodyBounds,
              visible[right]!.protectedUpperBodyBounds,
            ),
          ).toBe(false)
        }
      }
    }
  })

  it('does not move other actors when aggro changes', () => {
    const before = buildBattleFormation(input({ aggroActorId: 'ally-1' }))
    const after = buildBattleFormation(input({ aggroActorId: 'ally-3' }))

    for (const actorId of ['local', 'ally-2', 'ally-4']) {
      const beforeSlot = before.slots.find((slot) => slot.actorId === actorId)
      const afterSlot = after.slots.find((slot) => slot.actorId === actorId)

      expect(afterSlot?.baseSlotId).toBe(beforeSlot?.baseSlotId)
      expect(afterSlot?.x).toBe(beforeSlot?.x)
      expect(afterSlot?.y).toBe(beforeSlot?.y)
    }
  })

  it('keeps friendly selection out of formation geometry', () => {
    const selectedFirst = buildBattleFormation(input({ selectedActorId: 'ally-1' }))
    const selectedLast = buildBattleFormation(input({ selectedActorId: 'ally-4' }))

    expect(
      selectedLast.slots.map(({ actorId, x, y, scale, zIndex }) => ({
        actorId,
        x,
        y,
        scale,
        zIndex,
      })),
    ).toEqual(
      selectedFirst.slots.map(({ actorId, x, y, scale, zIndex }) => ({
        actorId,
        x,
        y,
        scale,
        zIndex,
      })),
    )
  })

  it('falls back before render when requested clearance cannot fit five actors', () => {
    const result = buildBattleFormation(
      input({
        constraints: {
          minUpperBodyClearance: 0.3,
          protectedUpperBodyRatio: 0.5,
          minimumTouchTargetPx: 44,
        },
      }),
    )

    expect(result.mode).toBe('frontline-three')
    expect(result.slots.filter((slot) => slot.visible).map((slot) => slot.actorId)).toEqual([
      'local',
      'ally-2',
      'ally-4',
    ])
    expect(result.overflowActorIds).toEqual(['ally-1', 'ally-3'])
  })

  it('deduplicates actor ids without changing their first-seen order', () => {
    const result = buildBattleFormation(
      input({ actorIds: ['local', 'ally-1', 'local', 'ally-2', 'ally-1'] }),
    )

    expect(result.slots.map((slot) => slot.actorId)).toEqual(['local', 'ally-1', 'ally-2'])
  })
})
