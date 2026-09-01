import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const packagePath = path.join(root, 'content', 'package.json')
const content = JSON.parse(fs.readFileSync(packagePath, 'utf8'))
const tree = content.talentTrees.find((candidate) => candidate.id === 'WARRIOR_TREE')

if (!tree || tree.nodes.length !== 96) {
  throw new Error('Expected the canonical 96-node WARRIOR_TREE.')
}

const supported = (type, key, values, targetId, metadata = {}) => ({
  type,
  key,
  values,
  ...(targetId ? { targetId } : {}),
  ...metadata,
})
const deferred = (type, key, values, owner, targetId) => ({
  type,
  key,
  values,
  ...(targetId ? { targetId } : {}),
  runtimeStatus: 'Deferred',
  deferredOwner: owner,
})
const zeroes = (node) => Array.from({ length: node.maxRank }, () => 0)
const values = (node, ...items) => {
  if (items.length !== node.maxRank) {
    throw new Error(`${node.id}: expected ${node.maxRank} rank values, received ${items.length}`)
  }
  return items
}

const modifierFactories = {
  'G-1-1': (n) => [supported('StatModifier', 'ARMOR_PERCENT', values(n, 2, 4, 6, 9))],
  'G-1-2': (n) => [supported('EventTriggered', 'ON_DAMAGE_TAKEN', values(n, 2, 3, 4))],
  'G-1-3': (n) => [supported('StatModifier', 'STAMINA_PERCENT', values(n, 2, 4, 6, 9))],
  'G-2-1': (n) => [
    supported('StatModifier', 'DODGE_PERCENT', values(n, 2, 4)),
    deferred('EventTriggered', 'ON_HP_THRESHOLD', values(n, 2, 2), 'COMBAT_SESSION'),
  ],
  'G-2-3': (n) => [supported('StatModifier', 'INCOMING_PHYSICAL_DAMAGE_REDUCTION_PERCENT', values(n, 1, 2, 3))],
  'G-3-3': (n) => [supported('ResourceModifier', 'MAX_RESOURCE_FLAT', values(n, 5, 10, 15))],
  'G-4-2': (n) => [
    supported('AbilityModifier', 'ABILITY_RESOURCE_COST_FLAT', values(n, 5, 10), 'PROVOKE'),
    deferred('AbilityModifier', 'ON_ABILITY_USED', zeroes(n), 'COMBAT_SESSION', 'PROVOKE'),
  ],
  'G-4-3': (n) => [supported('StatModifier', 'MAGIC_RESISTANCE_PERCENT', values(n, 2, 4, 6, 9))],
  'G-5-1': (n) => [supported('AbilityModifier', 'UNLOCK_ABILITY', values(n, 1), 'BASTION')],
  'G-5-3': (n) => [supported('StatModifier', 'INCOMING_MAGICAL_DAMAGE_REDUCTION_PERCENT', values(n, 2, 4))],
  'G-7-1': (n) => [supported('AbilityModifier', 'ABILITY_COOLDOWN_SECONDS', values(n, 10, 20), 'BASTION')],
  'G-7-3': (n) => [supported('StatModifier', 'MAX_HP_PERCENT', values(n, 2, 4, 6, 9))],
  'G-8-2': (n) => [deferred('AbilityModifier', 'ON_ABILITY_USED', zeroes(n), 'COMBAT_SESSION', 'BASTION')],
  'G-9-1': (n) => [
    supported('StatModifier', 'INCOMING_PHYSICAL_DAMAGE_REDUCTION_PERCENT', values(n, 6)),
    supported('StatModifier', 'INCOMING_MAGICAL_DAMAGE_REDUCTION_PERCENT', values(n, 6)),
    deferred('EventTriggered', 'ON_DAMAGE_TAKEN', zeroes(n), 'PARTY'),
  ],
  'B-1-1': (n) => [supported('StatModifier', 'ATTACK_POWER_PERCENT', values(n, 2, 4, 6, 9))],
  'B-1-2': (n) => [supported('EventTriggered', 'ON_ENEMY_KILLED', values(n, 8, 12, 16))],
  'B-1-3': (n) => [supported('StatModifier', 'ACCURACY_PERCENT', values(n, 1.5, 3, 4.5, 6))],
  'B-1-4': (n) => [supported('StatModifier', 'STRENGTH_PERCENT', values(n, 3, 6))],
  'B-2-2': (n) => [supported('AbilityModifier', 'UNLOCK_ABILITY', values(n, 1), 'WILD_STRIKE')],
  'B-2-3': (n) => [supported('StatModifier', 'CRITICAL_CHANCE_PERCENT', values(n, 1.5, 3, 4.5, 6))],
  'B-3-2': (n) => [supported('AbilityModifier', 'UNLOCK_ABILITY', values(n, 1), 'WHIRLWIND')],
  'B-3-1': (n) => [supported(
    'EventTriggered', 'ON_CRITICAL_HIT', values(n, 4, 8), undefined,
    { internalCooldownSeconds: 1 },
  )],
  'B-3-3': (n) => [supported('StatModifier', 'ARMOR_PENETRATION_PERCENT', values(n, 2, 4, 6, 9))],
  'B-4-2': (n) => [
    supported('AbilityModifier', 'ABILITY_DAMAGE_PERCENT', values(n, 10, 20), 'WHIRLWIND'),
    supported('AbilityModifier', 'ABILITY_COOLDOWN_SECONDS', values(n, 1, 2), 'WHIRLWIND'),
  ],
  'B-4-3': (n) => [supported('AbilityModifier', 'ABILITY_ARMOR_PENETRATION_PERCENT', values(n, 8, 15), 'WILD_STRIKE')],
  'B-5-1': (n) => [supported('AbilityModifier', 'UNLOCK_ABILITY', values(n, 1), 'BERSERK')],
  'B-5-2': (n) => [supported('StatModifier', 'ATTACK_SPEED_PERCENT', values(n, 1.5, 3, 4.5, 6))],
  'B-5-3': (n) => [supported('StatModifier', 'CRITICAL_DAMAGE_PERCENT', values(n, 8, 15))],
  'B-6-1': (n) => [supported('StatModifier', 'VAMPIRISM_PERCENT', values(n, 2, 4))],
  'B-8-2': (n) => [
    supported('AbilityModifier', 'ABILITY_COOLDOWN_SECONDS', values(n, 15, 30), 'BERSERK'),
    deferred('EventTriggered', 'ON_ENEMY_KILLED', zeroes(n), 'COMBAT_SESSION', 'WILD_STRIKE'),
  ],
  'B-9-1': (n) => [
    supported('StatModifier', 'ATTACK_POWER_PERCENT', values(n, 10)),
    supported('AbilityModifier', 'EFFECT_DURATION_SECONDS', values(n, 4), 'BERSERK'),
    deferred('EventTriggered', 'ON_CRITICAL_HIT', zeroes(n), 'COMBAT_SESSION'),
  ],
  'W-1-3': (n) => [supported('StatModifier', 'ACCURACY_PERCENT', values(n, 2, 4))],
  'W-2-3': (n) => [supported('StatModifier', 'STAMINA_PERCENT', values(n, 2, 4, 6))],
}

