# Repository maintenance

This pass is approved for documentation, repository navigation and offline tools.
Runtime modules, migrations, game content, source artwork and agent configuration
are not cleanup targets. Absence of an import is not proof that an offline tool or
asset is unused.

## Ownership rules

- Keep runtime definitions under `content/`, not beside build configuration.
- Keep human inspection exports under `content-analysis/`; do not load them in runtime.
- Put offline art utilities in `tools/assets/`, launcher scripts in `tools/dev/`
  and repository checks in `tools/repository/`.
- Keep root files limited to entry points and build/editor configuration.
- Preserve source art, agent configuration and existing persistence migrations.
- Archive old plans instead of presenting their completion counts as current status.

## Verified cleanup scope

55 files were relocated, including earlier plans/designs, root input JSON, the UI
brief, asset utilities and the local .NET tool manifest. Three obsolete tools and
one duplicate Source of Truth entry page were removed; their prior versions remain
in Git history. Four root files moved to their appropriate owners.

The offline batch slicer no longer recursively deletes its output folder. The grid
slicer no longer squares alpha when pasting resized RGBA cells. Regression tests
use temporary synthetic inputs; shipped game artwork was not regenerated.

Inspection exports were refreshed from composed content 0.36.0/balance 0.29.0
using the existing exporter, including its combat-balance report. No authored
definitions or balance values were edited.

No unused runtime code or artwork was deleted based merely on naming or an absent
static import. Class partials and legacy compatibility paths require their own
characterization/extraction tasks, not bulk file cleanup.

## Verification plan

1. Add and run failing repository-layout checks: unexpected root files, tracked
   generated/secrets files, case-sensitive local navigation links and duplicate
   ignore rules. Use `node --test tools/repository/check-layout.test.mjs`.
2. Archive old briefs/input packages and historical plans without changing their
   content meaning. Fix references to moved files. Keep current combat/UI plans.
3. Group reusable image tools under `tools/assets`; remove the obsolete PNG
   slicer, hardcoded destructive converter and stale Phase 3C mutation generator.
4. Replace the root README with current navigation/setup, remove the stale phase
   restriction from AGENTS, and separate historical design contracts from runtime.
5. Run layout tests/check, launcher syntax check, Release build/tests and content
   validation. Review renames and verify protected trees have no diff.
6. Publish a maintenance PR. Merge the combat hardening and maintenance PRs only
   after their respective required CI succeeds. Do not merge unrelated branches.

No database transaction, API, gameplay, UI or runtime dependency changes. The
existing Pillow dependency is pinned for reproducible offline-tool tests.
