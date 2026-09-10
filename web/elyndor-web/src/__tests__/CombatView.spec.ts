import { mount } from '@vue/test-utils'

import type { CombatActorSnapshot, CombatLootRoll } from '@/api/contracts'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('@microsoft/signalr', () => ({
  HubConnectionState: { Connected: 'Connected' },
  HttpTransportType: { LongPolling: 4 },
  LogLevel: { Warning: 3, Error: 4 },
  HubConnectionBuilder: class {
    withUrl() { return this }
    withAutomaticReconnect() { return this }
    configureLogging() { return this }
    build() {
      return {
        state: 'Connected',
        on: vi.fn<(...args: unknown[]) => void>(),
        onreconnecting: vi.fn<(...args: unknown[]) => void>(),
        onreconnected: vi.fn<(...args: unknown[]) => void>(),
        onclose: vi.fn<(...args: unknown[]) => void>(),
        start: vi.fn<() => Promise<void>>().mockResolvedValue(undefined),
        invoke: vi.fn<(...args: unknown[]) => Promise<unknown>>().mockResolvedValue({
          succeeded: false, errorCode: 'combat_not_found', snapshot: null, events: [],
        }),
      }
    }
  },
}))

import CombatView from '@/game/combat/views/CombatView.vue'
import { useCombatSessionStore } from '@/stores/combatSession'

