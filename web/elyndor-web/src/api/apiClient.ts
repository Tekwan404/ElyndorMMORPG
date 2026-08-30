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

  constructor(private readonly fetchImplementation: typeof fetch = fetch) {}

  setAccessToken(token: string | null): void {
    this.accessToken = token
  }

  setReauthenticate(handler: Reauthenticate): void {
    this.reauthenticate = handler
  }

  async request<T>(path: string, init: RequestInit = {}, allowReauthentication = true): Promise<T> {
    const response = await this.send(path, init)
    if (response.status === 401 && allowReauthentication && this.reauthenticate) {
      this.accessToken = null
      this.accessToken = await this.reauthenticate()
      const retry = await this.send(path, init)
      return this.read<T>(retry)
    }

    return this.read<T>(response)
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

export const apiClient = new ApiClient()
