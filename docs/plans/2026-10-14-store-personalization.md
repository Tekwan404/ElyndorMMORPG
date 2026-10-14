# Complete store personalization

Deliver paid rename, three permanent profile frames and three permanent profile
titles through the existing crystal wallet. Pricing and availability live in
versioned economy content. No billing, gameplay-stat bonuses or unfinished offers.

Persist purchased cosmetics with a character/id primary key and cascading character
foreign key; add nullable selected frame/title ids to Character through EF migration.
Use existing immutable crystal ledger as purchase receipt. Lock character then wallet
within one short PostgreSQL transaction, matching existing economic lock order.
Replay checks compare operation reference and payload fingerprint before current
availability checks. Repeat purchase cannot create another entitlement or debit.

Rename reuses CharacterNamePolicy and the unique normalized-name constraint.
Validation, duplicate/same-name, insufficient funds and concurrent name claims must
not charge. Catch only the specific normalized-name uniqueness violation. Replaying an
old rename after a newer one returns current state without undoing it.

Apply frames/titles to HUD, hero, menu and public profile where supported. Preview
must be local until owned cosmetic equip succeeds. Unequip/re-equip are free.
Add focused transaction/concurrency/replay tests, migration roundtrip, content
validation, Vue tests and mobile browser verification before updating PR 372.

## Delivered verification

Implemented on codex/market-crystal-store for PR 372. Release solution build passes
with zero warnings. PostgreSQL personalization tests cover receipt replay, entitlement
persistence, free selections, insufficient funds, concurrent duplicate purchases,
concurrent rename/name claims, shared-wallet spending, arena blocks and migration
rollback/reapply. Current content validates at 0.60.0. Frontend unit coverage and
mobile browser checks cover confirmation, purchase, free equip/removal and rename.
External payment integration remains explicitly out of scope.
