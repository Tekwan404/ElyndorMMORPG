import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const realtimeMock = vi.hoisted(() => ({
  reconnecting: null as ((error?: Error) => void) | null,
  reconnected: null as (() => void) | null,
  closed: null as ((error?: Error) => void) | null,
  handlers: new Map<string, (payload: unknown) => void>(),
  invoke: vi.fn<(method: string, ...args: unknown[]) => Promise<unknown>>(),
}))

const partyRefresh = vi.hoisted(() => vi.fn<() => Promise<void>>())
const dungeonRefresh = vi.hoisted(() => vi.fn<() => Promise<void>>())

vi.mock('@/game/party/partyStore', () => ({
  usePartyStore: () => ({ refresh: partyRefresh }),
}))

vi.mock('@/game/party/dungeonStore', () => ({
  useDungeonStore: () => ({ refresh: dungeonRefresh }),
}))

vi.mock('@microsoft/signalr', () => ({
  HubConnectionState: { Disconnected: 'Disconnected', Connected: 'Connected' },
  LogLevel: { Warning: 3, Error: 4 },
  HubConnectionBuilder: class {
    withUrl() { return this }
    withAutomaticReconnect() { return this }
    configureLogging() { return this }
    build() {
      const connection = {
        state: 'Disconnected',
        on: vi.fn<(event: string, callback: (payload: unknown) => void) => void>((event, callback) => {
          realtimeMock.handlers.set(event, callback)
        }),
        onreconnecting: vi.fn<(callback: (error?: Error) => void) => void>((callback) => {
          realtimeMock.reconnecting = callback
        }),
        onreconnected: vi.fn<(callback: () => void) => void>((callback) => {
          realtimeMock.reconnected = callback
        }),
        onclose: vi.fn<(callback: (error?: Error) => void) => void>((callback) => {
          realtimeMock.closed = callback
        }),
        start: vi.fn<() => Promise<void>>(async () => {
          connection.state = 'Connected'
        }),
        invoke: realtimeMock.invoke,
      }
      return connection
    }
  },
}))

import { apiClient } from '@/api/apiClient'
import { useCombatSessionStore } from '@/stores/combatSession'
import { TRAINING_DUMMY_ID } from '@/game/combat/trainingDummy'

function recoveryUpdate(sequence = 1) {
  return {
    succeeded: true, errorCode: null, events: [], reward: null,
    snapshot: {
      sessionId: 'recovery-session', status: 'Active', sequence,
      serverTimeUtc: '2026-10-05T10:00:00Z', contentVersion: '1', balanceVersion: '1',
      player: { actorId: 'player', hp: 100, abilities: [], cooldowns: {}, autoAttackEnabled: false },
      enemy: { actorId: 'enemy', definitionId: 'WOLF' },
    },
  }
}

