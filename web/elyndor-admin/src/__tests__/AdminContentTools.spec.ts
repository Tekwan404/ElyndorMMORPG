import { describe, expect, it } from 'vitest'

import { diffContentJson } from '@/admin/contentDiff'
import {
  addItemToLootTable,
  attachMonsterToLocation,
  createAndLinkMonsterLootTable,
  createDraftEntity,
  duplicateDraftEntity,
} from '@/admin/entityTemplates'

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
  })

  it('creates a monster bundle with AI, loot and location encounter', () => {
    const result = createDraftEntity(
      {
        monsters: [],
        monsterAiProfiles: [],
        lootTables: [],
        locations: [
          {
            id: 'FOREST',
            displayName: 'Лес',
            dangerLevel: 'ADVENTURE',
            encounters: [],
          },
        ],
      },
      {
        section: 'monsters',
        id: 'dire_wolf',
        name: 'Лютоволк',
        monsterTemplate: 'EliteMelee',
        createLootTable: true,
        locationIds: ['FOREST'],
        encounterWeight: 0.25,
      },
    )

    expect(result.entity).toMatchObject({
      id: 'DIRE_WOLF',
      rank: 'Elite',
      aiProfileId: 'DIRE_WOLF_BASIC_AI',
      lootTableId: 'DIRE_WOLF_LOOT',
    })
    expect(result.packageObject.monsterAiProfiles).toContainEqual({
      id: 'DIRE_WOLF_BASIC_AI',
      priorityAbilityIds: [],
      version: 1,
    })
    expect(result.packageObject.lootTables).toContainEqual({
      id: 'DIRE_WOLF_LOOT',
      version: 1,
      entries: [],
    })
    expect(result.packageObject.locations).toContainEqual(
      expect.objectContaining({
        id: 'FOREST',
        encounters: [{ monsterId: 'DIRE_WOLF', weight: 0.25 }],
      }),
    )
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
      slot: 'Amulet',
    })
  })

  it('duplicates monster relations without editing the source monster', () => {
    const result = duplicateDraftEntity(
      {
        monsters: [{
          id: 'WOLF',
          name: 'Wolf',
          displayName: 'Волк',
          aiProfileId: 'WOLF_BASIC_AI',
          lootTableId: 'WOLF_LOOT',
        }],
        monsterAiProfiles: [{
          id: 'WOLF_BASIC_AI',
          priorityAbilityIds: ['BITE'],
          version: 1,
        }],
        lootTables: [{
          id: 'WOLF_LOOT',
          version: 1,
          entries: [{ itemId: 'FANG', dropChance: 1, minQuantity: 1, maxQuantity: 1 }],
        }],
      },
      {
        section: 'monsters',
        sourceId: 'WOLF',
        id: 'DIRE_WOLF',
        name: 'Лютоволк',
      },
    )

    expect(result.entity).toMatchObject({
      id: 'DIRE_WOLF',
      displayName: 'Лютоволк',
      aiProfileId: 'DIRE_WOLF_BASIC_AI',
      lootTableId: 'DIRE_WOLF_LOOT',
    })
    expect(result.packageObject.lootTables).toContainEqual(
      expect.objectContaining({ id: 'DIRE_WOLF_LOOT' }),
    )
  })

  it('links existing monsters and newly-created loot data', () => {
    const withLoot = createAndLinkMonsterLootTable(
      { monsters: [{ id: 'WOLF', lootTableId: null }], lootTables: [] },
      'WOLF',
    )
    expect((withLoot.monsters as Array<Record<string, unknown>>)[0]?.lootTableId).toBe('WOLF_LOOT')

    const withLocation = attachMonsterToLocation(
      {
        locations: [{ id: 'FOREST', dangerLevel: 'ADVENTURE', encounters: [] }],
      },
      'WOLF',
      'FOREST',
      0.5,
    )
    expect((withLocation.locations as Array<Record<string, unknown>>)[0]?.encounters)
      .toEqual([{ monsterId: 'WOLF', weight: 0.5 }])

    const withItem = addItemToLootTable(
      {
        items: [{ id: 'FANG', stackable: true }],
        lootTables: [{ id: 'WOLF_LOOT', entries: [] }],
      },
      'WOLF_LOOT',
      'FANG',
    )
    expect((withItem.lootTables as Array<Record<string, unknown>>)[0]?.entries)
      .toEqual([{ itemId: 'FANG', dropChance: 1, minQuantity: 1, maxQuantity: 1 }])
  })

  it('creates abilities for inline monster workflows', () => {
    const ability = createDraftEntity(
      { abilities: [] },
      {
        section: 'abilities',
        id: 'shadow_bolt',
        name: 'Теневой снаряд',
        abilityScaling: 'SpellPower',
        abilitySchool: 'SHADOW',
      },
    ).entity

    expect(ability).toMatchObject({
      id: 'SHADOW_BOLT',
      isSpell: true,
      school: 'SHADOW',
      actions: [{ type: 'Damage', damageType: 'Magical', spellPowerCoefficient: 1 }],
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