const eventKeys = new Map([
  ['G-1-2', 'ON_DAMAGE_TAKEN'], ['G-1-4', 'ON_AUTO_ATTACK'], ['G-2-2', 'ON_ABILITY_USED'],
  ['G-2-4', 'ON_HP_THRESHOLD'], ['G-3-1', 'ON_DODGE'], ['G-3-2', 'ON_ABILITY_USED'],
  ['G-3-4', 'ON_DAMAGE_TAKEN'], ['G-4-1', 'ON_HP_THRESHOLD'], ['G-4-4', 'ON_DODGE'],
  ['G-5-2', 'ON_DAMAGE_TAKEN'], ['G-6-1', 'ON_AUTO_ATTACK'], ['G-6-2', 'ON_HP_THRESHOLD'],
  ['G-6-3', 'ON_ABILITY_USED'], ['G-6-4', 'ON_ABILITY_USED'], ['G-7-2', 'ON_HP_THRESHOLD'],
  ['G-7-4', 'ON_HP_THRESHOLD'], ['G-8-1', 'ON_DAMAGE_TAKEN'], ['G-8-3', 'ON_DAMAGE_TAKEN'],
  ['B-1-2', 'ON_ENEMY_KILLED'], ['B-2-1', 'ON_HP_THRESHOLD'], ['B-2-4', 'ON_ABILITY_USED'],
  ['B-3-1', 'ON_CRITICAL_HIT'], ['B-3-4', 'ON_CRITICAL_HIT'], ['B-4-1', 'ON_AUTO_ATTACK'],
  ['B-4-4', 'ON_HP_THRESHOLD'], ['B-5-4', 'ON_ABILITY_USED'], ['B-6-2', 'ON_CRITICAL_HIT'],
  ['B-6-3', 'ON_DAMAGE_TAKEN'], ['B-6-4', 'ON_CRITICAL_HIT'], ['B-7-1', 'ON_AUTO_ATTACK'],
  ['B-7-2', 'ON_HP_THRESHOLD'], ['B-7-3', 'ON_ABILITY_USED'], ['B-7-4', 'ON_HP_THRESHOLD'],
  ['B-8-1', 'ON_ABILITY_USED'], ['B-8-3', 'ON_HP_THRESHOLD'],
])

