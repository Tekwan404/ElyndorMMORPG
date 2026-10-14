# Market and crystal store

The city market now owns the four ordinary merchant entry points and the honor
quartermaster. Borin sells the existing Warrior starter equipment, Liora Archer
equipment, Elara Mage equipment, and Marcus potions, materials and shared accessories.
Existing item prices and stats are unchanged. Marcus retains MARCUS_SUPPLIES so old
buyback receipts and inventory sell actions remain usable. Class equipment is removed
from that catalog and is sold only by its designated NPC.

MerchantShop receives a merchant id and uses the existing authoritative merchant API.
Catalog loading rejects stale responses after an NPC changes. Failed loading has a retry.
The honor shop retains its existing API and purchase idempotency, but is displayed by
HonorShopView in the market rather than inside ArenaView.

The global HUD and menu provide a premium store entry. Crystal balance comes from the
existing store/skin reads and purchase/promo responses. Read sequence and mutation
version guards stop older GETs from overwriting a later balance. The game server remains
authoritative; this state is presentation only.

Premium home presents real server-backed products and links to the existing skin catalog.
Unavailable future presentation entries do not fill the storefront. Top-up is deliberately
UI-only, as requested: its accessible modal explains that replenishment is unavailable.
There are no payment packs, invoices, real-money prices or client currency grants.

## Art

Generated with the built-in image_gen tool on 2026-10-14. Final assets are saved under
web/elyndor-web/src/assets/ and encoded as WebP for the client:

- npc/market-merchants-atlas.webp: one cohesive 3-column, 2-row portrait sheet. Row-major
  order is Borin, Liora, Elara, Marcus, quartermaster, unused shield emblem. MerchantPortrait
  applies the same crop everywhere. The earlier separate photographic portrait variants
  were discarded after the user requested a single style.
- store/crystal-vault.webp: banner with dark text space on the left and violet crystal
  reliquary on the right.
- store/ether-crystal.webp: transparent premium-currency emblem.
- store/spatial-artifacts-atlas.webp: 3 by 3 sheet, row-major cracked ring, minor ring,
  expanded ring, seal, bottomless ring, pocket shard, void seal, unused coin, empty.
  ItemIcon uses this art when a standalone spatial asset is absent; ordinary icons and
  their error fallbacks are preserved.

### Final prompt set

Merchant atlas: Create one perfectly regular three-column by two-row square atlas of
six equally sized vertical 2:3 panels, no gaps, frames or text. All five people must
share the same illustrator, brushwork, scale, warm-left lighting, dark umber textured
background and chest-up crop. Classic hand-painted fantasy RPG character concept art,
stylized believable anatomy, painterly skin and weathered colors; no photographs,
cinematic stills, 3D rendering, anime or glossy skin. Row-major: rugged older male
armorer Borin with graying beard, leather apron and steel pauldron; auburn-braided
ranger Liora with green cloak, leather and bow; mature mage Elara with silver-streaked
dark hair, navy/gold robes and spellbook; wavy-haired, dark-bearded supplies merchant
Marcus with charcoal-blue scarf, brown leather and potion; scarred gray-haired arena
quartermaster in red gambeson and worn steel; matching dark panel with brass shield.
All heads and eyes aligned, shoulders fill the panels.

Crystal vault: Wide landscape illustration for an old browser dark fantasy MMORPG.
Rich painterly fantasy, medieval merchant vault, faceted violet and icy cyan ether
crystals in an ornate antique brass reliquary on the right, restrained magical glow,
worn stone, embroidered velvet, candlelight, deep ink navy and aged gold. Dark
uncluttered negative space on the left for HTML text. No typography, characters, logos
or watermark.

Currency: Single centered premium currency icon, three faceted ether shards with a
tall center, violet cores, icy cyan highlights and a subtle golden base. Hand-painted
fantasy inventory art, crisp silhouette, transparent background, generous padding.
No frame, text, letters or background.

Spatial atlas: Perfectly regular 3 by 3 square-cell sprite sheet with dark near-black
background, no gaps, borders or text. Objects centered with 15 percent padding,
consistent warm upper-left lighting, painted weathered brass, violet ether and cyan
accents. Classic browser RPG illustration, no photographs or 3D renders. Row-major:
cracked ring; delicate minor ring; ornate expanded ring with violet gemstone; round
engraved gold seal; dark gold bottomless ring with swirling void; brass-bound violet
shard pendant; obsidian/gold void seal; worn brass coin; empty dark cell.

## Verification

Use merchant catalog validation and PostgreSQL commerce tests, Vue tests for routing,
wallet races and spatial art, and browser checks of all NPCs, honor shop, crystal info,
purchase totals and HUD entry at 320 and 390 pixels. Browser layout checks use mocked
API data; integration tests exercise real PostgreSQL independently.
