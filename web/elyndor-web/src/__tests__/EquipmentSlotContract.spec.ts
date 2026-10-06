import { describe, expect, it } from 'vitest'

import {
  EQUIPMENT_SLOTS,
  EQUIPMENT_SLOT_TO_EQUIPPED_KEY,
  createEmptyEquippedInventory,
} from '@/api/contracts'

describe('canonical equipment slot contract', () => {
  it('contains exactly the twelve supported slots and no legacy aliases', () => {
    expect(EQUIPMENT_SLOTS).toEqual([
      'MainHand',
      'OffHand',
      'Head',
      'Shoulders',
      'Chest',
      'Hands',
      'Legs',
      'Feet',
      'Cloak',
      'Amulet',
      'Ring1',
      'Ring2',
    ])

    expect(new Set(Object.values(EQUIPMENT_SLOT_TO_EQUIPPED_KEY)).size).toBe(12)
    expect(Object.keys(createEmptyEquippedInventory())).toEqual([
      'mainHand',
      'offHand',
      'head',
      'shoulders',
      'chest',
      'hands',
      'legs',
      'feet',
      'cloak',
      'amulet',
      'ring1',
      'ring2',
    ])
  })
})
