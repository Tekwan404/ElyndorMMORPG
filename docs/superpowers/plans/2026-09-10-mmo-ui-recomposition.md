# MMO UI Recomposition Implementation Plan

- [x] Inspect the current shell, world, merchant, guild, menu, combat screens and local visual references.
- [x] Create an MMO visual foundation through shared tokens, shell framing, modal treatment, and mobile input behavior.
- [x] Recompose the menu around the player profile, ELY ID, and implemented social systems.
- [x] Move starter-town services into a compact NPC hub and add guild NPC placeholders from existing assets.
- [x] Add merchant search/type filtering without changing authoritative buy/sell actions.
- [x] Recompose combat styling around the battlefield and sticky action dock without changing combat state or commands.
- [x] Add focused merchant unit coverage and extend the mobile browser flow for menu, city, merchant, and guild surfaces.
- [ ] Deploy/merge after final review; this branch intentionally does not modify the user's existing uncommitted documents.

## Verification gate

- `npm run type-check`
- `npm run test:unit`
- `npm run lint`
- `npm run build`
- `npm run test:e2e -- --project=mobile-chromium`
