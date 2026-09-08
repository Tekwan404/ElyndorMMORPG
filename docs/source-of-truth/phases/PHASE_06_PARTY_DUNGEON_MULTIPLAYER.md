# Phase 06 — Friends, Party, Multiplayer Combat, Dungeon and Loot

## Status

Backend, persistence, frontend vertical slice, automated verification, and the server-side
two-account multiplayer flow are implemented. Both mock browser verification and a real
local Browser -> ASP.NET -> PostgreSQL smoke flow are covered. Real Telegram browser
verification remains pending because it requires authenticated Telegram sessions for
multiple accounts.

## Scope

This phase adds the first social-to-dungeon vertical slice:

```text
Player search
  -> friend request
  -> party invite
  -> leader starts shared encounter
  -> participants contribute through damage/healing/support/tanking
  -> encounter roster is frozen
  -> personal loot per eligible participant
  -> group roll for valuable equipment
  -> Ancient Mine checkpoint
  -> final boss
```

## Social rules

- Search supports character name, Telegram username, and stable player code.
- Friends use an explicit request accepted or declined by the target.
- Parties contain 1-5 characters.
- Only the leader invites, kicks, transfers leadership, disbands, or starts a
  world group encounter.
- Friends receive normal party invitations. A non-friend can receive a direct
  party invitation without first becoming a friend.
- Leader departure transfers leadership deterministically to the next valid
  member.

## World combat rules

- One server-authoritative combat session contains all player participants.
- A player is never represented as a companion.
- The leader starts the encounter.
- The initial party roster is frozen for that encounter.
- A member who was elsewhere when the encounter started may join after arriving
  at the location.
- A member added after start cannot join the encounter.
- A fled participant cannot rejoin.
- The combat continues when an individual participant flees. The last active
  participant fleeing ends the session in defeat.

## Dungeon rules

- Ancient Mine supports 1-5 players.
- The run has four ordinary encounters and one final boss.
- The dungeon run membership snapshot is separate from every encounter roster.
- A new party member may enter the run after joining the party, but cannot join
  an active encounter. They may join the next encounter or a restarted one.
- A wipe resets only the current encounter and preserves completed checkpoints.
- The leader explicitly restarts a wiped encounter; an active run member may
  leave outside combat and re-enter the same run later.
- Reconnect and re-entry never duplicate membership or rewards.

## Contribution and reward rules

Contribution uses an activity-specific `ParticipationPolicy`. It may include
participation time, qualifying actions, damage, effective healing, support,
mitigation, threat, and taunt. A universal damage threshold is not used.

Ordinary loot is independent personal loot:

```text
eligible participant -> independent server roll -> own result or no drop
```

Rare+ equipment is group loot:

```text
NEED -> GREED -> PASS
```

The server evaluates valuable equipment once per completed combat session and
persists that shared result, including an empty result. Each eligible
participant then receives the same persisted `LootRoll`; personal material
rolls remain independent.

Only eligible participants from the encounter roster see the roll. Every eligible
participant receives the same persisted roll immediately; concurrent finalization
still creates only one roll. `NEED` is server-authorized by equipability rules and
the response exposes the current character's `CanNeed` availability for the UI.
The roll expires to `PASS` after the server timer and resolves idempotently.

## Delivery order

1. Friends and player search.
2. Parties and invitations.
3. Multi-participant combat kernel and contribution ledger.
4. Party-started world encounters.
5. Ancient Mine run and encounter lifecycle.
6. Personal loot and persisted group equipment rolls.
7. Mobile UI and browser verification.
