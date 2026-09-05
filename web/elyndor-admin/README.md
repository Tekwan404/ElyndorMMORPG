# Elyndor Admin V2

Standalone desktop-first administration SPA.

Development:

```bash
npm ci
npm run dev
```

The Vite server listens on port 5174 and proxies `/api` to Elyndor.Server.

Production:

- built into `frontend-admin/` by `deploy/build-release.sh`;
- exposed through `admin.elyndor.su`;
- Caddy rewrites non-API requests to the internal `/__admin` static route;
- game.elyndor.su blocks the internal admin static route.

Authentication uses a server-side Telegram allowlist and a one-time six-digit code.
Access tokens remain in browser memory only.

## Content Workspace

The existing production-safe content workflow is now hosted inside Admin V2:

```text
Monsters / Items / Abilities / Talents / Locations / Classes / Loot / Merchants / Sets
→ edit structured form or JSON
→ validate
→ immutable revision
→ review diff
→ publish
→ release history / rollback
```

The legacy game `/admin` route remains temporarily available until the standalone
host is deployed and verified in production.

## Draft safety

Admin V2 protects local content work before an immutable revision is created:

- content edits are autosaved to browser local storage;
- autosave is bound to the exact LIVE payload SHA;
- a reload restores only a draft created from the same LIVE version;
- browser close/reload warns while the workspace is dirty;
- leaving the Content Workspace asks for confirmation;
- Reset, Publish and Rollback clear only the autosave that belongs to their LIVE base.

The server-side immutable revision remains the durable collaboration boundary.
Local autosave is a recovery mechanism, not a replacement for Save draft.
