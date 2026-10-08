# Paladin P0 talent-runtime contracts — 8 October 2026

Follow-up: the five P1 gaps listed below are completed by
[Paladin resurrection and P1 contracts](paladin-resurrection-p1.md), content
0.49.0 / balance 0.39.0. The original PR inventory below records its delivery scope.

This change is stacked on top of the prior **96/96 Paladin description review** (PR #343), not a global DPS rebalance. It closes concrete combat behavior gaps using the existing authoritative CombatSession and ability/effect pipeline.

## Finished contracts (14 of the 19 complete-contract gaps)

| ID | Authored mechanic | Server integration |
| --- | --- | --- |
| H-3-4 Improved Lay on Hands | −60/120s cooldown; restores 5/10% maximum Mana to caster | Cast-complete resource event and cooldown resolver |
| H-4-2 Improved Holy Shock | −1/2/3s cooldown; +2/4/6% heal/damage crit | Offensive and healing ability variants |
| H-8-3 Last Light | −15% incoming damage to effectively healed ally for 4s | Non-recursive direct-heal event, target effect |
| P-1-4 Improved Devotion Aura | +3/6/9 percentage points armor above baseline 10% | Existing non-stacking Aura effect magnitude |
| P-2-1 Bulwark | 10/20/30% proc on actual nonperiodic damage taken; +10 block for 5s | Shared damage event, no extra engine |
| P-2-4 Guardian's Favor | −5/10s Blessing of Protection cooldown, +1/2s duration | Ability snapshot and effect duration |
| P-4-4 Shield of Faith | After natural Holy Shield expiration, absorbs 4/8% maximum HP for 5s | OnExpireActions on the owned Holy Shield effect; natural expiration only |
| P-5-4 Master of Sanctuary | Additional 2/4 percentage points damage reduction; owner Mana +3/6 when protected member takes direct enemy damage, 2s ICD | Sanctuary effect strength + nonperiodic incoming hit proc |
| P-5-2 Improved Avenger's Shield | +10/20% damage and −1/2s cooldown | Ability resolver |
| P-8-3 Perfect Sanctuary | +5 percentage points incoming damage reduction; +5 block for 10s on self-cast | Blessing effect and successful cast completion |
| R-3-4 Improved Crusader Strike | −0.5/1s cooldown; +3/6% crit | Ability resolver |
| R-4-4 Righteous Verdict | +6/12% Judgement damage; +3/6% crit | Ability resolver |
| R-7-2 Wrathful Vengeance | +10/20% bonus critical damage during Avenging Wrath | Ability and autoattack crit modifiers |
| R-7-3 Seal Mastery | +10/20 percentage points additional Seal of Command hit; Judgement refreshes **existing** Vengeance, adds no stack | Autoattack damage event and Vengeance lifetime runtime |

## Partial contracts advanced

- **P-3-4 Consecrated Ground:** +10/20% periodic Consecration damage. Bonus threat is **not** completed; it remains on the deferred list and is deliberately omitted from the published tooltip.
- **H-6-1 Surge of Light:** rank 1 makes next Flash 0.35s faster and 10% cheaper; rank 2 instant and 20% cheaper.
- **R-6-1 Art of War:** same Flash of Light rank timing/Mana treatment after eligible physical critical hit.
- Fixed pre-existing PR #343's 1 outdated Judgement composition expectation. It is tracked in the base branch.

The revised Paladin talent package is versioned **0.47.0 / 0.37.0**. Existing content version assertions and focused runtime parity cases were updated. Talents remain 96 nodes, with stable IDs, prerequisites, and spend budgets.

## Remaining to finish before claiming all contracts complete

- **H-3-3 Judgement of Wisdom:** party Mana refund proc (amount, per-ally cooldown and eligible attacks).
- **H-7-1 Judgement of Light:** group healing-on-hit proc with personal cooldown.
- **P-3-4 Consecrated Ground:** bonus **threat** on Consecration (damage bonus implemented).
- **P-6-2 One-Handed Weapon Specialization:** authoritative shield+one-handed equipment capture is missing; do not guess from inferred weapon category or passive Block Chance.
- **P-8-1 Unbreakable Bastion:** completed in stacked P0 PR #346. Content owns threshold 25% of maximum HP, mitigation 20% and 20-second ICD. The interception occurs only after block/shields and before HP/death admission; a fully absorbed hit does not start its ICD.
- **R-5-2 Fanaticism:** the Judgement critical bonus exists; Retribution-only threat reduction remains incomplete.

**Separate correctness P0:** Intercession redirect for an already-lethal incoming hit currently restores HP after the damage event and cannot annul the previously emitted ActorDied event. Repair this in the authoritative damage-routing path rather than with post-hoc healing.

## Validation

Retain full backend CI and content validator; targeted Arena production parity extends existing cooldown tests for five talents, checks Lay on Hands refund and Last Light protection, incoming-damage Bulwark, self-cast Sanctuary, and safe Vengeance refresh. This pass intentionally does not add a large benchmark suite or change cross-class combat formulas.

## Follow-up P0 admission correction (PR #346)

- **Intercession:** old damage-event healing/redirect was removed. A session-owned post-shield HP interceptor moves the rounded redirected slice before `ApplyDamage` / `ActorDied`; direct transfer is exact true damage and skips shields, mitigation, scaling and recursive interception. A lethal hit can now be saved and the protecting Paladin may die in its place.
- **Cleanse:** uses `EffectEngine.RemoveInstance` for the first active eligible negative effect rather than category-wide `Dispel`. H-7-2 bonus healing still requires a successful removal.
- **Unbreakable Bastion:** talent metadata carries `threshold: 25`, `secondaryValues: [20]`, `internalCooldownSeconds: 20` with a matching Russian tooltip. An admitted non-self large hit consumes the ICD; wholly absorbed hits and redirect transfers do not.
- **Strict validation:** each known Paladin event-hook ID must be explicitly registered and its target must match `PALADIN_<ID>`, so an unknown prefix cannot claim runtime support. This validates routing identity, not proof that every qualitative talent mechanic is complete.
- **Known remaining work:** H-3-3, H-7-1, P-3-4 threat, P-6-2, R-5-2 threat; these remain P1 and are not marked complete by this PR. Global Mana scaling and general class balance also remain P1.

The PR is not merge-ready until full CI is green and all admission/regression tests pass.
