# Elyndor Web

Mobile-first Vue 3 client for the Elyndor Telegram Mini App.

The client sends player intent and renders authoritative server snapshots. Combat results, loot, progression, currencies, timers, and other critical game state must never be calculated or trusted here.

## Commands

```sh
npm ci
npm run dev
npm run lint
npm run test:unit
npm run build
```

Install Chromium once before the local E2E test:

```sh
npx playwright install chromium
npm run test:e2e
```

Run the full stack from the repository root with:

```sh
dotnet run --project apphost/Elyndor.AppHost
```

When Vite runs separately, `/api` is proxied to `http://localhost:5080` by default. Aspire injects the actual server endpoint when it orchestrates the frontend.

Generate API types only from the ASP.NET Core OpenAPI document while the standalone development server is running:

```sh
npm run api:generate
```
