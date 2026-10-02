# Friendly arena invitations

Arena invitations reuse the production 1v1 combat runtime and real character builds.
They create `Unranked` matches: no rating, Honor, ranked wins/losses, or PvE rewards.

## Player flow

Open Menu → Arena. Enter the friend's **character name**, then invite them.
The recipient opens Arena and accepts or declines; the sender may cancel.
Both players must have a live ArenaHub connection when accepting. Inviting an
offline friend is allowed so the Telegram notification can bring them back.
Invitations expire after five minutes. Queued players must leave matchmaking first.

## Server contract

- `GET /api/v1/arena/invites`: current incoming/outgoing pending invitations, up to 20.
- `POST /api/v1/arena/invites`: `{ requestId: UUID, targetName: string }`.
- `POST /api/v1/arena/invites/{id}`: `{ action: "accept" | "decline" | "cancel" }`.
- `ArenaInvitesChanged` on the existing ArenaHub prompts a canonical list refresh.
- Accepted matches use existing `ArenaMatchFound`, combat commands and reconnect.

The sender owns cancellation; only the addressed recipient may accept/decline.
The invitation UUID is its creation idempotency key. An accepted invitation stores
one match ID; terminal/replayed invitations never re-register combat. The existing
character operation guard and ordered PostgreSQL character/invitation row locks
serialize acceptance against other combat admission. Match creation and invitation
acceptance commit atomically. EF retry uses a stable candidate match ID so an
uncertain commit can still start that same match instead of creating another.

One pending outgoing invitation per sender and a 30-second send cooldown limit
notification spam. No character level normalization or new combat formulas.

## Telegram and deployment

Apply the `ArenaFriendInvitations` EF migration. Arena must be enabled using the
existing `Arena:Enabled` flag. Existing bot credentials and message sender are reused.
The bot sends a WebApp button pointing to
`https://elyndor.su/world?arenaInvite={id}`; opening it selects the Arena menu without
automatically consenting to a fight. Telegram can message only users who previously
started the bot and have not blocked it. Delivery is best-effort, logged on failure,
and never rolls back an already committed invitation; there is no delivery outbox.
Retries of the create request do not send duplicate notifications.

Pending invitations survive restart. Existing ArenaRecoveryService cancels orphaned
active matches; an old accepted invitation cannot resurrect them.

## Verification

Domain tests cover expiry and terminal transitions. PostgreSQL/API tests cover
authorization, replay, simultaneous acceptance, real runtime registration, no
progression, completed-match replay, offline acceptance, queue conflicts,
cancellation/decline and Telegram failure. Frontend tests cover request-ID retry,
authoritative match restore, button ownership and queued-state controls.
