import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('@microsoft/signalr', () => ({
  HubConnectionState: { Connected: 'Connected' },
  LogLevel: { Warning: 3, Error: 4 },
  HubConnectionBuilder: class {
    withUrl() {
      return this
    }
    withAutomaticReconnect() {
      return this
    }
    configureLogging() {
      return this
    }
    build() {
      return { state: 'Connected' }
    }
  },
}))

import type { CombatAbility, CombatActorSnapshot, InventoryItem } from '@/api/contracts'
import BattleScreen from '@/game/combat/views/BattleScreen.vue'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'

function actor(
  id: string,
  kind: CombatActorSnapshot['kind'],
  abilities: CombatAbility[] = [],
): CombatActorSnapshot {
  return {
    actorId: id,
    kind,
    definitionId: kind === 'Monster' ? 'WOLF' : id === 'ally' ? 'MAGE' : 'WARRIOR',
    name: id,
    hp: 90,
    maxHp: 100,
    resourceType: kind === 'Player' ? 'RAGE' : 'NONE',
    resource: 70,
    maxResource: kind === 'Player' ? 100 : 0,
    autoAttackEnabled: false,
    cooldowns: {},
    knownAbilityIds: abilities.map((ability) => ability.id),
    abilities,
    effects: [],
    level: 30,
    artId: kind === 'Monster' ? 'wolf' : null,
  }
}

function ability(index: number): CombatAbility {
  return {
    id: `ABILITY_${index}`,
    displayName: `Skill ${index}`,
    description: '',
    iconId: null,
    resourceCost: 0,
    cooldownSeconds: 0,
    targetType: 'SingleEnemy',
  }
}

