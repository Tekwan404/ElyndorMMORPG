# Paladin talent quality audit — 2026-10-08

Scope: **96 talents**, 32 each in Holy, Protection and Retribution. This pass is for early-playtest class quality, **not** a final damage/heal/tank rebalance. Preserve node IDs, locations, ranks, point budgets, prerequisites, existing damage formulas and role identity.

## What was changed

1. **All 96 player-facing talent descriptions** were rewritten as readable Russian tooltips with consistent terminology for threat, damage, cooldowns, healing, Seals, Blessings and Holy Shield. Existing approved descriptions with missing numbers remain qualitative; this does **not** fill unknown gameplay contracts.
2. **Retribution R-2-1 Improved Judgement:** cooldown benefit from 0.5 / 1.0 / 1.5 sec to **0.75 / 1.5 / 2.25 sec**; code and tooltip synchronized. Three talent points now provide a more tangible rotation improvement against the 8s base cooldown without reducing it to a spammable one-second button.
3. **Holy H-7-2 Sacred Cleansing:** the 30 / 60 healing bonus now requires that CLEANSE actually removed a dispellable effect. Previously this extra heal occurred even with no effect removed. This is a gameplay truthfulness and unintended free-heal fix; its magnitude stays unchanged.
4. Content/balance revisions **0.46.0 / 0.36.0**; synchronized existing release expectations. One focused no-dispel test was added to the existing Arena/production parity suite and its Judgement cooldown expectation updated. No new broad test suite.

## Branch assessment

### Holy — strong identity, watch efficiency

**Retain:** H-2-1 Illumination (critical direct-healing refund, up to 75%), H-3-1 Divine Favor guaranteed critical heal, H-5-2 Beacon, H-6-1 Surge of Light instant Flash at rank 2, H-9-1 Herald free Shock. This reads as an active healer with meaningful emergency buttons.

**Watch:** H-8-1 raises maximum critical-heal refund to **90%**, and H-5-3/H-8-2 stack Beacon transfer bonuses. Do not nerf them without observed effective HPS, overheal, mana sustain and group survivability. H-7-2 used to grant bonus healing even when nothing was dispelled; now it correctly requires a successful cleanse. H-6-3 Divine Replenishment's actual authored ability grants **30 Mana immediately**, followed by an 8s healing penalty of 10%; its former text incorrectly implied Mana recovery over several seconds.

### Protection — solid kit, but incomplete contract values

**Retain:** P-1-1 Toughness scales only equipment armor; P-1-2 block value; P-3-1 Holy Shield retaliation; P-5-3 Ardent Defender only below 35% HP; P-7-2 damage reduction during Consecration; P-7-3 ally protection.

**Watch:** P-4-1 extra hit, P-6-3 block healing and P-9-1 party shield can reward active blocking. Avoid stacking unbounded resource/shield loops. P-7-3 Intercession currently redirects damage after the original hit: code comments note an existing edge case where an already-lethal ActorDied event is authoritative, so the damage redirection does not retroactively save the ally. **This remains a known P0 correctness issue**, outside this lightweight talent-text pass.

### Retribution — functional two-handed rhythm

**Retain:** R-1-4 Seal of Command, R-2-2 Crusader Strike, R-3-2 Vengeance (up to 3 stacks), R-5-1 Templar's Verdict, R-6-2 Divine Storm group heal, R-7-1 Avenging Wrath, R-8-3 Divine Purpose and R-9-1 Incarnation.

**Changed:** R-2-1 has been strengthened modestly because three points for 1.5s off an 8s Judgement cooldown was a weak investment. New maximum reduction is 2.25s. The remaining Retribution damage coefficients and cooldowns are unchanged.

**Watch:** multi-layered Vengeance, Divine Purpose, Avenging Wrath and guaranteed Verdict critical hit may create burst spikes. Measure real solo/boss DPS, 1v1 and Mana starvation before changing them. PvP/party utility must count toward class value; raw DPS is not the only target.

## Important existing implementation gap

The 2026-10-01 report `docs/development/paladin-production-numeric-gaps.md` already identified **19 missing complete numerical contracts**, plus 3 partial cases, including support talents in every branch. Their descriptions mention benefits that do not all have fully authored magnitude, proc conditions and/or cooldown contracts in content. This pass **does not invent missing effect sizes or mark all 96 talents fully gameplay-implemented** merely because validation accepts their metadata. Use that inventory for a separate P0/P1 *mechanical completeness* implementation pass.

Priority follow-ups:
- **P0 gameplay correctness:** Verify all 19 incomplete contracts against current runtime and close no-op or placeholder behavior; address Intercession lethal-hit semantics.
- **P1 player-facing quality:** Check Aura non-stacking across multiple Paladins, party targeting, Consecration threat, Beacon overheal accounting, Seal ownership and boss-safe CC.
- **Later, with real samples:** collect combat metrics grouped by level/gear/talent spec for HPS/overheal, tank damage prevented/block uptime/deaths, Retribution ST/AoE DPS, Mana/OOM, and Arena win rates. Do not normalize numbers without sample sizes.

Validation: preserve existing backend/content tests and one focused cleanse regression; no synthetic full-DPS test matrix was added.
