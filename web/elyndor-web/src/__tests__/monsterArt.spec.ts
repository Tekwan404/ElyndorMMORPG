import { readdirSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'

import { describe, expect, it } from 'vitest'

import { monsterArtUrl } from '@/assets/monsterArt'

interface MonsterContentFile {
  monsters: Array<{ id: string; artId?: string | null }>
}

describe('monster art registry', () => {
  it('resolves a portrait for every authored monster', () => {
    const contentRoot = resolve(process.cwd(), '../../content/monsters')
    const root = resolve(process.cwd(), '../../content')
    const contentFiles = [
      resolve(root, 'package.json'),
      resolve(root, 'bosses/broodmother.json'),
      ...readdirSync(contentRoot)
        .filter(fileName => fileName.endsWith('.json'))
        .map(fileName => resolve(contentRoot, fileName)),
    ]
    const monsters = contentFiles.flatMap((filePath) => {
      const raw = readFileSync(filePath, 'utf8').replace(/^\uFEFF/, '')
      const content = JSON.parse(raw) as MonsterContentFile
      return content.monsters
    })

    const unresolved = monsters
      .filter(monster => !monsterArtUrl(monster.artId, monster.id))
      .map(monster => monster.id)

    expect(unresolved).toEqual([])

    const monsterById = new Map(monsters.map(monster => [monster.id, monster]))
    for (const [monsterId, expectedArtId] of [
      ['ECLIPSED_CITADEL_SENTINEL_L25', 'soldat-zatmeniia'],
      ['ANCIENT_MINE_GOBLIN_L16', 'shakhtnyi-dozornyi'],
      ['DEEP_FOREST_OSKVERNIONNYI_KABAN_L7', 'corrupted-boar'],
      ['SHATTERED_ORDER_MIRROR_CASTELLAN_L25', 'rytsar-zerkalnogo-iadra'],
      ['SHATTERED_ORDER_MIRROR_PRIEST_L25', 'zhrets-ugasshego-sveta'],
    ] as const) {
      const monster = monsterById.get(monsterId)
      expect(monster).toBeDefined()
      expect(monsterArtUrl(monster?.artId, monster?.id)).toMatch(new RegExp(`${expectedArtId}\\.`))
    }
  })
})
