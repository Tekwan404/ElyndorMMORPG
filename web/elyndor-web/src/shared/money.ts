/** Integer bronze units. Gold/Silver/Bronze are denominations of one balance. */
export type MoneyValue = number | string | bigint

const BRONZE_PER_SILVER = 100n
const BRONZE_PER_GOLD = 10_000n
const MAX_MONEY = 9_223_372_036_854_775_807n

export function moneyUnits(value: MoneyValue): bigint {
  if (typeof value === 'number' && !Number.isSafeInteger(value)) {
    throw new RangeError('Money must be a safe integer or an exact decimal string')
  }
  if (typeof value === 'string' && !/^\d+$/.test(value)) {
    throw new RangeError('Money must contain integer bronze units')
  }
  const units = BigInt(value)
  if (units < 0n || units > MAX_MONEY) throw new RangeError('Money is outside the supported range')
  return units
}

export function moneyParts(value: MoneyValue) {
  const units = moneyUnits(value)
  return {
    gold: units / BRONZE_PER_GOLD,
    silver: (units / BRONZE_PER_SILVER) % BRONZE_PER_SILVER,
    bronze: units % BRONZE_PER_SILVER,
  }
}

export function formatMoney(value: MoneyValue): string {
  const { gold, silver, bronze } = moneyParts(value)
  return [gold > 0n ? `${gold}g` : '', silver > 0n ? `${silver}s` : '',
    bronze > 0n || (gold === 0n && silver === 0n) ? `${bronze}b` : '']
    .filter(Boolean).join(' ')
}

export function canAffordMoney(balance: MoneyValue, price: MoneyValue): boolean {
  return moneyUnits(balance) >= moneyUnits(price)
}
