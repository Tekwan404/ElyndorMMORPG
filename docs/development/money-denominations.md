# Money denominations

Money has one authoritative non-negative PostgreSQL BIGINT balance, measured in bronze.
100 bronze = 1 silver; 100 silver = 1 gold. Example: 1254783 units = 125g 47s 83b.

The existing `Character.Gold` column and `gold`, `*Gold` API/content fields retain their names for
compatibility. Their unit is bronze, including prices, rewards and service fees. No second balance
is introduced. Existing numeric balances, prices and rewards remain unchanged, preserving purchasing power.
No migration or multiplication of production balances is needed.

The browser uses `shared/money.ts` for exact denomination and affordability calculations and
`MoneyAmount.vue` for coin presentation. Wallet response values above JavaScript's safe integer limit
are serialized as decimal strings; smaller values remain JSON numbers for compatibility.
The client accepts both and uses BigInt internally. Do not parse large wallet strings as Number.

Crystal remains a separate currency and is not converted into these denominations.
Guild Treasury and the separate Merchant integrity pass are outside this change.
