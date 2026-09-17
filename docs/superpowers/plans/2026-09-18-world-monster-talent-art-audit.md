# World, monster, and talent art audit

## Scope

- Make the world map scannable with numbered markers and a synchronized readable location index.
- Audit authored monster portraits against available assets; correct misleading assignments and ensure every combat monster resolves to a relevant available portrait.
- Align active ability fallback art with the current talent that unlocks each ability; correct Hunter's Mark and repair garbled Archer talent copy.

## Steps

1. Add focused regressions for map indexing, monster portrait coverage, and Hunter's Mark icon consistency.
2. Correct map presentation and localized labels without changing travel or encounter rules.
3. Repair monster art mappings using the existing asset catalog; use location/archetype fallbacks only when no dedicated portrait exists.
4. Correct talent/ability icon source mappings and Archer text encoding.
5. Run focused frontend tests and build, content validation, backend build/tests, and review the final diff.

## Boundaries

- No gameplay, map connectivity, combat, or reward-rule changes.
- No new image generation; reuse supplied artwork.
- Keep verification focused and report portraits that still reuse an archetype because no dedicated source art exists.
