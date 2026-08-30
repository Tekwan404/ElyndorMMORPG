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

Double-click `Start-Elyndor.cmd` to build the client, start PostgreSQL and the server,
and open the playable build at `http://127.0.0.1:5080`.

For a public Telegram test, set runtime secrets in the current PowerShell session
and then double-click `Start-Elyndor-Public.cmd` or run:

```powershell
$env:Authentication__SigningKey = '<random-secret-at-least-32-characters>'
$env:Authentication__Telegram__BotToken = '<telegram-bot-token>'
.\tools\dev\Elyndor.ps1 -Action Start -Public -Open
```

Public mode disables development authentication, OpenAPI, and Vite, then points
Tailscale Funnel at the single ASP.NET origin on port 5080. It prints the stable
`https://<node>.<tailnet>.ts.net` URL to configure as the Telegram Mini App URL.
PostgreSQL and the Aspire Dashboard are never routed through Funnel.

Inspect or stop the runtime with:

```powershell
.\tools\dev\Elyndor.ps1 -Action Status
.\tools\dev\Elyndor.ps1 -Action Stop
```

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
