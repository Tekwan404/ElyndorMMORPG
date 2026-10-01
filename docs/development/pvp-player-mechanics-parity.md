# Arena player-mechanics parity

Implementation branch: `feat/pvp-arena`. Starting HEAD and integrated main:
`b74db7f7fbe19121c976b976eed5921e0bbdccca`.

## Runtime boundary

Production entrants come from `CombatSessionFactory.CreateArenaPlayerAsync` and
the existing character-derived-state pipeline. `ArenaFighterAssembler` retains
owner hooks, includes talent-unlocked abilities and provides raw definitions.
It rejects genuinely unknown or deferred mechanics rather than dropping them.

Each Arena player has a mechanics-only `CombatSession` context referencing the
same actor and ability runtime as the Arena orchestrator. Existing class partials
resolve player mechanics; Arena remains the only scheduler and terminal-state
owner. The opponent is a real player, not a disguised monster. These contexts do
not start monster AI, encounters, companions, rewards or PvE finalization.

Source hooks execute once. Forwarded events notify only defender reactions and
are not appended again. Periodic effects and delayed actions execute only in the
Arena timestamp batch. Ability projection is pure: it can show a zero-cost proc
without consuming it or publishing another event.

Hostile controls pass through `EffectEngine`'s optional application policy,
including effects applied directly by class handlers. Diminishing returns commit
only after successful application: 100%, 50%, 25%, immunity, then reset using the
existing PvP window. PvE actors without a policy retain their normal rules.

The low-level manually assembled fighter path remains for generic kernel tests.
It is not the production character assembly path.

## Confirmed fixes

- Production class handlers, unlocked abilities, owner/defender procs and dynamic
  ability modifiers are connected without copying the class combat engine.
- Mana Shield pays in the shared damage pipeline, cannot absorb beyond available
  Mana and cannot spend the same Mana twice across multiple damage actions.
  Magic Absorption refunds only actual payments. Expired shields cannot absorb
  a tick at their expiration timestamp.
- Arcane Power grants both existing free-spell charges. Archmage counts paid
  utility spells and applies its Presence bonus without requiring Arcane Power.
- Fire burns receive their existing Fire/spell-power bonuses; execute modifiers
  use the opponent's state at impact.
- Regeneration integrates rate-change boundaries, including initial idle
  Deep Meditation, rather than applying an interval's final rate to all of it.
- Counterspell attributes interruption to its caster and the interrupted player;
  deferred deaths retain critical, weapon and unblockable metadata.
- Paladin cost/crit/cooldown/cast modifiers, real equipment conditions, defensive
  windows and each Wrath activation use shared runtime handlers. Divine Purpose
  discounts Verdict before admission, expires correctly and is consumed once.

## Verification interpretation

The composed content contains four trees, twelve branches and 384 nodes:
Warrior Berserker/Guardian/Warlord; Mage Fire/Arcane/Frost;
Archer Marksmanship/Beast Mastery/Survival; Paladin Holy/Protection/Retribution.

`ArenaAllTalentBuildExecutionTests` exercises every max-rank node individually,
each full branch and each full-tree stress build: 400 builds and 1151 ability
attempts. Stress builds intentionally exceed the learning-point budget.
This detects assembly, admission, casting and advancement failures; it does
**not** prove the exact numerical behavior of every conditional talent.
Focused production-path tests separately cover damage/cost modifiers, procs,
active talent abilities, defensive reactions, resources, expiration and CC.

Companion-dependent hooks and abilities are deliberately dormant in 1v1.
The owner can still enter Arena. Ally-only Intercession has no valid other ally
in 1v1; self-targeting it is rejected without rejecting the whole build.

## Remaining contract audit

Full parity must not be inferred from the capability catalog alone. The existing
Paladin runtime catalog recognizes some qualitative definitions whose complete
numeric contracts are not supplied by content. Missing magnitudes, proc chances
or durations must not be invented in an Arena-only implementation. These nodes
need an explicit content/runtime audit before claiming every Paladin effect is
fully operational. See `30_PALADIN_RUNTIME_AUDIT.md` for the original prohibition
on guessing missing values and `paladin-production-numeric-gaps.md` for the exact
nineteen incomplete contracts and partially defined mechanics.

No frontend, content balance, database schema or companion implementation is
changed by this workstream.

## Final verification (2026-10-01)

- `dotnet test Elyndor.slnx --configuration Release`: 935 unit tests and
  400 PostgreSQL integration tests passed; zero failures or skips.
- Content validator with `--strict-talents`: passed (4 trees, 12 branches,
  384 nodes, 441 modifiers). Catalog validation is not proof of behavior for
  the nineteen incomplete Paladin contracts listed separately.
- `git diff --check`: clean. Frontend checks were not rerun: frontend is unchanged.
- No merge to `main`; this is a testable player-mechanics integration, not a
  claim that the missing Paladin content contracts have been completed.
