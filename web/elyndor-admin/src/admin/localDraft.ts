const STORAGE_KEY = 'elyndor.admin.content-draft.v1'

export interface LocalContentDraft {
  basePayloadSha256: string
  payloadJson: string
  savedAtUtc: string
}

export function saveLocalContentDraft(
  basePayloadSha256: string,
  payloadJson: string,
  storage: Storage = globalThis.localStorage,
): LocalContentDraft {
  if (!basePayloadSha256.trim()) {
    throw new Error('Local draft requires a base payload SHA.')
  }

  const draft: LocalContentDraft = {
    basePayloadSha256,
    payloadJson,
    savedAtUtc: new Date().toISOString(),
  }
  storage.setItem(STORAGE_KEY, JSON.stringify(draft))
  return draft
}

export function readLocalContentDraft(
  storage: Storage = globalThis.localStorage,
): LocalContentDraft | null {
  const raw = storage.getItem(STORAGE_KEY)
  if (!raw) return null

  try {
    const parsed = JSON.parse(raw) as Partial<LocalContentDraft>
    if (
      typeof parsed.basePayloadSha256 !== 'string'
      || !parsed.basePayloadSha256
      || typeof parsed.payloadJson !== 'string'
      || typeof parsed.savedAtUtc !== 'string'
    ) {
      return null
    }

    return {
      basePayloadSha256: parsed.basePayloadSha256,
      payloadJson: parsed.payloadJson,
      savedAtUtc: parsed.savedAtUtc,
    }
  } catch {
    return null
  }
}

export function loadLocalContentDraft(
  expectedBasePayloadSha256: string,
  storage: Storage = globalThis.localStorage,
): LocalContentDraft | null {
  const draft = readLocalContentDraft(storage)
  return draft?.basePayloadSha256 === expectedBasePayloadSha256
    ? draft
    : null
}

export function clearLocalContentDraft(
  expectedBasePayloadSha256?: string,
  storage: Storage = globalThis.localStorage,
): void {
  if (!expectedBasePayloadSha256) {
    storage.removeItem(STORAGE_KEY)
    return
  }

  const draft = readLocalContentDraft(storage)
  if (draft?.basePayloadSha256 === expectedBasePayloadSha256) {
    storage.removeItem(STORAGE_KEY)
  }
}
