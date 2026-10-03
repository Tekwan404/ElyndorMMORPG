# UI Kit production migration: Quests, Party, Dungeon

Continue `feat/elyndor-ui-kit`; preserve gameplay, API, rewards and combat layout.

## Findings and bounded changes

- Party confirmation currently closes before the authoritative response. Keep it
  open/busy until success and show a retryable error inside the same modal.
- Party initial load must not offer Create before membership is known. Reuse
  system loading/empty/error and the existing panel title slot.
- Party and dungeon actions need explicit button feedback and conflicting-action
  guards. These are local view states, not a new server mutation dispatcher.
- Dungeon mutation error currently replaces the entire run card. Keep known
  progress visible; distinguish load failure from command failure.
- Permanent dungeon leave needs confirmation, distinct from temporary city exit.
- Quest journal needs shared tabs/states, scoped action progress, honest success
  feedback and abandon confirmation. Keep canonical server quest state.

## Verification

- [x] Reproduce party premature-close and dungeon hidden-progress bugs in tests.
- [x] Party/Dungeon implementation and targeted regressions.
- [x] Quest migration and targeted regressions.
- [x] Full frontend unit suite, lint, typecheck/build.
- [x] Real browser narrow-screen inspection using mocked data (not real-server certification).
- [x] Final diff review and pass report.

Secondary commerce/profession/AFK screens remain deferred. No new dependencies,
content edits, backend contracts or gameplay calculations.
