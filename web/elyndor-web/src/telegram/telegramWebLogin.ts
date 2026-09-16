import { apiClient, ApiRequestError } from '@/api/apiClient'

interface TelegramWebAuthenticationConfigResponse {
  enabled: boolean
  clientId: string | null
  redirectUri: string | null
}

interface TelegramWebAuthenticationResponse {
  webCredential: string
  expiresAtUtc: string
}

interface PendingTelegramWebLogin {
  state: string
  codeVerifier: string
  startedAtMs: number
}

const pendingLoginKey = 'elyndor.telegram-web-login.pending'
const pendingLoginMaxAgeMs = 10 * 60 * 1000

export async function beginTelegramWebLogin(): Promise<void> {
  const config = await apiClient.request<TelegramWebAuthenticationConfigResponse>(
    '/api/v1/auth/telegram-web/config',
    {},
    false,
  )

  if (!config.enabled || !config.clientId || !config.redirectUri) {
    throw new ApiRequestError(503, 'telegram_web_auth_not_configured')
  }

  const state = randomBase64Url(32)
  const codeVerifier = randomBase64Url(64)
  const codeChallenge = await createCodeChallenge(codeVerifier)
  const pending: PendingTelegramWebLogin = {
    state,
    codeVerifier,
    startedAtMs: Date.now(),
  }
  window.sessionStorage.setItem(pendingLoginKey, JSON.stringify(pending))

  const authorizationUrl = new URL('https://oauth.telegram.org/auth')
  authorizationUrl.searchParams.set('client_id', config.clientId)
  authorizationUrl.searchParams.set('redirect_uri', config.redirectUri)
  authorizationUrl.searchParams.set('response_type', 'code')
  authorizationUrl.searchParams.set('scope', 'openid profile')
  authorizationUrl.searchParams.set('state', state)
  authorizationUrl.searchParams.set('code_challenge', codeChallenge)
  authorizationUrl.searchParams.set('code_challenge_method', 'S256')

  window.location.assign(authorizationUrl.toString())
}

export async function completeTelegramWebLogin(): Promise<string | null> {
  const currentUrl = new URL(window.location.href)
  const code = currentUrl.searchParams.get('code')
  const state = currentUrl.searchParams.get('state')
  const error = currentUrl.searchParams.get('error')

  if (!code && !error) return null

  const pending = readPendingLogin()
  window.sessionStorage.removeItem(pendingLoginKey)
  clearTelegramCallbackParameters(currentUrl)

  if (error) {
    throw new ApiRequestError(
      401,
      error === 'access_denied'
        ? 'telegram_web_login_cancelled'
        : 'telegram_web_login_failed',
    )
  }

  if (!code || !state || !pending) {
    throw new ApiRequestError(401, 'telegram_web_state_invalid')
  }

  if (
    pending.state !== state
    || Date.now() - pending.startedAtMs > pendingLoginMaxAgeMs
    || Date.now() < pending.startedAtMs
  ) {
    throw new ApiRequestError(401, 'telegram_web_state_invalid')
  }

  const response = await apiClient.request<TelegramWebAuthenticationResponse>(
    '/api/v1/auth/telegram-web',
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ code, codeVerifier: pending.codeVerifier }),
    },
    false,
  )

  if (!response.webCredential) {
    throw new ApiRequestError(401, 'telegram_web_auth_invalid')
  }

  return response.webCredential
}

function readPendingLogin(): PendingTelegramWebLogin | null {
  const raw = window.sessionStorage.getItem(pendingLoginKey)
  if (!raw) return null

  try {
    const parsed = JSON.parse(raw) as Partial<PendingTelegramWebLogin>
    if (
      typeof parsed.state !== 'string'
      || typeof parsed.codeVerifier !== 'string'
      || typeof parsed.startedAtMs !== 'number'
    ) {
      return null
    }

    return {
      state: parsed.state,
      codeVerifier: parsed.codeVerifier,
      startedAtMs: parsed.startedAtMs,
    }
  } catch {
    return null
  }
}

function clearTelegramCallbackParameters(url: URL): void {
  url.searchParams.delete('code')
  url.searchParams.delete('state')
  url.searchParams.delete('error')
  url.searchParams.delete('error_description')
  const nextUrl = `${url.pathname}${url.search}${url.hash}`
  window.history.replaceState(window.history.state, document.title, nextUrl)
}

async function createCodeChallenge(codeVerifier: string): Promise<string> {
  const digest = await window.crypto.subtle.digest(
    'SHA-256',
    new TextEncoder().encode(codeVerifier),
  )
  return base64Url(new Uint8Array(digest))
}

function randomBase64Url(byteLength: number): string {
  const bytes = new Uint8Array(byteLength)
  window.crypto.getRandomValues(bytes)
  return base64Url(bytes)
}

function base64Url(bytes: Uint8Array): string {
  let binary = ''
  for (const byte of bytes) binary += String.fromCharCode(byte)
  return window.btoa(binary)
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/g, '')
}
