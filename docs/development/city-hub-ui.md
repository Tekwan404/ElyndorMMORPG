# City hub: UX and navigation contract

## Canonical five-tab navigation
The existing mobile bottom navigation stays **unchanged**:
- **Мир**: world map, locations, travel and transitions
- **Герой**: character, gear, inventory, talents, stats
- **Локация**: current authoritative location, including the illustrated CityView only when the player is in STARTER_TOWN and not travelling
- **Квесты**: journal
- **Меню**: friends, party, hotbar settings, release notes and admin options

There is no permanent City tab or Inventory tab. Travel and location navigation remain separate destinations exactly as in main.
A city is **one location kind**, not a global hub reachable from anywhere. The old WorldView remains active for regions and dungeons.
The city uses the original shell HUD and five navigation icons.

## City marker destinations
| Marker | Behavior |
| --- | --- |
| Guild | Group entry, with future guild gameplay clearly marked unavailable |
| Adventurers guild | Current AdventurerGuildBoard with quests/contracts |
| Auction | Existing AuctionView |
| Market | MerchantShop / PremiumStoreView / MailboxView |
| Teleport | Open world map, not instant teleport |
| Bank | Informational future-bank screen with real Inventory shortcut |
| Craft district | ForgeView / ProfessionView |
| Arena | Existing ArenaView and training dummy |

The Gates marker is intentionally absent: the bottom **Мир** tab already handles travel and the world map.
The city keeps access to an active world boss through a small shortcut with reserved heading space.
The `arenaInvite` query deep-link continues to open ArenaView independently of the city so already sent invites don't break.

## Supplied production assets
The supplied central-plaza painting is committed as `src/assets/world/elyndor-city-background.webp` (864px wide, ~370 KiB). It fills the space between the existing HUD and navigation using cover cropping, centered at 42% vertically.
Seven original transparent gold sign PNGs are individual `src/assets/world/city-*.webp` assets (320px wide, ~304 KiB combined). The decorative medallion is clipped from each sign; its name is readable HTML text. The obsolete small atlas is removed. Explicit medallion dimensions avoid the zero-height sprites in the previous layout.
The teleport badge is rendered separately with the existing icon kit, because no standalone teleport PNG was supplied. HUD and bottom navigation are unchanged.
The art is mobile-first: do not bake coordinates/hotspots or the interface into the JPG/WEBP itself; marker coordinates are percentages for responsive scaling.
The scene fills the available height with a 340px minimum. Longer names get wider buttons, and markers stay at least 44px in both dimensions. On smaller screens the left and right columns remain separated vertically, leaving the central fountain visible.

MerchantShop loads on an initially open mount as well as subsequent opens. CityView mounts it lazily, so watching only later prop changes would leave the first visit empty.

## Regression and release check
- `npm run type-check`, `npm run lint`, frontend unit suite
- mobile Playwright shell at 320, 360, 390, 430px with Telegram safe areas
- city markers must not overlap or be obstructed by the HUD/nav; art must not create horizontal scroll (Playwright checks every pair of button rectangles)
- mock a live boss and verify its shortcut does not overlap the heading at 320px
- verify: city entry -> merchant, auction, mailbox, forge, professions, arena, training, bank/inventory, guild contracts
- region -> map -> region, and city -> map -> city; no accidental teleport
- invite link and mid-fight reconnect still render combat instead of city
- all economic mutations remain controlled by their existing stores; do not refactor balances/commands with this pass

Do not merge based solely on unit tests: require mobile visual review with the **imported clean artwork and original supplied signs**.
