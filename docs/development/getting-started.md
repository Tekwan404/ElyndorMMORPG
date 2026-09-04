# Local development

## Requirements

- .NET SDK 10.0.302 or a newer 10.0 patch accepted by `global.json`.
- Node.js 24 LTS. The current frontend also supports Node.js 22.18 or newer.
- Docker Desktop or another Aspire-compatible container runtime.

## Start the complete local stack

Install frontend dependencies once:

```powershell
npm ci --prefix web/elyndor-web
```

Then start PostgreSQL, the ASP.NET Core server, Vue/Vite, and the Aspire dashboard:

```powershell
dotnet run --project apphost/Elyndor.AppHost
```

The AppHost generates the local PostgreSQL password and passes the `game` connection string to the server. No database password is stored in the repository.

## Quick launcher and Tailscale public test

Double-click `Elyndor-Control.cmd` to manage the complete Telegram test runtime from one menu:

- save or replace the Telegram Bot Token;
- configure one or more Telegram administrator user IDs;
- start the game through Tailscale Funnel;
- open the local Aspire Dashboard with server logs, traces, metrics, and resource health;
- inspect API/PostgreSQL status, CPU, memory, and URLs;
- restart or stop Elyndor and its Funnel route.

The Bot Token is entered with hidden input and stored under `.elyndor/` using
Windows DPAPI encryption for the current Windows user. The launcher generates a
separate JWT signing key. Neither secret is committed to Git.

To create or retrieve a token, open `@BotFather` in Telegram and use `/newbot` or
`/token`. In Elyndor Control Center select `6`, paste the token, then select `7` to configure
administrator numeric Telegram user IDs. Multiple IDs are entered as a comma-separated list, for
example `123456789,987654321`. Select `1` to start the public test runtime.
After Funnel starts, copy the printed public HTTPS URL into:

```text
@BotFather -> /mybots -> your bot -> Bot Settings -> Menu Button -> Configure menu button
```

The public URL must use HTTPS. Tailscale must be installed, signed in, and allowed
to use Funnel for the current tailnet.

The control center intentionally exposes only the Telegram/Tailscale workflow. A
loopback development mode still exists internally for automated tests.

For automation, the original PowerShell entrypoint remains available. `Start` and
`Restart` always use the encrypted Telegram configuration and open monitoring:

```powershell
.\tools\dev\Elyndor.ps1 -Action Start
```

Public mode disables development authentication, OpenAPI, and Vite, then points
Tailscale Funnel at the single ASP.NET origin on port 5080. It prints the stable
`https://<node>.<tailnet>.ts.net` URL to configure as the Telegram Mini App URL.
PostgreSQL and the Aspire Dashboard are never routed through Funnel.

Inspect resources, reopen the monitoring board or game, and stop the runtime with:

```powershell
.\tools\dev\Elyndor.ps1 -Action Status
.\tools\dev\Elyndor.ps1 -Action Dashboard
.\tools\dev\Elyndor.ps1 -Action Game
.\tools\dev\Elyndor.ps1 -Action Stop
```

The Dashboard URL is stored locally in `.elyndor/runtime-state.json`. Dashboard
and PostgreSQL endpoints remain loopback-only and are never exposed by Funnel.

`Stop` disables the public Funnel by default. Pass `-KeepFunnel` only when you intentionally want the HTTPS route to remain configured while the local service is offline.

## Run services separately

When the server is started outside Aspire, provide `ConnectionStrings__game` through an environment variable or .NET user secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:game" "Host=localhost;Port=5432;Database=elyndor;Username=postgres;Password=<local-password>" --project src/Elyndor.Server
dotnet run --project src/Elyndor.Server
npm run dev --prefix web/elyndor-web
```

Never commit Telegram tokens, database credentials, signing keys, or production
connection strings. Development authentication is available only in the Development
environment and only from a loopback browser origin. It is absent from PublicTest
and production.

## Verification

```powershell
dotnet build Elyndor.slnx --configuration Release
dotnet test Elyndor.slnx --configuration Release
dotnet run --project tools/Elyndor.ContentValidator -- content/package.json
npm run lint --prefix web/elyndor-web
npm run format:check --prefix web/elyndor-web
npm run test:unit --prefix web/elyndor-web
npm run build --prefix web/elyndor-web
npm run test:e2e --prefix web/elyndor-web
```
