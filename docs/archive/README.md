# Historical material

This directory preserves earlier decisions and authoring inputs. It is not the
runtime content root and does not establish current feature support or CI status.

- `plans/`: earlier implementation plans and the former AFK implementation checklist.
- `design/`: design proposals accompanying those plans.
- `briefs/`: superseded UI execution prompt.
- `audits/`: point-in-time item and Phase 3C coverage reports.
- `content-inputs/`: original itemization and Ancient Mines authoring JSON, formerly in the repository root.
- `00_*`: original v7.1 package audit/manifest; recorded hashes describe that historical package only.

The Phase 3C content mutation generator was removed: it overwrote the base package
using old balance/runtime assumptions rather than composing current overrides. Do
not recreate it as a runtime migration. Use the maintained content validator and
composed audit exports instead.

Source paths in old command snippets are historical. References to retained moved
documents are updated for navigation, not to make old plans executable again.
