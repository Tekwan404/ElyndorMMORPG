# Combat and dungeon navigation

Player abilities that use a global cooldown now use 0.5 seconds from ability start
(content 0.57.0 / balance 0.47.0). Casts and channels overlap that timer; a 1.1-second
Scorch can be followed by the next spell immediately after completion. Authored
off-GCD abilities stay off GCD. Individual cooldowns, resources and cast times remain.
The ability inspection displays effective cast time, including talent modifiers.
Mana costs are unchanged; continuous rotation budgets account for the faster cadence.

Dungeon encounter binding and participant validation happen under admission leases,
before the registry starts gameplay timers. Start responses capture their snapshot
and events before admission, so a zero-delay tick cannot race response enumeration.
Clients retain a newer terminal update when that initial response arrives later. A participant that left the entry during
preparation cannot be admitted. Pending encounter rosters are refreshed on retries.
A failed admission cleans its journal and bound encounter; publication failure after
admission retains the running combat for resume.

An active encounter without a durable journal is reconciled on run refresh. A runtime
that is still active or finalizing is preserved. Committed victory evidence or a
successfully finalized Victory advances the encounter once; an orphan without victory
evidence becomes Wiped and may be retried. No character progression or inventory reset
is required. Deploy the fix, refresh the run, and retry or leave it. A specific account
investigation still requires its character name or public hero code.

Dungeon floors are explicit content ranges, independent of boss positions. A floor is
open by default or uses `requiredEncounterId` to require a particular encounter victory.
The solo player or party leader selects an unlocked floor through
`POST /api/v1/dungeons/runs/{runId}/floor`; encounters inside that floor remain sequential.
Each floor resumes at its first unfinished encounter. Selection is blocked during combat;
completed floors can be inspected but cannot grant rewards again. All encounters must be
completed to finish the run. Existing encounter indexes and rewards remain unchanged.
Bundled content 0.58.0 starts with every floor open. Definitions without explicit floors
retain the legacy sequential layout for compatibility.
Dungeon loot previews include positive-chance loot from encounters and adds, without
claiming guaranteed drops or calculated probabilities. An optional filter shows only
the selected floor.

Terminal combat journals reference an immutable, hash-checked `ContentRevision` archived
before rewards are applied. Restart recovery uses that content for rewards, derived state,
corpses and dungeon advancement, even after content changes. Versioned legacy journals
require an unambiguous matching revision or the exact current versions; otherwise startup
fails with the journal preserved. Explicitly unversioned compatibility snapshots retain
their existing fallback. Recovery does not resume unfinished live combat.

Warrior Death's Embrace, Archer Marked Shot counters and Spirit Bond timers belong to the
individual participant. Companion attacks and healing hooks execute with their owner's
target and talent state, independent of participant ordering.

Profession workshop links open only that profession, with an optional gathering partner
learning shortcut. Mail is available from the main menu.
