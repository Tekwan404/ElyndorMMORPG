# Warrior mechanical contracts — 2026-10-07

Initial ability contracts landed in content `0.39.0` on 2026-10-07. Guardian progression was then normalized on 2026-10-08 in content `0.46.0` / balance `0.36.0`; numeric ability coefficients remain unchanged. This document records executable ability contracts and the updated unlock progression.

## Baseline and access

STRIKE unlocks at level 1, BATTLE_SHOUT at level 3 and HEAVY_BLOW at level 6 without spending talent points. CharacterKnownAbilityResolver, CombatSession snapshots and ArenaFighterAssembler expose the same learned kit. The baseline is defined once in `content/abilities/warrior-baseline.json`.

Each branch has one ability fragment and one talent branch replacement. Old Warrior nodes and duplicate ability definitions were removed from the root package. Other classes and balance coefficients are unaffected.

## Guardian nine-tier progression (2026-10-08)

Guardian now has the same nine playable tier thresholds as Berserker and Warlord: `0/5/10/15/20/25/30/35/40` spent branch points. All 31 talent IDs, names, ranks, modifiers and prerequisites are retained; only `tier`, `requiredSpentPoints`, node/tree version and UI layout change. This preserves invested ranks in existing builds; future learning follows the new gates.

- Tier 2: Last Stand and Revenge.
- Tier 3: Shield Block, Provoke and Sunder Armor.
- Tier 4: initial offensive control and improvements.
- Tier 5: Bastion and Shield Slam alongside one-handed specialization.
- Tier 6–8: heavier mitigation, taunt and defensive coordination, plus Shield Slam improvements.
- Tier 9: Unyielding Guardian (`G-6-5`) after 40 branch points, prerequisite Shield Slam (`G-6-1`); earliest character level 42.

The original `G-6-5` ID is intentionally stable even though its gameplay tier is now 9. UI rows follow actual gameplay tiers and server restrictions. The authoritative full node mapping lives in `content/talents/warrior-guardian.json` and the Source of Truth Warrior talent document.

## Audited talent abilities

All twenty IDs below execute through AbilityEngine plus the shared CombatSession player mechanics. Arena hosts the same mechanics; party and dungeon sessions use captured player membership. World bosses use boss-rank targets. `WarriorProductionContractTests` exercises all twenty plus the three baseline abilities in these five configurations, including resource costs, completion events, effects and cooldowns. Existing Guardian, Berserker, Warlord, proc policy and Arena full-tree tests cover branch hooks and composed builds.

| Branch | Ability | Mechanical contract |
| --- | --- | --- |
| Guardian | LAST_STAND | Temporary maximum HP, healing by the increase, expiry/removal clamp; generic magnitude upgrade. |
| Guardian | REVENGE | Block opens a five-second use window; damage, Rage cost, cooldown and improved stun. |
| Guardian | SHIELD_BLOCK | Timed block chance and block value, cap, upgrades and defensive-window support. |
| Guardian | PROVOKE | Threat promotion and forced target against NPCs; no forced player targeting. |
| Guardian | SUNDER_ARMOR | Stacking armor reduction and threat; becomes a successful-hit rider on SHIELD_SLAM when both are learned. |
| Guardian | CONCUSSION_BLOW | Damage and landed-hit stun; boss immunity retained. Avoidance does not stun; shield absorption still permits control. |
| Guardian | SHIELD_BASH | Damage and landed-hit control; talent silence follows the hit rather than command completion. |
| Guardian | BASTION | Timed incoming damage reduction and defensive-window hooks. |
| Guardian | CHALLENGING_SHOUT | Temporary NPC fixation without replacing accumulated threat; no player fixation. |
| Guardian | SHIELD_SLAM | Attack power plus block value, threat and optional Sunder rider/cost upgrade. |
| Berserker | WILD_STRIKE | Physical attack, cost/cooldown modifiers and existing critical/bleed hooks. |
| Berserker | WHIRLWIND | Shared multi-enemy target selection and physical attack with branch hooks. |
| Berserker | BERSERK | Timed attack power/crit/incoming damage window; blocks LAST_STAND, SHIELD_BLOCK and BASTION. Avatar kills reduce its running cooldown. |
| Warlord | BATTLE_CRY | Party attack power and timed accuracy/haste/crit upgrades; party resource restoration and capstone shield. |
| Warlord | ENDURANCE_CRY | Adds 6% of each recipient's base maximum HP for 15 seconds, without healing; expiry and dispel remove the bonus. |
| Warlord | WAR_BANNER | Four percentage points of crit, party kill resource rewards and flag upgrades. |
| Warlord | CRY_OF_VENGEANCE | Owner window and separate charges from direct ally damage; five-charge cap, half-second admission interval, next autoattack consumption, original-window expiry. |
| Warlord | VICTORY_FLAG | 8% outgoing damage, four-second immunity to new stun/silence, optional two-second lethal prevention. Existing control is not cleansed. |
| Warlord | RALLY_CRY | Heals each recipient for 6% of their own maximum HP, through the healing/event/threat pipeline; capstone restores owner Rage. |
| Warlord | BATTLE_STANDARD | Party autoattack trigger, 10% chance of a 25% secondary physical hit; no inherited crit, periodic/proc recursion or second bespoke dispatch. |

