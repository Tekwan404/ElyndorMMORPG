# Reset unfinished gameplay on every restart

The user selected every process restart, including crashes, rather than only deployments.
Reuse startup recovery before request admission: finish terminal journals and committed
rewards once, interrupt unfinished PvE/training/party fights, reconcile orphaned encounters,
then close remaining unfinished dungeon runs and leave their members. New entries start
fresh runs. Completed history, inventory, currencies, character progress, party membership,
reward receipts and pending loot remain durable. Existing PvP restart cancellation and
in-memory queue reset are retained. No raid expansion or schema/content change.

The dungeon state and membership reset share one short PostgreSQL transaction and an
advisory lock. Exclude runs with surviving live encounter/journal evidence; preserve the
existing registry safeguards. Do not admit players or reset runs if terminal recovery fails.
Repeated startup is idempotent. The frontend already clears retired battle state when
resume returns combat_not_found. The dungeon card refreshes party/run data when
reconnect completes without a combat snapshot, so an open card drops its stale run.

Verify failure-first integration cases for journal/no-journal interruption and idle runs,
completed history/rewards, reset transaction rollback, failure before reset, live-runtime
preservation and post-admission rejection. Run Release build, backend unit and relevant
Combat/Dungeon/PvP integration regressions. Inspect final diff and open a dedicated PR.


Verified: the three new restart integration cases failed against the previous behavior.
After implementation, all 21 startup recovery tests pass, including rollback and
post-admission guards. Full backend unit suite: 1532 passed. Combat/Dungeon/PvP
integration regressions: 158 passed. Release solution build: 0 warnings/errors.
The reconnect card test also failed first when the stale run was not refreshed.

Final client checks: 76 dungeon/realtime/combat/party tests passed. Frontend
production build/typecheck passed; lint has 0 errors and 22 existing warnings.
Independent review found no actionable issues in server reset or client refresh.
