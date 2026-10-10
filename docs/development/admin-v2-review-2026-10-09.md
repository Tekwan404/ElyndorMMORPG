# Admin V2 — operational UX and reliability pass (PR #361)

This pass keeps the existing admin application and server authorization model. It does not change the player-facing game's navigation, combat rules, or content publishing semantics.

## What changed

| Area | Before | After |
| --- | --- | --- |
| Mobile navigation | Sidebar hidden entirely below 720px; unreadable dot-only menu on tablets | Sticky, horizontally scrolling, touch-friendly nav on phones; text labels remain on tablets |
| Dashboard | Placeholder about future Content Workspace migration | Current live summary and working links to Content Workspace, Items and GM Forge |
| Previously disabled routes | Players / Server / Simulator / Drafts / Revisions / Releases inaccessible in the shell | Player inspector and server summary; direct scrolling links to existing content tools |
| Player inspection | No read-only web lookup | SUPER_ADMIN-only `GET /api/v1/admin/players/{telegramUserId}` for name, class, level, location, vitals, gear and GM-item counts |
| Editing JSON | Switching entity or editor mode silently lost un-applied JSON changes | Confirmation before discarding pending JSON; shell unsaved guard includes pending entity JSON |
| Draft reset | One click discarded unsaved work | Explicit confirmation; pending local draft flushed on navigation/unmount |
| Validation | Results could become stale after subsequent draft edits | Validation resets on draft changes and ignores responses for stale payloads |
| Session | Partial login could leave JWT cached; content-panel 401 didn't clear the shell | Rollback auth token on dashboard-load failure; expiration and 401 invalidate all admin views |
| GM Forge | Network timeout could lead to double minting on manual retry | Reuse the same request UUID for retries of the unchanged specification, plus clearer validation/errors |
| Player → Forge | Manual Telegram ID copy | One click to open GM Forge with the inspected player selected |

## Safe boundaries

- Player inspection is **read-only**. No direct edit/ban/give-gold action was added to the browser.
- Authentication remains on the server: `SUPER_ADMIN` is required for privileged API routes.
- A GM-forged item remains bound and marked DEV. It is intentionally not merchant-tradeable, auctionable, or exchangeable.
- GM gear can still earn combat rewards when used in the live world. Use a test character until reward isolation is built.
- The Server page exposes basic API status and content metadata, not private machine health or performance counters.
- Content changes continue through validate → immutable revision → publish/rollback; no direct unreviewed live edits were introduced.

## Manual smoke checklist

1. On a 375px mobile viewport log in; scroll the admin navigation and open Content, Players, Server and GM Forge.
2. In Content Workspace select a template; switch to JSON, edit but don't apply, and attempt to change entity/mode/section. Declining must preserve the editor.
3. Change the content draft after successful validation. The validation result must return to "Не проверено".
4. Reload after editing the draft, confirm autosave restoration, and test explicit Reset confirmation.
5. Search Players by Telegram ID, then open GM Forge from the result; the target must be pre-populated.
6. Simulate a GM Forge timeout and retry without modifying fields; the same request ID must be used.
7. Expire a JWT or receive a 401 from any privileged page; the shell must return to sign-in.
8. Validate, create a revision, preview its diff and publish/rollback only after review.

## Verification

Automated tests cover player lookup authorization, admin shell operations navigation, pending JSON edit protection, player search, GM Forge retry behavior and token invalidation. The authoritative build status is the GitHub Actions result for the latest PR head; this document does not claim live deployment testing.
