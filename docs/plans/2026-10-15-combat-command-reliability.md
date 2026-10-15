# Combat command reliability review

Verify the nine reported issues against current production code and regressions.
Fix consumable compensation cancellation and participant projection/validation first.
Map only gate admission timeout to combat_busy, centralize ability dispatch in the application
service and use the existing Guid JSON wire contract for StartCombat.

Replace the session's unbounded command-ID set with compact cached outcomes. IDs
remain session-unique (matching durable consumable receipt keys); outcomes belong
to a participant. Replay returns the original success/error and a current projected
snapshot without replaying effects/events. Do not evict an old ID and permit mutation
again. Limit IDs to 64 characters and outcome history to 16384 per participant;
refuse fresh commands at capacity while retaining old replay and other participants.
Reserve one bounded extra command for escape at capacity.
No snapshot copies are retained. Preserve a rejection marker on thrown mutations.

Measure gate hold/wait duration through System.Diagnostics.Metrics with no identity
tags and register the meter for OpenTelemetry. Keep database reservation under the
single-writer gate until measurements justify a separate flow.

Send one canonical CombatUpdated frame per publication; keep client CombatEnded
handling for old servers. Add names-versus-hub contract coverage and a real
HubConnection command lifecycle, and tests for compensation on disconnect, member
snapshot, cached failures/success, capacity, timeout mapping and gate timing.
No balance, schema, reward eligibility, raid or restart-policy changes.

Verification:
- Red: new core regressions failed for duplicate outcomes, ID length and capacity.
- Green: Release solution build, zero warnings/errors; all 1539 backend unit tests.
- 19 focused PostgreSQL/real SignalR tests plus the added receipt ownership/replay test passed.
- 49 focused frontend recovery/error tests, frontend typecheck/build and lint passed (existing lint warnings).
- Repository layout and diff whitespace checks passed; reviewer findings addressed.
- Full integration and required GitHub CI are checked before merge.
