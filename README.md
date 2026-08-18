# OSRS XP Tracker

![CI](https://github.com/peterwb1/osrs-xp-tracker/actions/workflows/pr.yml/badge.svg)
![Deploy](https://github.com/peterwb1/osrs-xp-tracker/actions/workflows/deploy.yml/badge.svg)

A multi-user web app for tracking Old School RuneScape account XP over time. Users sign up, register their RuneScape accounts by username, and a background job polls the official Hiscores API on a schedule to build a history of XP snapshots per skill.

This is a personal learning project, deliberately scoped small so it can be **finished and properly built** rather than half-built with lots of features. The focus is a solid foundation: real tests, real CI/CD, proper secrets management, structured logging — the unglamorous details that separate "side project" from "real service."

**Live demo:** [osrs-tracker-frontend.wittyground-486493c3.uksouth.azurecontainerapps.io](https://osrs-tracker-frontend.wittyground-486493c3.uksouth.azurecontainerapps.io)

## Features

**Accounts & auth**
- Sign up / log in with email + password (JWT). Every user's accounts and data are isolated.
- Track multiple OSRS accounts by username — validated against the official Hiscores when added.
- Remove accounts you no longer want to track.

**Tracking & history**
- A background poller snapshots every tracked account's skills (XP, level, rank) every 6 hours.
- A **Refresh** button polls the Hiscores on demand, with a short cooldown to stay polite to Jagex.
- Full per-skill history is retained for charting.

**Viewing your data**
- Per-account **dashboard**: total level, combat level, total XP, XP gained today / this week, fastest-growing skill, last level-up, and time since last poll.
- Skills table with icons showing current level, XP and rank.
- Per-skill **history charts** — switch between XP and rank, and between 7 / 30 / 90-day and all-time ranges.
- Light / dark mode.

> Designs for possible future features (account comparison, boss kill-count tracking, and more) live in [`docs/features/`](docs/features/).

## Tech stack

**Backend**
- ASP.NET Core 8 Web API (C#)
- Entity Framework Core 8 + PostgreSQL
- ASP.NET Identity + JWT auth
- `IHostedService` for background polling
- xUnit + FluentAssertions for tests

**Frontend**
- Next.js 16 (App Router) + TypeScript
- Tailwind v4
- TanStack Query for API calls + caching
- Recharts for the history charts

**Infrastructure**
- Docker + docker-compose locally
- Azure Container Apps (API + frontend)
- Azure Database for PostgreSQL Flexible Server (Burstable B1ms)
- GitHub Container Registry (GHCR) for Docker images
- GitHub Actions for CI/CD

## Data model

Five tables (plus the ASP.NET Identity tables).

- **Users** — handled by ASP.NET Identity (`Id`, `Email`, `PasswordHash`, etc.)
- **TrackedAccounts** — `Id`, `UserId` (FK), `OsrsUsername`, `DisplayName`, `CreatedAt`, `LastPolledAt`
- **Skills** — `Id`, `Name`, `DisplayOrder` — seeded once at startup with 24 rows (an `Overall` aggregate plus the 23 skills) in hiscore order
- **XpSnapshots** — `Id`, `TrackedAccountId` (FK), `SkillId` (FK), `Xp`, `Level`, `Rank`, `CapturedAt`
- **PollLog** — `Id`, `TrackedAccountId` (FK), `AttemptedAt`, `Success`, `ErrorMessage`

`XpSnapshots` is the biggest table. It has a composite index on `(TrackedAccountId, SkillId, CapturedAt)` so "show me the chart for one skill" stays fast.

## API surface

- `POST /api/auth/register` — email + password → user created + JWT returned
- `POST /api/auth/login` — credentials → JWT
- `GET /api/auth/me` — current user info
- `POST /api/accounts` — body has `osrsUsername`, `displayName` → validates the username exists on Hiscores, creates the account, takes an initial snapshot
- `GET /api/accounts` — list the current user's tracked accounts
- `DELETE /api/accounts/{id}` — remove an account
- `GET /api/accounts/{id}/skills` — current state of all skills (latest snapshot per skill)
- `GET /api/accounts/{id}/skills/{skillId}/history?days=30` — snapshots over a period, for the chart
- `GET /api/accounts/{id}/summary` — dashboard stats (total & combat level, XP gained today/this week, fastest skill, last level-up)
- `POST /api/accounts/{id}/refresh` — poll the Hiscores on demand (short cooldown; returns `429` if called too soon)
- `GET /health` — liveness + database connectivity (used by Container Apps probes)
- `GET /api/info` — app version and environment

## Background poller

One `IHostedService`. Runs on a loop:

1. Find every `TrackedAccount` whose `LastPolledAt` is older than the polling interval (default 6 hours).
2. For each one, with a small delay between requests (2 seconds):
   - Fetch the Hiscores.
   - Parse the response into a snapshot per skill.
   - Bulk-insert into `XpSnapshots`.
   - Update `LastPolledAt`.
   - Log the attempt to `PollLog`.
3. Sleep until the next cycle.

Six hours is a sensible default — it captures meaningful XP gains without hammering Jagex's servers. Configurable via `appsettings.json`. The same poll logic backs the manual **Refresh** endpoint.

## Engineering practices

The project aims to be a real service, not a throwaway. What's in place:

- **Tests in CI.** xUnit unit + integration tests run on every PR; the pipeline fails if they break.
- **Migrations, not drop-and-recreate.** Schema changes go through EF Core migrations, applied automatically on API startup.
- **Secrets out of git.** The JWT key and DB connection string come from environment/secrets; local dev uses a git-ignored `appsettings.Development.Local.json`.
- **Structured logging** via ASP.NET Core's built-in `ILogger`.
- **Health checks.** `/health` reports database connectivity; Container Apps uses it for probes.
- **A resilient Hiscores client.** Polly retries transient failures, with a delay between accounts so we don't hammer Jagex.
- **CORS locked down** to the known frontend origin(s), not `AllowAnyOrigin()`.

## Running locally

**Prerequisites:** [Docker Desktop](https://www.docker.com/products/docker-desktop/) for everything; plus [Node.js 20](https://nodejs.org/) and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8) only if you run the frontend/API outside Docker.

### Quickest — everything in Docker

```bash
git clone https://github.com/peterwb1/osrs-xp-tracker.git
cd osrs-xp-tracker
docker compose up --build
```

Open [http://localhost:3000](http://localhost:3000) (the API is at [http://localhost:8080](http://localhost:8080)). The first `--build` takes a few minutes (base images, compiling .NET, building Next.js); later runs are fast.

Wipe the database and start fresh:
```bash
docker compose down -v
```

### Working on the frontend (hot reload)

Run the database + API in Docker, and the Next.js dev server on your machine:

```bash
docker compose up db api        # terminal 1
```

```bash
cd web                          # terminal 2
npm install
npm run dev
```

The frontend runs at [http://localhost:3000](http://localhost:3000) with hot reload. `web/.env.local` already points it at the local API (`http://localhost:8080`).

### Running the API natively

```bash
# one-time: create your local JWT config from the example
cp api/OsrsTracker.Api/appsettings.Development.Local.json.example api/OsrsTracker.Api/appsettings.Development.Local.json
# then set a JWT key (any 32+ character string) inside that file

docker compose up db db-test    # database(s) only
cd api
dotnet run --project OsrsTracker.Api/OsrsTracker.Api.csproj
```

More detail — all the options, running tests, and working with migrations — is in [`docs/dev.md`](docs/dev.md).

### Environment variables

The API reads these at runtime. Locally they're set in `docker-compose.yml`; in production they're Azure Container Apps environment variables / secrets.

| Variable | Description |
|---|---|
| `ConnectionStrings__Default` | PostgreSQL connection string |
| `Jwt__Key` | Secret key for signing JWTs (32+ chars) |
| `Jwt__Issuer` | JWT issuer identifier |
| `Frontend__Url` | Production frontend origin (for CORS) |
| `ASPNETCORE_ENVIRONMENT` | `Development` or `Production` |

## Deployment

CI/CD runs on GitHub Actions:

- **`pr.yml`** — on every PR: `dotnet build` + `dotnet test` (with coverage) and `npm run lint` + `npm run build`.
- **`deploy.yml`** — builds the Docker images, pushes them to GHCR, and rolls out new Azure Container Apps revisions for the API and frontend. It authenticates to Azure via OIDC (no long-lived secret).
- **`bump.yml`** — on a merge to main, opens a PR bumping the patch version; merging that PR triggers a deploy.

The full one-time Azure setup (resource group, Postgres, Container Apps, wiring) is in [`docs/azure-deployment-guide.md`](docs/azure-deployment-guide.md).

### Cost

| Resource | Tier | Cost |
|---|---|---|
| Container Apps (API + frontend) | Consumption | ~£0 at low traffic (generous free grant) |
| PostgreSQL Flexible Server | Burstable B1ms | ~£10/month (free for the first 12 months only on a brand-new Azure account) |
| Container Apps environment / GHCR | — | Free |

The database is the main ongoing cost. `az group delete --name osrs-tracker-rg --yes` stops all billing.

## Project structure

```
osrs-xp-tracker/
├── api/                       # ASP.NET Core Web API (.NET 8)
│   ├── OsrsTracker.Api/       #   controllers, services (polling), data, DTOs, Program.cs
│   ├── OsrsTracker.Domain/    #   entities, hiscores parser, pure calculations (no framework deps)
│   ├── OsrsTracker.Tests/     #   xUnit unit + integration tests
│   └── OsrsTracker.sln
├── web/                       # Next.js 16 frontend (App Router, TypeScript)
│   ├── app/                   #   routes: login, register, accounts, account detail, skill history
│   ├── components/            #   dashboard, charts, theme toggle, reusable UI
│   ├── lib/                   #   api client, auth, query keys, helpers
│   └── Dockerfile
├── docs/                      # deployment guide, local dev guide, future-feature designs
│   └── features/
├── scripts/                   # version-bump helper
├── docker-compose.yml
└── .github/workflows/         # pr.yml (CI), deploy.yml, bump.yml
```
