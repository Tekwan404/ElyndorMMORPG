# Compact battle presentation

Remove the duplicate top BattleHeader from PvE/training and PvP. Keep one readable
HP/resource presentation attached to each actor via shared CombatActorStatus. PvE
status buttons are independent of scaled sprites; PvP keeps status inside CharacterFigure. Preserve server snapshots, actor IDs, target
selection (including dead allies), aggro, casts, effects, reconnect and rewards.
Keep hidden formation members selectable through a compact reserve strip inside the
battlefield; do not duplicate visible actors in that strip.

SkillPanel owns five columns and two rows of circular 44–52px icon buttons, no name
or cost captions. Preserve accessible names, cooldown/queue/disabled feedback, long
press (without casting) and F1 inspection. Page kits larger than ten instead of hiding
skills behind an expanding tall list. Keep existing skill art; no new generation is
needed because this is component framing and layout.

Verify failure-first component tests for actor vitals, resource updates, no header,
selection and paging/long press. Run relevant PvE/PvP frontend regressions, typecheck,
build, lint and Playwright layout checks for solo/training/party/PvP at phone widths.
No database, content balance, server combat or reward changes.

The user additionally requested depth staging: center the hostile actor in the far
background and spread the group across the foreground and second plane. Keep the local
hero at foreground center and stable actor positions when aggro/selection changes.
Boss/elite art scales within the background space; vitals remain attached and readable.
This is presentation geometry only and introduces no server distance/facing mechanic.

Implementation review caught and resolved two crowded cases: selected-enemy vitals
now sit below the multi-enemy target strip, and the companion has a separate upper
side panel instead of sharing the foreground actor status area. Transparent ally
container layers pass clicks through to the background enemy; actual figure slots
and independent status buttons remain interactive.

Validation: 55 focused component/screen tests and 97 combat/PvP regression tests
passed. Frontend typecheck/build passed; lint reports 0 errors and 22 pre-existing
warnings. Browser checks cover 320/390/580px, solo/3/5 party members, long training
resource values, 5x2 skills, paging and stable targeting. Backend/schema/content
checks are not required for this presentation-only slice; full repository CI still
runs on the pull request.
