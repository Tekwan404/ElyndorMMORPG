import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { canAffordMoney, formatMoney, moneyParts, moneyUnits } from '@/shared/money'
import MoneyAmount from '@/ui/components/MoneyAmount.vue'

describe('single balance money denominations', () => {
  it.each([[0, '0b'], [99, '99b'], [100, '1s'], [9999, '99s 99b'],
    [10000, '1g'], [1254783, '125g 47s 83b']] as const)('formats %s bronze as %s', (amount, text) => {
    expect(formatMoney(amount)).toBe(text)
  })

  it('preserves the full server bigint without rounding', () => {
    expect(formatMoney('9223372036854775807')).toBe('922337203685477g 58s 7b')
    expect(canAffordMoney('9007199254740992', '9007199254740993')).toBe(false)
    expect(canAffordMoney('9007199254740993', '9007199254740993')).toBe(true)
  })

  it.each([-1, 1.5, NaN, Infinity, Number.MAX_SAFE_INTEGER + 1, '', '1.5', '-1', '9223372036854775808'])('rejects invalid amount %s', value => {
    expect(() => moneyUnits(value)).toThrow(RangeError)
  })

  it('recombines denominations to the unchanged original amount', () => {
    for (const amount of [0n, 99n, 100n, 9999n, 10000n, 1254783n, 9223372036854775807n]) {
      const p = moneyParts(amount)
      expect(p.gold * 10000n + p.silver * 100n + p.bronze).toBe(amount)
    }
  })

  it('renders readable denominations and updates after a wallet refresh', async () => {
    const wrapper = mount(MoneyAmount, { props: { amount: 1254783 } })
    expect(wrapper.text()).toBe('125g47s83b')
    expect(wrapper.attributes('aria-label')).toContain('Золото: 125; серебро: 47; бронза: 83')
    await wrapper.setProps({ amount: '100' })
    expect(wrapper.text()).toBe('1s')
    await wrapper.setProps({ amount: 0 })
    expect(wrapper.text()).toBe('0b')
  })
})
