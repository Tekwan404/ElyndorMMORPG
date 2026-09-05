import { describe, expect, it } from 'vitest'

import { diffContentJson } from '@/admin/contentDiff'
import { createDraftEntity } from '@/admin/entityTemplates'

describe('admin content tools', () => {
  it('produces entity-aware publish diff paths', () => {
    const before = JSON.stringify({
      monsters: [{ id: 'WOLF', maxHp: 180 }],
      items: [],
    })
    const after = JSON.stringify({
      monsters: [{ id: 'WOLF', maxHp: 220 }, { id: 'DIRE_WOLF', maxHp: 400 }],
      items: [],
    })

    const diff = diffContentJson(before, after)

    expect(diff).toContainEqual({
      path: 'monsters[WOLF].maxHp',
      kind: 'changed',
      before: '180',
      after: '220',
    })
    expect(diff).toContainEqual({
      path: 'monsters[DIRE_WOLF]',
      kind: 'added',
      before: null,
      after: '{ id: DIRE_WOLF }',
    })
  })

  it('creates a monster together with a dedicated AI profile', () => {
    const result = createDraftEntity(
      { monsters: [], monsterAiProfiles: [] },
      { section: 'monsters', id: 'dire_wolf', name: 'Лютоволк' },
    )

    expect(result.entity.id).toBe('DIRE_WOLF')
    expect(result.entity.aiProfileId).toBe('DIRE_WOLF_BASIC_AI')
    expect(result.packageObject.monsterAiProfiles).toEqual([
      { id: 'DIRE_WOLF_BASIC_AI', priorityAbilityIds: [], version: 1 },
    ])
  })

  it('creates valid-shape item templates for consumables and equipment', () => {
    const consumable = createDraftEntity(
      { items: [] },
      { section: 'items', id: 'greater_potion', name: 'Большое зелье', itemType: 'Consumable' },
    ).entity
    expect(consumable).toMatchObject({
      id: 'GREATER_POTION',
      type: 'Consumable',
      stackable: true,
      maxStack: 20,
      healAmount: 50,
      consumableCooldownSeconds: 30,
    })

    const equipment = createDraftEntity(
      { items: [] },
      { section: 'items', id: 'old_ring', name: 'Старое кольцо', itemType: 'Equipment' },
    ).entity
    expect(equipment).toMatchObject({
      id: 'OLD_RING',
      type: 'Equipment',
      stackable: false,
      maxStack: 1,
      slot: 'Accessory',
    })
  })

  it('creates loot tables and merchants as local draft entities', () => {
    const loot = createDraftEntity(
      { lootTables: [] },
      { section: 'lootTables', id: 'dire_wolf_loot' },
    ).entity
    expect(loot).toEqual({ id: 'DIRE_WOLF_LOOT', version: 1, entries: [] })

    const merchant = createDraftEntity(
      {
        merchants: [],
        locations: [{ id: 'STARTER_TOWN' }],
      },
      {
        section: 'merchants',
        id: 'liora_supplies',
        name: 'Лиора',
        locationId: 'STARTER_TOWN',
      },
    ).entity
    expect(merchant).toMatchObject({
      id: 'LIORA_SUPPLIES',
      name: 'Лиора',
      locationId: 'STARTER_TOWN',
      itemIds: [],
    })
  })

  it('rejects duplicate or non-canonical ids', () => {
    expect(() =>
      createDraftEntity(
        { items: [{ id: 'WOLF_HIDE' }] },
        { section: 'items', id: 'wolf_hide', name: 'Дубликат' },
      ),
    ).toThrow('уже существует')

    expect(() =>
      createDraftEntity(
        { items: [] },
        { section: 'items', id: '123 bad', name: 'Bad' },
      ),
    ).toThrow('ID:')
  })
})