const warlordAbilities = new Map([
  ['W-2-1', 'BATTLE_CRY'], ['W-3-1', 'ENDURANCE_CRY'], ['W-3-3', 'WAR_BANNER'],
  ['W-4-2', 'CRY_OF_VENGEANCE'], ['W-5-1', 'VICTORY_FLAG'], ['W-6-1', 'RALLY_CRY'],
  ['W-7-1', 'BATTLE_STANDARD'],
])

const berserkerIcons = {
  'B-1-1': 'BERSERKER_WAR_MASK', 'B-1-2': 'BERSERKER_BLOOD_RENEWAL',
  'B-1-3': 'BERSERKER_KEEN_EYE', 'B-1-4': 'BERSERKER_CRUSHING_BLOW',
  'B-2-1': 'BERSERKER_WAR_MASK', 'B-2-2': 'BERSERKER_RAGE_SLASH',
  'B-2-3': 'BERSERKER_KEEN_EYE', 'B-2-4': 'BERSERKER_SUNDERING_BLADE',
  'B-3-1': 'BERSERKER_KEEN_EYE', 'B-3-2': 'BERSERKER_BLOOD_BLADES',
  'B-3-3': 'BERSERKER_SHATTER_GUARD', 'B-3-4': 'BERSERKER_BLOOD_BLADES',
  'B-4-1': 'BERSERKER_SUNDERING_BLADE', 'B-4-2': 'BERSERKER_BLOOD_BLADES',
  'B-4-3': 'BERSERKER_SHATTER_GUARD', 'B-4-4': 'BERSERKER_WAR_MASK',
  'B-5-1': 'BERSERKER_WAR_MASK', 'B-5-2': 'BERSERKER_RAGE_SLASH',
  'B-5-3': 'BERSERKER_BLOOD_BLADES', 'B-5-4': 'BERSERKER_WAR_MASK',
  'B-6-1': 'BERSERKER_BLOOD_RENEWAL', 'B-6-2': 'BERSERKER_SHATTER_GUARD',
  'B-6-3': 'BERSERKER_IRON_WILL', 'B-6-4': 'BERSERKER_RAGE_SLASH',
  'B-7-1': 'BERSERKER_SUNDERING_BLADE', 'B-7-2': 'BERSERKER_WAR_MASK',
  'B-7-3': 'BERSERKER_BLOOD_BLADES', 'B-7-4': 'BERSERKER_KEEN_EYE',
  'B-8-1': 'BERSERKER_RAGE_SLASH', 'B-8-2': 'BERSERKER_WAR_MASK',
  'B-8-3': 'BERSERKER_BLADE_GUARD', 'B-9-1': 'BERSERKER_WAR_MASK',
}

