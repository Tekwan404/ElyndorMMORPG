# Combat and dungeon navigation

Player abilities that use a global cooldown now use 0.5 seconds from ability start
(content 0.57.0 / balance 0.47.0). Casts and channels overlap that timer; a 1.1-second
Scorch can be followed by the next spell immediately after completion. Authored
off-GCD abilities stay off GCD. Individual cooldowns, resources and cast times remain.
The ability inspection displays effective cast time, including talent modifiers.
Mana costs are unchanged; continuous rotation budgets account for the faster cadence.

Dungeon encounter binding and participant validation happen under admission leases,
before the registry starts gameplay timers. A participant that left the entry during
preparation cannot be admitted. Pending encounter rosters are refreshed on retries.
A failed admission cleans its journal and bound encounter; publication failure after
admission retains the running combat for resume.

An active encounter without a durable journal is reconciled on run refresh. A runtime
that is still active or finalizing is preserved. Committed victory evidence or a
successfully finalized Victory advances the encounter once; an orphan without victory
evidence becomes Wiped and may be retried. No character progression or inventory reset
is required. Deploy the fix, refresh the run, and retry or leave it. A specific account
investigation still requires its character name or public hero code.

Dungeon floors follow authored encounter order and end at each boss. No saved encounter
indexes are reordered. The current run unlocks floors by encounter progress; completed
floors can be inspected but cannot grant rewards again. New runs start at floor one.
Dungeon loot previews include positive-chance loot from encounters and adds, without
claiming guaranteed drops or calculated probabilities. An optional filter shows only
the selected floor.

Profession workshop links open only that profession, with an optional gathering partner
learning shortcut. Mail is available from the main menu.
