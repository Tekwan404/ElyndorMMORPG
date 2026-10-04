# Verified artifact → incremental VPS deployment

## Pipeline

`ci.yml` builds backend, web and admin once. Unit/integration tests use that
backend build. `dotnet publish --no-build` assembles the production directory;
browser E2E starts this published backend and serves its bundled frontend.
The directory receives `REVISION` and `RELEASE-MANIFEST.json` before E2E and is
verified unchanged after E2E. Only a successful main push publishes the
`elyndor-linux-x64` artifact (directory, not nested tarball; retention 7 days).

`package-production.yml` downloads the artifact from that exact successful CI
run. Manual dispatch requires successful push CI for the exact main revision;
it never rebuilds or substitutes another commit. Expired/missing artifacts fail
closed: rerun CI for the desired main revision. Outdated revisions are rejected
before transfer. Production deployments are serialized and are not cancelled
halfway through activation when a newer commit arrives.

Rsync uses `--checksum --copy-dest=/opt/elyndor/current` to populate a fresh
random staging directory under `/tmp`. Unchanged files are copied locally on
the VPS, changed files cross SSH, obsolete files are absent. No `--inplace`,
hardlinks, or writes into the live release. The installer copies staging into a
root-owned release, verifies every file/checksum/revision, atomically swaps
`current`, restarts and checks readiness. Failure restores the previous symlink.
Temporary staging is cleaned on workflow exit; current and rollback releases
are never pruned. Application rollback does **not** roll back database migrations;
backups and backward-compatible schema changes remain required.

## One-time VPS setup before merging/enabling this workflow

Run as root, using files from the reviewed revision, not an unreviewed branch:

```bash
apt-get update
apt-get install -y rsync python3
revision=REPLACE_WITH_REVIEWED_40_CHARACTER_COMMIT_SHA
deploy_user=tekwan # use the account configured in ELYNDOR_DEPLOY_USER
install -d -m 755 /usr/local/lib/elyndor
curl -fsSL "https://raw.githubusercontent.com/Tekwan404/ElyndorMMORPG/$revision/deploy/release-manifest.py" -o /usr/local/lib/elyndor/release-manifest.py
curl -fsSL "https://raw.githubusercontent.com/Tekwan404/ElyndorMMORPG/$revision/deploy/install-release.sh" -o /usr/local/sbin/elyndor-deploy-release
chown root:root /usr/local/lib/elyndor/release-manifest.py /usr/local/sbin/elyndor-deploy-release
chmod 644 /usr/local/lib/elyndor/release-manifest.py
chmod 755 /usr/local/sbin/elyndor-deploy-release
usermod -aG elyndor "$deploy_user"
```

Fresh SSH sessions pick up group membership. The deploy account must be able to
read the existing release for delta transfer; this does not grant access to
`/etc/elyndor/elyndor.env`. Review existing sudoers and allow ONLY the helper:

```text
tekwan ALL=(root) NOPASSWD: /usr/local/sbin/elyndor-deploy-release
```

Use `visudo` and the real account name. Keep SSH known-hosts pinned with
`ELYNDOR_DEPLOY_KNOWN_HOSTS`. Verify `sudo -n ...` works over a new SSH session.
Do not grant general passwordless root shell/rsync access. The helper remains
compatible with archive input; manual `build-release.sh` is a fallback, not part
of automatic delivery. Install the same helper as `elyndor-deploy` if that alias
is used for manual deployments.

## Verification / timing

`sudo python3 -m unittest discover -s tools/deployment -p 'test_*.py'` on a
disposable Linux runner exercises checksum/revision validation, corruption,
restart/health rollback, inode isolation and real rsync delta transfer. Tests
refuse to touch an existing `/opt/elyndor`. CI keeps all existing backend,
frontend, admin, content and E2E checks. Deployment logs rsync transfer statistics.

The repeated builds and full VPS archive transfer are removed. A 4–5 minute
end-to-end target is an estimate, not a guarantee: measure CI/test duration,
artifact download/upload, rsync bytes and startup/migration time after rollout.
