import { describe, expect, it } from 'vitest'

import {
  filterAdminEntities,
  findAdminEntityReferences,
  presentAdminEntity,
  replaceDraftEntity,
  searchAdminPackage,
} from '@/admin/adminWorkspace'

describe('admin workspace helpers', () => {
  it('filters entities by id, visible name and useful metadata', () => {
    const entities = [
      {
        id: 'WOLF',
        displayName: 'Лесной волк',
        rank: 'Normal',
        description: 'Быстрый зверь',
      },
      {
        id: 'FOREST_BOAR',
        displayName: 'Лесной кабан',
        rank: 'Normal',
      },
    ]

    expect(filterAdminEntities(entities, 'wolf')).toEqual([entities[0]])
    expect(filterAdminEntities(entities, 'кабан')).toEqual([entities[1]])
    expect(filterAdminEntities(entities, 'быстрый')).toEqual([entities[0]])
  })

  it('builds compact entity labels for the catalog', () => {
    expect(presentAdminEntity({
      id: 'WOLF',
      displayName: 'Лесной волк',
      rank: 'Normal',
    })).toEqual({
      id: 'WOLF',
      title: 'Лесной волк',
      subtitle: 'Normal',
    })
  })

  it('replaces a form-edited entity directly in the local draft', () => {
    const packageObject = {
      monsters: [
        { id: 'WOLF', maxHp: 180 },
        { id: 'BOAR', maxHp: 220 },
      ],
    }

    const next = replaceDraftEntity(
      packageObject,
      'monsters',
      'WOLF',
      { id: 'WOLF', maxHp: 240 },
    )

    expect(next.monsters).toEqual([
      { id: 'WOLF', maxHp: 240 },
      { id: 'BOAR', maxHp: 220 },
    ])
    expect(packageObject.monsters[0]).toEqual({ id: 'WOLF', maxHp: 180 })
  })

  it('rejects duplicate ids while auto-syncing form changes', () => {
    expect(() => replaceDraftEntity(
      {
        items: [
          { id: 'OLD_RING' },
          { id: 'NEW_RING' },
        ],
      },
      'items',
      'OLD_RING',
      { id: 'NEW_RING' },
    )).toThrow('уже существует')
  })
  it('searches across configured content sections', () => {
    const packageObject = {
      monsters: [{ id: 'WOLF', displayName: 'Лесной волк' }],
      items: [{ id: 'WOLF_FANG', name: 'Волчий клык' }],
      lootTables: [{ id: 'WOLF_LOOT', entries: [] }],
    }
    const sections = [
      { key: 'monsters', label: 'Monsters' },
      { key: 'items', label: 'Items' },
      { key: 'lootTables', label: 'Loot' },
    ]

    expect(searchAdminPackage(packageObject, sections, 'wolf')).toEqual([
      expect.objectContaining({ section: 'monsters', entityId: 'WOLF' }),
      expect.objectContaining({ section: 'items', entityId: 'WOLF_FANG' }),
      expect.objectContaining({ section: 'lootTables', entityId: 'WOLF_LOOT' }),
    ])
  })

  it('finds direct entity references without counting the entity own id', () => {
    const packageObject = {
      monsters: [
        { id: 'WOLF', lootTableId: 'WOLF_LOOT' },
        { id: 'DIRE_WOLF', lootTableId: 'WOLF_LOOT' },
      ],
      locations: [
        { id: 'FOREST', encounters: [{ monsterId: 'WOLF', weight: 1 }] },
      ],
      lootTables: [
        { id: 'WOLF_LOOT', entries: [{ itemId: 'WOLF_FANG' }] },
      ],
    }
    const sections = [
      { key: 'monsters', label: 'Monsters' },
      { key: 'locations', label: 'Locations' },
      { key: 'lootTables', label: 'Loot' },
    ]

    expect(findAdminEntityReferences(packageObject, sections, 'WOLF')).toEqual([
      expect.objectContaining({
        section: 'locations',
        entityId: 'FOREST',
        path: 'encounters[0].monsterId',
      }),
    ])
    expect(findAdminEntityReferences(packageObject, sections, 'WOLF_LOOT')).toEqual([
      expect.objectContaining({
        section: 'monsters',
        entityId: 'WOLF',
        path: 'lootTableId',
      }),
      expect.objectContaining({
        section: 'monsters',
        entityId: 'DIRE_WOLF',
        path: 'lootTableId',
      }),
    ])
  })

})
