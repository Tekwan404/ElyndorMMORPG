# Undead homeland

Implement a copy of the existing BORDERLANDS region for UNDEAD characters, named
Земли мёртвых, with Тихоград as its city. Preserve encounter balance, rewards,
level ranges and services. OUTER_REACHES has separate faction locations with the
same presentation; only DEAD_REACHES is shared and requires level 60. Implementing
open-world PvP combat is outside this content and faction-access slice.

1. Copy composed locations and their local dungeon, quest, contract, merchant and
   gathering definitions into canonical content categories with separate IDs.
2. Enforce RequiredRaceId at travel admission and filter the location catalog and
   outgoing transitions. Start and respawn in the character's own city. Repair
   legacy opposite-faction positions on bootstrap and cancel invalid travel.
3. Reuse city systems with faction-specific presentation, background and NPCs.
4. Verify content parity, server access restrictions, creation/recovery and UI.

Travel retains its existing character row lock, idempotency receipts and transaction.
No schema migration is required: race and location are already persisted.
Do not interrupt active combat/AFK to repair an old location; reject new restricted
actions and repair on the next safe bootstrap. Do not change captured session state.

## User-supplied artwork, 2026-10-17

Import 80 undead skins (four classes, both genders, ten variants each), plus the
user's capital and continent map. Original PNGs in Downloads were renamed by
class/gender/variant; WebP copies retain their original dimensions.
The mapping is recorded in `docs/development/undead-character-art-manifest.json`.

Variant 1 is the free starter. Variants 2/3 are midgame/endgame presentation,
4–6 represent talent branches, and 7–10 are collector variants. All non-default
variants are available for crystals through the existing transactional store;
there are no real-money-only offers or stat bonuses. Prices live in content.
Skin compatibility includes race as well as class and gender. Existing human IDs,
ownership and prices remain unchanged.

Mirrored quests use their original persistent progress IDs through a race-specific
catalog projection. Existing quest rewards cannot be claimed again after relocation.
Same-faction saved dungeon runs move atomically to the equivalent entrance with
their run ID and checkpoint intact. Active encounters defer recovery. In a legacy
mixed-faction run, the relocated member leaves that run; other members keep it.

Auction browsing filters the seller's authoritative character race before paging.
Purchases validate buyer/seller race under the existing character locks before any
settlement. Existing lots need no migration because character race is immutable.
Direct trade and party invites/acceptance reject opposing factions. Arena matchmaking
and its shared ranking are unchanged. Each faction has its own approach and return
route to DEAD_REACHES; bootstrap repairs old undead OUTER_REACHES positions too.
