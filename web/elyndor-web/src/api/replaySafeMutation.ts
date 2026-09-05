import { apiClient, ApiRequestError } from './apiClient'

const storageKey = 'elyndor.pending-game-mutation.v1'

export type ReplaySafeIdField = 'mutationId' | 'requestId'

interface PendingGameMutation {
  key: string
  path: string
  idField: ReplaySafeIdField
  intentFingerprint: string
  body: Record<string, unknown>
  createdAtUtc: string
}

interface ReplaySafeMutationOptions {
  key: string
  path: string
  idField: ReplaySafeIdField
  intent: Record<string, unknown>
}

export async function runReplaySafeGameMutation<T>(
  options: ReplaySafeMutationOptions,
): Promise<T> {
  const fingerprint = stableStringify(options.intent)
  const pending = loadPendingMutation()

  if (pending) {
    if (
      pending.key === options.key
      && pending.path === options.path
      && pending.idField === options.idField
      && pending.intentFingerprint === fingerprint
    ) {
      return await executePending<T>(pending)
    }

    await reconcilePendingGameMutation()
  }

  const body: Record<string, unknown> = {
    ...options.intent,
    [options.idField]: createMutationId(),
  }
  const created: PendingGameMutation = {
    key: options.key,
    path: options.path,
    idField: options.idField,
    intentFingerprint: fingerprint,
    body,
    createdAtUtc: new Date().toISOString(),
  }
  savePendingMutation(created)
  return await executePending<T>(created)
}

export async function reconcilePendingGameMutation(): Promise<void> {
  const pending = loadPendingMutation()
  if (!pending) return
  await executePending<unknown>(pending)
}

export function hasPendingGameMutation(): boolean {
  return loadPendingMutation() !== null
}

export function clearPendingGameMutation(): void {
  storage()?.removeItem(storageKey)
}

async function executePending<T>(pending: PendingGameMutation): Promise<T> {
  try {
    const result = await apiClient.request<T>(pending.path, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(pending.body),
    })
    clearPendingGameMutation()
    return result
  } catch (error) {
    // An API problem means the server definitely acknowledged the request. A transport,
    // parsing, or other client-side failure is uncertain, so preserve the exact request
    // and replay it with the same id on the next attempt/reload.
    if (error instanceof ApiRequestError) clearPendingGameMutation()
    throw error
  }
}

function loadPendingMutation(): PendingGameMutation | null {
  const target = storage()
  if (!target) return null

  const raw = target.getItem(storageKey)
  if (!raw) return null

  try {
    const parsed = JSON.parse(raw) as Partial<PendingGameMutation>
    if (
      typeof parsed.key !== 'string'
      || typeof parsed.path !== 'string'
      || (parsed.idField !== 'mutationId' && parsed.idField !== 'requestId')
      || typeof parsed.intentFingerprint !== 'string'
      || typeof parsed.createdAtUtc !== 'string'
      || parsed.body === null
      || typeof parsed.body !== 'object'
      || Array.isArray(parsed.body)
    ) {
      target.removeItem(storageKey)
      return null
    }

    return parsed as PendingGameMutation
  } catch {
    target.removeItem(storageKey)
    return null
  }
}

function savePendingMutation(pending: PendingGameMutation): void {
  storage()?.setItem(storageKey, JSON.stringify(pending))
}

function storage(): Storage | null {
  try {
    return typeof globalThis.sessionStorage === 'undefined'
      ? null
      : globalThis.sessionStorage
  } catch {
    return null
  }
}

function createMutationId(): string {
  if (typeof globalThis.crypto?.randomUUID === 'function') {
    return globalThis.crypto.randomUUID()
  }

  const bytes = new Uint8Array(16)
  globalThis.crypto?.getRandomValues?.(bytes)
  if (bytes.every((value) => value === 0)) {
    for (let index = 0; index < bytes.length; index += 1) {
      bytes[index] = Math.floor(Math.random() * 256)
    }
  }
  bytes[6] = ((bytes[6] ?? 0) & 0x0f) | 0x40
  bytes[8] = ((bytes[8] ?? 0) & 0x3f) | 0x80

  const hex = Array.from(bytes, (value) => value.toString(16).padStart(2, '0')).join('')
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`
}

function stableStringify(value: unknown): string {
  if (value === null || typeof value !== 'object') return JSON.stringify(value)

  if (Array.isArray(value)) {
    return `[${value.map((item) => stableStringify(item)).join(',')}]`
  }

  const record = value as Record<string, unknown>
  return `{${Object.keys(record)
    .sort()
    .map((key) => `${JSON.stringify(key)}:${stableStringify(record[key])}`)
    .join(',')}}`
}
