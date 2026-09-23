import { describe, expect, it } from 'vitest'
import { readFileSync, readdirSync } from 'node:fs'
import { resolve } from 'node:path'

import { itemArtUrl } from '@/assets/itemArt'
import { monsterArtUrl } from '@/assets/monsterArt'

const monsterContentRoot = resolve(process.cwd(), '../../content/monsters')
const itemContentRoot = resolve(process.cwd(), '../../content/items')

function getItems() {
  const composedItems = new Map<string, { id: string; iconId?: string; setId?: string }>()

  readdirSync(itemContentRoot)
    .filter(fileName => fileName.endsWith('.json'))
    .sort((left, right) => left < right ? -1 : left > right ? 1 : 0)
    .forEach((fileName) => {
      const raw = readFileSync(resolve(itemContentRoot, fileName), 'utf8').replace(/^\uFEFF/, '')
      const items = (JSON.parse(raw) as { items: Array<{ id: string; iconId?: string; setId?: string }> }).items

      for (const item of items) composedItems.set(item.id, item)
    })

  return [...composedItems.values()]
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
    const item = getItems().find(candidate => candidate.id === 'SET_HEART_OF_BLIGHTED_GROVE_WARRIOR_GUARDIAN_HEAD')
    const art = itemArtUrl(item?.iconId)

    expect(item?.iconId).toBe('sets/heart-of-blighted-grove/heart_guardian/heart_guardian_helmet')
    expect(art).toMatch(/sets\/heart-of-blighted-grove\/heart_guardian\/heart_guardian_helmet\.webp$/)
  })

  it('resolves the Ancient Mine marksman helmet artwork from its canonical IconId', () => {
    const item = getItems().find(candidate => candidate.id === 'SET_ANCIENT_MINE_ARCHER_MARKSMANSHIP_HEAD')

    expect(item?.iconId).toBe('sets/ancient-mine/mine_tracker/mine_tracker_helmet')
    expect(itemArtUrl(item?.iconId)).toMatch(/sets\/ancient-mine\/mine_tracker\/mine_tracker_helmet\.webp$/)
  })

  it('resolves the Eclipsed Oracle boots from their canonical set artwork', () => {
    const item = getItems().find(candidate => candidate.id === 'MAGE_LEGENDARY_ECLIPSED_ORACLE_FEET')

    expect(item?.iconId).toBe('sets/black-bastion/eclipsed_oracle/eclipsed_oracle_boots')
    expect(itemArtUrl(item?.iconId)).toMatch(/sets\/black-bastion\/eclipsed_oracle\/eclipsed_oracle_boots\.webp$/)
  })

  it('resolves the Echoes of the Deep hood artwork', () => {
    const item = getItems().find(candidate => candidate.id === 'DUNGEON_MINES_MAGE_HOOD_RARE')

    expect(itemArtUrl(item?.iconId)).toMatch(/dungeon_mines_mage_hood_rare\.webp$/)
  })

  it('resolves the drowned pilgrim lantern artwork', () => {
    const item = getItems().find(candidate => candidate.id === 'FONAR_UTONUVSHEGO_PALOMNIKA')

    expect(itemArtUrl(item?.iconId)).toMatch(/imported\/fonar_utonuvshego_palomnika\.webp$/)
  })

  it('resolves every unambiguous named world-equipment artwork', () => {
    const expectedIconIds = {
      LIK_POKHISHCHENNOI_DUSHI: 'items_outside_sets/shattered_order_citadel/stolen_soul_visage',
      OSKOLOK_SERDTSA_BASTIONA: 'items_outside_sets/black_bastion/bastion_heart_shard',
      RUNNOE_SERDTSE_KOLOSSA: 'items_outside_sets/black_bastion/colossus_runic_heart',
      SERDTSE_CHASHCHI: 'items_outside_sets/corrupted_grove/grove_heart',
      SFERA_POSLEDNEGO_REZERVA: 'items_outside_sets/shattered_order_citadel/last_reserve_orb',
      SOSUD_PLENIONNOI_DUSHI: 'items_outside_sets/open_world_late/captured_soul_vial',
      ZNAMIA_PEPELNOGO_MARSHALA: 'items_outside_sets/black_bastion/ashen_marshal_banner',
      VENETS_IADOVITOI_MATRONY: 'items_outside_sets/corrupted_grove/venom_matron_crown',
      ARBALET_CHIORNYKH_VRAT: 'items_outside_sets/black_bastion/black_gate_crossbow',
      KLINOK_PERVOGO_STRAZHA: 'items_outside_sets/black_bastion/first_guardian_blade',
      KLINOK_ZERKALNOGO_EKHA: 'items_outside_sets/open_world_mid/reflection_mirror_blade',
      KLIUCH_KOMENDANTA: 'items_outside_sets/black_bastion/commandant_key',
      KRUSHITEL_KRIVOKORNIA: 'items_outside_sets/corrupted_grove/crookedroot_crusher',
      LUK_BEZMOLVNOGO_ZNAMENI: 'items_outside_sets/black_bastion/silent_banner_bow',
      LUK_TROINOGO_ASPEKTA: 'items_outside_sets/shattered_order_citadel/triple_aspect_bow',
      MOLOT_ARK_TORA: 'items_outside_sets/black_bastion/ark_tor_hammer',
      POSOKH_CHIORNOI_ZVEZDY: 'items_outside_sets/black_bastion/black_star_staff',
      POSOKH_SORVANNOGO_ZAKLIATIIA: 'items_outside_sets/open_world_mid/interrupted_mana_staff',
      ZERKALNYI_BASTARD: 'items_outside_sets/shattered_order_citadel/mirror_bastard_sword',
      FOKUS_CHIORNOI_ZVEZDY: 'items_outside_sets/open_world_late/blackstar_focus',
      FOKUS_OSTATOCHNOI_MANY: 'items_outside_sets/shattered_order_citadel/residual_mana_focus',
      KADILO_INKVIZITORA: 'items_outside_sets/black_bastion/inquisitor_censer',
      KADILO_OCHISHCHAIUSHCHEGO_PLAMENI: 'items_outside_sets/corrupted_grove/purifying_flame_censer',
      SFERA_ZATMIONNOGO_ORAKULA: 'items_outside_sets/black_bastion/eclipsed_oracle_orb',
      KOLTSO_RAZBITOGO_OTRAZHENIIA: 'items_outside_sets/shattered_order_citadel/broken_reflection_ring',
      PECHAT_BEZMOLVNOGO_SUDA: 'items_outside_sets/black_bastion/silent_judgement_seal',
      PECHAT_TRIEDINSTVA: 'items_outside_sets/shattered_order_citadel/triune_seal',
    }

    for (const [itemId, expectedIconId] of Object.entries(expectedIconIds)) {
      const item = getItems().find(candidate => candidate.id === itemId)

      expect(item?.iconId).toBe(expectedIconId)
      expect(itemArtUrl(item?.iconId)).toBeDefined()
    }
  })

  it('resolves artwork for every authored set piece', () => {
    const importedSetIds = [
      'SET_HEART_OF_BLIGHTED_GROVE_',
      'SET_SHATTERED_ORDER_RAID_',
      'SET_BLACK_BASTION_',
    ]
    const setItems = getItems().filter(item => importedSetIds.some(prefix => item.setId?.startsWith(prefix)))
    const missingArt = setItems.filter(item => !itemArtUrl(item.iconId)).map(item => item.id)

    expect(setItems.length).toBeGreaterThan(0)
    expect(missingArt).toEqual([])
  })
})