describe('CombatView', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('renders server-provided monster presentation directly from the combat snapshot', () => {
    const store = useCombatSessionStore()
    store.snapshot = {
      sessionId: crypto.randomUUID(), sequence: 4, status: 'Active',
      serverTimeUtc: '2026-09-01T12:00:00Z',
      contentVersion: '0.9.3',
      balanceVersion: '0.9.1',
      player: actor('Player', 'WARRIOR', 'Warrior', 140, 180, 35, 100, [
        { id: 'STRIKE', resourceCost: 0, cooldownSeconds: 0 },
        { id: 'WILD_STRIKE', resourceCost: 25, cooldownSeconds: 6 },
      ]),
      enemy: actor('Monster', 'WOLF', 'Волк', 120, 180, 0, 0, [], 3, 'wolf'),
    }

    const wrapper = mount(CombatView)

    expect(wrapper.text()).toContain('Волк')
    expect(wrapper.text()).toContain('УР. 3')
    expect(wrapper.text()).toContain('Удар')
    expect(wrapper.text()).toContain('Дикий удар')
    expect(wrapper.text()).not.toContain('Вихрь')
    expect(wrapper.find('img[alt="Волк"]').exists()).toBe(true)
    expect(wrapper.findAll('[role="progressbar"]')).toHaveLength(3)
    expect(wrapper.find('[data-combat-battlefield]').exists()).toBe(true)
    expect(wrapper.findAll('.ability-slot')).toHaveLength(6)
    expect(wrapper.get('[data-combat-log-toggle]').attributes('aria-expanded')).toBe('false')
    expect(wrapper.find('.combat-log li').exists()).toBe(false)
  })

  it.each([
    ['MAGE', 'mage-male-transparent.webp'],
    ['ARCHER', 'archer-male-transparent.webp'],
  ] as const)('renders the personal combat portrait for %s', (classId, expectedAsset) => {
    const store = useCombatSessionStore()
    store.snapshot = {
      sessionId: crypto.randomUUID(), sequence: 4, status: 'Active',
      serverTimeUtc: '2026-09-01T12:00:00Z',
      contentVersion: '0.17.0',
      balanceVersion: '0.14.0',
      player: actor('Player', classId, classId, 140, 180, 35, 100, []),
      enemy: actor('Monster', 'WOLF', 'Р’РѕР»Рє', 120, 180, 0, 0, [], 3, 'wolf'),
    }

    const wrapper = mount(CombatView)

    expect(wrapper.get('.player-figure img').attributes('src')).toContain(expectedAsset)
  })

  it('renders multiple enemy targets and switches the selected target', async () => {
    const store = useCombatSessionStore()
    const player = actor('Player', 'WARRIOR', 'Warrior', 180, 180, 0, 100, [])
    const wolf = actor('Monster', 'WOLF', 'Волк', 80, 100, 0, 0, [], 3, 'wolf')
    const alpha = actor('Monster', 'WOLF_ALPHA', 'Альфа-волк', 150, 150, 0, 0, [], 4, 'wolf')
    store.snapshot = {
      sessionId: crypto.randomUUID(),
      sequence: 4,
      status: 'Active',
      serverTimeUtc: '2026-09-06T16:00:00Z',
      contentVersion: '0.10.1',
      balanceVersion: '0.8.0',
      player,
      enemy: wolf,
      enemies: [wolf, alpha],
      selectedTargetActorId: wolf.actorId,
    }
    const selectTarget = vi.spyOn(store, 'selectTarget').mockResolvedValue(undefined)

    const wrapper = mount(CombatView)

    const targets = wrapper.findAll('[data-combat-targets] button')
    expect(targets).toHaveLength(2)
    expect(targets[0]!.classes()).toContain('active')
    expect(wrapper.text()).toContain('Альфа-волк')

    await wrapper.get(`[data-target-actor-id="${alpha.actorId}"]`).trigger('click')
    expect(selectTarget).toHaveBeenCalledWith(alpha.actorId)
  })

  it('presents a two-player encounter as a compact shared front', () => {
    const store = useCombatSessionStore()
    const player = actor('Player', 'WARRIOR', 'Воин', 160, 180, 40, 100, [])
    const ally = actor('Player', 'MAGE', 'Маг', 70, 120, 60, 100, [])
    const enemy = actor('Monster', 'WOLF', 'Волк', 180, 180, 0, 0, [], 3, 'wolf')
    store.snapshot = {
      sessionId: crypto.randomUUID(),
      sequence: 4,
      status: 'Active',
      serverTimeUtc: '2026-09-06T16:00:00Z',
      contentVersion: '0.10.1',
      balanceVersion: '0.8.0',
      player,
      enemy,
      players: [player, ally],
      participantRoster: [
        {
          accountId: crypto.randomUUID(),
          characterId: crypto.randomUUID(),
          actorId: player.actorId,
          status: 'Active',
          rosteredAtUtc: '2026-09-06T16:00:00Z',
          joinedAtUtc: '2026-09-06T16:00:01Z',
        },
        {
          accountId: crypto.randomUUID(),
          characterId: crypto.randomUUID(),
          actorId: ally.actorId,
          status: 'Active',
          rosteredAtUtc: '2026-09-06T16:00:00Z',
          joinedAtUtc: '2026-09-06T16:00:01Z',
        },
      ],
    }

    const wrapper = mount(CombatView)

    expect(wrapper.get('.combat-screen').attributes('data-party-size')).toBe('2')
    expect(wrapper.get('[data-combat-party-roster]').text()).toContain('Слаженный отряд')
    expect(wrapper.findAll('.combat-party-roster__member')).toHaveLength(2)
    expect(wrapper.get('.combat-party-roster__member--self').text()).toContain('Воин')
    expect(wrapper.findAll('.party-formation__unit')).toHaveLength(2)
  })

  it('attributes monster damage to the server-provided monster name while player auto attack is disabled', async () => {
    const store = useCombatSessionStore()
    const player = actor('Player', 'WARRIOR', 'Warrior', 128, 180, 5, 100, [
      { id: 'STRIKE', resourceCost: 0, cooldownSeconds: 0 },
    ])
    const enemy = actor('Monster', 'WOLF', 'Волк', 180, 180, 0, 0, [], 3, 'wolf')
    player.autoAttackEnabled = false
    store.snapshot = {
      sessionId: crypto.randomUUID(), sequence: 3, status: 'Active',
      serverTimeUtc: '2026-09-01T12:00:02Z',
      contentVersion: '0.9.3',
      balanceVersion: '0.9.1',
      player, enemy,
    }
    store.events = [
      {
        sequence: 2,
        type: 'DamageDealt',
        actorId: player.actorId,
        sourceActorId: enemy.actorId,
        targetActorId: player.actorId,
        definitionId: 'AUTO_ATTACK',
        amount: 12,
        amountBeforeShields: 12,
        serverTimeUtc: '2026-09-01T12:00:02Z',
      },
    ]

    const wrapper = mount(CombatView)
    expect(wrapper.get('[data-combat-log-toggle]').text()).toContain('12 урона')
    expect(wrapper.find('.combat-log li').exists()).toBe(false)

    await wrapper.get('[data-combat-log-toggle]').trigger('click')
    const row = wrapper.get('.combat-log li')
    expect(row.attributes('data-side')).toBe('enemy')
    expect(row.text()).toContain('ВОЛК')
    expect(row.text()).toContain('12 урона')
    expect(row.text()).not.toContain('ВЫ')
    expect(wrapper.get('[data-autoattack-toggle]').text()).toContain('Выключена')
  })
  it('renders a live AA cast bar from authoritative autoattack timing', () => {
    const store = useCombatSessionStore()
    const player = actor('Player', 'ARCHER', 'Archer', 120, 120, 100, 100, [])
    const enemy = actor('Monster', 'WOLF', 'Волк', 180, 180, 0, 0, [], 3, 'wolf')
    player.autoAttackEnabled = true
    player.autoAttackIntervalSeconds = 2
    player.nextAutoAttackAtUtc = new Date(Date.now() + 1_000).toISOString()
    store.snapshot = {
      sessionId: crypto.randomUUID(), sequence: 3, status: 'Active',
      serverTimeUtc: new Date().toISOString(),
      contentVersion: '0.17.0',
      balanceVersion: '0.14.0',
      player, enemy,
    }

    const wrapper = mount(CombatView)
    const bar = wrapper.get('[data-autoattack-cast]')

    expect(bar.text()).toContain('AA · Автоатака')
    expect(bar.text()).not.toContain('OFF')
    expect(bar.get('i > span').attributes('style')).toContain('width:')
  })

  it('renders authoritative player and enemy cast bars', () => {
    const store = useCombatSessionStore()
    const player = actor('Player', 'MAGE', 'Mage', 100, 120, 80, 100, [
      { id: 'FIREBALL', resourceCost: 20, cooldownSeconds: 3, displayName: 'Огненный шар' },
    ])
    const enemy = actor('Monster', 'WOLF', 'Волк', 150, 180, 0, 0, [
      { id: 'BITE', resourceCost: 0, cooldownSeconds: 4, displayName: 'Укус' },
    ], 3, 'wolf')
    player.activeCast = {
      abilityId: 'FIREBALL',
      startedAtUtc: new Date(Date.now() - 500).toISOString(),
      resolvesAtUtc: new Date(Date.now() + 1500).toISOString(),
    }
    enemy.activeCast = {
      abilityId: 'BITE',
      startedAtUtc: new Date(Date.now() - 250).toISOString(),
      resolvesAtUtc: new Date(Date.now() + 750).toISOString(),
    }
    store.snapshot = {
      sessionId: crypto.randomUUID(),
      sequence: 7,
      status: 'Active',
      serverTimeUtc: new Date().toISOString(),
      contentVersion: '0.10.0',
      balanceVersion: '0.8.0',
      player,
      enemy,
    }

    const wrapper = mount(CombatView)

    expect(wrapper.get('[data-player-cast]').text()).toContain('Огненный шар')
    expect(wrapper.get('[data-enemy-cast]').text()).toContain('Укус')
  })

  it('disables Need for a loot item rejected by the server equipability contract', () => {
    const store = useCombatSessionStore()
    const player = actor('Player', 'MAGE', 'Mage', 100, 120, 80, 100, [])
    const enemy = actor('Monster', 'SPIDER_BROODMOTHER_L14', 'Broodmother', 0, 220, 0, 0, [])
    store.snapshot = {
      sessionId: crypto.randomUUID(),
      sequence: 10,
      status: 'Victory',
      serverTimeUtc: '2026-09-01T12:00:00Z',
      contentVersion: '0.13.5',
      balanceVersion: '0.11.0',
      player,
      enemy,
    }
    store.lootRolls = [lootRoll(false)]

    const wrapper = mount(CombatView)

    const buttons = wrapper.findAll('[data-loot-rolls] .loot-roll__actions button')
    expect(buttons).toHaveLength(3)
    expect(buttons[0]!.attributes('disabled')).toBeDefined()
    expect(buttons[1]!.attributes('disabled')).toBeUndefined()
    expect(buttons[2]!.attributes('disabled')).toBeUndefined()
  })
})

