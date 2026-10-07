# Equipment set progression

Implemented content contract, content `0.42.0`, balance `0.32.0`.

| Required level | Sets | Members per set | Bonus thresholds | Purpose |
| --- | --- | --- | --- | --- |
| 1–17 | 0 | — | — | Standalone item hunt |
| 18 | 4 class sets | 4 armor | 2/4 | Small stat bonuses |
| 35 | 4 class sets | 6 armor | 2/4 | Hybrid equipment |
| 45 | 4 class sets | 6 armor | 2/4 | Class direction |
| 55 | 4 class sets | 6 armor | 2/4/6 | Stats and a simple class mechanic |
| 60 Normal | 12 branch sets | 8 | 2/4/6 | Stats only |
| 60 PvE T1 | 12 branch sets | 8 | 2/4/6 | Stats, rotation, branch mechanic |
| 60 PvP T1 | 12 branch sets | 8 | 2/4/6 | Stats only |

L18 uses Head, Chest, Hands and Legs. Later armor families add Shoulders and Feet.
L60 adds Amulet and one ring definition. Two instances of that ring may be equipped,
but count as one distinct set member. There is no eight-piece bonus. Weapon, cloak
and the second ring can hold the existing branch Unique rewards; any six of the
eight set templates grant the full bonus. Branch specialization is a build direction,
not a new equip restriction: the existing class restrictions continue to apply.

Old L10/L14/L23 templates keep their IDs, names, artwork and item-level ranges as
standalone equipment. The two former L18 shoulder/feet members become standalone.
The 72 new L35/L45/L55 templates reuse the corresponding existing artwork. Each
tier has field loot sources at its required level; dungeon summons keep their
rewardless definitions. L60 accessories use existing accessory art. PvE T1's
aggregate independent drop expectation stays at 0.144 pieces per eligible kill
(`96 × 0.0015`). PvP accessories cost 50 Honor for the amulet and 45 for the ring;
the six existing armor prices are unchanged. All eight cost 555 Honor.

Holy PvE T1 is named **Регалии Непогасшего Рассвета**. Its stable identifier stays
unchanged; the early Paladin family keeps **Клятва Расколотого Рассвета**.

## Balance generation

Run `python tools/content/generate-set-bonuses.py` from the repository root.
`tools/content/set-bonus-budgets.json` contains relative flat-stat allocation and
the fraction of one slot-neutral item budget assigned to each stat threshold:
L18 6%, L35 7%, L45 8%, L55 9%, L60 10%. The generator uses the shared itemization
level curve, rarity multipliers and stat power weights. It does not alter authored
percentages, proc magnitudes or thresholds. Values are rounded to four decimals;
the inventory tooltip displays at most two decimals.

The budget for one flat threshold grows through 44.7279 / 82.5425 / 170.4771 /
266.6506 / 343.3556 / 415.6411 power units at L18 / L35 / L45 / L55 / Normal60 /
Legendary T1. L55 uses Epic budget rarity despite Legendary pieces, reserving
power for its introductory mechanic. A threshold with multiple flat stats shares
one budget; it does not receive a full budget for each stat. Percentage-only
thresholds retain their authored values. These numbers express stat allocation,
not a universal DPS score for branch mechanics.

Standalone Legendary families retain their roll envelope and scaling to L59.
Set membership does not increase the per-piece item budget. This preserves the
choice between a stronger standalone roll and a set threshold. PvE T1 pieces
have a higher rarity budget than their Normal counterparts. Combat performance
still depends on branch, ability choices, encounter and proc uptime; static
budget checks do not establish equal performance across all twelve branches.

## Authoritative effects

Set bonuses reference `specialEffectIds`; the corresponding definitions live in
the set's `specialEffects`. `EquipmentSetEffectResolver` captures them alongside
the loadout. `SetPassiveCombatRuntime` extends the existing passive evaluator and
effect engine; CombatSession does not dispatch on individual set IDs. Combat
factories, hosted player mechanics, build snapshots and simulation use the same
definitions. Equipped sets are captured at combat creation.

Only eligible direct events advance counters. Periodic, reflected, copied healing
and proc events cannot recursively activate these bonuses. AoE hit counters count
one action, companion counters resolve to their owner, and actor counters remain
separate. Resource refunds clamp to the maximum. Cooldown reductions never create
a cooldown or move readiness before the current authoritative time. Charges expire
at their exact end timestamp and successful direct hits consume them once; misses
and unrelated abilities do not consume them. Temporary charges/counters are owned
by the active session, not persisted as additional player equipment state.

L55: Warrior spends 40 Rage in 8 seconds for a next direct hit +8% (6-second ICD);
Mage spends 60 Mana in 8 seconds for a next direct spell +8% (6-second ICD);
Archer's third Focus ability refunds 8 Focus; Paladin's third class ability grants
+5% AP/SP for 4 seconds. Damage charges last 8 seconds.

PvE T1 durations and secondary values, where the design did not specify them:

| Branch | Four pieces | Six pieces |
| --- | --- | --- |
| Guardian | Block: next Revenge +15% | Shield Block: Block Value +15% for 6s, next Revenge +25%; stronger Revenge charge wins |
| Berserker | Spend 30 Rage in 8s: AS +5% for 4s, ICD 4s | Wild Strike/Heavy Blow crit: next direct hit +20% |
| Warlord | Cry: next direct class ability +10% | Third command: next Cry effect magnitudes +15% |
| Fire | Direct Fire crit: own Burn +1s, capped at +4s total, ICD 1s | Pyroblast/Fire Blast against own Burning target: +20% direct damage |
| Frost | Direct Frost hit: Ice Lance CD −0.5s, ICD 0.5s | Controlled target: next Ice Lance +25% |
| Arcane | Spend 60 Mana in 8s: SP +5% for 4s, ICD 6s | Spark/Missiles: Arcane Power CD −1s, ICD 1s |
| Marksman | Direct shot: Aimed Shot CD −0.5s, ICD 0.5s | Third direct shot: next Aimed Shot +20% |
| Beast Mastery | Pet direct crit: owner's next shot +10%, ICD 1s | Four alternating owner/pet hits: owner AS +5% for 6s |
| Survival | Direct hit on own Sting/Trap target: trap CDs −0.5s, ICD 1s | Sting/Trap ability: next direct shot against own affected target +20% |
| Holy | Critical effective direct heal: Holy Shock CDs −1s, ICD 1s | Third effective direct heal: one shared next Holy Shock damage/healing charge +20% |
| Protection | Block: next Hammer of the Righteous +15% | Third block while Holy Shield is active: next offensive hit +20% |
| Retribution | Crusader Strike crit: Judgement CD −1s | Judgement hit: next Templar's Verdict +20% |

All next-action charges last 8 seconds. Normal and PvP have no special effects.
No database migration is needed. Bundled content changes reach production through
the existing release/publication workflow, not by mutating running player rows.
