import { describe, expect, it } from 'vitest'
import { readFileSync, readdirSync } from 'node:fs'
import { resolve } from 'node:path'

import { itemArtUrl } from '@/assets/itemArt'
import { monsterArtUrl } from '@/assets/monsterArt'

const monsterContentRoot = resolve(process.cwd(), '../../content/monsters')
const itemContentRoot = resolve(process.cwd(), '../../content/items')

function getItems() {
  return readdirSync(itemContentRoot)
    .filter(fileName => fileName.endsWith('.json'))
    .flatMap((fileName) => {
      const raw = readFileSync(resolve(itemContentRoot, fileName), 'utf8').replace(/^\uFEFF/, '')
      return (JSON.parse(raw) as { items: Array<{ id: string; iconId?: string; setId?: string }> }).items
    })
}

function getMonster(monsterId: string) {
  const monsters = readdirSync(monsterContentRoot)
    .filter(fileName => fileName.endsWith('.json'))
    .flatMap((fileName) => {
      const raw = readFileSync(resolve(monsterContentRoot, fileName), 'utf8').replace(/^\uFEFF/, '')
      return (JSON.parse(raw) as { monsters: Array<{ id: string; artId: string }> }).monsters
    })

  return monsters.find(monster => monster.id === monsterId)
}

describe('authored content art', () => {
  it('uses the dedicated corrupted wolf portrait instead of mature-wolf art', () => {
    const monster = getMonster('BLIGHTED_GROVE_PORCHENYI_VOLK_L15')

    expect(monster?.artId).toBe('porchenyi-volk')
    expect(monsterArtUrl(monster?.artId, monster?.id)).toMatch(/porchenyi-volk\.(png|webp)$/)
  })

  it('uses the exact forest-wolf source without sharing the generic wolf art key', () => {
    const variants = [
      getMonster('FOREST_WOLF_L1'),
      getMonster('FOREST_WOLF_L4'),
      getMonster('WHISPERING_FOREST_LESNOI_VOLK_L3'),
    ]

    expect(variants.map(monster => monster?.artId)).toEqual(Array(3).fill('enemy-lesnoy-volk'))
    expect(variants.map(monster => monsterArtUrl(monster?.artId, monster?.id)))
      .toEqual(Array(3).fill(expect.stringMatching(/enemy-lesnoy-volk\.webp$/)))
  })

  it('resolves the Heart of the Blighted Grove guardian helmet artwork', () => {
    const art = itemArtUrl('sets/set_heart_of_blighted_grove_warrior_guardian_head')

    expect(art).toMatch(/set_heart_of_blighted_grove_warrior_guardian_head\.(png|webp)$/)
  })

  it('resolves artwork for every imported raid set piece', () => {
    const importedSetIds = [
      'SET_HEART_OF_BLIGHTED_GROVE_',
      'SET_SHATTERED_ORDER_RAID_',
      'SET_BLACK_BASTION_',
    ]
    const setItems = getItems().filter(item => importedSetIds.some(prefix => item.setId?.startsWith(prefix)))
    const missingArt = setItems.filter(item => !itemArtUrl(item.iconId)).map(item => item.id)

    expect(setItems.length).toBeGreaterThan(0)
    expect(missingArt).toEqual([])
    expect(itemArtUrl('imported/runnyi_fonar_shtreka')).toMatch(/runnyi_fonar_shtreka\.webp$/)
  })
})
