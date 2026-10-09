# GM Forge — developer-only test equipment

GM Forge is an **admin Telegram command**, not an item-drop feature or a player crafting recipe. It creates a new inventory instance for an existing character. The normal loot generator, item definitions and marketplace economics are unchanged.

## Commands

Send from an authorized Telegram admin account/chat (same authorization as existing \`giveitem\`).

\`\`\`text
/gmforge <telegramId> <ITEM_ID> quality=PERFECT
/gmforge <telegramId> <ITEM_ID> stars=5 enhance=5
/gmforge <telegramId> <ITEM_ID> WEAPON_DAMAGE=1500 CRITICAL_DAMAGE=150
/gmforge <telegramId> <ITEM_ID> quality=PERFECT enhance=5 WEAPON_DAMAGE=1500 CRITICAL_DAMAGE=150
/gmforge <telegramId> clone:<ITEM_INSTANCE_GUID> stars=5 CRITICAL_DAMAGE=150
\`\`\`

The names after the item ID are case-insensitive, e.g. \`critical_damage=150\` works.
All values are **actual stat bonuses**, not roll percentages: \`WEAPON_DAMAGE=1500\` adds 1500 bonus weapon damage; \`CRITICAL_DAMAGE=150\` adds +150 percentage points to crit damage. The existing template's base weapon damage is not overwritten.

- \`quality=NORMAL|ELITE|BOSS\`: uses the existing loot profile for a fresh rolled instance.
- \`quality=PERFECT\` or \`PERFECT\`: forces maximum bonus-affix count, rolls every generated affix at its maximum, and sets 5 stars/100% quality for a newly created item.
- \`stars=1..5\`: developer-only override of star classification (without changing affix values). For an authentic 5-star result use \`quality=PERFECT\`; ordinary \`stars=5\` is a synthetic diagnostic.
- \`enhance=0..5\`: applies sequential enhancements without materials/currency.
- \`STAT_ID=value\`: overrides or adds a combat affix, bypassing budget/pool restrictions for testing. Range 0..1,000,000, invariant decimal point, approved stat IDs only. This may intentionally create stat combinations impossible in normal loot.
- \`clone:<GUID>\`: copies the owner's **persisted generated** item to a new instance and then applies requested modifications. The original is never modified; the command response includes the new instance ID. Clone mode preserves existing affix slots; it does not reroll the count or content pool. With \`PERFECT\`, its existing slots are made maximum.

Only generated equipment is currently supported; fixed stat-only templates are rejected.

## Safety

- The endpoint is not exposed as a player API. Existing Telegram admin allowlists apply.
- New instances carry \`SourceType=GM_FORGE\` for provenance and the existing admin update audit for idempotency.
- \`BindState=BOUND\` and a permanent item lock prevent trade, auction, merchant sale and salvage; the item can still be equipped for combat experiments.
- The item may still be used to defeat mobs and gain combat rewards. **Use a dedicated developer/test character**; full prevention of progression farming with such items would require an additional combat reward gate.
- Ordinary item generation and Reforge remain unchanged; only this admin-only creation path can invoke forced perfect generation.
- The UI is not yet integrated into the admin web app: this is a functional Telegram-command-first implementation.

## Manual verification

1. Mint an ordinary sword and a perfect sword for the same developer character; inspect the inventory tooltip and compare star/affix quality.
2. Mint a sword with \`WEAPON_DAMAGE=1500 CRITICAL_DAMAGE=150\`, equip it, and use a training dummy to check the live combat pipeline.
3. Clone an existing item, confirm the original stays unchanged, then compare both builds.
4. Verify GM items are excluded from auction/trade/sale while being equipable; confirm the audit prevents a second item on Telegram update replay.
