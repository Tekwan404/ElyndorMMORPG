> Integration update: the live guild now uses WorldPvpBoard and the existing
> World PvP endpoints. This document preserves the original presentation proposal;
> its bounty/title DTOs are not enabled server contracts. See
> docs/development/dead-reaches-pvp.md for current runtime and reward rules.

# World PvP — UI/UX handoff (frontend ownership only)

Date: 2026-10-17. Scope owner: frontend. World PvP battle server/API/settlement owner: game developer.

## Product boundary

The World PvP display is **not** Arena, and must never re-use `useArenaStore`, Arena Honor or Arena rating.

Entry: **City → Гильдия авантюристов → Война фракций**; field guild posts can display the same read-only panel. Normal navigation remains five items; no duplicate entry in the generic Game Menu.

Only **Селия** (HUMAN) / **Морвена** (UNDEAD) remains visible as the Guild registrar. Hunt master, quartermaster and cartographer cards are removed from the Guild scene (their image assets are left untouched). The existing **Контракты** section and its accept/claim interactions remain unchanged and retain their own space.

Five World PvP panels:
- **Обзор**: personal World PvP score, world rank, kills/assists, streak, season header.
- **Рейтинг**: independent leaderboard, all / human / undead filters, personal highlighting, visible deaths/assists.
- **Награды**: *separate* Field Honor and Blood Tokens balances; equipment/cosmetics/collectibles filters; future readonly catalog.
- **Розыск**: bounty target, kill streak, approximate last known region, potential rewards for an enemy player's head. Ally-faction targets hidden when personal faction known.
- **Титулы**: unlocked/equipped/locked state and progress to the server-defined title unlock; visual-only until backend wiring.

The layout is optimized for Telegram mobile screens, wraps narrow labels, provides clear focus indicators and keeps filtering locally without extra network requests.

## Implementation state

- `web/elyndor-web/src/game/worldPvp/worldPvpContracts.ts`: frontend *proposal* for a read-only dashboard shape; it is NOT a backend contract.
- `WorldPvpView.vue`: view bound only to an optional `dashboard` input, with explicit missing-data states and no API call.
- `AdventurerGuildBoard.vue`: two main Guild sections — contracts and war; the registrar is the single Guild NPC. World PvP currently renders disconnected UI / upcoming season.
- `/dev/world-pvp`: development-only sample dashboard with a conspicuous **DEMO** warning. The fixture is **never** passed to the player Guild.
- No request to the backend, no server or database changes, no item purchases, no stat writes, no client-side reward calculator or guess about a live balance.

## Suggested eventual server response

One read-only data aggregation (path, paging and HTTP shapes chosen by the server owner):

```ts
interface WorldPvpDashboard {
  season: { id: string; name: string; status: 'Upcoming' | 'Active' | 'Finished'; endsAtUtc: string | null }
  personal: {
    characterId: string; faction: 'HUMAN' | 'UNDEAD';
    rank: number | null; score: number; kills: number; assists: number;
    deaths: number; bestStreak: number; currentStreak: number;
    fieldHonor: number; bloodTokens: number
  } | null
  standings: Array<{
    characterId: string; name: string; faction: 'HUMAN' | 'UNDEAD';
    rank: number; score: number; kills: number; assists: number; deaths: number
  }>
  rewards: Array<{
    id: string; name: string; description: string;
    category: 'Equipment' | 'Cosmetic' | 'Collectible';
    cost: number; currency: 'BloodTokens' | 'FieldHonor';
    requiredRank?: number | null; owned: boolean
  }>
  bounties: Array<{
    id: string; targetCharacterId: string; targetName: string;
    targetFaction: 'HUMAN' | 'UNDEAD'; killStreak: number;
    lastSeenLocationName: string | null; honorReward: number; tokenReward: number
  }>
  titles?: Array<{
    id: string; name: string; description: string; requirement: string;
    progress: number | null; target: number | null;
    unlocked: boolean; equipped: boolean
  }>
}
```

The UI types are a draft presentation schema, NOT a directive to implement this data shape exactly. Stable character IDs prevent list glitches. Scores and money come from server results; there is **no client authority** over balance or eligibility. Server should page/paginate leaderboards and catalogs before launch if data grows beyond one page; this initial UI shows the provided slice.

## Economics and scoring — design proposals, not implemented gameplay

- Field Honor: a **world-only** measure/currency, never sourced from Arena Honor.
- Blood Tokens: scarce world activity currency used for cosmetics, collectibles and possibly designated equipment. Spend/grant is server-owned.
- Season score differs from currency: the score resets between seasons while balances should follow game-defined persistence rules. The UI never mixes them.
- Prefer credit based on effective participation (damage/healing/defense/objectives), unique foes and anti-repeat controls, not kill count alone.
- Rank filters use server-provided **global ranks** and do not invent per-faction positions.
- Kill streaks/bounties encourage player pursuit without exposing real-time precise location.
- No purchase action until the owner supplies server-side validation/settlement and error states; even dev preview only says "Покупка пока недоступна".

## Acceptance for API wiring later

1. Missing dashboard: all four panels show honest empty states; no fake zero values.
2. Loading & request failures can be signaled to component; retry is an event for the future caller.
3. No Arena store import; arena screens untouched.
4. Rewards, balances and bounties read from the independent World PvP payload.
5. Verify server enforces all faction and payout restrictions; hiding a bounty in UI is only a presentation convenience.
6. Purchase action stays disabled until server owner provides atomic authoritative purchase contract.
7. Never expose unlaunched mock season/reward/title/target data in production, except a clearly marked development preview.
8. Regression tests must verify that the Guild registrar and existing contract accept/claim UI remain accessible when switching back from World PvP.
