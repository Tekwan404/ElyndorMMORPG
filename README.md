# Elyndor

Telegram-first MMORPG: ASP.NET Core/.NET 10, PostgreSQL/EF Core, SignalR,
Vue 3/TypeScript and Vite. The server owns combat, progression and the economy.

## Start locally

Requirements: the SDK pinned in [global.json](global.json), Node.js 24 LTS and
Docker. Install both clients, then run the Aspire stack:

```powershell
npm ci --prefix web/elyndor-web
npm ci --prefix web/elyndor-admin
dotnet tool restore
dotnet run --project apphost/Elyndor.AppHost
```

On Windows, `Elyndor-Control.cmd` manages the existing Telegram/Tailscale test
workflow. See [local setup](docs/development/getting-started.md) for authentication,
secrets and launcher commands, and the [production runbook](docs/deployment/vps-production.md)
for deployment, migrations, backups and rollback.

## Repository map

| Directory | Ownership |
| --- | --- |
| `src/Elyndor.Core` | Domain rules and authoritative combat mechanics; no transport/database dependencies. |
| `src/Elyndor.Infrastructure` | EF Core persistence, application services, content loading and runtime hosting. |
| `src/Elyndor.Server` | ASP.NET endpoints, authentication and SignalR hubs. |
| `src/Elyndor.ServiceDefaults`, `apphost` | Observability defaults and local orchestration. |
| `web/elyndor-web`, `web/elyndor-admin` | Player and content-admin clients. |
| `content` | Versioned runtime definitions and composed overrides. |
| `tests` | Backend unit/integration coverage; client tests live with each client. |
| `tools` | Content validation, asset preparation, launcher and repository checks. |
| `deploy` | Production packaging/deployment scripts. |
| `docs` | Specifications, current engineering notes and a clearly separated archive. |
| `reference` | Source artwork and visual references, not the shipped client asset root. |
| `content-analysis` | Derived inspection exports, not runtime content. |
| `.agents`, `.claude`, `.codex` | Preserved development-agent configuration. |
| `.config` | Repository-local .NET tool manifest. |

## Read the right source

- [Documentation index](docs/README.md): setup, specifications, current work and archives.
- [AGENTS.md](AGENTS.md): engineering invariants and agent workflow.
- [Content authoring](content/README.md): composition, validation and equipment semantics.
- [Tool index](tools/README.md): supported maintenance commands.
- [Content inspection exports](content-analysis/README.md): regeneration and freshness.

Gameplay specifications take precedence over UI specifications and visual references.
Historical phase documents describe their original checkpoints; they are not a
current feature inventory. Runtime availability must be verified against composed
content, feature configuration, production services and tests. Do not copy gameplay
numbers or obsolete supported/deferred lists into this README.

## Verification

```powershell
node --test tools/repository/check-layout.test.mjs
node tools/repository/check-layout.mjs
dotnet build Elyndor.slnx --configuration Release
dotnet test Elyndor.slnx --configuration Release --no-build
dotnet run --project tools/Elyndor.ContentValidator --configuration Release --no-build -- content/package.json
npm run lint --prefix web/elyndor-web
npm run test:unit --prefix web/elyndor-web
npm run build --prefix web/elyndor-web
npm run lint --prefix web/elyndor-admin
npm run test:unit --prefix web/elyndor-admin
npm run build --prefix web/elyndor-admin
```

[CI](.github/workflows/ci.yml) also verifies strict item-art coverage, content
diagnostics, production publish layout and browser flows. Contribution and merge
rules: [CONTRIBUTING.md](CONTRIBUTING.md) and [Git workflow](docs/development/git-workflow.md).
