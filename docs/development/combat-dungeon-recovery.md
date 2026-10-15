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

Dungeon encounters follow their authored sequence. Floor grouping and selection were
removed at the user's request on 2026-10-15. Existing saved encounter indexes and
reward rules remain unchanged. Dungeon loot previews show all positive-chance loot
from encounters and adds, without claiming guaranteed drops or calculated probabilities.
The optional legacy floors API field remains empty for older clients.

Profession workshop links open only that profession, with an optional gathering partner
learning shortcut. Mail is available from the main menu.

Terminal combat journals pin an immutable, hashed content revision. Recovery uses
that revision for rewards, dungeon progress and defeat respawn even after current
content changes or removes the encounter. Missing or ambiguous versioned legacy
content preserves the journal and stops recovery instead of substituting a newer
package. Explicitly unversioned compatibility snapshots retain their legacy fallback.

Participant runtime state isolates Death's Embrace, Marked Shot and Spirit Bond.
Companion attacks resolve in their owner's context, independent of participant order.
These fixes do not restore floor grouping or floor selection.
