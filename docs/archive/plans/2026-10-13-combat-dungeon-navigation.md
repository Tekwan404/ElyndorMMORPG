# Combat, dungeon and navigation corrections

Use player GCD 0.5 seconds from ability start, overlapping cast time, at the central player ability composition boundary; keep individual cooldowns, casts, resource validation, command idempotency and monster AI unchanged. Add composed cast duration to battle contracts and visible inspection metadata.

Bind dungeon encounter under participant admission guard after durable journal creation, before registry timer admission. Conditional cleanup must handle pre-admission failure without cancelling admitted gameplay. Reconcile terminal/orphaned encounter state using committed journal/reward evidence; never infer a victory from a missing runtime alone. Block navigation during active encounters; reconcile before navigation. Ignore delayed updates from retired combat sessions on the client.

Group authored encounter order into floors ending at bosses. Inspect unlocked floors by run progression without skipping/replaying encounters or duplicating rewards. Display server-authored dungeon loot from encounter monsters and adds. Keep persistent run ordering unchanged.

Profession pins open only the selected workshop; gathering partner is a compact optional learning action. Move mailbox to the game menu. No database drop.

Verify one-hit victory ordering, terminal runtime repair, navigation preservation, stale updates, player half-second GCD with cast/cooldown preservation, floor locking, loot attribution and mobile layout. Build/test/typecheck and CI gate merge.
