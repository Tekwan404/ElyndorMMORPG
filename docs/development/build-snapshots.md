# Character build snapshots

## Admin commands

The existing Telegram admin bot supports:

```text
/builddump <telegramId>
/gear <telegramId>
/talents <telegramId>
```

All commands use the existing webhook secret, sender allowlist and chat authorization. They cannot be used by ordinary players. `/builddump` sends a readable TXT report and a machine-readable JSON document; shorter gear/talent reports are sent as messages when they fit Telegram's limit.

The JSON envelope contains `BuildHash`, `EquipmentHash`, `TalentHash` and `Snapshot`. It is the diagnostic source of truth, not a client-submitted character state or an import API.

## Snapshot contract

`CharacterBuildSnapshotService` uses the production derived-stat pipeline and a pinned content snapshot. Admin capture runs in a repeatable-read transaction. Captured data includes identity, level, content/balance versions, content hash, engine version, raw/effective stats, resource profile, equipment instances, rolled affixes, enhancement, accepted reforge history, set bonuses/passives, selected talent definitions/ranks, unlocked abilities, resolved modifiers, stat formula and class profile. The spatial artifact is captured separately from ordinary equipped slots.

Permanent build stats are recorded, not temporary combat buffs/debuffs or current HP/resource. Raw and effective values expose stat caps; penetration is capped for effective combat use. Defense reduction uses the existing shared mitigation formula with the character's level as the diagnostic reference level. Actual target-dependent mitigation still belongs to the damage pipeline.

`AbilityPanel` is the server's known-ability list, not a browser-local reordered hotbar. Ability definitions and resolved talent modifiers are stored separately; executing/replaying the build requires the matching content release and engine implementation. No new combat simulator or snapshot import is included.

Hashes use SHA-256 with canonical object-key ordering and decimal representation. JSONB round trips preserve hashes. Build hashing excludes the display name, current vitals and capture time; equipment and talent changes alter the appropriate hashes. Character identity, content and engine versions remain part of build identity.

## Training logs

Production training creation captures the same derived build used to construct the session and persists an immutable session/account-to-build reference. Changing equipment afterward cannot change the recorded training build. The reference survives combat registry cleanup and database reload.

Both existing Telegram combat-log export paths append the pinned build summary and send its full JSON as a separate document. The v2 archive's existing send gate and retry behavior remain authoritative. A failed document delivery permits retry; Telegram delivery itself is not an exactly-once transaction, so a retry can repeat the TXT document. Old sessions without a captured reference are explicitly marked unavailable instead of being assigned the player's current build.

## Storage and deployment

Apply the EF migration `20261004140827_CharacterBuildSnapshots` using the project's normal deployment migration workflow. It creates only:

- `game.character_build_archives`: immutable payloads keyed by BuildHash; repeated/concurrent captures use insert-on-conflict.
- `game.training_build_references`: immutable session/account references with a restrictive foreign key to the archive.

Archives intentionally survive character changes/deletion and have no character foreign key. They contain player identity and should remain accessible only to authorized operators. Automatic retention/pruning is not implemented in this slice; references must be removed before referenced archive rows can be pruned. The existing in-memory combat-event archive retains its existing lifetime; storing a build does not make the complete combat-event log durable.

`/builddiff` and automated build replay are separate follow-up work.
