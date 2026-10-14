# Market and crystal storefront

Implement a focused update of existing commerce: four server-defined NPC catalogs,
honor shop in the market, and a prominent premium storefront with shared crystal
balance. Preserve the existing Marcus id for supplies and historical buyback.
Do not change prices, grant currencies, or add billing. The user explicitly chose
UI-only top-up for this slice.

Component boundaries: MarketDistrict emits navigation intent; MerchantShop receives
merchantId and uses existing server mutations; HonorShopView owns honor catalog
loading; EtherStoreHeader and CrystalInfoModal present premium identity; the session
store owns the last server crystal balance, refreshed by wallet-changing responses.
Existing premium product and skin purchasing retain server validation/idempotency.

Use ink navy #0b101b, weathered gold #c9aa70, parchment #eee5d5 and restrained ether
violet #ba98e3. Georgian serif headings use the existing game typography. The crystal
vault artwork is the visual focus; other surfaces remain quiet and readable on phones.
Generate a unified five-NPC portrait atlas, a vault banner, transparent currency icon
and matching spatial artifact icons. The user rejected mixing photographic portraits
with the existing painted art; all market portraits must share the same final sheet.
Preserve existing item/skin art instead of generating inaccurate product depictions.

Verify separate backend merchant catalogs, class routing, load failure and stale NPC
requests, authoritative wallet updates, unavailable top-up, premium purchase totals,
honor loading/errors, and mobile navigation at 320/390px. Run relevant content,
commerce integration, Vue unit/lint/typecheck/build and browser checks. No migration.
