import type { Pinia } from 'pinia'
import { watch } from 'vue'

import { usePartyStore } from '@/game/party/partyStore'
import { useGameSessionStore } from '@/stores/gameSession'

const PARTY_INVITE_QUERY_PARAMETER = 'partyInvite'
const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i

export function takePartyInviteId(location: Location, history: History): string | null {
  const url = new URL(location.href)
  const inviteId = url.searchParams.get(PARTY_INVITE_QUERY_PARAMETER)
  if (!inviteId || !GUID_PATTERN.test(inviteId)) return null

  url.searchParams.delete(PARTY_INVITE_QUERY_PARAMETER)
  history.replaceState(history.state, '', `${url.pathname}${url.search}${url.hash}`)
  return inviteId
}

export function installPartyInviteDeepLink(pinia: Pinia): void {
  const inviteId = takePartyInviteId(window.location, window.history)
  if (!inviteId) return

  const session = useGameSessionStore(pinia)
  const party = usePartyStore(pinia)
  let handled = false

  const stop = watch(
    () => session.state,
    (state) => {
      if (handled || state !== 'world') return

      handled = true
      stop()
      void party.acceptInvite(inviteId)
    },
    { immediate: true },
  )
}
