export interface ApiProblem {
  code?: string
  correlationId?: string
}

export interface ApiStatus {
  service: string
  status: string
  utcNow: string
}

export interface AdminChallenge {
  challengeId: string
  expiresAtUtc: string
}

export interface AuthenticationResponse {
  accessToken: string
  expiresAtUtc: string
  roles: string[]
}

export interface ContentAdminCurrent {
  contentVersion: string
  balanceVersion: string
  revisionId: string | null
  releaseId: string | null
  payloadSha256: string
}

export interface ContentAdminHistory {
  revisions: Array<{
    id: string
    createdAtUtc: string
    createdBy: string
    note: string | null
  }>
  releases: Array<{
    id: string
    publishedAtUtc: string
    publishedBy: string
    note: string | null
  }>
}

export class AdminApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    public readonly correlationId?: string,
  ) {
    super(code)
  }
}

let accessToken: string | null = null

export function setAdminAccessToken(token: string | null): void {
  accessToken = token
}

export async function adminRequest<T>(
  path: string,
  init: RequestInit = {},
): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`)

  const response = await fetch(path, { ...init, headers })
  if (response.ok) return await response.json() as T

  const problem = await response.json().catch(() => ({})) as ApiProblem
  throw new AdminApiError(
    response.status,
    problem.code ?? `http_${response.status}`,
    problem.correlationId,
  )
}
