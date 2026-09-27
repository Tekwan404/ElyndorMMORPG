# Player commerce settlement

Scope: server-side direct trade and buyout settlement, no marketplace UI.

- PostgreSQL transaction owns item transfer, Gold debit/credit, terminal state and request receipt.
- Reuse CharacterMutation for request identity and payload fingerprints; lock character rows in UUID order.
- Reuse CharacterItem.TransactionLockId for offers and escrow. Preserve instance IDs, rolls and affixes.
- Offers contain whole inventory instances (including whole stacks); partial-stack authoring is deferred.
- Lock and confirm apply to an explicit offer revision. Changes clear both locks and confirmations.
- Trade connections are attached by the authenticated transport. Disconnect cancels an uncommitted trade.
- Auction duration is 48 hours. Expiration delivers to Mailbox. Cancellation returns to inventory when there is space, otherwise Mailbox. Listing fee is retained.
- A listing row serializes buy/cancel/expire. Settlement and proceeds commit together.
- Unknown item trade policies fail closed. Only unbound, unequipped eligible items can move.

Verification: real PostgreSQL integration tests for success, rollback, insufficient funds, missing ownership,
disconnect, request replay/conflict, concurrent confirmations/buyers, expiration/cancellation and mailbox replay.
Run backend build/unit tests and inspect migrations/diff. Docker availability is checked separately.
