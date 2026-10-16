# Effect presentation and compact HUD polish

Repair the existing HUD, premium product cards and effect inspection and complete verified no-op effects. Keep the screenshot's compact identity/currencies row,
full-width paired resource bars and thin experience bar; prevent overflow on phones.

Project active effects through one Core snapshot factory in PvE and Arena. Preserve
explicit metadata, derive numeric mechanics from the live definition and remaining
shield amount, and name runtime-only class states. Expose instance/source identity,
kind, disposition, application time and stack limit as additive optional contracts.
Describe special class markers at their authoritative implementation where needed.
No invented numerical bonuses, no frontend game-rule calculations.

Separate beneficial/harmful effects visually; use existing spell/item artwork and
semantic fallback icons, readable inspection, remaining time and stack/charge count.
Hide expired effects, keep inspection stable across refresh, support tap/keyboard,
and preserve critical boss warnings. Fix premium item sizing and badges so labels
never cover artwork. Verify backend projections, runtime marker descriptions, mobile
320/390px layouts and desktop screenshots plus regression suites and final review.


Confirmed runtime repairs (2026-10-16): Wisdom doubles only base mana regeneration
for its existing ten-second duration, as explicitly selected by the user. Existing
regeneration bonuses remain additive to that base; Focus/Rage are unaffected.
Concentration applies its existing 8% cast reduction to casted spells, including
allies, without changing channels, physical casts or ongoing casts. Matching auras
retain per-caster ownership and use the strongest multiplier without stacking.
Divine Replenishment now applies its authored .90 outgoing healing multiplier for
eight seconds. Hammer stuns ordinary targets for its authored three seconds while
bosses only receive the existing interrupt. Repentance stuns ordinary targets for
four seconds; the user selected a 10% outgoing damage reduction for bosses for
those four seconds. These values live in the existing Paladin content package.

Verification includes authored-content runtime tests, aura replacement with two
Paladins, source/instance projections and mobile browser inspection. No migration
or new dependency is required; optional snapshot fields preserve old payloads.