## Shared runtime changes

- Temporary maximum health lives in EffectEngine. Every removal path unwinds actor state. Replace/StrongestWins update bonuses atomically and heal only the net increase.
- Timed effects may declare event actions using the existing event pipeline, ProcGuard admission/deduplication/ICD and AbilityEngine action execution. Battle Standard uses this path instead of a Warrior autoattack handler. Damage events preserve their pre-critical base damage for secondary hits, avoiding double mitigation and inherited critical scaling. Existing target composition preserves Executioner on these secondary hits in both hosts.
- Victory Flag control immunity uses generic effect metadata/application policy. Cry upgrades are normal timed ability actions rather than separate day-long hooks.
- Berserker bleed definitions declare physical periodic damage; the effect-ID special case in periodic resolution was deleted.
- Static Warlord cost/cooldown/duration transformations reuse WarlordStaticAbilityHookResolver. Party hooks observe captured players as well as the companion, including crits, damage, deaths and kills. Secondary actors initialize their Guardian/Warlord passives too.
- Remaining conditional/party hooks still use the existing TalentRuntimeEngine; no second talent dispatcher or proc policy was added.
- Conditional Warlord party protection is removed when its owner dies or flees. Cry upgrades replace earlier applications, preserving their rank and duration. Banner kill resources are awarded by the actual banner owner once rather than once per Warlord who knows the ability.

## Active decisions and economy review

Guardian's repeated Sunder button is folded into Shield Slam after the latter is learned. Its repeatable combat decisions are Strike, Revenge and Shield Slam; Heavy Blow is an optional Rage spender. Taunts, interrupts and defensive windows remain situational rather than another required repeating rotation.

Berserker retains Strike for generation, Heavy Blow and Wild Strike as active spenders, Whirlwind for multiple enemies and Berserk as an offensive window. Restoring baseline progression prevents a talent-gated autoattack-only early loop. Warlord keeps seven distinct command/support choices; no cry or banner was removed merely for file symmetry.

Rage remains authoritative, capped and charged once. Strike grants 10, Battle Shout grants 20 every 15 seconds; Heavy Blow spends 30 every four seconds subject to GCD. Support spending remains a deliberate tradeoff against damage. Warlord cry cadence modifies base cooldowns; flag-driven reductions respect the existing 20-second ICD. The capstone kill reduction has a one-second admission interval. Numerical retuning is intentionally deferred.

Equipment note (updated after #334/#339): each Warrior branch now has Normal, PvE T1 and PvP T1 level-60 sets with eight unique members and 2/4/6 thresholds. Normal/PvP T1 bonuses remain stat-only; PvE T1 effects are handled by the shared `SetPassiveCombatRuntime`: Guardian block/Revenge, Berserker Rage-spend/crit, Warlord cry/command. The previous no-PvP/no-proc statement is historical and does not describe the current content.

## Remaining Warrior-specific debt

Guardian threat/control windows and Warlord charge/party-resource hooks remain class adapters over shared runtime services. A complete data-driven migration of these stateful rules is outside this incremental change. Compatibility readers for previously published deferred talent metadata remain; current Warrior content has no duplicate legacy tree. The unused historical BATTLE_FOCUS kernel fixture remains inaccessible to production characters.

## Verification

Release backend build passes with zero warnings/errors. Full backend suites pass: 1,293 unit tests and 579 PostgreSQL integration tests. Frontend lint has zero errors (13 existing warnings), 492 unit tests pass, typecheck/build pass, and all five mobile battle preview E2E tests pass. Content validation passes at version 0.39.0; the strict icon/balance/progression/benchmark run completes with the repository's existing optional-icon and balance warnings. Repository layout and diff whitespace checks pass. Static review findings are covered by regressions; GitHub CI is a separate merge gate.