function lootRoll(canNeed: boolean): CombatLootRoll {
  return {
    lootRollId: crypto.randomUUID(),
    itemId: 'RECRUIT_IRON_SWORD',
    name: 'Iron Sword',
    rarity: 'Epic',
    quantity: 1,
    endsAtUtc: new Date(Date.now() + 20_000).toISOString(),
    eligibleCharacterIds: [],
    canNeed,
  }
}

function actor(
  kind: 'Player' | 'Monster', definitionId: string, name: string,
  hp: number, maxHp: number, resource: number, maxResource: number,
  abilities: {
    id: string
    resourceCost: number
    cooldownSeconds: number
    displayName?: string
  }[],
  level?: number,
  artId?: string | null,
): CombatActorSnapshot {
  return {
    actorId: crypto.randomUUID(), kind, definitionId, name, hp, maxHp,
    resourceType: kind === 'Player'
      ? definitionId === 'MAGE' ? 'MANA' : definitionId === 'ARCHER' ? 'FOCUS' : 'RAGE'
      : 'NONE',
    resource,
    maxResource,
    autoAttackEnabled: false, cooldowns: {},
    knownAbilityIds: abilities.map((ability) => ability.id),
    abilities: abilities.map((ability) => ({
      ...ability,
      displayName: ability.displayName
        ?? (ability.id === 'STRIKE' ? 'Удар' : ability.id === 'WILD_STRIKE' ? 'Дикий удар' : ability.id),
      description: 'Server-provided ability presentation.',
      iconId: null,
    })),
    effects: [],
    level, artId,
  }
}