describe('BattleScreen', () => {
  it('keeps a failed Victory in manual recovery instead of presenting completed rewards', async () => {
    const store = useCombatSessionStore()
    store.snapshot = { sessionId: 'session', sequence: 2, status: 'Victory', serverTimeUtc: '2026-10-05T12:00:00Z', contentVersion: 'test', balanceVersion: 'test', player: actor('local', 'Player'), enemy: actor('enemy', 'Monster') }
    store.recoveryRequired = true
    store.errorCode = 'combat_recovery_required'
    store.connectionState = 'disconnected'
    const getPendingLoot = vi.spyOn(useGameSessionStore(), 'getPendingLoot').mockResolvedValue({ items: [] } as never)
    vi.spyOn(store, 'connect').mockResolvedValue(undefined)
    const resume = vi.spyOn(store, 'resume').mockResolvedValue(false)
    const wrapper = mount(BattleScreen)
    await flushPromises()
    expect(wrapper.find('[data-result]').exists()).toBe(false)
    expect(wrapper.get('[data-combat-connection]').text()).toContain('Награды ещё не подтверждены')
    expect(getPendingLoot).not.toHaveBeenCalled()
    await wrapper.get('[data-combat-connection] button').trigger('click')
    await flushPromises()
    expect(resume).toHaveBeenCalledTimes(1)
    expect(wrapper.emitted('leave')).toBeUndefined()
    expect(wrapper.find('[data-result]').exists()).toBe(false)
    store.recoveryRequired = false
    store.errorCode = null
    store.connectionState = 'connected'
    await flushPromises()
    // A successful manual resume also clears the screen's local failed-reconnect state.
    resume.mockResolvedValue(true)
    await wrapper.get('[data-combat-connection] button').trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-result]').attributes('data-result')).toBe('Victory')
    expect(getPendingLoot).toHaveBeenCalledTimes(1)
    wrapper.unmount()
  })
  it('retains the flee confirmation and shows the failed action in the dialog', async () => {
    const store = useCombatSessionStore()
    store.snapshot = { sessionId: 'session', sequence: 1, status: 'Active', serverTimeUtc: '2026-09-25T12:00:00Z', contentVersion: 'test', balanceVersion: 'test', player: actor('local', 'Player'), enemy: actor('enemy', 'Monster') }
    vi.spyOn(store, 'flee').mockImplementation(async () => { store.errorCode = 'combat_action_failed'; return false })
    const wrapper = mount(BattleScreen)
    await wrapper.get('[data-combat-exit]').trigger('click')
    const submit = [...document.querySelectorAll<HTMLButtonElement>('[role="dialog"] button')].find(button => button.textContent?.trim() === 'Сбежать')!
    submit.click()
    await flushPromises()
    expect(document.querySelector('[role="dialog"]')).not.toBeNull()
    expect(document.querySelector('[role="dialog"] [role="alert"]')).not.toBeNull()
    wrapper.unmount()
  })
  it('keeps commands blocked until resume succeeds and retains retry on sync failure', async () => {
    const store = useCombatSessionStore()
    store.snapshot = { sessionId: 'session', sequence: 1, status: 'Active', serverTimeUtc: '2026-09-25T12:00:00Z', contentVersion: 'test', balanceVersion: 'test', player: actor('local', 'Player', [ability(1)]), enemy: actor('enemy', 'Monster') }
    store.connectionState = 'disconnected'
    vi.spyOn(store, 'connect').mockImplementation(async () => { store.connectionState = 'connected' })
    let complete!: (success: boolean) => void
    vi.spyOn(store, 'resume').mockImplementation(() => new Promise<boolean>(resolve => { complete = resolve }))
    const wrapper = mount(BattleScreen)
    await wrapper.get('[data-combat-connection] button').trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-ability-slot]').attributes('aria-disabled')).toBe('true')
    expect(wrapper.get('.battle-arena__enemy').attributes('disabled')).toBeDefined()
    complete(false)
    await flushPromises()
    expect(wrapper.get('[data-combat-connection]').text()).toContain('Повторить подключение')
    expect(wrapper.get('[data-ability-slot]').attributes('aria-disabled')).toBe('true')
    wrapper.unmount()
  })
  it('offers explicit reconnect after transport closes', () => {
    const store = useCombatSessionStore()
    store.snapshot = { sessionId: 'session', sequence: 1, status: 'Active', serverTimeUtc: '2026-09-25T12:00:00Z', contentVersion: 'test', balanceVersion: 'test', player: actor('local', 'Player', [ability(1)]), enemy: actor('enemy', 'Monster') }
    store.connectionState = 'disconnected'
    const wrapper = mount(BattleScreen)
    expect(wrapper.get('[data-combat-connection]').text()).toContain('Повторить подключение')
    expect(wrapper.get('[data-ability-slot]').attributes('aria-disabled')).toBe('true')
    wrapper.unmount()
  })
  it('announces reconnect and prevents sending abilities until sync completes', async () => {
    const store = useCombatSessionStore()
    store.snapshot = { sessionId: 'session', sequence: 1, status: 'Active', serverTimeUtc: '2026-09-25T12:00:00Z', contentVersion: 'test', balanceVersion: 'test', player: actor('local', 'Player', [ability(1)]), enemy: actor('enemy', 'Monster') }
    store.connectionState = 'reconnecting'
    const wrapper = mount(BattleScreen)
    expect(wrapper.get('[data-combat-connection]').text()).toContain('Восстанавливаем')
    expect(wrapper.get('[data-ability-slot]').attributes('aria-disabled')).toBe('true')
    store.connectionState = 'connected'
    await wrapper.vm.$nextTick()
    expect(wrapper.find('[data-combat-connection]').exists()).toBe(false)
    expect(wrapper.get('[data-ability-slot]').attributes('aria-disabled')).toBe('false')
    wrapper.unmount()
  })

  it('lets keyboard users inspect an ability without activating it', async () => {
    const store = useCombatSessionStore()
    store.snapshot = { sessionId: 'session', sequence: 1, status: 'Active', serverTimeUtc: '2026-09-25T12:00:00Z', contentVersion: 'test', balanceVersion: 'test', player: actor('local', 'Player', [ability(1)]), enemy: actor('enemy', 'Monster') }
    const wrapper = mount(BattleScreen)
    await wrapper.get('[data-ability-slot]').trigger('keydown', { key: 'F1' })
    expect(wrapper.get('[data-ability-inspection]').text()).toContain(ability(1).displayName)
    wrapper.unmount()
  })
  beforeEach(() => {
    setActivePinia(createPinia())
    useCombatSessionStore().connectionState = 'connected'
  })

  it('uses one screen for party formation, skills, controls and collapsed log', () => {
    const store = useCombatSessionStore()
    const local = actor('local', 'Player', [1, 2, 3, 4].map(ability))
    const ally = { ...actor('ally', 'Player'), genderId: 'FEMALE' as const }
    const enemy = {
      ...actor('enemy', 'Monster'),
      currentAggroTargetActorId: 'ally',
    }
    store.snapshot = {
      sessionId: 'session',
      sequence: 1,
      status: 'Active',
      serverTimeUtc: '2026-09-25T12:00:00Z',
      contentVersion: 'test',
      balanceVersion: 'test',
      player: local,
      players: [local, ally],
      enemy,
    }

    const wrapper = mount(BattleScreen)

    expect(wrapper.get('[data-battle-screen]').attributes('data-party-size')).toBe('2')
    expect(wrapper.findAll('[data-character-figure]')).toHaveLength(2)
    expect(wrapper.findAll('[data-ability-slot]')).toHaveLength(4)
    expect(wrapper.get('[data-skill-grid]').attributes('data-columns')).toBe('4')
    expect(wrapper.get('[data-combat-log-toggle]').attributes('aria-expanded')).toBe('false')
  })

  it('keeps the arena fixed when abilities require a second hotbar row', () => {
    const store = useCombatSessionStore()
    const local = actor('local', 'Player', [1, 2, 3, 4, 5, 6, 7, 8].map(ability))
    store.snapshot = {
      sessionId: 'session',
      sequence: 1,
      status: 'Active',
      serverTimeUtc: '2026-09-25T12:00:00Z',
      contentVersion: 'test',
      balanceVersion: 'test',
      player: local,
      enemy: actor('enemy', 'Monster'),
    }

    const wrapper = mount(BattleScreen)

    expect(wrapper.get('[data-battle-screen]').classes()).not.toContain(
      'battle-screen--scroll-fallback',
    )
    expect(wrapper.get('[data-battle-screen]').attributes('data-skill-rows')).toBe('2')
  })

  it('does not truncate large ability kits and renders combat errors as a toast', () => {
    const store = useCombatSessionStore()
    const local = actor(
      'local',
      'Player',
      Array.from({ length: 16 }, (_, index) => ability(index + 1)),
    )
    store.snapshot = {
      sessionId: 'session',
      sequence: 1,
      status: 'Active',
      serverTimeUtc: '2026-09-25T12:00:00Z',
      contentVersion: 'test',
      balanceVersion: 'test',
      player: local,
      enemy: actor('enemy', 'Monster'),
    }
    store.errorCode = 'combat_invalid_target'

    const wrapper = mount(BattleScreen)

    expect(wrapper.findAll('[data-ability-slot]')).toHaveLength(16)
    expect(wrapper.get('[data-combat-error-toast]').attributes('role')).toBe('alert')
    expect(wrapper.get('[data-combat-error-toast]').attributes('aria-live')).toBe('assertive')
  })

  it('keeps roster selection independent from a later aggro change', async () => {
    const store = useCombatSessionStore()
    const local = actor('local', 'Player')
    const ally = actor('ally', 'Player')
    const enemy = {
      ...actor('enemy', 'Monster'),
      currentAggroTargetActorId: 'local',
    }
    store.snapshot = {
      sessionId: 'session',
      sequence: 1,
      status: 'Active',
      serverTimeUtc: '2026-09-25T12:00:00Z',
      contentVersion: 'test',
      balanceVersion: 'test',
      player: local,
      players: [local, ally],
      enemy,
    }
    const wrapper = mount(BattleScreen)

    await wrapper.get('[data-ally-strip-actor="ally"]').trigger('click')
    store.snapshot.enemy.currentAggroTargetActorId = 'ally'
    await wrapper.vm.$nextTick()

    expect(store.selectedFriendlyTargetActorId).toBe('ally')
    expect(wrapper.findAll('[data-character-figure="ally"]')).toHaveLength(1)
    expect(wrapper.get('[data-character-figure="ally"]').attributes('data-selected')).toBe('true')
    expect(wrapper.get('[data-character-figure="ally"]').attributes('data-aggro')).toBe('true')
  })

  it('switches the server-selected hostile actor without changing friendly selection', async () => {
    const store = useCombatSessionStore()
    const local = actor('local', 'Player')
    const wolf = actor('wolf', 'Monster')
    const alpha = {
      ...actor('alpha', 'Monster'),
      name: 'Альфа-волк',
      artId: 'alpha-wolf',
    }
    store.snapshot = {
      sessionId: 'session',
      sequence: 1,
      status: 'Active',
      serverTimeUtc: '2026-09-25T12:00:00Z',
      contentVersion: 'test',
      balanceVersion: 'test',
      player: local,
      enemy: wolf,
      enemies: [wolf, alpha],
      selectedTargetActorId: wolf.actorId,
    }
    store.selectFriendlyTarget('local')
    const selectTarget = vi.spyOn(store, 'selectTarget').mockResolvedValue(undefined)
    const wrapper = mount(BattleScreen)

    await wrapper.get('[data-target-actor-id="alpha"]').trigger('click')

    expect(selectTarget).toHaveBeenCalledWith('alpha')
    expect(store.selectedFriendlyTargetActorId).toBe('local')
  })

  it('renders authoritative cast names and unblockable enemy state', () => {
    const store = useCombatSessionStore()
    const fireball = {
      ...ability(1),
      id: 'FIREBALL',
      displayName: 'Огненный шар',
    }
    const crush = {
      ...ability(2),
      id: 'CRUSH',
      displayName: 'Сокрушение',
      isUnblockable: true,
    }
    const local = actor('local', 'Player', [fireball])
    const enemy = actor('enemy', 'Monster', [crush])
    local.activeCast = {
      abilityId: 'FIREBALL',
      startedAtUtc: new Date(Date.now() - 100).toISOString(),
      resolvesAtUtc: new Date(Date.now() + 900).toISOString(),
    }
    enemy.activeCast = {
      abilityId: 'CRUSH',
      startedAtUtc: new Date(Date.now() - 100).toISOString(),
      resolvesAtUtc: new Date(Date.now() + 900).toISOString(),
    }
    store.snapshot = {
      sessionId: 'session',
      sequence: 1,
      status: 'Active',
      serverTimeUtc: new Date().toISOString(),
      contentVersion: 'test',
      balanceVersion: 'test',
      player: local,
      enemy,
    }

    const wrapper = mount(BattleScreen)

    expect(wrapper.get('[data-player-cast]').text()).toContain('Огненный шар')
    expect(wrapper.get('[data-enemy-cast]').text()).toContain('Сокрушение')
    expect(wrapper.get('[data-enemy-cast]').classes()).toContain('battle-cast--unblockable')
  })

  it('keeps consumables separate from skills and keeps server Need eligibility', () => {
    const store = useCombatSessionStore()
    const game = useGameSessionStore()
    game.snapshot = {
      character: { inventory: { items: [consumable()] } },
    } as never
    store.snapshot = {
      sessionId: 'session',
      sequence: 1,
      status: 'Active',
      serverTimeUtc: '2026-09-25T12:00:00Z',
      contentVersion: 'test',
      balanceVersion: 'test',
      player: actor('local', 'Player'),
      enemy: actor('enemy', 'Monster'),
    }
    store.lootRolls = [
      {
        lootRollId: 'roll',
        itemId: 'SWORD',
        name: 'Меч',
        rarity: 'Epic',
        quantity: 1,
        endsAtUtc: '2026-09-25T12:01:00Z',
        eligibleCharacterIds: [],
        canNeed: false,
      },
    ]

    const wrapper = mount(BattleScreen)

    expect(
      wrapper
        .get('[data-consumable-bar]')
        .find('[data-combat-consumable="HEALING_POTION"]')
        .exists(),
    ).toBe(true)
    expect(wrapper.get('[data-combat-hotbar]').find('[data-combat-consumable]').exists()).toBe(
      false,
    )
    const lootButtons = wrapper.findAll('[data-loot-rolls] .loot-roll__actions button')
    expect(lootButtons[0]!.attributes('disabled')).toBeDefined()
    expect(lootButtons[1]!.attributes('disabled')).toBeUndefined()
  })

  it('keeps an explicit exit action in active training combat', async () => {
    const store = useCombatSessionStore()
    const enemy = {
      ...actor('enemy', 'Monster'),
      definitionId: 'TRAINING_DUMMY',
    }
    store.snapshot = {
      sessionId: 'training',
      sequence: 1,
      status: 'Active',
      serverTimeUtc: '2026-09-25T12:00:00Z',
      contentVersion: 'test',
      balanceVersion: 'test',
      player: actor('local', 'Player'),
      enemy,
    }
    const leave = vi.spyOn(store, 'leave').mockResolvedValue(true)
    const wrapper = mount(BattleScreen)

    await wrapper.get('[data-leave-combat]').trigger('click')

    expect(leave).toHaveBeenCalledOnce()
    expect(wrapper.emitted('leave')).toHaveLength(1)
  })
})

