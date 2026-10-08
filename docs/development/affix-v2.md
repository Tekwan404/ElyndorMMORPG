# Affix V2

Content 0.50.0 / balance 0.40.0 publishes generation version 2 for all 711 rolled
templates. The effective catalog has 878 definitions, including 167 fixed items.
The current item release is authored directly in `content/items/`: one definition
per ID. Updates replace that definition; they do not add ordered hotfix overlays.
Git retains previous releases. Category boundaries remain useful for editing.
The base package and item files carry the current 0.50.0 / 0.40.0 release metadata.

## Eligibility and generation

`content/itemization/system.json` defines shared eligibility rules, role pools,
selection weights and mutually exclusive stat groups. `ItemAffixEligibilityPolicy`
provides the candidate set for both drops and Reforge. Validation checks that
every reachable random selection can fill the maximum affix count, including
after guaranteed stats and exclusive groups have consumed candidates.
Disjoint exclusion groups have a direct capacity calculation; overlapping groups
use a local cache of remaining candidate states, avoiding repeated permutations
during content loading without introducing a global cache.

- Holy has a healing pool; Guardian, Protection and Beast Mastery have shared
  role pools. Unique pools change only where the Holy role contradicts the old pool.
- Mage pools exclude Attack Speed, which affects autoattacks, not casts/channels.
- Unrestricted accessory/world pools allow one primary and one penetration type.
  Paladin hybrid pools still permit Strength and Intellect together.
- Block Value requires a shield. Weapon Damage requires a MainHand weapon with
  damage endpoints. Class-specific templates filter incompatible class stats.
- Selection weights are distinct from stat power costs. Mage pools at item levels
  50–60 favor resource and core stats. Reforge uses the instance's concrete item
  level for these weights, matching drop selection.

All existing stat power costs are unchanged. V2 budget normalization uses eligible
stats and rounds expanded slot multipliers upwards before step flooring. This fixes
four shield Stamina ranges that collapsed to 1..1 through decimal rounding.
The L55 Mage chest guarantees Mana instead of Crit: the selected leveling loadout
otherwise fell below the existing L60 sustained-rotation requirement. Its budget
and number of guaranteed affixes stay unchanged.

## Player state and economy

No schema migration is needed. At the owner's request, this beta release does not
introduce a separate legacy stat-cost table or a migration guarantee for V1 loot.
The change itself does not wipe a database. Existing persisted values are not
mass-recalculated. Reforge still uses its persisted slot envelope and retains
the existing birth-quality rules, transaction boundaries and idempotency checks.

## Combat consumers

PvE and Arena use the shared character-derived state, DamagePipeline and
HealingPipeline. Ability coefficients and avoidance flags still determine which
stats affect a particular action; pool eligibility does not change combat formulas.

| Stat | Player damage / defense | Healing | Archer companion |
| --- | --- | --- | --- |
| Strength | AP; shield Block Value with a real shield | No direct effect | Through inherited owner AP |
| Agility | AP, Crit, Dodge with diminishing returns | Through player Crit | Through inherited owner AP |
| Intellect | SP, MR and Mana | Through SP | Through inherited owner SP when profile coefficient is nonzero |
| Stamina | HP and MR | Survival only | Inherited Stamina contributes companion HP |
| Attack Power | Physical ability/autoattack coefficients | No | Inherited AP |
| Spell Power | Spell coefficients | Heal coefficients | Inherited SP when configured |
| Crit / Crit Damage | Crit-enabled attacks/spells | Crit-enabled heals | Owner secondary stats are not inherited |
| Accuracy | Miss check, not Dodge suppression | No | Companion uses its own profile/talent accuracy |
| Attack Speed | Autoattack interval, capped at +50% | No cast-speed effect | Companion has its own interval modifiers |
| Dodge | Dodge-enabled incoming physical hits | Survival only | Owner Dodge is not inherited |
| Armor / MR | Matching damage mitigation | Survival only | Companion uses its own profile/talents |
| Armor / Magic Penetration | Matching outgoing damage type | No | Owner penetration is not inherited |
| HP | Player survival | Does not increase heal amount | Flat owner HP is not inherited |
| Max Resource | Mana/Rage/Focus capacity | Longer casting sustain | Owner resource is not inherited |
| Weapon Damage | Weapon damage endpoints and matching actions | No | No direct inheritance |
| Block Chance / Value | Shield-enabled physical mitigation | Survival only | No owner block inheritance |

Primary conversion and caps: `CharacterStatCalculator` and
`CharacterResourceProfileResolver`. Companion inheritance:
`ArcherCompanionRuntimeResolver`. Autoattack speed application:
`CombatSessionFactory`. Arena setup uses the same derived character state before
constructing arena fighters. A zero effect on healing/pets is not a broken stat;
it is a reason to keep that stat out of a specialized healing pool.

## Reproducing the audit

```powershell
dotnet run --project tools/Elyndor.ContentValidator --configuration Release -- --audit-affixes --export-analysis=.elyndor/affix-audit content/package.json
dotnet run --project tools/Elyndor.ContentValidator --configuration Release -- --audit-mage-mana --affix-mana-sample --export-analysis=.elyndor/mana-audit content/package.json
```

`01-items.json` includes acquisition references. `05-affix-inventory.json` includes
every effective definition and real generator min/max/step envelopes for every
declared item level. `06-affix-balance.json` contains 1,260 equal-budget cases at
levels 10/20/30/40/60, ordinary, near-cap and capped stats. It uses production
stat, damage, healing and companion calculations with documented benchmark inputs.

These are isolated marginal-stat comparisons, not a claim of complete encounter
or arena balance. The mana audit separately measures actual Mage rotations until
the first unaffordable spell; resource capacity has no instantaneous DPS/HPS gain.
Proc rotations, target behavior and healing overheal require encounter-specific
benchmarks. Published weights were retained rather than inferred from one scenario.

The [release audit](../archive/audits/2026-10-08-affix-v2/README.md) records the
baseline conflicts, complete inventory, ranges, numeric comparisons and mana sample.