describe('combat realtime recovery', () => {
  afterEach(() => {
    vi.clearAllTimers()
    vi.useRealTimers()
  })
  beforeEach(() => {
    setActivePinia(createPinia())
    realtimeMock.reconnecting = null
    realtimeMock.reconnected = null
    realtimeMock.closed = null
    realtimeMock.handlers.clear()
    realtimeMock.invoke.mockReset()
    partyRefresh.mockReset().mockResolvedValue(undefined)
    dungeonRefresh.mockReset().mockResolvedValue(undefined)
    vi.restoreAllMocks()
    vi.spyOn(apiClient, 'ensureFreshAccessToken').mockResolvedValue('fresh-token')
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
  })

  it('moves through reconnecting and syncing before restoring authoritative state', async () => {
    realtimeMock.invoke.mockImplementation(async (method) => {
      if (method === 'ResumeCombatFromSequence') {
        return {
          succeeded: false,
          errorCode: 'combat_not_found',
          snapshot: null,
          events: [],
          reward: null,
        }
      }
      if (method === 'Ping') return 'pong'
      return null
    })

    const store = useCombatSessionStore()
    await store.connect()

    realtimeMock.reconnecting?.(new Error('temporary network loss'))
    expect(store.connectionState).toBe('reconnecting')
    expect(store.reconnectCount).toBe(1)

    realtimeMock.reconnected?.()
    expect(store.connectionState).toBe('syncing')

    await vi.waitFor(() => {
      expect(store.connectionState).toBe('connected')
    })

    expect(store.lastResyncedAtUtc).not.toBeNull()
    expect(realtimeMock.invoke).toHaveBeenCalledWith('ResumeCombatFromSequence', 0)
    expect(partyRefresh).toHaveBeenCalledTimes(1)
    expect(dungeonRefresh).toHaveBeenCalledTimes(1)
    expect(store.diagnostic).toBeNull()
  })

  it('ignores an older response from the same combat session including future events and reward', async () => {
    const store = useCombatSessionStore()
    await store.connect()

    const sessionId = '00000000-0000-0000-0000-000000000501'
    const playerId = '00000000-0000-0000-0000-000000000502'
    const enemyId = '00000000-0000-0000-0000-000000000503'
    const currentSnapshot = {
      sessionId,
      sequence: 10,
      status: 'Active',
      serverTimeUtc: '2026-09-12T10:00:10Z',
      contentVersion: '1',
      balanceVersion: '1',
      player: { actorId: playerId, abilities: [], cooldowns: {} },
      enemy: { actorId: enemyId, definitionId: 'BOSS' },
    }

    const handler = realtimeMock.handlers.get('CombatUpdated')
    expect(handler).toBeDefined()

    handler?.({
      succeeded: true,
      errorCode: null,
      snapshot: currentSnapshot,
      events: [],
      reward: null,
    })
    expect(store.snapshot?.sequence).toBe(10)

    handler?.({
      succeeded: true,
      errorCode: null,
      snapshot: {
        ...currentSnapshot,
        sequence: 9,
        serverTimeUtc: '2026-09-12T10:00:09Z',
      },
      events: [{
        sequence: 99,
        type: 'DamageDealt',
        actorId: playerId,
        sourceActorId: playerId,
        targetActorId: enemyId,
        definitionId: null,
        amount: 999,
        amountBeforeShields: 999,
        serverTimeUtc: '2026-09-12T10:00:09Z',
      }],
      reward: {
        xpEarned: 999,
        goldEarned: 999,
        leveledUp: false,
        previousLevel: 1,
        currentLevel: 1,
        items: [],
      },
    })

    expect(store.snapshot?.sequence).toBe(10)
    expect(store.events).toEqual([])
    expect(store.reward).toBeNull()
  })

  it('requests and applies the missing event tail when a sequence gap is detected', async () => {
    const store = useCombatSessionStore()
    await store.connect()

    const sessionId = '00000000-0000-0000-0000-000000000511'
    const playerId = '00000000-0000-0000-0000-000000000512'
    const enemyId = '00000000-0000-0000-0000-000000000513'
    const currentSnapshot = {
      sessionId,
      sequence: 3,
      status: 'Active',
      serverTimeUtc: '2026-09-12T10:00:03Z',
      contentVersion: '1',
      balanceVersion: '1',
      player: { actorId: playerId, abilities: [], cooldowns: {} },
      enemy: { actorId: enemyId, definitionId: 'BOSS' },
    }
    const handler = realtimeMock.handlers.get('CombatUpdated')
    expect(handler).toBeDefined()

    const event = (sequence: number) => ({
      sequence,
      type: 'DamageDealt',
      actorId: playerId,
      sourceActorId: playerId,
      targetActorId: enemyId,
      definitionId: 'AUTO_ATTACK',
      amount: sequence,
      amountBeforeShields: sequence,
      serverTimeUtc: `2026-09-12T10:00:0${sequence}Z`,
    })
    realtimeMock.invoke.mockImplementation(async (method, lastSeenSequence) => {
      if (method !== 'ResumeCombatFromSequence') return null
      expect(lastSeenSequence).toBe(1)
      return {
        succeeded: true,
        errorCode: null,
        snapshot: currentSnapshot,
        events: [event(2), event(3)],
        reward: null,
        fullResyncRequired: false,
      }
    })

    handler?.({
      succeeded: true,
      errorCode: null,
      snapshot: { ...currentSnapshot, sequence: 1 },
      events: [event(1)],
      reward: null,
    })

    handler?.({
      succeeded: true,
      errorCode: null,
      snapshot: currentSnapshot,
      events: [event(3)],
      reward: null,
    })

    await vi.waitFor(() => {
      expect(store.events.map((item) => item.sequence)).toEqual([1, 2, 3])
    })
    expect(store.snapshot?.sequence).toBe(3)
    expect(realtimeMock.invoke).toHaveBeenCalledWith('ResumeCombatFromSequence', 1)
  })

  it('bounds a hanging resume and ignores its late authoritative result', async () => {
    vi.useFakeTimers()
    let finish!: (value: unknown) => void
    realtimeMock.invoke.mockImplementation(() => new Promise(resolve => { finish = resolve }))
    const store = useCombatSessionStore()
    await store.connect()
    realtimeMock.handlers.get('CombatUpdated')?.(recoveryUpdate())
    const result = store.resume()
    expect(store.connectionState).toBe('syncing')
    await vi.advanceTimersByTimeAsync(30_000)
    expect(store.connectionState).toBe('disconnected')
    expect(await result).toBe(false)
    expect(store.errorCode).toBe('combat_resume_timeout')
    finish(recoveryUpdate(99))
    await Promise.resolve()
    expect(store.snapshot?.sequence).toBe(1)
    expect(store.lastResyncedAtUtc).toBeNull()
  })

  it('keeps failed automatic resume blocked and available for explicit recovery', async () => {
    realtimeMock.invoke.mockRejectedValue(new Error('resume failed'))
    const store = useCombatSessionStore()
    await store.connect()
    realtimeMock.handlers.get('CombatUpdated')?.(recoveryUpdate())
    realtimeMock.reconnected?.()
    await vi.waitFor(() => expect(store.connectionState).toBe('disconnected'))
    expect(store.lastResyncedAtUtc).toBeNull()
    const calls = realtimeMock.invoke.mock.calls.length
    await store.toggleAutoAttack()
    expect(realtimeMock.invoke.mock.calls).toHaveLength(calls)
    realtimeMock.invoke.mockResolvedValue({ succeeded: false, errorCode: 'combat_not_found' })
    expect(await store.resume()).toBe(true)
    expect(store.connectionState).toBe('connected')
  })

  it('finishes combat recovery without waiting for loot, telemetry or party refresh', async () => {
    vi.useFakeTimers()
    realtimeMock.invoke.mockImplementation(method => method === 'ResumeCombatFromSequence'
      ? Promise.resolve(recoveryUpdate()) : new Promise(() => {}))
    partyRefresh.mockImplementation(() => new Promise(() => {}))
    dungeonRefresh.mockImplementation(() => new Promise(() => {}))
    const store = useCombatSessionStore()
    await store.connect()
    realtimeMock.reconnected?.()
    await vi.advanceTimersByTimeAsync(0)
    expect(store.connectionState).toBe('connected')
    expect(store.lastResyncedAtUtc).not.toBeNull()
    await vi.advanceTimersByTimeAsync(30_000)
    expect(store.latencyMs).toBeNull()
    expect(store.threat).toBeNull()
  })

  it('shares gap recovery with manual resume and blocks commands until the tail is applied', async () => {
    vi.useFakeTimers()
    let finish!: (value: unknown) => void
    realtimeMock.invoke.mockImplementation(() => new Promise(resolve => { finish = resolve }))
    const store = useCombatSessionStore()
    await store.connect()
    const event = (sequence: number) => ({ sequence, type: 'DamageDealt', amount: 1 })
    const handler = realtimeMock.handlers.get('CombatUpdated')!
    handler({ ...recoveryUpdate(1), events: [event(1)] })
    handler({ ...recoveryUpdate(3), events: [event(3)] })
    expect(store.connectionState).toBe('syncing')
    const manual = store.resume()
    await store.selectTarget('another-enemy')
    handler({ ...recoveryUpdate(4), events: [event(4)] })
    expect(realtimeMock.invoke.mock.calls).toEqual([['ResumeCombatFromSequence', 1]])
    finish({ ...recoveryUpdate(4), events: [event(2), event(3), event(4)] })
    expect(await manual).toBe(true)
    expect(store.events.map(event => event.sequence)).toEqual([1, 2, 3, 4])
    expect(store.connectionState).toBe('connected')
  })

  it('does not declare recovery complete if the returned tail still has a gap', async () => {
    realtimeMock.invoke.mockResolvedValue({
      ...recoveryUpdate(3), events: [{ sequence: 3, type: 'DamageDealt', amount: 1 }],
    })
    const store = useCombatSessionStore()
    await store.connect()
    expect(await store.resume()).toBe(false)
    expect(store.connectionState).toBe('disconnected')
    expect(store.lastResyncedAtUtc).toBeNull()
    expect(realtimeMock.invoke.mock.calls).toEqual([['ResumeCombatFromSequence', 0]])
  })

  it('recovers a silent active snapshot even when the transport remains connected', async () => {
    vi.useFakeTimers()
    realtimeMock.invoke.mockImplementation(method => method === 'ResumeCombatFromSequence'
      ? Promise.resolve(recoveryUpdate(2)) : Promise.resolve([]))
    const store = useCombatSessionStore()
    await store.connect()
    realtimeMock.handlers.get('CombatUpdated')?.(recoveryUpdate())
    await vi.advanceTimersByTimeAsync(15_000)
    expect(store.snapshot?.sequence).toBe(2)
    expect(realtimeMock.invoke).toHaveBeenCalledWith('ResumeCombatFromSequence', 0)
    expect(store.lastResyncedAtUtc).not.toBeNull()
  })

  it('invalidates an in-flight command result when recovery starts', async () => {
    let finish!: (value: unknown) => void
    realtimeMock.invoke.mockImplementation(method => method === 'StartAutoAttack'
      ? new Promise(resolve => { finish = resolve })
      : Promise.resolve(method === 'ResumeCombatFromSequence' ? recoveryUpdate(2) : []))
    const store = useCombatSessionStore()
    await store.connect()
    realtimeMock.handlers.get('CombatUpdated')?.(recoveryUpdate())
    const command = store.toggleAutoAttack()
    await vi.waitFor(() => expect(realtimeMock.invoke).toHaveBeenCalledWith(
      'StartAutoAttack', 'recovery-session', expect.any(String),
    ))
    expect(await store.resume()).toBe(true)
    finish(recoveryUpdate(99))
    await command
    expect(store.snapshot?.sequence).toBe(2)
    expect(store.autoAttackPending).toBe(false)
    expect(store.connectionState).toBe('connected')
  })

  it('does not let a superseded command timeout restart successful recovery', async () => {
    vi.useFakeTimers()
    realtimeMock.invoke.mockImplementation(method => method === 'StartAutoAttack'
      ? new Promise(() => {})
      : Promise.resolve(method === 'ResumeCombatFromSequence' ? recoveryUpdate(2) : []))
    const store = useCombatSessionStore()
    await store.connect()
    realtimeMock.handlers.get('CombatUpdated')?.(recoveryUpdate())
    const command = store.toggleAutoAttack()
    await vi.advanceTimersByTimeAsync(0)
    expect(await store.resume()).toBe(true)
    for (let elapsed = 0; elapsed < 30_000; elapsed += 10_000) {
      realtimeMock.handlers.get('CombatUpdated')?.(recoveryUpdate(2))
      await vi.advanceTimersByTimeAsync(10_000)
    }
    await command
    expect(store.autoAttackPending).toBe(false)
    expect(store.connectionState).toBe('connected')
    expect(store.errorCode).toBeNull()
    expect(realtimeMock.invoke.mock.calls.filter(([method]) => method === 'ResumeCombatFromSequence')).toHaveLength(1)
  })

  it('retains the current event history when an older full resync response arrives', async () => {
    const store = useCombatSessionStore()
    await store.connect()
    realtimeMock.handlers.get('CombatUpdated')?.({
      ...recoveryUpdate(), events: [{ sequence: 1, type: 'DamageDealt', amount: 1 }],
    })
    realtimeMock.invoke.mockResolvedValue({ ...recoveryUpdate(0), fullResyncRequired: true })
    expect(await store.resume()).toBe(false)
    expect(store.snapshot?.sequence).toBe(1)
    expect(store.events.map(event => event.sequence)).toEqual([1])
    expect(store.connectionState).toBe('disconnected')
  })

  it.each(['StartCombat', 'StartDungeonEncounter', 'StartTraining', 'ResetTraining', 'AttachCombat'])(
    'accepts %s completion after its new session was already delivered by push', async method => {
      let finish!: (value: unknown) => void
      realtimeMock.invoke.mockImplementation(() => new Promise(resolve => { finish = resolve }))
      const store = useCombatSessionStore()
      await store.connect()
      if (method === 'ResetTraining') {
        const initial = recoveryUpdate()
        initial.snapshot.enemy.definitionId = TRAINING_DUMMY_ID
        realtimeMock.handlers.get('CombatUpdated')?.(initial)
      }
      const operations: Record<string, () => Promise<boolean>> = {
        StartCombat: () => store.startCombat({ encounterId: 'encounter', monsterId: 'WOLF' } as never),
        StartDungeonEncounter: () => store.startDungeonEncounter('run'),
        StartTraining: () => store.startTraining(),
        ResetTraining: () => store.resetTraining(),
        AttachCombat: () => store.attachCombat('attached-session'),
      }
      const result = operations[method]!()
      await vi.waitFor(() => expect(realtimeMock.invoke).toHaveBeenCalled())
      const update = recoveryUpdate()
      update.snapshot.sessionId = 'new-session'
      realtimeMock.handlers.get('CombatUpdated')?.(update)
      finish(update)
      expect(await result).toBe(true)
      expect(store.snapshot?.sessionId).toBe('new-session')
      expect(store.lifecyclePending).toBe(false)
      expect(store.errorCode).toBeNull()
    },
  )

  it('accepts empty-to-existing resume completion after the session arrived by push', async () => {
    let finish!: (value: unknown) => void
    realtimeMock.invoke.mockImplementation(method => method === 'ResumeCombatFromSequence'
      ? new Promise(resolve => { finish = resolve }) : Promise.resolve([]))
    const store = useCombatSessionStore()
    await store.connect()
    const result = store.resume()
    realtimeMock.handlers.get('CombatUpdated')?.(recoveryUpdate())
    finish(recoveryUpdate())
    expect(await result).toBe(true)
    expect(store.snapshot?.sessionId).toBe('recovery-session')
    expect(store.connectionState).toBe('connected')
    expect(store.lastResyncedAtUtc).not.toBeNull()
  })

  it('starts a fresh event sequence after resetting a fully resynchronized training session', async () => {
    vi.useFakeTimers()
    let finish!: (value: unknown) => void
    realtimeMock.invoke.mockImplementation(() => new Promise(resolve => { finish = resolve }))
    const store = useCombatSessionStore()
    await store.connect()
    const initial = recoveryUpdate(10)
    initial.snapshot.enemy.definitionId = TRAINING_DUMMY_ID
    const handler = realtimeMock.handlers.get('CombatUpdated')!
    handler({ ...initial, fullResyncRequired: true })
    const reset = store.resetTraining()
    await vi.advanceTimersByTimeAsync(0)
    expect(realtimeMock.invoke).toHaveBeenCalledWith('ResetTraining')
    const update = recoveryUpdate()
    update.snapshot.sessionId = 'reset-session'
    update.snapshot.enemy.definitionId = TRAINING_DUMMY_ID
    const response = { ...update, events: [{ sequence: 1, type: 'CombatStarted' }] }
    handler(response)
    expect(store.snapshot?.sessionId).toBe('reset-session')
    finish(response)
    expect(await reset).toBe(true)
    expect(store.snapshot?.sessionId).toBe('reset-session')
    expect(store.events.map(event => event.sequence)).toEqual([1])
    expect(realtimeMock.invoke.mock.calls).toEqual([['ResetTraining']])
  })

  it('retains failed terminal state for manual recovery without completing rewards or polling forever', async () => {
    vi.useFakeTimers()
    const terminal = recoveryUpdate(2)
    terminal.snapshot.status = 'Victory'
    const terminalReward = { xpEarned: 25, goldEarned: 10, leveledUp: false, previousLevel: 1, currentLevel: 1, items: [] }
    const failed = { ...terminal, succeeded: false, errorCode: 'combat_recovery_required', reward: terminalReward }
    realtimeMock.invoke.mockResolvedValue(failed)
    const store = useCombatSessionStore()
    await store.connect()
    realtimeMock.handlers.get('CombatUpdated')?.(recoveryUpdate())
    const command = store.toggleAutoAttack()
    await vi.advanceTimersByTimeAsync(0)
    await command
    expect(store.snapshot?.status).toBe('Victory')
    expect(store.recoveryRequired).toBe(true)
    expect(store.isActive).toBe(true)
    expect(store.reward).toBeNull()
    expect(store.connectionState).toBe('disconnected')
    expect(store.errorCode).toBe('combat_recovery_required')
    realtimeMock.handlers.get('CombatUpdated')?.(recoveryUpdate(1))
    expect(store.recoveryRequired).toBe(true)
    expect(store.errorCode).toBe('combat_recovery_required')
    expect(realtimeMock.invoke.mock.calls.filter(([method]) => method === 'ResumeCombatFromSequence')).toHaveLength(0)
    expect(await store.resume()).toBe(false)
    await vi.advanceTimersByTimeAsync(300_000)
    expect(realtimeMock.invoke.mock.calls.filter(([method]) => method === 'ResumeCombatFromSequence')).toHaveLength(1)
    expect(store.lastResyncedAtUtc).toBeNull()
    realtimeMock.invoke.mockImplementation(method => Promise.resolve(method === 'ResumeCombatFromSequence'
      ? { ...terminal, reward: terminalReward } : []))
    expect(await store.resume()).toBe(true)
    expect(store.recoveryRequired).toBe(false)
    expect(store.isActive).toBe(false)
    expect(store.connectionState).toBe('connected')
    expect(store.reward?.xpEarned).toBe(25)
  })

  it.each(['domain rejection', 'transport error'])('resumes authoritative state after an ordinary command %s', async failure => {
    vi.useFakeTimers()
    realtimeMock.invoke.mockImplementation(method => {
      if (method === 'StartAutoAttack') return failure === 'domain rejection'
        ? Promise.resolve({ succeeded: false, errorCode: 'combat_invalid_state' })
        : Promise.reject(new Error('command failed'))
      return Promise.resolve(method === 'ResumeCombatFromSequence' ? recoveryUpdate(2) : [])
    })
    const store = useCombatSessionStore()
    await store.connect()
    realtimeMock.handlers.get('CombatUpdated')?.(recoveryUpdate())
    await store.toggleAutoAttack()
    await vi.advanceTimersByTimeAsync(0)
    expect(store.snapshot?.sequence).toBe(2)
    expect(store.connectionState).toBe('connected')
    expect(realtimeMock.invoke.mock.calls.filter(([method]) => method === 'StartAutoAttack')).toHaveLength(1)
    expect(realtimeMock.invoke.mock.calls.filter(([method]) => method === 'ResumeCombatFromSequence')).toEqual([
      ['ResumeCombatFromSequence', 0],
    ])
  })

  it('does not mark a resume without an authoritative snapshot as synchronized', async () => {
    realtimeMock.invoke.mockResolvedValue({ succeeded: true, snapshot: null, events: [], reward: null })
    const store = useCombatSessionStore()
    await store.connect()
    expect(await store.resume()).toBe(false)
    expect(store.connectionState).toBe('disconnected')
    expect(store.lastResyncedAtUtc).toBeNull()
  })

  it('resumes authoritative state after a loot choice rejection without replaying the choice', async () => {
    vi.useFakeTimers()
    realtimeMock.invoke.mockImplementation(method => Promise.resolve(method === 'ChooseLootRoll'
      ? { succeeded: false, errorCode: 'combat_loot_choice_rejected' }
      : method === 'ResumeCombatFromSequence' ? recoveryUpdate(2) : []))
    const store = useCombatSessionStore()
    await store.connect()
    realtimeMock.handlers.get('CombatUpdated')?.(recoveryUpdate())
    expect(await store.chooseLootRoll('roll', 'Need')).toBe(false)
    await vi.advanceTimersByTimeAsync(0)
    expect(store.snapshot?.sequence).toBe(2)
    expect(store.connectionState).toBe('connected')
    expect(realtimeMock.invoke.mock.calls.filter(([method]) => method === 'ChooseLootRoll')).toEqual([
      ['ChooseLootRoll', 'roll', 'Need'],
    ])
    expect(realtimeMock.invoke).toHaveBeenCalledWith('ResumeCombatFromSequence', 0)
  })
})