function consumable(): InventoryItem {
  return {
    id: 'potion-instance',
    definitionId: 'HEALING_POTION',
    name: 'Healing Potion',
    type: 'Consumable',
    rarity: 'Common',
    requiredLevel: 1,
    quantity: 3,
    slot: null,
    equippedSlot: null,
    stats: {
      strength: 0,
      agility: 0,
      intellect: 0,
      stamina: 0,
      maxHp: 0,
      attackPower: 0,
      spellPower: 0,
      criticalChance: 0,
      criticalDamage: 0,
      accuracy: 0,
      armor: 0,
      magicResistance: 0,
      dodge: 0,
      armorPenetration: 0,
      magicPenetration: 0,
      attackSpeed: 0,
      maxResource: 0,
    },
    description: '',
    setId: null,
    weaponCategory: null,
    armorCategory: null,
    consumableActions: [
      {
        type: 'RestoreHp',
        amount: 50,
        resourceType: null,
        effectId: null,
        dispelCategory: null,
      },
    ],
    consumableCooldownCategoryId: 'POTION',
    consumableCooldownSeconds: 30,
    buyPriceGold: 1,
    sellPriceGold: 1,
    isLocked: false,
    iconId: null,
    appearanceProfileId: null,
    weaponBaseAttackIntervalSeconds: null,
    attackSpeedPercent: 0,
    dodgePercent: 0,
  }
}