for (const node of tree.nodes) {
  if (modifierFactories[node.id]) {
    node.modifiers = modifierFactories[node.id](node)
  } else if (warlordAbilities.has(node.id)) {
    node.modifiers = [deferred(
      'AbilityModifier', 'UNLOCK_ABILITY', Array.from({ length: node.maxRank }, () => 1),
      'PARTY', warlordAbilities.get(node.id),
    )]
  } else {
    const owner = node.branchId === 'WARLORD' ? 'PARTY' : 'COMBAT_SESSION'
    const key = node.branchId === 'WARLORD' ? 'ON_PARTY_EVENT' : (eventKeys.get(node.id) ?? 'ON_ABILITY_USED')
    node.modifiers = [deferred('EventTriggered', key, zeroes(node), owner)]
  }

  if (node.branchId === 'BERSERKER') {
    node.iconId = berserkerIcons[node.id]
  } else {
    delete node.iconId
  }
}

const abilities = [
  {
    id: 'BITE', type: 'Instant', targetType: 'SingleEnemy', resourceCost: 0,
    cooldown: '00:00:04', castTime: '00:00:00', usesGlobalCooldown: true,
    globalCooldownCategory: 'Standard', isSpell: false, school: 'PHYSICAL',
    actions: [{ type: 'Damage', amount: 4, damageType: 'Physical', attackPowerCoefficient: 0.9 }],
  },
  {
    id: 'BASTION', type: 'Instant', targetType: 'Self', resourceCost: 40,
    cooldown: '00:01:30', castTime: '00:00:00', usesGlobalCooldown: false,
    globalCooldownCategory: 'None', isSpell: false, school: 'PHYSICAL',
    actions: [{
      type: 'ApplyEffect', effect: {
        id: 'BASTION_DAMAGE_REDUCTION', kind: 'StatModifier', duration: '00:00:06',
        maxStacks: 1, stackPolicy: 'Refresh', magnitude: 0.7,
        modifiedStat: 'IncomingDamageMultiplier', modifierMode: 'Multiplicative',
      },
    }],
  },
  {
    id: 'WILD_STRIKE', type: 'Instant', targetType: 'SingleEnemy', resourceCost: 25,
    cooldown: '00:00:06', castTime: '00:00:00', usesGlobalCooldown: true,
    globalCooldownCategory: 'Standard', isSpell: false, school: 'PHYSICAL',
    actions: [{ type: 'Damage', damageType: 'Physical', attackPowerCoefficient: 1.35 }],
  },
  {
    id: 'WHIRLWIND', type: 'Instant', targetType: 'AllEnemiesInCombat', resourceCost: 35,
    cooldown: '00:00:10', castTime: '00:00:00', usesGlobalCooldown: true,
    globalCooldownCategory: 'Standard', isSpell: false, school: 'PHYSICAL',
    actions: [{ type: 'Damage', damageType: 'Physical', attackPowerCoefficient: 0.7 }],
  },
  {
    id: 'BERSERK', type: 'Instant', targetType: 'Self', resourceCost: 50,
    cooldown: '00:02:00', castTime: '00:00:00', usesGlobalCooldown: false,
    globalCooldownCategory: 'None', isSpell: false, school: 'PHYSICAL',
    actions: [
      { type: 'ApplyEffect', effect: { id: 'BERSERK_ATTACK_POWER', kind: 'StatModifier', duration: '00:00:08', maxStacks: 1, stackPolicy: 'Refresh', magnitude: 0.15, modifiedStat: 'AttackPower', modifierMode: 'Percent' } },
      { type: 'ApplyEffect', effect: { id: 'BERSERK_CRITICAL_CHANCE', kind: 'StatModifier', duration: '00:00:08', maxStacks: 1, stackPolicy: 'Refresh', magnitude: 8, modifiedStat: 'CriticalChance', modifierMode: 'Flat' } },
      { type: 'ApplyEffect', effect: { id: 'BERSERK_ATTACK_SPEED', kind: 'StatModifier', duration: '00:00:08', maxStacks: 1, stackPolicy: 'Refresh', magnitude: 0.25, modifiedStat: 'AttackSpeed', modifierMode: 'Percent' } },
    ],
  },
]

