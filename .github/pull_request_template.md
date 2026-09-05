## Summary

- 

## Scope

- [ ] Gameplay or content semantics changed
- [ ] Database schema / persistence changed
- [ ] Frontend or player flow changed
- [ ] Admin / content platform changed
- [ ] Docs / housekeeping only

## Safety

- [ ] No secrets, local config, logs, build artifacts, `*.patch` or `*.diff`
- [ ] No unrelated changes or scope creep
- [ ] Source of Truth updated when behavior changed
- [ ] Server-authoritative and content-versioning invariants preserved where applicable
- [ ] Admin changes preserve `draft -> validate -> revision -> review diff -> publish`

## Verification

- [ ] Relevant local checks were run
- [ ] Content validator was run when content changed
- [ ] Frontend unit/build checks were run when frontend changed
- [ ] E2E was run when a player flow changed
- [ ] GitHub CI is green

## Notes

- 
