import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr'

import { apiClient } from '@/api/apiClient'

export type LiveStateTopic = 'social' | 'party'

type Listener = () => void

const listeners: Record<LiveStateTopic, Set<Listener>> = {
  social: new Set(),
  party: new Set(),
}

let connection: HubConnection | null = null
let starting: Promise<void> | null = null

function emit(topic: LiveStateTopic): void {
  for (const listener of listeners[topic]) listener()
}

function emitSubscribedTopics(): void {
  if (listeners.social.size > 0) emit('social')
  if (listeners.party.size > 0) emit('party')
}

export function subscribeLiveState(topic: LiveStateTopic, listener: Listener): () => void {
  listeners[topic].add(listener)
  return () => listeners[topic].delete(listener)
}

export async function connectLiveState(): Promise<void> {
  if (connection?.state === HubConnectionState.Connected) return
  if (starting) return await starting

  starting = connectCore().finally(() => {
    starting = null
  })
  return await starting
}

async function connectCore(): Promise<void> {
  if (!connection) {
    connection = new HubConnectionBuilder()
      .withUrl('/hubs/state', {
        accessTokenFactory: async () => await apiClient.ensureFreshAccessToken(),
      })
      .withAutomaticReconnect([0, 1_000, 3_000, 10_000])
      .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
      .build()

    connection.on('SocialUpdated', () => emit('social'))
    connection.on('PartyUpdated', () => emit('party'))
    connection.onreconnected(() => emitSubscribedTopics())
  }

  if (connection.state === HubConnectionState.Disconnected)
    await connection.start()
}
