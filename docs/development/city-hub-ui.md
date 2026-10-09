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
| Gates | Open world map, not instant travel |
| Auction | Existing AuctionView |
| Market | MerchantShop / PremiumStoreView / MailboxView |
| Teleport | Open world map, not instant teleport |
| Bank | Informational future-bank screen with real Inventory shortcut |
| Craft district | ForgeView / ProfessionView |
| Arena | Existing ArenaView and training dummy |

The city keeps access to active world boss through a small banner/shortcut.
The `arenaInvite` query deep-link continues to open ArenaView independently of the city so already sent invites don't break.

## UI assets
The initial branch uses the **existing tracked** `src/assets/world/starter-town.webp` as an interim city background. The nine markers are **independent DOM buttons** with CSS ornamentation and vector glyphs; all names and click targets remain screen-reader accessible. Never flatten icons/text into the background screenshot.

Before release, optimize/import the supplied clean 9:16 city illustration into `src/assets/world/` and the ornate transparent marker assets into the city feature folder. Recheck marker percentage positions against that final art; current percentages are layout anchors only. Keep the fallback bundled background if art cannot be loaded.

## Regression and release check
- `npm run type-check`, `npm run lint`, frontend unit suite
- mobile Playwright shell at 320, 360, 390, 430px with Telegram safe areas
- city markers must not overlap or be obstructed by the HUD/nav; art must not create horizontal scroll
- verify: city entry -> merchant, auction, mailbox, forge, professions, arena, training, bank/inventory, guild contracts
- region -> map -> region, and city -> map -> city; no accidental teleport
- invite link and mid-fight reconnect still render combat instead of city
- all economic mutations remain controlled by their existing stores; do not refactor balances/commands with this pass

Do not merge based solely on unit tests: require mobile visual review with the **final clean artwork**.
