# Affix V2 release audit — 2026-10-08

Baseline: main `da85ac5b`, content 0.49.0 / balance 0.39.0.
Result: content 0.50.0 / balance 0.40.0, 878 effective items, 711 rolled / 167 fixed.

| Artifact | Coverage |
| --- | --- |
| [inventory.csv](inventory.csv) | All 878 IDs: level, class, slot, structural stats, guarantees, pool, count profile, source file and acquisition references |
| [ranges.csv](ranges.csv) | 21,473 real generator envelopes, with published/effective budget, min/max/step for each item level and stat |
| [baseline-conflicts.csv](baseline-conflicts.csv) | Exact IDs for four collapsed shield ranges, 180 Mage attack-speed pool mismatches, 28 Holy role mismatches, 64 unrestricted mixed-primary/penetration pools |
| [stat-balance.csv](stat-balance.csv) | 1,260 equal-budget production-path DPS/HPS/EHP/companion cases, including near-cap and capped cases |
| [mage-mana.csv](mage-mana.csv) | 24 Mage rotation samples at levels 10/18/20/30/35/40/55/60 |

121 templates changed shared pool assignment. Existing IDs, art, set membership,
set size and bonus thresholds stay intact. The L55 Mage chest changes its third
guarantee from Crit to Mana to keep the existing sustained rotation contract.
No stat power cost changes and no new dependencies were needed.

Item consolidation removed 15 duplicate definitions (893 authored records became
878), with a full definition-by-ID equality check before the intentional chest
balance adjustment. Three obsolete hotfix files were folded into their canonical
owners. Field-world, raid equipment and branch Unique files lost their ordering
prefixes. A test now enforces a single authored source per item ID.

The engineering note [describes the policies, combat consumers and benchmark
limits](../../../development/affix-v2.md). JSON exports remain reproducible with
the ContentValidator; CSV snapshots avoid committing duplicated full packages.
