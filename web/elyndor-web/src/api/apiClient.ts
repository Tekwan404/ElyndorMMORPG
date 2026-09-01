import type { ApiProblem } from './contracts'

export class ApiRequestError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    public readonly correlationId?: string,
  ) {
    super(code)
  }
}

type Reauthenticate = () => Promise<string>

export class ApiClient {
  private accessToken: string | null = null
  private reauthenticate: Reauthenticate | null = null
  private reauthenticationPromise: Promise<string> | null = null

  constructor(private readonly fetchImplementation: typeof fetch = fetch) {}

  setAccessToken(token: string | null): void {
    this.accessToken = token
  }

  getAccessToken(): string | null {
    return this.accessToken
  }

  setReauthenticate(handler: Reauthenticate): void {
    this.reauthenticate = handler
  }

  async ensureFreshAccessToken(minValiditySeconds = 30): Promise<string> {
    if (this.accessToken && hasJwtValidity(this.accessToken, minValiditySeconds)) {
      return this.accessToken
    }

    this.accessToken = null
    return await this.refreshAccessToken()
  }

  async request<T>(path: string, init: RequestInit = {}, allowReauthentication = true): Promise<T> {
    const response = await this.send(path, init)
    if (response.status === 401 && allowReauthentication && this.reauthenticate) {
      this.accessToken = null
      await this.refreshAccessToken()
      const retry = await this.send(path, init)
      return this.read<T>(retry)
    }

    return this.read<T>(response)
  }

  private async refreshAccessToken(): Promise<string> {
    if (!this.reauthenticate) {
      throw new ApiRequestError(401, 'reauthentication_unavailable')
    }

    if (!this.reauthenticationPromise) {
      this.reauthenticationPromise = this.reauthenticate()
        .then((token) => {
          this.accessToken = token
          return token
        })
        .finally(() => {
          this.reauthenticationPromise = null
        })
    }

    return await this.reauthenticationPromise
  }

  private send(path: string, init: RequestInit): Promise<Response> {
    const headers = new Headers(init.headers)
    headers.set('Accept', 'application/json')
    if (this.accessToken) {
      headers.set('Authorization', `Bearer ${this.accessToken}`)
    }

    return this.fetchImplementation.call(globalThis, path, { ...init, headers })
  }

  private async read<T>(response: Response): Promise<T> {
    if (response.ok) {
      return (await response.json()) as T
    }

    const problem = (await response.json().catch(() => ({}))) as ApiProblem
    throw new ApiRequestError(
      response.status,
      problem.code ?? `http_${response.status}`,
      problem.correlationId,
    )
  }
}

function hasJwtValidity(token: string, minValiditySeconds: number): boolean {
  try {
    const payload = token.split('.')[1]
    if (!payload) return false

    const normalized = payload.replace(/-/g, '+').replace(/_/g, '/')
    const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, '=')
    const decoded = JSON.parse(globalThis.atob(padded)) as { exp?: number }
    if (typeof decoded.exp !== 'number') return false

    return decoded.exp - Math.floor(Date.now() / 1_000) > minValiditySeconds
  } catch {
    return false
  }
}

export const apiClient = new ApiClient()
