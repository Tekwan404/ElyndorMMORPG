# City hub: UX and route contract (first pass)

## Player experience
The STARTER_TOWN scene is a real illustrated hub, not the legacy list of service cards.

The **World** navigation destination represents both the map and the current area:
- inside a region/dungeon, the World tab opens the map; selecting it again or using “Осмотреть локацию” opens the local scene
- the region scene has a visible “Карта мира” button
- the **City** tab appears only when the current authoritative location is city-kind; it never teleports a character who is outside the city
- quests, hero, inventory and social/settings remain available in the bottom navigation
- the main navigation never contains both “Мир” and “Локация” as peer destinations

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
