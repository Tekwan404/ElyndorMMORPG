import { describe, expect, it } from 'vitest'

import {
  filterAdminEntities,
  presentAdminEntity,
  replaceDraftEntity,
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
})
