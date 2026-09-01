import { describe, expect, it } from 'vitest'

import { createRoutes } from '@/router'

describe('UI playground route boundary', () => {
  it('is absent from production routes', () => {
    expect(createRoutes(false).some((route) => route.path === '/dev/ui')).toBe(false)
    expect(createRoutes(false).some((route) => route.path === '/dev/talents')).toBe(false)
  })

  it('is available in development routes', () => {
    expect(createRoutes(true).some((route) => route.path === '/dev/ui')).toBe(true)
    expect(createRoutes(true).some((route) => route.path === '/dev/talents')).toBe(true)
  })
})