for (const ability of abilities) {
  const index = content.abilities.findIndex((candidate) => candidate.id === ability.id)
  if (index >= 0) content.abilities[index] = ability
  else content.abilities.push(ability)
}

content.monsterAiProfiles = [{
  id: 'WOLF_BASIC_AI',
  priorityAbilityIds: ['BITE'],
  version: 1,
}]
content.monsters = [{
  id: 'WOLF',
  name: 'Forest Wolf',
  rank: 'Normal',
  level: 3,
  maxHp: 180,
  stats: {
    level: 3,
    accuracy: 95,
    dodge: 3,
    criticalChance: 5,
    criticalDamage: 1,
    armor: 16,
    magicResistance: 8,
    armorPenetration: 0,
    magicPenetration: 0,
    attackPower: 12,
    spellPower: 0,
  },
  autoAttackInterval: '00:00:02.5000000',
  autoAttackBaseDamage: 6,
  abilityIds: ['BITE'],
  aiProfileId: 'WOLF_BASIC_AI',
  version: 1,
}]

content.contentVersion = '0.6.0'
content.balanceVersion = '0.5.0'
content.publishedAtUtc = '2026-09-01T00:00:00+00:00'

fs.writeFileSync(packagePath, `${JSON.stringify(content, null, 2)}\n`, 'utf8')

const coveragePath = path.join(root, 'docs', 'development', 'phase-3c-talent-coverage.md')
const coverageRows = tree.nodes.map((node) => {
  const contracts = node.modifiers.map((modifier) => {
    const target = modifier.targetId ? `:${modifier.targetId}` : ''
    const owner = modifier.runtimeStatus === 'Deferred' ? ` -> ${modifier.deferredOwner}` : ''
    return `${modifier.runtimeStatus ?? 'Supported'} ${modifier.key}${target}${owner}`
  }).join('<br>')
  return `| ${node.id} | ${node.branchId} | ${node.englishName} | ${contracts} | ${node.iconId ?? 'generated'} |`
})
const coverage = `# Phase 3C Warrior Talent Coverage\n\n`
  + `Generated from \`content/package.json\` by \`scripts/update-phase3c-talent-content.mjs\`.\n\n`
  + `- Nodes: ${tree.nodes.length}\n`
  + `- Supported hooks: ${tree.nodes.flatMap((node) => node.modifiers).filter((modifier) => modifier.runtimeStatus !== 'Deferred').length}\n`
  + `- Deferred hooks: ${tree.nodes.flatMap((node) => node.modifiers).filter((modifier) => modifier.runtimeStatus === 'Deferred').length}\n`
  + `- Deferred hooks remain data contracts until their owning phase supplies CombatSession, Party, Monster, Boss/Elite, or equipment runtime.\n\n`
  + `| ID | Branch | Talent | Runtime contracts | Icon |\n`
  + `| --- | --- | --- | --- | --- |\n`
  + `${coverageRows.join('\n')}\n`
fs.writeFileSync(coveragePath, coverage, 'utf8')
