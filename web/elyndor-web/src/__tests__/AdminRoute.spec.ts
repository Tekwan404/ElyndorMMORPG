import { describe, expect, it } from 'vitest'

import { createRoutes } from '@/router'

describe('admin route', () => {
  it('is part of the production route table', () => {
    const route = createRoutes(false).find((item) => item.path === '/admin')
    expect(route?.name).toBe('admin')
  })
})
