# Elyndor MMO UI Recomposition Design

**Status:** In implementation
**Scope:** Vue shell and existing player-facing world, city, merchant, guild, menu, and combat surfaces.

## Problem

The current screens contain the correct gameplay flows, but the hierarchy reads as a collection of repeated dashboard cards. City services are buried in a long location page, the merchant has no way to narrow a large storefront, the menu exposes only tabs, and combat gives equal visual weight to the battlefield, log, and controls.

## Direction

Use a dark-fantasy mobile game language: ink and steel surfaces, restrained worn-gold frames, serif display type, atmospheric location art, and one dominant visual scene per screen. Borders and rounded containers are reserved for meaningful groups; repeated information uses rows, chips, or icon tiles.

The existing server state, API contracts, navigation routes, and combat actions remain unchanged. This slice is presentation and information architecture only, except for client-side merchant filtering.

## Screen decisions

- **Global shell:** keep five stable bottom destinations and the compact player HUD. Make location, level, health/resource, gold, connection state, and ELY ID legible without adding another navigation layer.
- **World/city:** keep the large location scene, then present starter-town services as an immediately available two-column NPC hub. Training, Marcus, guild registrar, and innkeeper have explicit actions or status.
- **Merchant:** add text search and type chips for all, equipment, consumables, and materials. Filtering only changes the visible client-side list; purchases still use the existing server action.
- **Guild:** show registrar, hunt master, quartermaster, and cartographer portraits as placeholders. Only the existing contract board is interactive; the other NPCs communicate the planned guild layout without pretending to implement new systems.
- **Menu:** replace the tab strip with a compact profile/ELY ID header and icon tiles for the currently implemented Friends and Party flows. Unsupported systems are not rendered as dead buttons.
- **Combat:** make the battlefield the visual anchor, keep the ability dock close to it and sticky while scrolling, and reduce the combat log to a secondary expandable strip.
- **Input:** preserve vertical scrolling and use `touch-action: manipulation` on interactive controls so double-tap zoom does not interfere with taps; do not disable pinch zoom globally.

## Boundaries

No new backend systems, gameplay rules, inventory mutations, NPC behavior, or unsupported menu routes are introduced. New UI checks assert only existing flows and the new merchant filter/NPC presentation.
