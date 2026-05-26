# OSRS XP Tracker
 
A multi-user web app for tracking Old School RuneScape account XP over time. Users sign up, register their RuneScape accounts by username, and a background job polls the official Hiscores API on a schedule to build a history of XP snapshots per skill.
 
This is a personal learning project, deliberately scoped small so it can be **finished and properly built** rather than half-built with lots of features. The focus is on a solid foundation: real tests, real CI/CD, proper secrets management, structured logging — the unglamorous details that separate "side project" from "real service."
 
## Scope
 
A multi-user web app where:
 
- Anyone can sign up.
- Each user can track one or more OSRS accounts (just their RSN).
- A background job polls the official Hiscores every few hours and saves a snapshot of all 23+ skills per account.
- Users can view their accounts: current levels, total XP, XP gained over time, per-skill history charts.
That's it. No goals, no projections, no calculators, no collection log, no social features. Just: sign up → add your username → see your XP grow over time.
 
## Tech stack
 
### Backend
 
- ASP.NET Core 8 Web API (C#)
- Entity Framework Core 8
- PostgreSQL
- ASP.NET Identity + JWT auth
- `IHostedService` for background polling
- xUnit + FluentAssertions for tests
### Frontend
 
- React 18 + TypeScript + Vite
- Tailwind
- TanStack Query for API calls
- React Router
- Recharts for the XP-over-time chart
### Infrastructure
 
- Docker + docker-compose locally
- Azure Container Apps for the API
- Azure Database for PostgreSQL (flexible server, burstable tier)
- Azure Static Web Apps for the frontend
- GitHub Actions for CI/CD
- Azure Container Registry for built images
## Data model
 
Five tables.
 
- **Users** — handled by ASP.NET Identity (`Id`, `Email`, `PasswordHash`, etc.)
- **TrackedAccounts** — `Id`, `UserId` (FK), `OsrsUsername`, `DisplayName`, `CreatedAt`, `LastPolledAt`
- **Skills** — `Id`, `Name`, `DisplayOrder` — seeded once at startup with the 23 skills in hiscore order
- **XpSnapshots** — `Id`, `TrackedAccountId` (FK), `SkillId` (FK), `Xp`, `Level`, `Rank`, `CapturedAt`
- **PollLog** *(optional but useful)* — `Id`, `TrackedAccountId` (FK), `AttemptedAt`, `Success`, `ErrorMessage`
The `XpSnapshots` table will be the biggest. Index on `(TrackedAccountId, SkillId, CapturedAt DESC)` so "show me the chart for one skill" stays fast.
 
## API surface
 
- `POST /api/auth/register` — email + password → user created + JWT returned
- `POST /api/auth/login` — credentials → JWT
- `GET /api/auth/me` — current user info
- `POST /api/accounts` — body has `osrsUsername`, `displayName` → validates the username exists on Hiscores, creates `TrackedAccount`, takes initial snapshot
- `GET /api/accounts` — list current user's tracked accounts
- `DELETE /api/accounts/{id}` — remove an account
- `GET /api/accounts/{id}/skills` — current state of all 23 skills (latest snapshot per skill)
- `GET /api/accounts/{id}/skills/{skillId}/history?days=30` — snapshots over a period for the chart
## Background poller
 
One `IHostedService`. Runs on a loop:
 
1. Find every `TrackedAccount` whose `LastPolledAt` is older than the polling interval (default 6 hours).
2. For each one, with a small delay between requests (2 seconds):
   - Fetch the Hiscores.
   - Parse the response into 23 skill rows.
   - Bulk-insert into `XpSnapshots`.
   - Update `LastPolledAt`.
   - Log to `PollLog`.
3. Sleep until the next cycle.
Six hours is a sensible default — captures meaningful XP gains without hammering Jagex's servers. Configurable via `appsettings.json`.
 
## Roadmap
 
Eight focused weekends. Each ends with something demonstrable.
 
### Weekend 1 — Local backend foundation
 
- New solution: `OsrsTracker.Api`, `OsrsTracker.Domain`, `OsrsTracker.Tests`
- EF Core wired to local Postgres (Postgres running in Docker)
- Skills, TrackedAccounts, XpSnapshots tables with migrations
- Skill seeder runs at startup
- One endpoint working end-to-end: `POST /api/accounts` (no auth yet) → fetches Hiscores → saves snapshot
- A few xUnit tests around the Hiscores parser
**Goal:** you can `curl` an endpoint and see XP data appear in your local database.
 
### Weekend 2 — Auth and ownership
 
- ASP.NET Identity wired up with JWT
- Register / login / me endpoints
- `TrackedAccount` linked to `UserId`, all account endpoints scoped to the current user
- Tests: registration, login, accessing another user's account returns 403
**Goal:** you can register two users, and they can't see each other's accounts.
 
### Weekend 3 — Background polling
 
- `IHostedService` polling loop
- Polite delays, error handling for 404/503
- `PollLog` table populated
- Configurable poll interval via `appsettings.json`
- Tests for the "which accounts are due?" logic
**Goal:** start the API, leave it running, and watch new snapshots appear every 6 hours.
 
### Weekend 4 — Read endpoints + frontend bootstrap
 
- `GET /api/accounts/{id}/skills` (current state)
- `GET /api/accounts/{id}/skills/{skillId}/history` (chart data)
- Vite + React + TS + Tailwind project alongside the API
- Auth flow in the frontend: login → JWT in localStorage → axios/fetch interceptor
- Skeleton pages: Login, Register, Account List, Account Detail
**Goal:** you can log in via the React app and see an empty account list.
 
### Weekend 5 — Frontend properly
 
- Account list page with "Add account" form
- Account detail page: table of 23 skills with current level/XP/rank
- Per-skill chart with Recharts (XP over time)
- "Last polled at" display
- TanStack Query for caching and refetching
**Goal:** real, usable UI. You can add your own RSN and see your skills.
 
### Weekend 6 — Dockerise everything
 
- Multi-stage Dockerfile for the API
- Dockerfile for the frontend (build → serve via nginx)
- `docker-compose.yml` with API + Postgres + frontend
- One command starts the whole thing
- README updated with "how to run locally"
**Goal:** anyone (including you in six months) can clone the repo and `docker compose up` and have it working.
 
### Weekend 7 — Deploy to Azure
 
- Azure Container Registry — push the API image
- Azure Database for PostgreSQL Flexible Server — provisioned with the lowest burstable tier
- Azure Container Apps — deploy the API image, wire env vars (connection string, JWT secret) via secrets
- Azure Static Web Apps — deploy the frontend
- One real public URL, working end-to-end
**Goal:** you can send a friend a link, they can sign up, and it works.
 
### Weekend 8 — CI/CD
 
- GitHub Actions: PR workflow runs `dotnet build` + `dotnet test` + `npm test`
- Main branch workflow: builds Docker image, pushes to ACR, triggers Container Apps revision, deploys frontend to Static Web Apps
- Branch protection on main requiring the checks to pass
- A README badge showing CI status
**Goal:** push to main → 5 minutes later → live in production. No manual steps.
 
After weekend 8 the project is **done**. Don't reach for features until the foundation is genuinely solid.
 
## Definition of "genuinely solid"
 
Before calling the foundation done, it should have:
 
- **Tests that actually run in CI.** Not "I wrote some" — the pipeline fails if they break.
- **A real README.** Architecture diagram, how to run locally, environment variables documented, deploy instructions.
- **Migrations, not `dotnet ef database drop`.** New schema changes go through migrations from day one.
- **Secrets in secret stores**, not in `appsettings.json` checked into git.
- **Logging that's useful.** Structured logs (Serilog) so you can diagnose the inevitable production issue.
- **Health checks.** `/health` endpoint returning DB connectivity. Container Apps uses this.
- **A rate-limited Hiscores client** with retry on 503, not "fire-and-hope".
- **CORS configured properly** — not `AllowAnyOrigin()` in production.
## Budget
 
- Azure Container Apps: free tier covers casual use comfortably
- PostgreSQL Burstable B1ms: free for 12 months on a new Azure account, then ~£10/month
- Static Web Apps: free tier
- Container Registry: ~£4/month for Basic tier (or use GitHub Container Registry for free)
- Domain (optional): ~£10/year
**Realistic monthly cost: £0 for the first year, ~£15/month after.**
 
## Getting started
 
*To be filled in once the project is bootstrapped (Weekend 1).*
 
```bash
# Planned local setup
git clone <repo>
cd osrs-xp-tracker
docker compose up
```
 
## Project structure
 
*To be filled in once the project is bootstrapped.*
 
```
osrs-xp-tracker/
├── api/                    # ASP.NET Core Web API
│   ├── OsrsTracker.Api/
│   ├── OsrsTracker.Domain/
│   └── OsrsTracker.Tests/
├── web/                    # React + Vite frontend
├── docker-compose.yml
└── .github/workflows/      # CI/CD
```
