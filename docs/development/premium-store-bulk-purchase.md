# Premium Store bulk purchase

`POST /api/v1/economy/store/purchase` accepts `sku`, `mutationId` and `packCount`.
Omitted `packCount` means one pack for compatibility. Integers 1–10,000 are
accepted, with an additional maximum of 1,000,000 granted items per request.
The store snapshot exposes `maxPackCount` including remaining account limits.

The server calculates quantity and price with checked arithmetic from the
current offer. Character and wallet row locks serialize inventory and currency
mutations. Capacity is checked before spending. Item grants, purchase receipt
and currency ledger commit together. Receipts persist pack count; replay with
the same identity and pack count grants nothing again, even if content changes.
A different SKU/account/pack count conflicts. Account limits count packs.

Deployment must apply `20261004172315_PremiumStoreBulkPurchase`. Historical
receipts receive `PackCount = 1`, matching their original one-pack purchases.

New spatial artifacts use the existing equip/replace flow, not additive bags:

| Item | Capacity bonus | Crystals |
| --- | ---: | ---: |
| SPATIAL_ASTRAL_RING | 100 | 2,250 |
| SPATIAL_DIMENSION_CORE | 150 | 3,250 |
| SPATIAL_ETERNITY_SEAL | 250 | 5,000 |

The product sheet reuses `UIModal` with a persistent action footer. Pack count
supports manual input, a stepper and 1/5/10/25/50/100 presets. Totals are previews;
the server remains authoritative. Existing item art is reused for the new offers.
