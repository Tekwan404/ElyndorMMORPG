export async function isApiHealthy(signal?: AbortSignal): Promise<boolean> {
  try {
    const response = await fetch('/api/v1/status', {
      headers: { Accept: 'application/json' },
      signal,
    })

    return response.ok
  } catch {
    return false
  }
}
